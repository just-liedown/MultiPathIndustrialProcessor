using StardewModdingAPI;

namespace OnlineShopping;

public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        Monitor.Log("OnlineShopping: loaded (v0.1.0).", LogLevel.Info);

        // TODO(v1): Add Harmony patches to inject a "Buy" option when calling Robin on the phone,
        // then open the vanilla ShopMenu with Robin's shop stock.
    }
}
