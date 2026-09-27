using System.Collections.Generic;
using UnityEngine;

namespace ValheimArena.Core
{
    public static class PlayerPresence
    {
        private static readonly int ZReact = "va_p_react".GetStableHashCode();
        private static readonly int ZTeam = "va_p_team".GetStableHashCode();
        private static readonly int ZVisible = "va_p_visible".GetStableHashCode();
        private static readonly int ZCollision = "va_p_coll".GetStableHashCode();

        private class Applied
        {
            public readonly HashSet<Renderer> Hidden = new HashSet<Renderer>();
            public readonly HashSet<Collider> Ignored = new HashSet<Collider>();
            public bool Visible = true;
            public CollisionMode Collision = CollisionMode.Normal;
            public RigidbodyConstraints SavedConstraints;
            public bool SavedColliderEnabled = true;
        }

        private static readonly Dictionary<Player, Applied> State = new Dictionary<Player, Applied>();
        private static float _tick;

        public static bool Invulnerable => ArenaSettingsStore.Current.playerInvulnerable;

        private static readonly Dictionary<Player, ZNetView> Views = new Dictionary<Player, ZNetView>();

        private static ZDO Zdo(Player p)
        {
            if (!p) return null;
            if (!Views.TryGetValue(p, out var nv) || !nv)
            {
                nv = p.GetComponent<ZNetView>();
                Views[p] = nv;
            }
            return nv && nv.IsValid() ? nv.GetZDO() : null;
        }

        public static bool Reacts(Player p)
        {
            if (p == Player.m_localPlayer) return ArenaSettingsStore.Current.creaturesReact;
            var z = Zdo(p);
            return z == null || z.GetBool(ZReact, true);
        }

        public static int TeamOf(Player p)
        {
            if (p == Player.m_localPlayer) return ArenaSettingsStore.Current.playerTeam;
            var z = Zdo(p);
            return z == null ? 0 : z.GetInt(ZTeam, 0);
        }

        private static bool VisibleOf(Player p)
        {
            if (p == Player.m_localPlayer) return ArenaSettingsStore.Current.playerVisible;
            var z = Zdo(p);
            return z == null || z.GetBool(ZVisible, true);
        }

        public static CollisionMode CollisionOf(Player p)
        {
            if (p == Player.m_localPlayer) return (CollisionMode)ArenaSettingsStore.Current.playerCollision;
            var z = Zdo(p);
            return z == null ? CollisionMode.Normal : (CollisionMode)z.GetInt(ZCollision, 0);
        }

        public static bool Untouchable(Player p) => p && (CollisionOf(p) != CollisionMode.Normal || !Reacts(p));

        public static bool CreatureCannotHit(Player p, Character attacker) =>
            (object)attacker != null && attacker && !attacker.IsPlayer() && Untouchable(p);

        public static void Update()
        {
            _tick -= Time.unscaledDeltaTime;
            bool periodic = _tick <= 0f;
            if (periodic)
            {
                _tick = 0.5f;
                PublishLocal();
                var gone = new List<Player>();
                foreach (var kv in State)
                    if (!kv.Key) gone.Add(kv.Key);
                foreach (var p in gone) State.Remove(p);
                var goneViews = new List<Player>();
                foreach (var kv in Views)
                    if (!kv.Key) goneViews.Add(kv.Key);
                foreach (var p in goneViews) Views.Remove(p);
            }

            foreach (var p in Player.GetAllPlayers())
            {
                if (!p) continue;
                if (!State.TryGetValue(p, out var a))
                {
                    a = new Applied();
                    State[p] = a;
                }
                ApplyVisibility(p, a, VisibleOf(p), periodic);
                ApplyCollision(p, a, CollisionOf(p), periodic, p == Player.m_localPlayer);
            }
        }

        private static void PublishLocal()
        {
            var z = Zdo(Player.m_localPlayer);
            if (z == null || !z.IsOwner()) return;
            var s = ArenaSettingsStore.Current;
            if (z.GetBool(ZReact, true) != s.creaturesReact) z.Set(ZReact, s.creaturesReact);
            if (z.GetInt(ZTeam, 0) != s.playerTeam) z.Set(ZTeam, s.playerTeam);
            if (z.GetBool(ZVisible, true) != s.playerVisible) z.Set(ZVisible, s.playerVisible);
            if (z.GetInt(ZCollision, 0) != s.playerCollision) z.Set(ZCollision, s.playerCollision);
        }

        private static void ApplyVisibility(Player p, Applied a, bool visible, bool periodic)
        {
            if (visible)
            {
                if (!a.Visible)
                {
                    foreach (var r in a.Hidden)
                        if (r) r.forceRenderingOff = false;
                    a.Hidden.Clear();
                    a.Visible = true;
                }
                return;
            }
            if (a.Visible || periodic)
            {
                foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r) continue;
                    r.forceRenderingOff = true;
                    a.Hidden.Add(r);
                }
                a.Hidden.RemoveWhere(r => !r);
                a.Visible = false;
            }
        }

        private static void ApplyCollision(Player p, Applied a, CollisionMode mode, bool periodic, bool local)
        {
            var col = Reflect.Collider(p);
            var body = Reflect.Body(p);
            if (!col || !body) return;

            if (mode != a.Collision)
            {
                if (a.Collision == CollisionMode.ThroughCreatures)
                {
                    foreach (var c in a.Ignored)
                        if (c) Physics.IgnoreCollision(col, c, false);
                    a.Ignored.Clear();
                }
                else if (a.Collision == CollisionMode.Disabled)
                {
                    col.enabled = a.SavedColliderEnabled;
                    if (local) body.constraints = a.SavedConstraints;
                }

                if (mode == CollisionMode.Disabled)
                {
                    a.SavedColliderEnabled = col.enabled;
                    col.enabled = false;
                    if (local)
                    {
                        a.SavedConstraints = body.constraints;
                        body.linearVelocity = Vector3.zero;
                        body.constraints = RigidbodyConstraints.FreezeAll;
                    }
                }
                a.Collision = mode;
                periodic = true;
            }

            if (mode == CollisionMode.Disabled && periodic && col.enabled)
            {
                col.enabled = false;
            }

            if (mode == CollisionMode.ThroughCreatures && periodic)
            {
                Vector3 pp = p.transform.position;
                foreach (var ch in Character.GetAllCharacters())
                {
                    if (!ch || ch.IsPlayer()) continue;
                    if ((ch.transform.position - pp).sqrMagnitude > 150f * 150f) continue;
                    foreach (var c in ch.GetComponentsInChildren<Collider>())
                    {
                        if (!c || c.isTrigger) continue;
                        Physics.IgnoreCollision(col, c, true);
                        a.Ignored.Add(c);
                    }
                }
                a.Ignored.RemoveWhere(c => !c);
            }
        }

        public static string CollisionName(CollisionMode m)
        {
            switch (m)
            {
                case CollisionMode.Normal: return L.T("Обычная");
                case CollisionMode.ThroughCreatures: return L.T("Сквозь существ");
                default: return L.T("Отключена (заморозка на месте)");
            }
        }
    }
}
