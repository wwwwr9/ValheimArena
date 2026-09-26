using HarmonyLib;
using ValheimArena.Core;
using ValheimArena.UI;

namespace ValheimArena.Patches
{
    /// <summary>Свободная камера и слежение подменяют кадр GameCamera целиком.</summary>
    [HarmonyPatch(typeof(GameCamera), "LateUpdate")]
    internal static class GameCameraLateUpdatePatch
    {
        private static bool Prefix(GameCamera __instance)
        {
            if (!ArenaCamera.Active) return true;
            ArenaCamera.UpdateCamera(__instance);
            return false;
        }
    }

    /// <summary>
    /// Для игры наша камера — «свободный полёт»: персонаж перестаёт слушать ввод, хотбар скрыт,
    /// трава и детали освещения считаются от камеры, а не от игрока.
    /// </summary>
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.InFreeFly))]
    internal static class InFreeFlyPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (!__result && ArenaCamera.Active) __result = true;
        }
    }

    /// <summary>Пока открыто окно мода — курсор свободен.</summary>
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
    internal static class MouseCapturePatch
    {
        private static void Postfix()
        {
            if (!ArenaUI.WantsCursor) return;
            ZCursor.LockState = UnityEngine.CursorLockMode.None;
            ZCursor.Show();
        }
    }

    /// <summary>Пока открыто окно мода — персонаж не бегает и не бьёт от кликов по окну.</summary>
    [HarmonyPatch(typeof(PlayerController), "TakeInput")]
    internal static class PlayerControllerTakeInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (__result && ArenaUI.WantsCursor) __result = false;
        }
    }

    [HarmonyPatch(typeof(Player), "TakeInput")]
    internal static class PlayerTakeInputPatch
    {
        private static void Postfix(ref bool __result)
        {
            if (__result && ArenaUI.WantsCursor) __result = false;
        }
    }
}
