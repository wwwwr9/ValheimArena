using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.Patches
{
    internal static class Omniscience
    {
        public static bool Knows(BaseAI ai, Character target)
        {
            if ((object)target == null || !target) return false;
            var u = ArenaUnit.GetByAI(ai);
            if (u == null || u.Behavior != ArenaBehavior.Hunt) return false;
            var s = u.Session;
            if (s == null || !s.Live || s.State != ArenaState.Fighting) return false;
            if (target is Player p && (p.InGhostMode() || p.InDebugFlyMode())) return false;
            return !target.IsDead() && s.IsInside(target.transform.position, 5f);
        }
    }

    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSenseTarget), new[] { typeof(Character), typeof(bool) })]
    internal static class CanSenseTargetPatch
    {
        private static bool Prefix(BaseAI __instance, Character target, ref bool __result)
        {
            if (!Omniscience.Knows(__instance, target)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSeeTarget), new[] { typeof(Character) })]
    internal static class CanSeeTargetPatch
    {
        private static bool Prefix(BaseAI __instance, Character target, ref bool __result)
        {
            if (!Omniscience.Knows(__instance, target)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanHearTarget), new[] { typeof(Character) })]
    internal static class CanHearTargetPatch
    {
        private static bool Prefix(BaseAI __instance, Character target, ref bool __result)
        {
            if (!Omniscience.Knows(__instance, target)) return true;
            __result = true;
            return false;
        }
    }

    internal static class HoldPosition
    {
        public static bool Hold(BaseAI ai, Vector3 dir)
        {
            var u = ArenaUnit.GetByAI(ai);
            if (u == null || u.Behavior != ArenaBehavior.Hold) return false;
            var s = u.Session;
            if (s == null || !s.Live) return false;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude > 0.0001f) ai.LookTowards(flat.normalized);
            if (u.Character && u.Character.IsFlying()) u.Character.SetMoveDir(ArenaFlight.HoldMoveDir(u));
            else ai.StopMoving();
            return true;
        }
    }

    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.MoveTowards))]
    internal static class MoveTowardsPatch
    {
        private static bool Prefix(BaseAI __instance, Vector3 dir) => !HoldPosition.Hold(__instance, dir);
    }

    [HarmonyPatch(typeof(BaseAI), "MoveTowardsSwoop")]
    internal static class MoveTowardsSwoopPatch
    {
        private static bool Prefix(BaseAI __instance, Vector3 dir) => !HoldPosition.Hold(__instance, dir);
    }

    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    internal static class FlyingAltitudeKeeper
    {
        private static void Postfix(MonsterAI __instance)
        {
            var u = ArenaUnit.GetByAI(__instance);
            if (u == null || u.Behavior != ArenaBehavior.Hunt || !u.IsOwner) return;
            var ch = u.Character;
            if (!ch || !ch.IsFlying() || ch.InAttack()) return;
            var s = u.Session;
            if (s == null || !s.Live || s.State != ArenaState.Fighting) return;
            if (ch.GetMoveDir().sqrMagnitude > 0.01f) return;
            var weapon = (ch as Humanoid)?.GetCurrentWeapon();
            if (weapon?.m_shared == null || !weapon.m_shared.m_aiWhenFlying) return;
            float min = weapon.m_shared.m_aiWhenFlyingAltitudeMin;
            float max = weapon.m_shared.m_aiWhenFlyingAltitudeMax;
            float alt = ch.transform.position.y - ArenaFlight.Surface(ch.transform.position);
            if (min > 0f && alt < min + 0.3f) ch.SetMoveDir(Vector3.up);
            else if (max < 1000f && alt > max - 0.3f) ch.SetMoveDir(Vector3.down);
        }
    }

    [HarmonyPatch(typeof(BaseAI), "MoveTo")]
    internal static class FlyingMoveToPatch
    {
        private static void Prefix(BaseAI __instance, ref Vector3 point)
        {
            var u = ArenaUnit.GetByAI(__instance);
            if (u == null || u.Behavior != ArenaBehavior.Hunt || !u.Character || !u.Character.IsFlying()) return;
            var s = u.Session;
            if (s == null || !s.Live || s.State != ArenaState.Fighting) return;
            point = ArenaFlight.AdjustMoveTarget(u, point);
        }
    }

    internal static class SpawnContext
    {
        private static readonly List<ArenaUnit> Stack = new List<ArenaUnit>();

        public static ArenaUnit Current => Stack.Count > 0 ? Stack[Stack.Count - 1] : null;

        public static bool Push(Character c)
        {
            Stack.Add(ArenaUnit.Get(c));
            return true;
        }

        public static void Pop(bool pushed)
        {
            if (pushed && Stack.Count > 0) Stack.RemoveAt(Stack.Count - 1);
        }
    }

    [HarmonyPatch(typeof(Character), "Awake")]
    internal static class CharacterAwakePatch
    {
        private static void Postfix(Character __instance)
        {
            ArenaUnit.TryAttachFromZdo(__instance);
            var summoner = SpawnContext.Current;
            if (summoner && !ArenaUnit.Get(__instance) && !__instance.IsPlayer())
                Arena.AdoptSummon(__instance, summoner);
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
    internal static class AttackTriggerContext
    {
        private static void Prefix(Attack __instance, out bool __state) => __state = SpawnContext.Push(Reflect.AttackCharacter(__instance));
        private static Exception Finalizer(Exception __exception, bool __state) { SpawnContext.Pop(__state); return __exception; }
    }

    [HarmonyPatch(typeof(Attack), "FireProjectileBurst")]
    internal static class AttackBurstContext
    {
        private static void Prefix(Attack __instance, out bool __state) => __state = SpawnContext.Push(Reflect.AttackCharacter(__instance));
        private static Exception Finalizer(Exception __exception, bool __state) { SpawnContext.Pop(__state); return __exception; }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.SpawnOnHitTerrain))]
    internal static class AttackSpawnOnTerrainContext
    {
        private static void Prefix(Character character, out bool __state) => __state = SpawnContext.Push(character);
        private static Exception Finalizer(Exception __exception, bool __state) { SpawnContext.Pop(__state); return __exception; }
    }

    [HarmonyPatch(typeof(Projectile), "SpawnOnHit")]
    internal static class ProjectileSpawnContext
    {
        private static void Prefix(Projectile __instance, out bool __state) => __state = SpawnContext.Push(Reflect.ProjectileOwner(__instance));
        private static Exception Finalizer(Exception __exception, bool __state) { SpawnContext.Pop(__state); return __exception; }
    }

    [HarmonyPatch(typeof(SE_Spawn), nameof(SE_Spawn.UpdateStatusEffect))]
    internal static class StatusSpawnContext
    {
        private static void Prefix(SE_Spawn __instance, out bool __state) => __state = SpawnContext.Push(__instance.m_character);
        private static Exception Finalizer(Exception __exception, bool __state) { SpawnContext.Pop(__state); return __exception; }
    }

    [HarmonyPatch(typeof(SpawnOnDamaged), "OnDamaged")]
    internal static class DamagedSpawnContext
    {
        private static void Prefix(SpawnOnDamaged __instance, out bool __state) => __state = SpawnContext.Push(__instance.GetComponent<Character>());
        private static Exception Finalizer(Exception __exception, bool __state) { SpawnContext.Pop(__state); return __exception; }
    }

    [HarmonyPatch(typeof(Destructible), "RPC_Damage")]
    internal static class DestructibleSpawnContext
    {
        private static void Prefix(out bool __state) => __state = SpawnContext.Push(null);
        private static Exception Finalizer(Exception __exception, bool __state) { SpawnContext.Pop(__state); return __exception; }
    }

    [HarmonyPatch(typeof(SpawnAbility), nameof(SpawnAbility.Awake))]
    internal static class SpawnAbilityRemember
    {
        public static readonly Dictionary<SpawnAbility, ArenaUnit> Creators = new Dictionary<SpawnAbility, ArenaUnit>();

        private static void Postfix(SpawnAbility __instance)
        {
            var creator = SpawnContext.Current;
            if (!creator) return;
            if (Creators.Count > 64)
            {
                var dead = new List<SpawnAbility>();
                foreach (var kv in Creators)
                    if (!kv.Key || !kv.Value) dead.Add(kv.Key);
                foreach (var k in dead) Creators.Remove(k);
            }
            Creators[__instance] = creator;
        }
    }

    [HarmonyPatch(typeof(SpawnAbility), "SetupAoe")]
    internal static class SpawnAbilityAdopt
    {
        private static void Prefix(SpawnAbility __instance, Character owner)
        {
            if (!owner) return;
            var summoner = ArenaUnit.Get(Reflect.SpawnAbilityOwner(__instance));
            if (!summoner) SpawnAbilityRemember.Creators.TryGetValue(__instance, out summoner);
            if (summoner) Arena.AdoptSummon(owner, summoner);
        }
    }
}
