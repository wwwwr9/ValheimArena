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
        private static readonly List<ZDO> Near = new List<ZDO>();
        private static readonly List<ZDO> Keep = new List<ZDO>();
        private static readonly List<ZDO> Orphans = new List<ZDO>();

        private static void Prefix(ZDOMan __instance, Vector3 refPosition, long uid)
        {
            Keep.Clear();
            Orphans.Clear();
            if (!ZNet.instance || !ZoneSystem.instance) return;
            var zone = ZoneSystem.GetZone(refPosition);
            var peer = uid == ZDOMan.GetSessionID() ? null : ZNet.instance.GetPeer(uid);
            var sim = peer != null ? peer.m_simulationDistance : ZNet.instance.GetSyncedSimulationDistance();
            Near.Clear();
            __instance.FindSectorObjects(zone, new SimulationDistance(sim.NearSimulationDistance, 0, sim.IsClassic), Near);
            foreach (var zdo in Near)
            {
                if (!zdo.Persistent || !zdo.GetBool(UnitHash)) continue;
                long owner = zdo.GetOwner();
                if (owner == uid)
                {
                    if (!ZNetScene.InActiveArea(zdo.GetPosition(), zone)) Keep.Add(zdo);
                }
                else if (!Connected(owner))
                {
                    Orphans.Add(zdo);
                }
            }
            Near.Clear();
        }

        private static void Postfix(long uid)
        {
            foreach (var zdo in Keep)
                if (zdo.IsValid() && !zdo.HasOwner()) zdo.SetOwner(uid);
            foreach (var zdo in Orphans)
                if (zdo.IsValid() && !Connected(zdo.GetOwner())) zdo.SetOwner(uid);
            Keep.Clear();
            Orphans.Clear();
        }

        public static bool Connected(long owner) =>
            owner != 0L && (owner == ZDOMan.GetSessionID() || (ZNet.instance && ZNet.instance.GetPeer(owner) != null));
    }
}
