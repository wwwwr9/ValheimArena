using HarmonyLib;
using UnityEngine;

namespace ValheimArena
{
    /// <summary>Доступ к закрытым полям и методам игры.</summary>
    internal static class Reflect
    {
        public static readonly AccessTools.FieldRef<MonsterAI, Character> TargetCreature =
            AccessTools.FieldRefAccess<MonsterAI, Character>("m_targetCreature");

        public static readonly AccessTools.FieldRef<MonsterAI, StaticTarget> TargetStatic =
            AccessTools.FieldRefAccess<MonsterAI, StaticTarget>("m_targetStatic");

        public static readonly AccessTools.FieldRef<MonsterAI, Vector3> LastKnownTargetPos =
            AccessTools.FieldRefAccess<MonsterAI, Vector3>("m_lastKnownTargetPos");

        public static readonly AccessTools.FieldRef<MonsterAI, bool> BeenAtLastPos =
            AccessTools.FieldRefAccess<MonsterAI, bool>("m_beenAtLastPos");

        public static readonly AccessTools.FieldRef<MonsterAI, float> TimeSinceSensedTarget =
            AccessTools.FieldRefAccess<MonsterAI, float>("m_timeSinceSensedTargetCreature");

        public static readonly AccessTools.FieldRef<MonsterAI, float> TimeSinceAttacking =
            AccessTools.FieldRefAccess<MonsterAI, float>("m_timeSinceAttacking");

        public static readonly AccessTools.FieldRef<BaseAI, Vector3> SpawnPoint =
            AccessTools.FieldRefAccess<BaseAI, Vector3>("m_spawnPoint");

        public static readonly AccessTools.FieldRef<Character, HitData> LastHit =
            AccessTools.FieldRefAccess<Character, HitData>("m_lastHit");

        public static readonly AccessTools.FieldRef<Character, Rigidbody> Body =
            AccessTools.FieldRefAccess<Character, Rigidbody>("m_body");

        public static readonly AccessTools.FieldRef<Character, CapsuleCollider> Collider =
            AccessTools.FieldRefAccess<Character, CapsuleCollider>("m_collider");

        public static readonly AccessTools.FieldRef<Aoe, Character> AoeOwner =
            AccessTools.FieldRefAccess<Aoe, Character>("m_owner");

        public static readonly AccessTools.FieldRef<GameCamera, AudioListener> Listener =
            AccessTools.FieldRefAccess<GameCamera, AudioListener>("m_listner");

        public static readonly AccessTools.FieldRef<GameCamera, Camera> Camera =
            AccessTools.FieldRefAccess<GameCamera, Camera>("m_camera");

        public static readonly AccessTools.FieldRef<GameCamera, Camera> SkyCamera =
            AccessTools.FieldRefAccess<GameCamera, Camera>("m_skyCamera");

        public static readonly AccessTools.FieldRef<Attack, Humanoid> AttackCharacter =
            AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");

        public static readonly AccessTools.FieldRef<Projectile, Character> ProjectileOwner =
            AccessTools.FieldRefAccess<Projectile, Character>("m_owner");

        public static readonly AccessTools.FieldRef<SpawnAbility, Character> SpawnAbilityOwner =
            AccessTools.FieldRefAccess<SpawnAbility, Character>("m_owner");

        private static readonly System.Reflection.MethodInfo SetPatrolPointMethod =
            AccessTools.Method(typeof(BaseAI), "SetPatrolPoint", new[] { typeof(Vector3) });

        /// <summary>Точка патруля (как у босса у алтаря): вокруг неё существо бродит без цели.</summary>
        public static void SetPatrolPoint(BaseAI ai, Vector3 point)
        {
            try { SetPatrolPointMethod?.Invoke(ai, new object[] { point }); }
            catch (System.Exception e) { ArenaPlugin.Log.LogDebug("SetPatrolPoint: " + e.Message); }
        }

        private static readonly System.Reflection.MethodInfo WakeupMethod = AccessTools.Method(typeof(MonsterAI), "Wakeup");
        private static readonly System.Reflection.MethodInfo SetAlertedMethod = AccessTools.Method(typeof(BaseAI), "SetAlerted");

        public static void MonsterWakeup(MonsterAI ai)
        {
            try { WakeupMethod?.Invoke(ai, null); }
            catch (System.Exception e) { ArenaPlugin.Log.LogDebug("Wakeup: " + e.Message); }
        }

        public static void SetAlerted(BaseAI ai, bool alerted)
        {
            try { SetAlertedMethod?.Invoke(ai, new object[] { alerted }); }
            catch (System.Exception e) { ArenaPlugin.Log.LogDebug("SetAlerted: " + e.Message); }
        }
    }
}
