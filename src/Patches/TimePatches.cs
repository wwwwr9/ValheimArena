using HarmonyLib;
using ValheimArena.Core;

namespace ValheimArena.Patches
{
    [HarmonyPatch(typeof(Game), nameof(Game.Unpause))]
    internal static class UnpauseTimeScalePatch
    {
        private static void Postfix() => Arena.OnGameUnpaused();
    }
}
