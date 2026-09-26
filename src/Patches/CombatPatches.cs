using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ValheimArena.Core;
using ValheimArena.Profiles;

namespace ValheimArena.Patches
{
    /// <summary>
    /// Главный рычаг столкновений: кто кому враг. Решает клиент, который считает ИИ существа (его владелец),
    /// по правилам арены бойца (они у всех одинаковые) и по настройкам игроков из ZDO их персонажей.
    /// </summary>
    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), new[] { typeof(Character), typeof(Character) })]
    internal static class IsEnemyPatch
    {
        private static bool Prefix(Character a, Character b, ref bool __result)
        {
            if ((object)a == null || (object)b == null || a == b) return true;
            var ua = ArenaUnit.Get(a);
            var ub = ArenaUnit.Get(b);

            if (ua == null && ub == null)
            {
                // Дикие существа и игрок, которого существа не замечают или не могут задеть (любой игрок, не только свой).
                bool pa = a.IsPlayer(), pb = b.IsPlayer();
                if (pa != pb && PlayerPresence.Untouchable((Player)(pa ? a : b)))
                {
                    __result = false;
                    return false;
                }
                return true;
            }

            if (ua != null && ub != null)
            {
                if (ua.SessionId == ub.SessionId)
                {
                    // Посторонние между собой — как в игре (волк и олень остаются собой).
                    if (ua.Team == 0 && ub.Team == 0 && (HostilityMode)(ua.Rules?.hostilityMode ?? 0) == HostilityMode.Teams)
                        return true;
                    __result = Hostility.UnitsHostile(ua, ub);
                    return false;
                }
                // Бойцы разных арен друг другу посторонние.
                if (Isolated(ua) || Isolated(ub))
                {
                    __result = false;
                    return false;
                }
                return true;
            }

            var unit = ua ?? ub;
            var other = ua != null ? b : a;

            if (other.IsPlayer())
            {
                var player = (Player)other;
                if (PlayerPresence.Untouchable(player))
                {
                    __result = false;
                    return false;
                }
                // Правила чужой арены ещё не пришли — ведём себя как игра.
                if (unit.Rules == null) return true;
                __result = Hostility.UnitVsPlayer(unit, player, unitIsSeeker: ua != null);
                return false;
            }
            if (unit.Rules == null) return true;
            if (Isolated(unit))
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static bool Isolated(ArenaUnit u) => u.Rules?.isolateOutsiders ?? true;
    }

    /// <summary>Гуманоиды получают атаки-предметы в Start — тогда же применяем профиль атак.</summary>
    [HarmonyPatch(typeof(Humanoid), "Start")]
    internal static class HumanoidStartPatch
    {
        private static void Postfix(Humanoid __instance)
        {
            var u = ArenaUnit.Get(__instance);
            if (u) u.TryApplyItems();
        }
    }

    /// <summary>Отключённые в редакторе атаки ИИ не использует.</summary>
    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanUseAttack))]
    internal static class CanUseAttackPatch
    {
        private static void Postfix(BaseAI __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!__result) return;
            var u = ArenaUnit.Get(__instance.GetComponent<Character>());
            if (u && u.IsAttackDisabled(item)) __result = false;
        }
    }

    /// <summary>Заморозка ИИ до старта, на паузе и (по желанию) после итога.</summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    internal static class MonsterUpdateAIPatch
    {
        private static bool Prefix(MonsterAI __instance, ref bool __result)
        {
            return AIFreeze.Check(__instance, ref __result);
        }
    }

    [HarmonyPatch(typeof(AnimalAI), nameof(AnimalAI.UpdateAI))]
    internal static class AnimalUpdateAIPatch
    {
        private static bool Prefix(AnimalAI __instance, ref bool __result)
        {
            return AIFreeze.Check(__instance, ref __result);
        }
    }

    internal static class AIFreeze
    {
        public static bool Check(BaseAI ai, ref bool result)
        {
            var u = ArenaUnit.Get(ai.GetComponent<Character>());
            if (!u || !Arena.ShouldFreeze(u)) return true;
            if (u.IsOwner)
            {
                ai.StopMoving();
                ai.ChargeStop();
            }
            result = true;
            return false;
        }
    }

    /// <summary>Неуязвимость наблюдателя, дружественный огонь, множители урона профиля.</summary>
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    internal static class RpcDamagePatch
    {
        private static bool Prefix(Character __instance, HitData hit)
        {
            if (hit == null) return true;
            Character attackerCh = hit.GetAttacker();
            if (__instance == Player.m_localPlayer)
            {
                // Неуязвимость — никакого урона, ошеломления и отбрасывания; «сквозь существ» / «не реагируют» —
                // удары существ проходят мимо (урон по персонажу считает его собственный клиент).
                if (PlayerPresence.Invulnerable) return false;
                if (PlayerPresence.CreatureCannotHit(Player.m_localPlayer, attackerCh)) return false;
            }
            // И наоборот: оружие игрока-«призрака» существ не задевает.
            else if (!__instance.IsPlayer() && attackerCh is Player ap && PlayerPresence.Untouchable(ap))
            {
                return false;
            }

            var victim = ArenaUnit.Get(__instance);
            var attacker = ArenaUnit.Get(attackerCh);
            if (victim == null && attacker == null) return true;

            if (victim != null && attacker != null && victim != attacker && victim.SessionId == attacker.SessionId
                && !(victim.Rules?.friendlyFire ?? true))
            {
                if (!Hostility.UnitsHostile(attacker, victim) && !Hostility.UnitsHostile(victim, attacker))
                    return false;
            }
            // Modify, а не HitData.ApplyModifier: тот не трогает общий урон и урон «только по не-игрокам».
            if (attacker != null && Mathf.Abs(attacker.DamageDealtMultiplier - 1f) > 0.0001f)
                hit.m_damage.Modify(attacker.DamageDealtMultiplier);
            if (victim != null && Mathf.Abs(victim.DamageTakenMultiplier - 1f) > 0.0001f)
                hit.m_damage.Modify(victim.DamageTakenMultiplier);
            if (victim != null) DotAttribution.Remember(__instance, hit);
            return true;
        }
    }

    /// <summary>
    /// Урон мимо RPC_Damage: горение, яд, дым, стоимость атак здоровьем. Неуязвимого персонажа он не трогает,
    /// а горение/яд от существ — и того, кого существа задеть не могут.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    [HarmonyPriority(Priority.High)]
    internal static class PlayerApplyDamageGuard
    {
        private static bool Prefix(Character __instance, HitData hit)
        {
            if (__instance != Player.m_localPlayer || (object)__instance == null) return true;
            if (PlayerPresence.Invulnerable) return false;
            return !PlayerPresence.CreatureCannotHit(Player.m_localPlayer, hit?.GetAttacker());
        }
    }

    /// <summary>Снаряды существ пролетают сквозь игрока, которого существа задеть не могут.</summary>
    [HarmonyPatch(typeof(Projectile), "IsValidTarget")]
    internal static class ProjectileGhostPatch
    {
        private static void Postfix(Projectile __instance, IDestructible destr, ref bool __result)
        {
            if (!__result || !(destr is Player p)) return;
            if (PlayerPresence.CreatureCannotHit(p, Reflect.ProjectileOwner(__instance))) __result = false;
        }
    }

    /// <summary>Учёт нанесённого урона для статистики.</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class ApplyDamagePatch
    {
        private static void Prefix(Character __instance, out float __state)
        {
            __state = ArenaUnit.Get(__instance) != null ? __instance.GetHealth() : -1f;
        }

        private static void Postfix(Character __instance, HitData hit, float __state)
        {
            if (__state < 0f || !__instance) return;
            float delta = __state - __instance.GetHealth();
            if (delta > 0f) Arena.OnDamage(__instance, DotAttribution.Resolve(__instance, hit?.GetAttacker()), delta);
        }
    }

    /// <summary>
    /// Кто наложил горение, яд или урон духом: тики этих эффектов игра наносит без атакующего, и без памяти урон
    /// не засчитывался бы бойцу, а убийцей добитого эффектом стала бы «стихия».
    /// </summary>
    internal static class DotAttribution
    {
        private const float Memory = 20f;
        private static readonly Dictionary<Character, KeyValuePair<Character, float>> Last =
            new Dictionary<Character, KeyValuePair<Character, float>>();

        public static void Remember(Character victim, HitData hit)
        {
            var attacker = hit.GetAttacker();
            if (!attacker || attacker == victim) return;
            var d = hit.m_damage;
            if (d.m_fire <= 0f && d.m_poison <= 0f && d.m_spirit <= 0f) return;
            if (Last.Count > 256)
            {
                var dead = new List<Character>();
                foreach (var kv in Last) if (!kv.Key || Time.time - kv.Value.Value > Memory) dead.Add(kv.Key);
                foreach (var c in dead) Last.Remove(c);
            }
            Last[victim] = new KeyValuePair<Character, float>(attacker, Time.time);
        }

        public static Character Resolve(Character victim, Character attacker)
        {
            if (attacker) return attacker;
            if ((object)victim != null && Last.TryGetValue(victim, out var e) && e.Key && Time.time - e.Value < Memory) return e.Key;
            return null;
        }
    }

    /// <summary>
    /// «Оставлять добычу» выключено — добыча не выпадает и через рэгдолл. Игра выключает CharacterDrop только после
    /// того, как рэгдолл запомнил список (Ragdoll.SaveLootList → GenerateDropList), поэтому флаг проверяется в самом
    /// списке. Ванильно список строится только при включённой добыче — других вызывающих нет.
    /// </summary>
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    internal static class DropListPatch
    {
        private static readonly AccessTools.FieldRef<CharacterDrop, bool> DropsEnabled =
            AccessTools.FieldRefAccess<CharacterDrop, bool>("m_dropsEnabled");

        private static bool Prefix(CharacterDrop __instance, ref List<KeyValuePair<GameObject, int>> __result)
        {
            if (DropsEnabled(__instance)) return true;
            __result = new List<KeyValuePair<GameObject, int>>();
            return false;
        }
    }

    /// <summary>
    /// Облик по звёздам: игра задаёт его только для 1★ и 2★ и при уровне выше заданных возвращает обычный облик —
    /// 5★ выглядел бы как 0★. Выше заданного — облик последней звезды.
    /// </summary>
    [HarmonyPatch(typeof(LevelEffects), nameof(LevelEffects.SetupLevelVisualization))]
    internal static class LevelLookPatch
    {
        private static void Prefix(LevelEffects __instance, ref int level)
        {
            int max = __instance.m_levelSetups != null ? __instance.m_levelSetups.Count + 1 : 1;
            if (level > max && max > 1) level = max;
        }
    }

    /// <summary>
    /// Правило «Урон не зависит от числа игроков рядом»: внутри живой арены множители игры за игроков в 100 м
    /// (удары существ ×(1 + 0,04 на игрока), урон по существам ÷(1 + 0,3 на игрока)) равны единице.
    /// </summary>
    internal static class PlayerScaling
    {
        public static bool Neutral(Vector3 pos)
        {
            foreach (var s in Arena.Sessions.Values)
                if (s.Live && s.Rules != null && s.Rules.ignorePlayerScaling && s.IsInside(pos, 5f)) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.GetDifficultyDamageScalePlayer))]
    internal static class DifficultyScalePlayerPatch
    {
        private static bool Prefix(Vector3 pos, ref float __result)
        {
            if (!PlayerScaling.Neutral(pos)) return true;
            __result = 1f;
            return false;
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.GetDifficultyDamageScaleEnemy))]
    internal static class DifficultyScaleEnemyPatch
    {
        private static bool Prefix(Vector3 pos, ref float __result)
        {
            if (!PlayerScaling.Neutral(pos)) return true;
            __result = 1f;
            return false;
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    internal static class OnDeathPatch
    {
        private static void Prefix(Character __instance, out bool __state)
        {
            __state = false;
            if (ArenaUnit.Get(__instance) == null) return;
            var last = Reflect.LastHit(__instance);
            Arena.OnDeath(__instance, DotAttribution.Resolve(__instance, last?.GetAttacker()));
            // Существа, которых порождает смерть (m_deathEffects: деление слизней и т.п.), — на сторону погибшего.
            __state = SpawnContext.Push(__instance);
        }

        private static System.Exception Finalizer(System.Exception __exception, bool __state)
        {
            SpawnContext.Pop(__state);
            return __exception;
        }
    }

    /// <summary>
    /// AoE по умолчанию не бьёт существ с тем же именем, что у владельца (m_hitSame) и бьёт союзников
    /// (m_hitFriendly). На арене «тролль против тролля» это ломает бой, поэтому решаем по командам.
    /// </summary>
    [HarmonyPatch(typeof(Aoe), "ShouldHit")]
    internal static class AoeShouldHitPatch
    {
        private static void Postfix(Aoe __instance, Collider collider, ref bool __result)
        {
            if (!collider) return;
            var owner = Reflect.AoeOwner(__instance);
            var go = Projectile.FindHitObject(collider);
            if (!go) return;
            var target = go.GetComponent<Character>();
            if (!target || target == owner) return;
            // Площадной удар существа не задевает игрока «сквозь существ» / без реакции существ.
            if (__result && target is Player tp && PlayerPresence.CreatureCannotHit(tp, owner))
            {
                __result = false;
                return;
            }
            if (__result && owner is Player op && !target.IsPlayer() && PlayerPresence.Untouchable(op))
            {
                __result = false;
                return;
            }
            var ou = ArenaUnit.Get(owner);
            if (ou == null) return;
            var tu = ArenaUnit.Get(target);
            if (tu == null || tu.SessionId != ou.SessionId) return;
            var s = ou.Rules;
            if (s == null) return;
            bool hostile = Hostility.UnitsHostile(ou, tu);
            if (hostile)
            {
                if (!__result && s.aoeHitsSameKind && target.m_name == owner.m_name && __instance.m_hitEnemy && __instance.m_hitCharacters
                    && !(__instance.m_dodgeable && target.IsDodgeInvincible()))
                    __result = true;
            }
            else if (!s.friendlyFire)
            {
                __result = false;
            }
        }
    }

    /// <summary>Собственный урон эффектов (AoE) из профиля бойца.</summary>
    [HarmonyPatch(typeof(Aoe), nameof(Aoe.Setup))]
    internal static class AoeSetupPatch
    {
        private static void Postfix(Aoe __instance, Character owner, HitData hitData)
        {
            var u = ArenaUnit.Get(owner);
            if (u == null || u.Effects.Count == 0) return;
            if (hitData != null && __instance.m_useAttackSettings) return;
            if (!EffectKey.Find(u, __instance, out var fx)) return;
            __instance.m_damage = fx.damage.ToDamageTypes();
            __instance.m_attackForce = fx.attackForce;
        }
    }

    /// <summary>
    /// Правка эффекта записана под именем префаба, у которого нашёлся Aoe/Projectile (он может висеть на дочернем
    /// объекте) — ищем от объекта компонента вверх до корня.
    /// </summary>
    internal static class EffectKey
    {
        public static bool Find(ArenaUnit u, Component c, out EffectOverride fx)
        {
            for (var t = c.transform; t; t = t.parent)
                if (u.Effects.TryGetValue(Utils.GetPrefabName(t.gameObject), out fx)) return true;
            fx = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
    internal static class ProjectileSetupPatch
    {
        private static void Postfix(Projectile __instance, Character owner, HitData hitData)
        {
            if (hitData != null) return;
            var u = ArenaUnit.Get(owner);
            if (u == null || u.Effects.Count == 0) return;
            if (!EffectKey.Find(u, __instance, out var fx)) return;
            __instance.m_damage = fx.damage.ToDamageTypes();
            __instance.m_attackForce = fx.attackForce;
        }
    }
}
