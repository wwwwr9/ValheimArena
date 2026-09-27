using HarmonyLib;
using ValheimArena.UI;

namespace ValheimArena.Patches
{
    [HarmonyPatch(typeof(ZInput), "TryGetButtonState")]
    internal static class ButtonStatePatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!ArenaUI.WantsKeyboard) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetKeyDown), new[] { typeof(UnityEngine.KeyCode), typeof(bool) })]
    internal static class EscapeKeyPatch
    {
        private static bool Prefix(UnityEngine.KeyCode key, ref bool __result)
        {
            if (key != UnityEngine.KeyCode.Escape || !ArenaUI.WantsKeyboard) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ZInput), "Internal_GetMouseScrollWheel")]
    internal static class ScrollWheelPatch
    {
        private static void Postfix(ref float __result)
        {
            if (ArenaUI.WantsMouse) __result = 0f;
        }
    }
}
