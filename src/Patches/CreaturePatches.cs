using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.Patches
{
    // ======================================================================== союзники лекарей и поддержки

    /// <summary>
    /// Атаки «на раненого союзника» (лечение) и «на союзника» (поддержка): друг в игре — всякий, кто не враг
    /// (BaseAI.HaveFriendInRange / HaveHurtFriendInRange), первый попавшийся. Изоляция арены делает «не врагами»
    /// всех посторонних — лекарь шёл бы лечить зрителей и диких за стеной. Бойцу арены друг — только не враждебный
    /// ему боец той же арены, ближайший. Лечение себя, как в игре, остаётся.
    /// </summary>
    internal static class ArenaFriends
    {
        public static bool Find(BaseAI ai, List<Character> characters, float range, bool hurtOnly, out Character result)
        {
            result = null;
            var me = ArenaUnit.GetByAI(ai);
            if (me == null || me.Session == null) return false;
            float best = float.MaxValue;
            Vector3 pos = ai.transform.position;
            foreach (var c in characters)
            {
                if (!c || c.IsDead()) continue;
                if (c == me.Character)
                {
                    if (!hurtOnly) continue;
                }
                else
                {
                    var u = ArenaUnit.Get(c);
                    if (u == null || u.SessionId != me.SessionId) continue;
                    if (Hostility.UnitsHostile(me, u) || Hostility.UnitsHostile(u, me)) continue;
                }
                if (hurtOnly && c.GetHealth() >= c.GetMaxHealth()) continue;
                float d = Vector3.Distance(c.transform.position, pos);
                if (d > range || d >= best) continue;
                best = d;
                result = c;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(BaseAI), "HaveFriendInRange", new[] { typeof(List<Character>), typeof(float) })]
    internal static class FriendInRangePatch
    {
        private static bool Prefix(BaseAI __instance, List<Character> characters, float range, ref Character __result) =>
            !ArenaFriends.Find(__instance, characters, range, false, out __result);
    }

    [HarmonyPatch(typeof(BaseAI), "HaveHurtFriendInRange", new[] { typeof(List<Character>), typeof(float) })]
    internal static class HurtFriendInRangePatch
    {
        private static bool Prefix(BaseAI __instance, List<Character> characters, float range, ref Character __result) =>
            !ArenaFriends.Find(__instance, characters, range, true, out __result);
    }

    // ======================================================================== жизненный цикл бойца

    /// <summary>
    /// Детёныш-боец не вырастает: игра создаёт взрослого новым объектом (Growup), и тот потерял бы арену,
    /// команду, профиль и поведение, а строка статистики детёныша осталась бы «жив».
    /// </summary>
    [HarmonyPatch(typeof(Growup), "GrowUpdate")]
    internal static class GrowupPatch
    {
        private static bool Prefix(Growup __instance) => ArenaUnit.Get(__instance.GetComponent<Character>()) == null;
    }

    /// <summary>Приручённым бойцом зрители не командуют («за мной» / «стой») — это сбивает его в бою.</summary>
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.Interact))]
    internal static class TameableInteractPatch
    {
        private static bool Prefix(Tameable __instance, ref bool __result)
        {
            if (ArenaUnit.Get(__instance.GetComponent<Character>()) == null) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>Бойца не приручают посреди боя (он ест добычу, пока не встревожен, и становится приручённым).</summary>
    [HarmonyPatch(typeof(Tameable), "TamingUpdate")]
    internal static class TamingUpdatePatch
    {
        private static bool Prefix(Tameable __instance) => ArenaUnit.Get(__instance.GetComponent<Character>()) == null;
    }

    /// <summary>Приручённые бойцы не размножаются: потомство не было бы бойцом и осталось бы в мире.</summary>
    [HarmonyPatch(typeof(Procreation), "Procreate")]
    internal static class ProcreatePatch
    {
        private static bool Prefix(Procreation __instance) => ArenaUnit.Get(__instance.GetComponent<Character>()) == null;
    }
}
