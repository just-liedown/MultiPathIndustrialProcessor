using HarmonyLib;
using StardewModdingAPI;
using OnlineShopping.Patches;

namespace OnlineShopping;

public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        Monitor.Log(helper.Translation.Get("log.loaded"), LogLevel.Info);

        var harmony = new Harmony(this.ModManifest.UniqueID);
        PhonePatches.Apply(harmony, this.Monitor, helper.Translation);
    }
}
