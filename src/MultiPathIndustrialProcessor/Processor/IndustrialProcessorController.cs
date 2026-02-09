using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Objects;

namespace MultiPathIndustrialProcessor.Processor;

internal sealed class IndustrialProcessorController
{
    private const string BuildingId = "IndustrialProcessor";
    private const string OutputChestId = "Output";
    private const string StateKey = "MultiPathIndustrialProcessor/IndustrialProcessorState";
    private const int HoldRepeatIntervalTicks = 6;
    private const string InteractRequestMessageType = "MIP/InteractRequest";
    private const string InteractResponseMessageType = "MIP/InteractResponse";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string modUniqueId;
    private SButton? holdButton;
    private int holdCooldownTicks;
    private Point? holdPortTile;
    private string? holdLocationName;
    private long nextNonce;
    private long? pendingNonce;
    private bool pendingSilent;

    public IndustrialProcessorController(IModHelper helper, IMonitor monitor, string modUniqueId)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.modUniqueId = modUniqueId;
    }

    public void Register()
    {
        helper.Events.Content.AssetRequested += this.OnAssetRequested;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.Input.ButtonReleased += this.OnButtonReleased;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
        helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Buildings"))
        {
            e.Edit(static asset =>
            {
                var data = asset.AsDictionary<string, BuildingData>().Data;
                if (data.ContainsKey(BuildingId))
                    return;

                data[BuildingId] = new BuildingData
                {
                    Name = "Multi-Path Industrial Processor",
                    Description = "An industrial processor with multiple input ports and a unified output.",
                    BuildingType = BuildingId,
                    Builder = "Robin",

                    Size = new Point(7, 4),
                    Texture = $"Buildings/{BuildingId}",
                    SourceRect = new Rectangle(0, 0, 7 * 64, 4 * 64),

                    BuildCost = 0,
                    BuildDays = 1,
                    BuildMaterials = new List<BuildingMaterial>
                    {
                        new() { ItemId = "(O)388", Amount = 1 } // 1 wood for testing
                    },

                    Chests = new List<BuildingChest>
                    {
                        new()
                        {
                            Id = OutputChestId,
                            Type = BuildingChestType.Chest
                        }
                    }
                };
            });
        }

        if (e.NameWithoutLocale.IsEquivalentTo($"Buildings/{BuildingId}"))
        {
            e.LoadFromModFile<Texture2D>($"assets/buildings/{BuildingId}.png", AssetLoadPriority.Medium);
        }
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (!e.Button.IsActionButton())
            return;

        var location = Game1.currentLocation;
        if (location is null)
            return;

        var who = Game1.player;
        var playerTile = who.TilePoint;
        var grabTile = who.GetGrabTile().ToPoint();

        // Prefer player standing tile for port selection, but also support interacting with the tile in
        // front of the player (grab tile) to match vanilla behavior.
        var portTileCandidates = new[] { playerTile, grabTile };

        // Host-authoritative multiplayer:
        // - main player runs the logic locally (and is the only one that mutates state / advances timers)
        // - farmhands send interaction requests to the main player, except output chest UI which is safe to open locally
        if (Context.IsMainPlayer)
        {
            foreach (var portTile in portTileCandidates)
            {
                if (!this.TryHandleAtPortTile(location, who, portTile, silent: false, allowMenus: true, out var consumed, out _))
                    continue;

                helper.Input.Suppress(e.Button);

                // enable hold-to-repeat only if we actually consumed input (i.e. started a job)
                if (consumed)
                {
                    this.holdButton = e.Button;
                    this.holdCooldownTicks = HoldRepeatIntervalTicks;
                }
                else
                {
                    this.holdButton = null;
                    this.holdCooldownTicks = 0;
                }

                return;
            }
        }
        else
        {
            foreach (var portTile in portTileCandidates)
            {
                if (!this.TryGetProcessorForPortTile(location, portTile, out var building))
                    continue;

                if (!this.TryClassifyPort(building, portTile, out var kind, out _))
                    continue;

                helper.Input.Suppress(e.Button);

                if (kind == PortKind.Output)
                {
                    _ = this.OpenOutput(building);
                    return;
                }

                if (kind == PortKind.Terminal)
                {
                    Game1.addHUDMessage(new HUDMessage("IndustrialProcessor: terminal not implemented yet (v1)."));
                    return;
                }

                // request host to handle all state mutations and inventory consumption
                this.SendInteractRequest(location, portTile, silent: false);

                // enable hold-to-repeat only for module ports; the response decides whether we keep repeating
                if (kind == PortKind.Module)
                {
                    this.holdButton = e.Button;
                    this.holdCooldownTicks = 0;
                    this.holdPortTile = portTile;
                    this.holdLocationName = location.NameOrUniqueName;
                }
                else
                {
                    this.holdButton = null;
                    this.holdCooldownTicks = 0;
                    this.holdPortTile = null;
                    this.holdLocationName = null;
                }

                return;
            }
        }
    }

    private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
    {
        if (this.holdButton is null)
            return;

        if (e.Button != this.holdButton.Value)
            return;

        this.holdButton = null;
        this.holdCooldownTicks = 0;
        this.holdPortTile = null;
        this.holdLocationName = null;
        this.pendingNonce = null;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (this.holdButton is null)
            return;

        // stop repeating if player released the button, opened a menu, etc.
        if (!this.helper.Input.IsDown(this.holdButton.Value) || Game1.activeClickableMenu is not null)
        {
            this.holdButton = null;
            this.holdCooldownTicks = 0;
            this.holdPortTile = null;
            this.holdLocationName = null;
            this.pendingNonce = null;
            return;
        }

        if (this.pendingNonce is not null)
            return; // wait for host response before sending another request

        if (this.holdCooldownTicks > 0)
        {
            this.holdCooldownTicks--;
            return;
        }

        var location = Game1.currentLocation;
        if (location is null)
            return;

        var who = Game1.player;
        var playerTile = who.TilePoint;
        var grabTile = who.GetGrabTile().ToPoint();

        if (Context.IsMainPlayer)
        {
            // repeat on the same interaction model as a press: stand tile first, then grab tile.
            var portTileCandidates = new[] { playerTile, grabTile };
            foreach (var portTile in portTileCandidates)
            {
                if (this.TryHandleAtPortTile(location, who, portTile, silent: true, allowMenus: true, out var consumed, out _) && consumed)
                {
                    this.holdCooldownTicks = HoldRepeatIntervalTicks;
                    return;
                }
            }

            // nothing consumed => stop repeating to avoid spamming attempts
            this.holdButton = null;
            this.holdCooldownTicks = 0;
            return;
        }

        // Farmhand: keep repeating only while still interacting with the same port tile.
        if (this.holdPortTile is null || this.holdLocationName is null)
        {
            this.holdButton = null;
            this.holdCooldownTicks = 0;
            return;
        }

        if (!string.Equals(location.NameOrUniqueName, this.holdLocationName, StringComparison.Ordinal))
        {
            this.holdButton = null;
            this.holdCooldownTicks = 0;
            this.holdPortTile = null;
            this.holdLocationName = null;
            return;
        }

        var candidates = new[] { playerTile, grabTile };
        if (candidates[0] != this.holdPortTile.Value && candidates[1] != this.holdPortTile.Value)
        {
            this.holdButton = null;
            this.holdCooldownTicks = 0;
            this.holdPortTile = null;
            this.holdLocationName = null;
            return;
        }

        this.SendInteractRequest(location, this.holdPortTile.Value, silent: true);
    }

    private bool TryHandleAtPortTile(GameLocation location, Farmer who, Point portTile, bool silent, bool allowMenus, out bool consumed, out string? message)
    {
        consumed = false;
        message = null;

        var buildingSearchTiles = new[]
        {
            portTile,
            new Point(portTile.X, portTile.Y - 1),
            new Point(portTile.X, portTile.Y + 1),
            new Point(portTile.X + 1, portTile.Y),
            new Point(portTile.X - 1, portTile.Y)
        };

        foreach (var tile in buildingSearchTiles)
        {
            var building = location.getBuildingAt(new Vector2(tile.X, tile.Y));
            if (building is null || !this.IsProcessor(building))
                continue;

            if (!this.TryHandleInteraction(building, portTile, who, silent, allowMenus, out consumed, out message))
                continue;

            return true;
        }

        return false;
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        // Only the main player should advance timers and mutate building state.
        if (!Context.IsMainPlayer)
            return;

        foreach (var location in Game1.locations)
        {
            foreach (var building in this.EnumerateBuildings(location))
            {
                if (!this.IsProcessor(building))
                    continue;

                this.ProcessBuilding(building);
            }
        }
    }

    private bool IsProcessor(Building building)
    {
        return string.Equals(building.buildingType.Value, BuildingId, StringComparison.Ordinal);
    }

    private IEnumerable<Building> EnumerateBuildings(GameLocation location)
    {
        // Stardew 1.6 no longer exposes BuildableGameLocation publicly; buildings are still stored on
        // buildable locations, so we reflect the backing field for enumeration.
        var type = location.GetType();
        var buildingsField = type.GetField("buildings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (buildingsField?.GetValue(location) is System.Collections.IEnumerable raw)
        {
            foreach (var entry in raw)
            {
                if (entry is Building b)
                    yield return b;
            }
        }
    }

    private bool TryHandleInteraction(Building building, Point portTile, Farmer who, bool silent, bool allowMenus, out bool consumed, out string? message)
    {
        consumed = false;
        message = null;

        var origin = new Point(building.tileX.Value, building.tileY.Value);
        // v1 layout (per design doc):
        // - bottom: 6 input ports
        // - top: 1 unified output port
        // - left: coal port
        // - right: management terminal
        var outputPort = new Point(origin.X + 3, origin.Y - 1);
        var coalPort = new Point(origin.X - 1, origin.Y + 2);
        var terminalPort = new Point(origin.X + 7, origin.Y + 2);

        if (portTile == outputPort)
        {
            if (allowMenus)
                _ = this.OpenOutput(building);
            return true;
        }

        if (portTile == terminalPort)
        {
            if (!silent)
            {
                monitor.Log("IndustrialProcessor: terminal not implemented yet (v1).", LogLevel.Info);
                message = "IndustrialProcessor: terminal not implemented yet (v1).";
            }
            return true;
        }

        if (portTile == coalPort)
            return this.TryHandleCoalPort(building, who, silent, out message);

        if (portTile.Y == origin.Y + 4)
        {
            var dx = portTile.X - origin.X;
            var module = dx switch
            {
                0 => IndustrialModule.Animal,
                1 => IndustrialModule.Brew,
                2 => IndustrialModule.Preserve,
                3 => IndustrialModule.Smelt,
                4 => IndustrialModule.Wool,
                5 => IndustrialModule.Oil,
                _ => (IndustrialModule?)null
            };

            if (module is not null)
                return this.TryStartModuleJob(building, module.Value, who, silent, out consumed, out message);
        }

        return false;
    }

    private bool OpenOutput(Building building)
    {
        var chest = building.GetBuildingChest(OutputChestId);
        if (chest is null)
        {
            monitor.Log("IndustrialProcessor: missing Output chest (building data not applied?).", LogLevel.Warn);
            return true;
        }

        chest.ShowMenu();
        return true;
    }

    private bool TryHandleCoalPort(Building building, Farmer who, bool silent, out string? message)
    {
        message = null;
        var state = this.GetOrCreateState(building);

        var held = who.ActiveItem;
        if (held is null)
        {
            var available = state.Fuel - state.FuelReserved;
            message = $"IndustrialProcessor: fuel = {state.Fuel} (available {available}, reserved {state.FuelReserved}).";
            if (!silent)
                monitor.Log(message, LogLevel.Info);
            return true;
        }

        if (held.QualifiedItemId != "(O)382")
        {
            message = "IndustrialProcessor: put Coal here to add fuel.";
            if (!silent)
                monitor.Log(message, LogLevel.Info);
            return true;
        }

        var add = held.Stack;
        state.Fuel += add;
        this.SaveState(building, state);
        this.ConsumeActiveItem(who, add);
        var availableAfter = state.Fuel - state.FuelReserved;
        message = $"IndustrialProcessor: added fuel +{add} (total {state.Fuel}, available {availableAfter}).";
        if (!silent)
            monitor.Log(message, LogLevel.Info);
        return true;
    }

    private bool TryStartModuleJob(Building building, IndustrialModule module, Farmer who, bool silent, out bool consumed, out string? message)
    {
        consumed = false;
        message = null;

        var held = who.ActiveItem;
        if (held is null)
            return true; // handled, but nothing to consume

        var state = this.GetOrCreateState(building);
        var moduleState = state.GetModuleState(module);
        var capacity = moduleState.GetCapacity();

        if (moduleState.Tasks.Count >= capacity)
        {
            if (!silent)
            {
                message = $"IndustrialProcessor: {module} is full ({capacity}).";
                monitor.Log(message, LogLevel.Info);
            }
            return true;
        }

        if (module == IndustrialModule.Smelt)
        {
            var started = this.TryStartSmeltJob(building, who, state, moduleState, held, silent, out message);
            consumed = started;
            return true;
        }

        if (!this.TryCreateMachineJob(module, held, who, out var job))
        {
            if (!silent)
            {
                message = $"IndustrialProcessor: item not accepted by {module}.";
                monitor.Log(message, LogLevel.Info);
            }
            return true;
        }

        moduleState.Tasks.Add(job);
        this.SaveState(building, state);
        this.ConsumeActiveItem(who, 1);
        if (!silent)
        {
            message = $"IndustrialProcessor: started {module}, ready in {job.Minutes} min -> {job.OutputItemId} x{job.OutputStack}.";
            monitor.Log(message, LogLevel.Info);
        }
        consumed = true;
        return true;
    }

    private bool TryStartSmeltJob(Building building, Farmer who, IndustrialProcessorState state, IndustrialModuleState moduleState, Item held, bool silent, out string? message)
    {
        message = null;
        // Coal is handled by the dedicated coal port.
        if (!IndustrialSmelting.TryGetRecipe(held.QualifiedItemId, out var recipe))
        {
            if (!silent)
            {
                message = "IndustrialProcessor: smelting port accepts ore/quartz only.";
                monitor.Log(message, LogLevel.Info);
            }
            return false;
        }

        if (held.Stack < recipe.InputCount)
        {
            if (!silent)
            {
                message = $"IndustrialProcessor: need {recipe.InputCount}x input for smelting.";
                monitor.Log(message, LogLevel.Info);
            }
            return false;
        }

        var now = this.GetNowAbsoluteMinutes();
        var job = new IndustrialJob
        {
            Minutes = recipe.Minutes,
            OutputItemId = recipe.OutputItemId,
            OutputStack = recipe.OutputStack,
            OutputQuality = 0,
            FuelCost = 1
        };

        var availableFuel = state.Fuel - state.FuelReserved;
        if (availableFuel >= job.FuelCost)
        {
            state.FuelReserved += job.FuelCost;
            job.ReadyAtAbsoluteMinutes = now + job.Minutes;
        }
        else
        {
            // no fuel yet; job is queued and the timer will start once fuel is available
            job.ReadyAtAbsoluteMinutes = 0;
        }

        moduleState.Tasks.Add(job);

        this.SaveState(building, state);
        this.ConsumeActiveItem(who, recipe.InputCount);
        if (job.ReadyAtAbsoluteMinutes > 0)
        {
            if (!silent)
            {
                message = $"IndustrialProcessor: started Smelt, ready in {recipe.Minutes} min -> {recipe.OutputItemId} x{recipe.OutputStack}.";
                monitor.Log(message, LogLevel.Info);
            }
        }
        else
        {
            if (!silent)
            {
                message = $"IndustrialProcessor: queued Smelt (waiting for coal), will take {recipe.Minutes} min -> {recipe.OutputItemId} x{recipe.OutputStack}.";
                monitor.Log(message, LogLevel.Info);
            }
        }

        return true;
    }

    private bool TryCreateMachineJob(IndustrialModule module, Item input, Farmer who, out IndustrialJob job)
    {
        job = default!;

        string[] machineIds = module switch
        {
            IndustrialModule.Animal => new[] { "(BC)24", "(BC)16" }, // mayonnaise machine, cheese press
            IndustrialModule.Brew => new[] { "(BC)12" }, // keg
            IndustrialModule.Preserve => new[] { "(BC)15" }, // preserves jar
            IndustrialModule.Wool => new[] { "(BC)17" }, // loom
            IndustrialModule.Oil => new[] { "(BC)19" }, // oil maker
            _ => Array.Empty<string>()
        };

        foreach (var machineId in machineIds)
        {
            if (!this.TrySimulateMachine(machineId, input, who, out job))
                continue;

            return true;
        }

        return false;
    }

    private bool TrySimulateMachine(string machineItemId, Item input, Farmer who, out IndustrialJob job)
    {
        job = default!;

        if (ItemRegistry.Create(machineItemId) is not StardewValley.Object machine)
            return false;

        var machineData = machine.GetMachineData();
        if (machineData is null)
            return false;

        var inputCopy = input.getOne();
        inputCopy.Stack = 1;
        inputCopy.Quality = input.Quality;

        var ok = machine.PlaceInMachine(machineData, inputCopy, probe: false, who: who, showMessages: false, playSounds: false);
        if (!ok)
            return false;

        var output = machine.heldObject.Value;
        if (output is null)
            return false;

        var minutes = machine.MinutesUntilReady;
        if (minutes <= 0)
            minutes = 10;

        var now = this.GetNowAbsoluteMinutes();
        job = new IndustrialJob
        {
            ReadyAtAbsoluteMinutes = now + minutes,
            Minutes = minutes,
            OutputItemId = output.QualifiedItemId,
            OutputStack = output.Stack,
            OutputQuality = output.Quality
        };
        return true;
    }

    private void ProcessBuilding(Building building)
    {
        var state = this.GetOrCreateState(building);
        var output = building.GetBuildingChest(OutputChestId);
        if (output is null)
            return;

        var now = this.GetNowAbsoluteMinutes();
        var changed = false;

        foreach (var module in IndustrialProcessorState.ModuleOrder)
        {
            var moduleState = state.GetModuleState(module);

            for (var i = moduleState.Tasks.Count - 1; i >= 0; i--)
            {
                var task = moduleState.Tasks[i];

                // Smelting jobs may be queued without fuel; start their timer once fuel becomes available.
                if (task.ReadyAtAbsoluteMinutes == 0)
                {
                    if (task.FuelCost <= 0)
                    {
                        task.ReadyAtAbsoluteMinutes = now + task.Minutes;
                        changed = true;
                        continue;
                    }

                    var availableFuel = state.Fuel - state.FuelReserved;
                    if (availableFuel >= task.FuelCost)
                    {
                        state.FuelReserved += task.FuelCost;
                        task.ReadyAtAbsoluteMinutes = now + task.Minutes;
                        changed = true;
                    }

                    continue;
                }

                if (task.ReadyAtAbsoluteMinutes > now)
                    continue;

                var item = ItemRegistry.Create(task.OutputItemId, task.OutputStack);
                if (item is null)
                    continue;

                item.Quality = task.OutputQuality;
                var leftover = output.addItem(item);
                if (leftover is not null)
                    continue; // output full

                if (task.FuelCost > 0)
                {
                    state.FuelReserved -= task.FuelCost;
                    state.Fuel -= task.FuelCost;
                }

                moduleState.Tasks.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            this.SaveState(building, state);
    }

    private long GetNowAbsoluteMinutes()
    {
        var days = Game1.Date.TotalDays;
        var time = Game1.timeOfDay;
        var hour = time / 100;
        var minute = time % 100;
        return days * 24L * 60L + hour * 60L + minute;
    }

    private IndustrialProcessorState GetOrCreateState(Building building)
    {
        if (building.modData.TryGetValue(StateKey, out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<IndustrialProcessorState>(raw, JsonOptions);
                if (parsed is not null)
                    return parsed.Normalize();
            }
            catch
            {
                // ignore
            }
        }

        var fresh = IndustrialProcessorState.CreateDefault();
        this.SaveState(building, fresh);
        return fresh;
    }

    private void SaveState(Building building, IndustrialProcessorState state)
    {
        building.modData[StateKey] = JsonSerializer.Serialize(state, JsonOptions);
    }

    private void ConsumeActiveItem(Farmer who, int amount)
    {
        if (amount <= 0)
            return;

        var item = who.ActiveItem;
        if (item is null)
            return;

        if (item.Stack <= amount)
        {
            who.removeItemFromInventory(item);
            return;
        }

        item.Stack -= amount;
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (!string.Equals(e.FromModID, this.modUniqueId, StringComparison.Ordinal))
            return;

        if (string.Equals(e.Type, InteractRequestMessageType, StringComparison.Ordinal))
        {
            if (!Context.IsMainPlayer)
                return;

            var req = e.ReadAs<InteractRequest>();
            var who = Game1.GetPlayer(e.FromPlayerID);
            var location = who?.currentLocation ?? Game1.getLocationFromName(req.LocationName);

            var handled = false;
            var consumed = false;
            string? message = null;

            if (who is not null && location is not null)
                handled = this.TryHandleAtPortTile(location, who, new Point(req.TileX, req.TileY), req.Silent, allowMenus: false, out consumed, out message);

            helper.Multiplayer.SendMessage(
                new InteractResponse
                {
                    Nonce = req.Nonce,
                    Handled = handled,
                    Consumed = consumed,
                    Message = req.Silent ? null : message
                },
                InteractResponseMessageType,
                modIDs: new[] { this.modUniqueId },
                playerIDs: new[] { e.FromPlayerID }
            );

            return;
        }

        if (string.Equals(e.Type, InteractResponseMessageType, StringComparison.Ordinal))
        {
            var resp = e.ReadAs<InteractResponse>();
            if (this.pendingNonce is null || resp.Nonce != this.pendingNonce.Value)
                return;

            this.pendingNonce = null;

            if (!this.pendingSilent && !string.IsNullOrWhiteSpace(resp.Message))
                Game1.addHUDMessage(new HUDMessage(resp.Message));

            // If the host didn't consume input (i.e. couldn't start a job), stop repeating to avoid spam.
            if (this.holdButton is not null && !resp.Consumed)
            {
                this.holdButton = null;
                this.holdCooldownTicks = 0;
                this.holdPortTile = null;
                this.holdLocationName = null;
                return;
            }

            // If we did consume, schedule the next repeat interval.
            if (this.holdButton is not null && resp.Consumed)
                this.holdCooldownTicks = HoldRepeatIntervalTicks;

            return;
        }
    }

    private void SendInteractRequest(GameLocation location, Point portTile, bool silent)
    {
        // Only farmhands should send requests. Main player runs logic locally.
        if (Context.IsMainPlayer)
            return;

        if (this.pendingNonce is not null)
            return;

        var nonce = ++this.nextNonce;
        this.pendingNonce = nonce;
        this.pendingSilent = silent;

        helper.Multiplayer.SendMessage(
            new InteractRequest
            {
                Nonce = nonce,
                LocationName = location.NameOrUniqueName,
                TileX = portTile.X,
                TileY = portTile.Y,
                Silent = silent
            },
            InteractRequestMessageType,
            modIDs: new[] { this.modUniqueId },
            playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID }
        );
    }

    private bool TryGetProcessorForPortTile(GameLocation location, Point portTile, out Building building)
    {
        building = null!;

        var buildingSearchTiles = new[]
        {
            portTile,
            new Point(portTile.X, portTile.Y - 1),
            new Point(portTile.X, portTile.Y + 1),
            new Point(portTile.X + 1, portTile.Y),
            new Point(portTile.X - 1, portTile.Y)
        };

        foreach (var tile in buildingSearchTiles)
        {
            var found = location.getBuildingAt(new Vector2(tile.X, tile.Y));
            if (found is null || !this.IsProcessor(found))
                continue;

            building = found;
            return true;
        }

        return false;
    }

    private bool TryClassifyPort(Building building, Point portTile, out PortKind kind, out IndustrialModule? module)
    {
        kind = default;
        module = null;

        var origin = new Point(building.tileX.Value, building.tileY.Value);
        var outputPort = new Point(origin.X + 3, origin.Y - 1);
        var coalPort = new Point(origin.X - 1, origin.Y + 2);
        var terminalPort = new Point(origin.X + 7, origin.Y + 2);

        if (portTile == outputPort)
        {
            kind = PortKind.Output;
            return true;
        }

        if (portTile == coalPort)
        {
            kind = PortKind.Coal;
            return true;
        }

        if (portTile == terminalPort)
        {
            kind = PortKind.Terminal;
            return true;
        }

        if (portTile.Y == origin.Y + 4)
        {
            var dx = portTile.X - origin.X;
            module = dx switch
            {
                0 => IndustrialModule.Animal,
                1 => IndustrialModule.Brew,
                2 => IndustrialModule.Preserve,
                3 => IndustrialModule.Smelt,
                4 => IndustrialModule.Wool,
                5 => IndustrialModule.Oil,
                _ => (IndustrialModule?)null
            };

            if (module is not null)
            {
                kind = PortKind.Module;
                return true;
            }
        }

        return false;
    }

    private enum PortKind
    {
        Output,
        Coal,
        Terminal,
        Module
    }

    private sealed class InteractRequest
    {
        public long Nonce { get; set; }
        public string LocationName { get; set; } = "";
        public int TileX { get; set; }
        public int TileY { get; set; }
        public bool Silent { get; set; }
    }

    private sealed class InteractResponse
    {
        public long Nonce { get; set; }
        public bool Handled { get; set; }
        public bool Consumed { get; set; }
        public string? Message { get; set; }
    }
}
