using HarmonyLib;
using ValheimArena.UI;

namespace ValheimArena.Patches
{
    /// <summary>
    /// Пока в поле ввода мода набирается текст, кнопки игры молчат: буква «m» в поиске
    /// не открывает карту, «Tab» — инвентарь и т.п.
    /// </summary>
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

    /// <summary>
    /// Esc в поле ввода мода только снимает фокус: меню паузы игра открывает по низкоуровневому
    /// ZInput.GetKeyDown(Escape) (Menu.Update), мимо кнопок игры.
    /// </summary>
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

    /// <summary>Колесо над окном мода прокручивает окно, а не камеру игры.</summary>
    [HarmonyPatch(typeof(ZInput), "Internal_GetMouseScrollWheel")]
    internal static class ScrollWheelPatch
    {
        private static void Postfix(ref float __result)
        {
            if (ArenaUI.WantsMouse) __result = 0f;
        }
    }
}
