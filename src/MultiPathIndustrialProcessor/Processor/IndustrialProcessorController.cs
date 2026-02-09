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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IModHelper helper;
    private readonly IMonitor monitor;

    public IndustrialProcessorController(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;
    }

    public void Register()
    {
        helper.Events.Content.AssetRequested += this.OnAssetRequested;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
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
        var playerTile = who.Tile;

        // The design doc treats "stand at the port" as the action selector, so use the player's tile
        // rather than the cursor tile to avoid mis-clicks.
        var candidateTiles = new[]
        {
            playerTile,
            playerTile + new Vector2(0, -1),
            playerTile + new Vector2(0, 1),
            playerTile + new Vector2(1, 0),
            playerTile + new Vector2(-1, 0)
        };

        foreach (var tile in candidateTiles)
        {
            var building = location.getBuildingAt(tile);
            if (building is null || !this.IsProcessor(building))
                continue;

            if (!this.TryHandleInteraction(building, playerTile, who))
                continue;

            helper.Input.Suppress(e.Button);
            return;
        }
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (!Context.IsWorldReady)
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

    private bool TryHandleInteraction(Building building, Vector2 cursorTile, Farmer who)
    {
        var origin = new Point(building.tileX.Value, building.tileY.Value);
        // v1 layout (per design doc):
        // - bottom: 6 input ports
        // - top: 1 unified output port
        // - left: coal port
        // - right: management terminal
        var outputPort = new Vector2(origin.X + 3, origin.Y - 1);
        var coalPort = new Vector2(origin.X - 1, origin.Y + 2);
        var terminalPort = new Vector2(origin.X + 7, origin.Y + 2);

        if (cursorTile == outputPort)
            return this.OpenOutput(building);

        if (cursorTile == terminalPort)
        {
            monitor.Log("IndustrialProcessor: terminal not implemented yet (v1).", LogLevel.Info);
            return true;
        }

        if (cursorTile == coalPort)
            return this.TryHandleCoalPort(building, who);

        var modulePorts = new Dictionary<Vector2, IndustrialModule>
        {
            [new Vector2(origin.X + 0, origin.Y + 4)] = IndustrialModule.Animal,
            [new Vector2(origin.X + 1, origin.Y + 4)] = IndustrialModule.Brew,
            [new Vector2(origin.X + 2, origin.Y + 4)] = IndustrialModule.Preserve,
            [new Vector2(origin.X + 3, origin.Y + 4)] = IndustrialModule.Smelt,
            [new Vector2(origin.X + 4, origin.Y + 4)] = IndustrialModule.Wool,
            [new Vector2(origin.X + 5, origin.Y + 4)] = IndustrialModule.Oil
        };

        if (!modulePorts.TryGetValue(cursorTile, out var module))
            return false;

        return this.TryStartModuleJob(building, module, who);
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

    private bool TryHandleCoalPort(Building building, Farmer who)
    {
        var state = this.GetOrCreateState(building);

        var held = who.ActiveItem;
        if (held is null)
        {
            monitor.Log($"IndustrialProcessor: fuel = {state.Fuel}.", LogLevel.Info);
            return true;
        }

        if (held.QualifiedItemId != "(O)382")
        {
            monitor.Log("IndustrialProcessor: put Coal here to add fuel.", LogLevel.Info);
            return true;
        }

        var add = held.Stack;
        state.Fuel += add;
        this.SaveState(building, state);
        this.ConsumeActiveItem(who, add);
        monitor.Log($"IndustrialProcessor: added fuel +{add} (total {state.Fuel}).", LogLevel.Info);
        return true;
    }

    private bool TryStartModuleJob(Building building, IndustrialModule module, Farmer who)
    {
        var held = who.ActiveItem;
        if (held is null)
            return true;

        var state = this.GetOrCreateState(building);
        var moduleState = state.GetModuleState(module);
        var capacity = moduleState.GetCapacity();

        if (moduleState.Tasks.Count >= capacity)
        {
            monitor.Log($"IndustrialProcessor: {module} is full ({capacity}).", LogLevel.Info);
            return true;
        }

        if (module == IndustrialModule.Smelt)
            return this.TryStartSmeltJob(building, who, state, moduleState, held);

        if (!this.TryCreateMachineJob(module, held, who, out var job))
        {
            monitor.Log($"IndustrialProcessor: item not accepted by {module}.", LogLevel.Info);
            return true;
        }

        moduleState.Tasks.Add(job);
        this.SaveState(building, state);
        this.ConsumeActiveItem(who, 1);
        monitor.Log($"IndustrialProcessor: started {module}, ready in {job.Minutes} min -> {job.OutputItemId} x{job.OutputStack}.", LogLevel.Info);
        return true;
    }

    private bool TryStartSmeltJob(Building building, Farmer who, IndustrialProcessorState state, IndustrialModuleState moduleState, Item held)
    {
        // Coal is handled by the dedicated coal port.
        if (!IndustrialSmelting.TryGetRecipe(held.QualifiedItemId, out var recipe))
        {
            monitor.Log("IndustrialProcessor: smelting port accepts ore/quartz only.", LogLevel.Info);
            return true;
        }

        if (held.Stack < recipe.InputCount)
        {
            monitor.Log($"IndustrialProcessor: need {recipe.InputCount}x input for smelting.", LogLevel.Info);
            return true;
        }

        var now = this.GetNowAbsoluteMinutes();
        moduleState.Tasks.Add(new IndustrialJob
        {
            ReadyAtAbsoluteMinutes = now + recipe.Minutes,
            Minutes = recipe.Minutes,
            OutputItemId = recipe.OutputItemId,
            OutputStack = recipe.OutputStack,
            OutputQuality = 0,
            FuelCost = 1
        });

        this.SaveState(building, state);
        this.ConsumeActiveItem(who, recipe.InputCount);
        monitor.Log($"IndustrialProcessor: started Smelt, ready in {recipe.Minutes} min -> {recipe.OutputItemId} x{recipe.OutputStack} (fuel on finish).", LogLevel.Info);
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
                if (task.ReadyAtAbsoluteMinutes > now)
                    continue;

                if (task.FuelCost > 0 && state.Fuel < task.FuelCost)
                    continue;

                var item = ItemRegistry.Create(task.OutputItemId, task.OutputStack);
                if (item is null)
                    continue;

                item.Quality = task.OutputQuality;
                var leftover = output.addItem(item);
                if (leftover is not null)
                    continue; // output full

                if (task.FuelCost > 0)
                    state.Fuel -= task.FuelCost;

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
}
