using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.UI
{
    public static class Overlay
    {
        private static GUIStyle _result, _resultShadow, _feed, _feedShadow, _hint, _hintShadow;

        private static void EnsureStyles()
        {
            if (_result != null) return;
            _result = new GUIStyle(GuiKit.TagLabel) { fontSize = 20 };
            _resultShadow = new GUIStyle(GuiKit.TagShadow) { fontSize = 20 };
            _feed = new GUIStyle(GuiKit.TagLabel) { alignment = TextAnchor.MiddleRight };
            _feedShadow = new GUIStyle(GuiKit.TagShadow) { alignment = TextAnchor.MiddleRight };
            _hint = new GUIStyle(GuiKit.TagLabel) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal };
            _hintShadow = new GUIStyle(GuiKit.TagShadow) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal };
        }

        public static void Draw(float scale)
        {
            var s = ArenaSettingsStore.Current;
            var arena = Arena.Current;
            if (arena == null || !s.overlay) return;
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            float w = Screen.width / scale, h = Screen.height / scale;

            if (s.nameTags) DrawTags(scale, arena);
            DrawTopBar(w, arena);
            DrawCountdownAndResult(w, h, arena);
            if (s.killFeed) DrawFeed(w, arena);
            DrawTargetCard(w, h);
            DrawHints(h);
        }

        private static void DrawTags(float scale, ArenaSession arena)
        {
            var cam = Utils.GetMainCamera();
            if (!cam) return;
            var s = ArenaSettingsStore.Current;
            Vector3 cp = cam.transform.position;
            float maxSq = s.tagDistance * s.tagDistance;
            foreach (var u in ArenaUnit.All)
            {
                if (!u || u.SessionId != arena.Id || !u.Alive) continue;
                var ch = u.Character;
                Vector3 top = ch.GetTopPoint() + Vector3.up * 0.45f;
                if ((top - cp).sqrMagnitude > maxSq) continue;
                Vector3 sp = cam.WorldToScreenPoint(top);
                if (sp.z <= 0f) continue;
                float x = sp.x / scale;
                float y = (Screen.height - sp.y) / scale;
                bool focused = ArenaCamera.Target == u && ArenaCamera.Mode == CameraMode.Follow;
                Color c = Hostility.UnitColor(u);
                string text = GuiKit.Colored((focused ? "▶ " : "") + Arena.Label(u), c);
                if (!u.NView.GetZDO().HasOwner()) text += GuiKit.Colored(L.T(" (заморожен)"), GuiKit.MutedColor);
                var r = new Rect(x - 110f, y - 34f, 220f, 18f);
                GuiKit.ShadowLabel(r, text, GuiKit.TagLabel, GuiKit.TagShadow);
                if (s.hpBars)
                {
                    float frac = ch.GetHealthPercentage();
                    GuiKit.Bar(new Rect(x - 40f, y - 14f, 80f, 6f), frac, Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.3f, 0.9f, 0.3f), frac), new Color(0f, 0f, 0f, 0.7f));
                    GuiKit.Bar(new Rect(x - 40f, y - 8f, 80f, 2f), 1f, c, c);
                }
            }
        }

        private static void DrawTopBar(float w, ArenaSession arena)
        {
            string head = (arena.IsLocal ? L.T("Арена") : L.F("Арена игрока {0}", arena.HostName)) + " · " + arena.StateName;
            if (arena.State == ArenaState.Fighting || arena.State == ArenaState.Paused)
                head += " · " + GuiKit.FmtTime(arena.FightSeconds);
            if (Mathf.Abs(Arena.TimeScale - 1f) > 0.001f)
                head += Arena.TimeScale <= 0.001f ? L.T(" · ВРЕМЯ ОСТАНОВЛЕНО") : " · ×" + GuiKit.Fmt(Arena.TimeScale);

            var sb = new System.Text.StringBuilder();
            foreach (var side in arena.Sides.Values)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(GuiKit.Colored($"{side.Name}: {side.Alive}/{side.Spawned}", side.Color));
            }
            var r1 = new Rect(w * 0.5f - 400f, 6f, 800f, 20f);
            GuiKit.ShadowLabel(r1, GuiKit.Colored(head, GuiKit.Accent), GuiKit.TagLabel, GuiKit.TagShadow);
            if (sb.Length > 0)
            {
                var r2 = new Rect(w * 0.5f - 500f, 26f, 1000f, 20f);
                GuiKit.ShadowLabel(r2, sb.ToString(), GuiKit.TagLabel, GuiKit.TagShadow);
            }
        }

        private static void DrawCountdownAndResult(float w, float h, ArenaSession arena)
        {
            if (arena.State == ArenaState.Countdown)
            {
                int n = Mathf.CeilToInt(arena.CountdownLeft);
                var r = new Rect(w * 0.5f - 200f, h * 0.3f, 400f, 60f);
                GuiKit.ShadowLabel(r, GuiKit.Colored(n > 0 ? n.ToString() : L.T("В БОЙ!"), GuiKit.Accent), GuiKit.Big, GuiKit.BigShadow);
            }
            else if (arena.State == ArenaState.Paused)
            {
                var r = new Rect(w * 0.5f - 200f, h * 0.3f, 400f, 60f);
                GuiKit.ShadowLabel(r, GuiKit.Colored(L.T("ПАУЗА"), GuiKit.Accent), GuiKit.Big, GuiKit.BigShadow);
            }
            else if (arena.State == ArenaState.Finished && ArenaSession.Now - arena.FightEnd < 12.0 && !string.IsNullOrEmpty(arena.ResultText))
            {
                var r = new Rect(w * 0.5f - 500f, h * 0.22f, 1000f, 30f);
                GuiKit.ShadowLabel(r, GuiKit.Colored(arena.ResultText, GuiKit.Accent), _result, _resultShadow);
            }
        }

        private static void DrawFeed(float w, ArenaSession arena)
        {
            float y = 54f;
            int shown = 0;
            for (int i = arena.Feed.Count - 1; i >= 0 && shown < 7; i--)
            {
                var f = arena.Feed[i];
                float age = Time.unscaledTime - f.Time;
                if (age > 14f) break;
                string text = GuiKit.Colored(f.Killer, f.KillerColor) + "  ✕  " + GuiKit.Colored(f.Victim, f.VictimColor);
                var r = new Rect(w - 520f, y, 510f, 18f);
                GuiKit.ShadowLabel(r, text, _feed, _feedShadow);
                y += 19f;
                shown++;
            }
        }

        private static void DrawTargetCard(float w, float h)
        {
            if (ArenaCamera.Mode != CameraMode.Follow) return;
            var u = ArenaCamera.Target;
            if (!u || !u.Alive) return;
            var ch = u.Character;
            var box = new Rect(w * 0.5f - 210f, h - 112f, 420f, 84f);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(box, GuiKit.White);
            GUI.color = old;
            Color c = Hostility.UnitColor(u);
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, 400f, 20f), GuiKit.Colored(Arena.Label(u), c), GuiKit.Label);
            float frac = ch.GetHealthPercentage();
            GuiKit.Bar(new Rect(box.x + 10f, box.y + 30f, 400f, 10f), frac, Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.3f, 0.9f, 0.3f), frac), new Color(0f, 0f, 0f, 0.7f));
            string target = "—";
            if (u.Monster)
            {
                var t = Reflect.TargetCreature(u.Monster);
                var tu = ArenaUnit.Get(t);
                if (tu) target = GuiKit.Colored(Arena.Label(tu), Hostility.UnitColor(tu));
                else if (t) target = ValheimArena.Profiles.ProfileLogic.Loc(t.m_name);
            }
            GUI.Label(new Rect(box.x + 10f, box.y + 44f, 400f, 18f),
                L.F("HP {0:0}/{1:0}   урон {2:0}   убийств {3}", ch.GetHealth(), ch.GetMaxHealth(), (u.Stat?.dealt ?? 0f), u.Stat?.kills ?? 0), GuiKit.Small);
            GUI.Label(new Rect(box.x + 10f, box.y + 62f, 400f, 18f), L.T("Цель: ") + target, GuiKit.Small);
        }

        private static void DrawHints(float h)
        {
            if (!ArenaCamera.Active) return;
            string hint = ArenaCamera.Mode == CameraMode.Free
                ? L.F("Свободная камера · WASD, Пробел/E, Ctrl/Q, Shift · {0} — сменить режим", ArenaPlugin.KeyCamera.Value)
                : L.F("Слежение · {0}/{1} — цели · колесо — дистанция · {2} — сменить режим", ArenaPlugin.KeyPrevTarget.Value, ArenaPlugin.KeyNextTarget.Value, ArenaPlugin.KeyCamera.Value);
            GuiKit.ShadowLabel(new Rect(10f, h - 26f, 900f, 20f), GuiKit.Colored(hint, GuiKit.MutedColor), _hint, _hintShadow);
        }
    }
}
