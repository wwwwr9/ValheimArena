using UnityEngine;

namespace ValheimArena.Core
{
    /// <summary>
    /// Полёт бойцов арены. В игре летун в погоне идёт к точке цели, поднятой лишь до m_flyAltitudeMin над землёй,
    /// а зависает там, где «видит» цель в радиусе атаки. На арене цель есть и видна всегда — и летуны висят у земли,
    /// а атаки, разрешённые только с высоты (m_aiWhenFlyingAltitudeMin), не срабатывают вовсе.
    /// Здесь точка движения поднимается: подлёт — на крейсерской высоте, у цели — на высоте, с которой достаёт
    /// текущая атака (стрелки держат высоту, кусачие пикируют).
    /// </summary>
    public static class ArenaFlight
    {
        /// <summary>Крейсерская высота над землёй: из правил арены или по самому существу и его атакам.</summary>
        public static float Cruise(ArenaUnit u)
        {
            var r = u.Rules;
            var ai = u.AI;
            float cruise;
            if (r != null && r.flyHeight > 0.1f)
            {
                cruise = r.flyHeight;
            }
            else
            {
                float min = ai ? ai.m_flyAltitudeMin : 3f;
                float max = ai ? Mathf.Min(ai.m_flyAltitudeMax, 40f) : 10f;
                cruise = Mathf.Clamp(Mathf.Max(min + 3f, (min + max) * 0.5f), 4f, 15f);
                // Атака, которую можно применять только с высоты, должна быть доступна с крейсерской высоты.
                if (u.Character is Humanoid h)
                {
                    var inv = h.GetInventory();
                    if (inv != null)
                        foreach (var item in inv.GetAllItems())
                        {
                            var s = item?.m_shared;
                            if (s == null || !s.m_aiWhenFlying || !item.IsWeapon()) continue;
                            if (s.m_aiWhenFlyingAltitudeMin > 0f) cruise = Mathf.Max(cruise, s.m_aiWhenFlyingAltitudeMin + 1.5f);
                            if (s.m_aiWhenFlyingAltitudeMax < 1000f) cruise = Mathf.Min(cruise, s.m_aiWhenFlyingAltitudeMax - 0.5f);
                        }
                }
            }
            if (r != null && r.heightLimit > 0.1f) cruise = Mathf.Min(cruise, r.heightLimit - 1f);
            return Mathf.Max(1.5f, cruise);
        }

        /// <summary>Высота поверхности под точкой: суша или вода (летуны над морем не ныряют).</summary>
        public static float Surface(Vector3 p)
        {
            var zs = ZoneSystem.instance;
            if (!zs) return p.y;
            float ground = zs.GetGroundHeight(p);
            return Mathf.Max(ground, zs.m_waterLevel);
        }

        /// <summary>Поднять точку движения летуна в режиме «идёт к врагам».</summary>
        public static Vector3 AdjustMoveTarget(ArenaUnit u, Vector3 point)
        {
            float cruise = Cruise(u);
            float range = 3f, needAlt = 0f, maxAlt = float.MaxValue;
            var weapon = (u.Character as Humanoid)?.GetCurrentWeapon();
            if (weapon?.m_shared != null)
            {
                range = Mathf.Max(1f, weapon.m_shared.m_aiAttackRange);
                if (weapon.m_shared.m_aiWhenFlying)
                {
                    needAlt = weapon.m_shared.m_aiWhenFlyingAltitudeMin;
                    if (weapon.m_shared.m_aiWhenFlyingAltitudeMax < 1000f) maxAlt = weapon.m_shared.m_aiWhenFlyingAltitudeMax;
                }
            }
            Vector3 pos = u.transform.position;
            float dxz = Utils.DistanceXZ(point, pos);
            float reach = range * 0.85f;
            // Далеко — крейсерская высота; в радиусе атаки — не выше, чем атака достаёт (3D-дистанция).
            float want = dxz > reach ? cruise : Mathf.Min(cruise, Mathf.Sqrt(Mathf.Max(0f, reach * reach - dxz * dxz)));
            want = Mathf.Max(want, needAlt + 0.5f);
            want = Mathf.Min(want, maxAlt - 0.5f);
            float baseY = Mathf.Max(point.y, Surface(point));
            point.y = Mathf.Max(point.y, baseY + Mathf.Max(0f, want));
            return point;
        }

        /// <summary>«Стоит на месте» для летуна: висеть на крейсерской высоте над точкой, где стоит.</summary>
        public static Vector3 HoldMoveDir(ArenaUnit u)
        {
            Vector3 pos = u.transform.position;
            float desired = Surface(pos) + Cruise(u);
            float dy = Mathf.Clamp((desired - pos.y) / 3f, -1f, 1f);
            return Mathf.Abs(dy) < 0.05f ? Vector3.zero : new Vector3(0f, dy, 0f);
        }

        /// <summary>Высота появления летуна: не у самой земли.</summary>
        public static float SpawnAltitude(GameObject prefab)
        {
            var ai = prefab ? prefab.GetComponent<BaseAI>() : null;
            return ai ? Mathf.Clamp(ai.m_flyAltitudeMin + 2f, 3f, 10f) : 3f;
        }

        /// <summary>
        /// Всегда летающий по прототипу боец (без случайных посадок), приземлившийся и не атакующий дольше 4 с —
        /// поднимаем. Существа со своим циклом взлёт/посадка (m_randomFly) не трогаем: у них это часть поведения.
        /// </summary>
        public static void EnsureAirborne(ArenaUnit u, float dt)
        {
            if (!u.PrefabFlies || !u.IsOwner || !u.AI || u.AI.m_randomFly) { u.GroundedFor = 0f; return; }
            var ch = u.Character;
            if (ch.IsFlying() || ch.InAttack() || ch.IsStaggering()) { u.GroundedFor = 0f; return; }
            u.GroundedFor += dt;
            if (u.GroundedFor < 4f) return;
            u.GroundedFor = 0f;
            ch.TakeOff();
        }
    }
}
