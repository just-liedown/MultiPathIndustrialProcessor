using StardewModdingAPI;
using StardewModdingAPI.Events;
using MultiPathIndustrialProcessor.Processor;

namespace MultiPathIndustrialProcessor;

public sealed class ModEntry : Mod
{
    private ITranslationHelper I18n = null!;
    private IndustrialProcessorController? processor;

    public override void Entry(IModHelper helper)
    {
        this.I18n = helper.Translation;
        Monitor.Log(this.I18n.Get("log.entry"), LogLevel.Info);
        Monitor.Log(this.I18n.Get("log.loaded"), LogLevel.Info);

        this.processor = new IndustrialProcessorController(helper, this.Monitor);
        this.processor.Register();

        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        Monitor.Log(this.I18n.Get("log.game_launched"), LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        Monitor.Log(this.I18n.Get("log.save_loaded"), LogLevel.Info);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        Monitor.Log(this.I18n.Get("log.day_started"), LogLevel.Info);
    }
}
