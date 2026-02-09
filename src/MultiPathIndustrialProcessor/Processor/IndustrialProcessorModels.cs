namespace MultiPathIndustrialProcessor.Processor;

internal enum IndustrialModule
{
    Animal,
    Brew,
    Preserve,
    Smelt,
    Wool,
    Oil
}

internal sealed class IndustrialJob
{
    public long ReadyAtAbsoluteMinutes { get; set; }
    public int Minutes { get; set; }

    public string OutputItemId { get; set; } = "";
    public int OutputStack { get; set; } = 1;
    public int OutputQuality { get; set; }

    public int FuelCost { get; set; }
}

internal sealed class IndustrialModuleState
{
    public int CapacityLevel { get; set; }
    public List<IndustrialJob> Tasks { get; set; } = new();

    public int GetCapacity()
    {
        // v1: 8 -> 16 -> 32 -> 64
        return this.CapacityLevel switch
        {
            <= 0 => 8,
            1 => 16,
            2 => 32,
            _ => 64
        };
    }
}

internal sealed class IndustrialProcessorState
{
    public int Version { get; set; } = 1;
    public int Fuel { get; set; }
    public Dictionary<IndustrialModule, IndustrialModuleState> Modules { get; set; } = new();

    public static readonly IndustrialModule[] ModuleOrder = new[]
    {
        IndustrialModule.Animal,
        IndustrialModule.Brew,
        IndustrialModule.Preserve,
        IndustrialModule.Smelt,
        IndustrialModule.Wool,
        IndustrialModule.Oil
    };

    public static IndustrialProcessorState CreateDefault()
    {
        var state = new IndustrialProcessorState();
        foreach (var module in ModuleOrder)
            state.Modules[module] = new IndustrialModuleState { CapacityLevel = 0 };
        return state;
    }

    public IndustrialModuleState GetModuleState(IndustrialModule module)
    {
        if (this.Modules.TryGetValue(module, out var state))
            return state;

        state = new IndustrialModuleState { CapacityLevel = 0 };
        this.Modules[module] = state;
        return state;
    }

    public IndustrialProcessorState Normalize()
    {
        foreach (var module in ModuleOrder)
            _ = this.GetModuleState(module);
        return this;
    }
}
