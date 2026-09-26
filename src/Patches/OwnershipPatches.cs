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
    /// </summary>
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
            // Та же выборка, что у самого ReleaseNearbyZDOS.
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
