using HarmonyLib;
using ValheimArena.Core;

namespace ValheimArena.Patches
{
    /// <summary>
    /// Закрытие меню Esc (Game.Unpause) возвращает игре ход времени ×1 — замедление арены вернуть, иначе
    /// оверлей показывал бы «×0.25», а игра шла бы как обычно.
    /// </summary>
    [HarmonyPatch(typeof(Game), nameof(Game.Unpause))]
    internal static class UnpauseTimeScalePatch
    {
        private static void Postfix() => Arena.OnGameUnpaused();
    }
}
