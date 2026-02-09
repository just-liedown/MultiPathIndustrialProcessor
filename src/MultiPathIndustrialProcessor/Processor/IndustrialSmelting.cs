namespace MultiPathIndustrialProcessor.Processor;

internal static class IndustrialSmelting
{
    internal readonly record struct SmeltRecipe(string OutputItemId, int InputCount, int OutputStack, int Minutes);

    public static bool TryGetRecipe(string qualifiedInputItemId, out SmeltRecipe recipe)
    {
        // Matches vanilla furnace ratios (v1 subset).
        // Input counts are per job; fuel is handled by the processor's global pool.
        recipe = qualifiedInputItemId switch
        {
            "(O)378" => new SmeltRecipe(OutputItemId: "(O)334", InputCount: 5, OutputStack: 1, Minutes: 30),  // copper ore -> copper bar
            "(O)380" => new SmeltRecipe(OutputItemId: "(O)335", InputCount: 5, OutputStack: 1, Minutes: 120), // iron ore -> iron bar
            "(O)384" => new SmeltRecipe(OutputItemId: "(O)336", InputCount: 5, OutputStack: 1, Minutes: 300), // gold ore -> gold bar
            "(O)386" => new SmeltRecipe(OutputItemId: "(O)337", InputCount: 5, OutputStack: 1, Minutes: 480), // iridium ore -> iridium bar
            "(O)390" => new SmeltRecipe(OutputItemId: "(O)338", InputCount: 1, OutputStack: 1, Minutes: 90),  // quartz -> refined quartz
            "(O)82" => new SmeltRecipe(OutputItemId: "(O)338", InputCount: 1, OutputStack: 3, Minutes: 180),   // fire quartz -> refined quartz x3
            _ => default
        };

        return recipe.OutputItemId.Length > 0;
    }
}
