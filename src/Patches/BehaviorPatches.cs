using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.Patches
{
    // ======================================================================== «идёт к врагам»

    /// <summary>
    /// Режим «идёт к врагам»: во время боя боец знает, где всё, что находится на арене. Без этого MonsterAI
    /// доходит до места, где последний раз видел врага, и встаёт (или бродит там же), пока тот за препятствием.
    /// </summary>
    internal static class Omniscience
    {
        public static bool Knows(BaseAI ai, Character target)
        {
            if ((object)target == null || !target) return false;
            var u = ArenaUnit.GetByAI(ai);
            if (u == null || u.Behavior != ArenaBehavior.Hunt) return false;
            var s = u.Session;
            if (s == null || !s.Live || s.State != ArenaState.Fighting) return false;
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

    // ======================================================================== «стоит на месте»

    /// <summary>
    /// Режим «стоит на месте»: всё перемещение ИИ идёт через MoveTowards/MoveTowardsSwoop — там разворачиваемся
    /// к цели, но не шагаем. Атаки (в т.ч. с рывком анимации) работают как обычно.
    /// </summary>
    internal static class HoldPosition
    {
        /// <summary>true — движение перехвачено (развернуться, но не шагать; летун лишь держит высоту).</summary>
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

    // ======================================================================== полёт

    /// <summary>
    /// Летуны в режиме «идёт к врагам» подлетают на крейсерской высоте и держат высоту атаки
    /// (иначе игра ведёт их к цели у самой земли — см. <see cref="ArenaFlight"/>).
    /// </summary>
    /// <summary>
    /// Летун в радиусе атаки игра «останавливает» (StopMoving) на той высоте, где он оказался. Если его текущая атака
    /// разрешена только выше/ниже (m_aiWhenFlyingAltitudeMin/Max), он так и висел бы без удара — подправляем высоту.
    /// </summary>
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

    // ======================================================================== призывы

    /// <summary>
    /// Кто сейчас что-то порождает. Существа, созданные внутри этих вызовов (Character.Awake), встают на сторону
    /// создателя. Стек — на случай вложенных порождений; посторонний создатель тоже кладётся (пустым), чтобы,
    /// например, слизень, разделившийся от удара бойца, не записался бойцу в союзники.
    /// </summary>
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

    /// <summary>Восстановление бойца из ZDO, иначе — порождение бойца арены встаёт на его сторону.</summary>
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

    /// <summary>Атака: удар, область, снаряд, m_spawnOnTrigger.</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
    internal static class AttackTriggerContext
    {
        private static void Prefix(Attack __instance, out bool __state) => __state = SpawnContext.Push(Reflect.AttackCharacter(__instance));
        private static Exception Finalizer(Exception __exception, bool __state) { SpawnContext.Pop(__state); return __exception; }
    }

    /// <summary>Очереди снарядов идут и после срабатывания атаки.</summary>
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

    /// <summary>
    /// Способность призыва, запускающаяся сама при появлении (m_spawnOnAwake), стартует раньше, чем игра сообщит
    /// ей владельца (Setup), — её m_owner так и остаётся пустым. Запоминаем создателя в момент появления
    /// (Awake идёт внутри атаки/снаряда, т.е. внутри контекста создателя).
    /// </summary>
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

    /// <summary>
    /// Способность призыва (боссы, посохи) порождает существ в сопрограмме, вне любого контекста, но сразу после
    /// каждого вызывает SetupAoe(порождённый, точка) — здесь и забираем порождённого на сторону призвавшего:
    /// владелец способности, а если его нет — тот, в чьём контексте способность появилась.
    /// </summary>
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
