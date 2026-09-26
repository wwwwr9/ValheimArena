using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.Patches
{
    /// <summary>
    /// Сторож владения бойцами арены (работает на сервере мира).
    ///
    /// Раз в 2 с сервер обходит объекты вокруг каждого игрока: объект этого игрока, вышедший из его активной зоны
    /// (±1,5 зоны = ±96 м от центра зоны, а не от персонажа — от него это 64…128 м), остаётся без владельца.
    /// Существо без владельца игра прячет (Character.CustomFixedUpdate: видимость = HasOwner), его ИИ и физику
    /// никто не считает, но экземпляр на клиентах живёт до ±2 зон: висят метка арены и полоска босса, бой не
    /// кончается, очистка его не находит — «призрак». Арена радиусом 70+ м упирается в эту кромку.
    ///
    /// Бойцу арены владение оставляем, пока он в ближней области владельца (там у владельца есть экземпляр,
    /// значит, он продолжает его считать). Передача другому игроку, в чьей активной зоне боец оказался, — как в игре.
    ///
    /// Боец, чей владелец отключился (игра снимает владельца только с непостоянных объектов) или у которого
    /// владельца нет, достаётся игроку, в чьей ближней области он лежит: иначе он виден, но заморожен, и бой
    /// не кончается. Так работает и выделенный сервер, у которого своих экземпляров у арены нет.
    /// </summary>
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
            // Ближняя область — по дистанции симуляции этого игрока (сервер урезает её до своей): там у него экземпляры.
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

        /// <summary>Владелец есть и он в игре: сам сервер или подключённый игрок.</summary>
        public static bool Connected(long owner) =>
            owner != 0L && (owner == ZDOMan.GetSessionID() || (ZNet.instance && ZNet.instance.GetPeer(owner) != null));
    }
}
