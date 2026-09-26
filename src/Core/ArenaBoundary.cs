using UnityEngine;

namespace ValheimArena.Core
{
    /// <summary>Видимая граница арены (кольцо по рельефу + столбы) и удержание бойцов внутри.</summary>
    public static class ArenaBoundary
    {
        private const int Segments = 160;
        private const int Posts = 24;

        private static GameObject _root;
        private static LineRenderer _ring;
        private static LineRenderer[] _posts;
        private static Material _material;
        private static Vector3 _builtCenter;
        private static float _builtRadius = -1f;
        private static int _builtShape = -1;
        private static float _rebuildTimer;

        public static readonly Color RingColor = new Color(1f, 0.55f, 0.1f, 0.9f);

        public static void Rebuild()
        {
            _builtRadius = -1f;
            _rebuildTimer = 0f;
        }

        public static void Hide()
        {
            if (_root) _root.SetActive(false);
        }

        public static void Update()
        {
            var session = Arena.Current;
            if (session == null || !ArenaSettingsStore.Current.showBoundary)
            {
                Hide();
                return;
            }
            EnsureObjects();
            _root.SetActive(true);

            // Рельеф под кольцом подгружается постепенно — перестраиваем изредка и при изменении параметров.
            var r = session.Rules;
            _rebuildTimer -= Time.unscaledDeltaTime;
            bool changed = _builtCenter != session.Center || !Mathf.Approximately(_builtRadius, r.radius) || _builtShape != r.shape;
            if (changed || _rebuildTimer <= 0f)
            {
                _rebuildTimer = 5f;
                Build(session);
            }
        }

        private static void EnsureObjects()
        {
            if (_root) return;
            _root = new GameObject("ValheimArena_Boundary");
            Object.DontDestroyOnLoad(_root);
            _material = CreateMaterial();
            _ring = CreateLine(_root.transform, "ring", 0.18f);
            _ring.loop = true;
            _posts = new LineRenderer[Posts];
            for (int i = 0; i < Posts; i++)
            {
                _posts[i] = CreateLine(_root.transform, "post" + i, 0.12f);
                _posts[i].positionCount = 2;
            }
        }

        private static LineRenderer CreateLine(Transform parent, string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = _material;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = RingColor;
            lr.endColor = RingColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            return lr;
        }

        private static Material CreateMaterial()
        {
            string[] names = { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit", "Unlit/Color", "UI/Default" };
            foreach (var n in names)
            {
                var sh = Shader.Find(n);
                if (sh)
                {
                    var m = new Material(sh) { color = RingColor };
                    return m;
                }
            }
            ArenaPlugin.Log.LogWarning(L.T("Не найден шейдер для границы арены — кольцо может быть невидимым."));
            return new Material(Shader.Find("Standard"));
        }

        private static void Build(ArenaSession s)
        {
            _builtCenter = s.Center;
            _builtRadius = s.Rules.radius;
            _builtShape = s.Rules.shape;
            _ring.positionCount = Segments;
            for (int i = 0; i < Segments; i++)
            {
                Vector3 p = s.PerimeterPoint(i / (float)Segments);
                p.y = Arena.GroundHeight(p) + 0.35f;
                _ring.SetPosition(i, p);
            }
            for (int i = 0; i < Posts; i++)
            {
                Vector3 p = s.PerimeterPoint(i / (float)Posts);
                float y = Arena.GroundHeight(p);
                _posts[i].SetPosition(0, new Vector3(p.x, y, p.z));
                _posts[i].SetPosition(1, new Vector3(p.x, y + 3f, p.z));
            }
        }

        /// <summary>Вернуть персонажа внутрь границы (и под потолок для летающих).</summary>
        public static void Enforce(Character ch, ArenaSession s, BoundaryMode mode)
        {
            var r = s.Rules;
            if (mode == BoundaryMode.None && r.heightLimit <= 0f) return;
            var body = Reflect.Body(ch);
            Vector3 pos = ch.transform.position;
            bool moved = false;

            if (mode != BoundaryMode.None && !s.IsInside(pos, 0.2f))
            {
                if (mode == BoundaryMode.ReturnToCenter)
                {
                    Vector3 p = s.RandomPointInside(0.3f);
                    // Летуна возвращаем на ту же высоту над землёй, а не на землю.
                    float above = ch.IsFlying() ? Mathf.Max(0.5f, pos.y - Arena.GroundHeight(pos)) : 0.5f;
                    p.y = Arena.GroundHeight(p) + above;
                    pos = p;
                    if (body) body.linearVelocity = Vector3.zero;
                }
                else
                {
                    Vector3 inside = s.ClampInside(pos, (r.radius - 0.4f) / Mathf.Max(1f, r.radius));
                    Vector3 outward = pos - inside;
                    outward.y = 0f;
                    pos = inside;
                    if (body && outward.sqrMagnitude > 0.0001f)
                    {
                        Vector3 n = outward.normalized;
                        Vector3 v = body.linearVelocity;
                        float outSpeed = Vector3.Dot(v, n);
                        if (outSpeed > 0f) body.linearVelocity = v - n * outSpeed;
                    }
                }
                moved = true;
            }

            if (r.heightLimit > 0f && ch.IsFlying())
            {
                // Над водой — от поверхности воды, как крейсерская высота летунов (ArenaFlight.Surface), а не от дна.
                float ceiling = Mathf.Max(Arena.GroundHeight(pos), ZoneSystem.instance ? ZoneSystem.instance.m_waterLevel : float.MinValue) + r.heightLimit;
                if (pos.y > ceiling)
                {
                    pos.y = ceiling;
                    if (body && body.linearVelocity.y > 0f)
                    {
                        var v = body.linearVelocity;
                        v.y = 0f;
                        body.linearVelocity = v;
                    }
                    moved = true;
                }
            }

            if (!moved) return;
            if (body) body.position = pos;
            ch.transform.position = pos;
        }
    }
}
