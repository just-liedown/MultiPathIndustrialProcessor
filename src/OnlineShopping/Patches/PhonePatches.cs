using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace OnlineShopping.Patches;

internal static class PhonePatches
{
    private const string BuyCarpenterCallId = "OnlineShopping_BuyCarpenter";

    private static IMonitor Monitor = null!;
    private static ITranslationHelper I18n = null!;

    public static void Apply(Harmony harmony, IMonitor monitor, ITranslationHelper i18n)
    {
        Monitor = monitor;
        I18n = i18n;

        harmony.Patch(
            original: AccessTools.Method(typeof(DefaultPhoneHandler), nameof(DefaultPhoneHandler.CallCarpenter)),
            postfix: new HarmonyMethod(typeof(PhonePatches), nameof(After_CallCarpenter))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(DefaultPhoneHandler), nameof(DefaultPhoneHandler.TryHandleOutgoingCall)),
            prefix: new HarmonyMethod(typeof(PhonePatches), nameof(Before_TryHandleOutgoingCall))
        );
    }

    private static void After_CallCarpenter()
    {
        try
        {
            if (Game1.activeClickableMenu is not DialogueBox box)
                return;

            var responsesField = AccessTools.Field(typeof(DialogueBox), "responses");
            if (responsesField is null)
                return;

            var responses = responsesField.GetValue(box) as Response[];
            if (responses is null || responses.Length == 0)
                return;

            if (responses.Any(r => string.Equals(r.responseKey, BuyCarpenterCallId, StringComparison.Ordinal)))
                return;

            var extended = responses
                .Concat(new[] { new Response(BuyCarpenterCallId, I18n.Get("phone.buy")) })
                .ToArray();

            responsesField.SetValue(box, extended);

            // Rebuild question UI so the new response becomes clickable immediately.
            AccessTools.Method(typeof(DialogueBox), "setUpQuestions")?.Invoke(box, Array.Empty<object?>());
        }
        catch (Exception ex)
        {
            Monitor.Log($"OnlineShopping: failed to inject Buy option for Robin.\n{ex}", LogLevel.Warn);
        }
    }

    private static bool Before_TryHandleOutgoingCall(string callId, ref bool __result)
    {
        if (!string.Equals(callId, BuyCarpenterCallId, StringComparison.Ordinal))
            return true;

        try
        {
            // Open the vanilla carpenter shop remotely (v1).
            // Note: shop IDs are case-sensitive depending on the game version/data.
            __result =
                Utility.TryOpenShopMenu("Carpenter", ownerName: "Robin", playOpenSound: true) ||
                Utility.TryOpenShopMenu("carpenter", ownerName: "Robin", playOpenSound: true);

            if (!__result)
                Monitor.Log("OnlineShopping: couldn't open the Carpenter shop menu (unknown shopId).", LogLevel.Warn);
        }
        catch (Exception ex)
        {
            __result = false;
            Monitor.Log($"OnlineShopping: error while opening the Carpenter shop menu.\n{ex}", LogLevel.Error);
        }

        return false; // handled
    }
}
