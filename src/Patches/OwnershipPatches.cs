using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.Patches
{
    [HarmonyPatch(typeof(ZDOMan), "ReleaseNearbyZDOS", new[] { typeof(Vector3), typeof(long) })]
    internal static class KeepArenaOwnershipPatch
    {
        private static readonly int UnitHash = ArenaUnit.ZUnit.GetStableHashCode();

        private static void Prefix(ZDOMan __instance, Vector3 refPosition, long uid, out List<ZDO> __state)
        {
            __state = null;
            if (!ZNet.instance || !ZoneSystem.instance) return;
            var zone = ZoneSystem.GetZone(refPosition);
            var sim = ZNet.instance.GetSyncedSimulationDistance();
            var near = new List<ZDO>();
            __instance.FindSectorObjects(zone, new SimulationDistance(sim.NearSimulationDistance, 0, sim.IsClassic), near);
            foreach (var zdo in near)
            {
                if (!zdo.Persistent || zdo.GetOwner() != uid) continue;
                if (ZNetScene.InActiveArea(zdo.GetPosition(), zone)) continue;
                if (!zdo.GetBool(UnitHash)) continue;
                (__state ??= new List<ZDO>()).Add(zdo);
            }
        }

        private static void Postfix(long uid, List<ZDO> __state)
        {
            if (__state == null) return;
            foreach (var zdo in __state)
                if (zdo.IsValid() && !zdo.HasOwner()) zdo.SetOwner(uid);
        }
    }
}
