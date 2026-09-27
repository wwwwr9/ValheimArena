using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ValheimArena.UI
{
    public static class GuiKit
    {
        public static GUIStyle Window, WindowTitle, Header, SubHeader, Label, Small, Muted, Button, ButtonOn, Toolbar, ToolbarOn,
            Box, Row, RowAlt, Field, TagLabel, TagShadow, Big, BigShadow, Center;

        public static Texture2D White;
        private static Texture2D _windowBg, _titleBg, _rowBg, _rowAltBg, _rowHover, _btnBg, _btnHover, _btnOn, _fieldBg, _fieldFocus, _boxBg;
        private static bool _ready;

        private static readonly Dictionary<string, string> Buffers = new Dictionary<string, string>();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static readonly Color Accent = new Color(1f, 0.62f, 0.2f);
        public static readonly Color TextColor = new Color(0.92f, 0.9f, 0.86f);
        public static readonly Color MutedColor = new Color(0.62f, 0.6f, 0.56f);

        private static bool Repaint => Event.current.type == EventType.Repaint;

        public static void Ensure()
        {
            if (_ready && _windowBg) return;
            _ready = true;
            White = Tex(Color.white);
            _windowBg = Tex(new Color(0.07f, 0.065f, 0.06f, 0.97f));
            _titleBg = Tex(new Color(0.16f, 0.12f, 0.08f, 1f));
            _rowBg = Tex(new Color(1f, 1f, 1f, 0.03f));
            _rowAltBg = Tex(new Color(1f, 1f, 1f, 0.07f));
            _rowHover = Tex(new Color(1f, 0.62f, 0.2f, 0.18f));
            _btnBg = Tex(new Color(0.22f, 0.2f, 0.18f, 1f));
            _btnHover = Tex(new Color(0.34f, 0.29f, 0.24f, 1f));
            _btnOn = Tex(new Color(0.55f, 0.33f, 0.1f, 1f));
            _fieldBg = Tex(new Color(0.03f, 0.03f, 0.03f, 1f));
            _fieldFocus = Tex(new Color(0.1f, 0.08f, 0.05f, 1f));
            _boxBg = Tex(new Color(1f, 1f, 1f, 0.045f));

            Window = new GUIStyle(GUIStyle.none);
            Window.normal.background = _windowBg;
            WindowTitle = new GUIStyle(GUIStyle.none)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true
            };
            WindowTitle.normal.background = _titleBg;
            WindowTitle.normal.textColor = Accent;

            Label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = false, richText = true };
            Label.normal.textColor = TextColor;
            Label.hover.textColor = Color.white;
            Small = new GUIStyle(Label) { fontSize = 11 };
            Muted = new GUIStyle(Small) { wordWrap = true };
            Muted.normal.textColor = MutedColor;
            Header = new GUIStyle(Label) { fontSize = 15, fontStyle = FontStyle.Bold };
            Header.normal.textColor = Accent;
            SubHeader = new GUIStyle(Label) { fontSize = 13, fontStyle = FontStyle.Bold };
            SubHeader.normal.textColor = new Color(0.95f, 0.8f, 0.55f);
            SubHeader.hover.textColor = Color.white;
            Center = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };

            Button = new GUIStyle(GUI.skin.button) { fontSize = 12, padding = new RectOffset(8, 8, 4, 4), richText = true };
            Button.normal.background = _btnBg;
            Button.hover.background = _btnHover;
            Button.active.background = _btnOn;
            Button.focused.background = _btnBg;
            Button.normal.textColor = TextColor;
            Button.hover.textColor = Color.white;
            Button.active.textColor = Color.white;
            ButtonOn = new GUIStyle(Button);
            ButtonOn.normal.background = _btnOn;
            ButtonOn.hover.background = _btnOn;
            ButtonOn.normal.textColor = Color.white;

            Toolbar = new GUIStyle(Button) { fontSize = 13, padding = new RectOffset(10, 10, 6, 6) };
            ToolbarOn = new GUIStyle(ButtonOn) { fontSize = 13, padding = new RectOffset(10, 10, 6, 6), fontStyle = FontStyle.Bold };

            Box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 6, 6), margin = new RectOffset(0, 0, 4, 4) };
            Box.normal.background = _boxBg;
            Row = new GUIStyle(GUIStyle.none)
            {
                padding = new RectOffset(6, 6, 3, 3), margin = new RectOffset(0, 0, 1, 1), fontSize = 12,
                richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = false
            };
            Row.normal.background = _rowBg;
            Row.normal.textColor = TextColor;
            Row.hover.background = _rowHover;
            Row.hover.textColor = Color.white;
            RowAlt = new GUIStyle(Row);
            RowAlt.normal.background = _rowAltBg;

            Field = new GUIStyle(GUI.skin.textField) { fontSize = 12, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            Field.normal.background = _fieldBg;
            Field.hover.background = _fieldBg;
            Field.focused.background = _fieldFocus;
            Field.normal.textColor = Color.white;
            Field.hover.textColor = Color.white;
            Field.focused.textColor = Color.white;

            TagLabel = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            TagShadow = new GUIStyle(TagLabel);
            TagShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            Big = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            BigShadow = new GUIStyle(Big);
            BigShadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        }

        public static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        public static string Colored(string text, Color c) => "<color=#" + Hex(c) + ">" + text + "</color>";

        public const float TitleHeight = 26f;

        public static void DrawWindow(int index, Rect rect, string title, Action body)
        {
            UiInput.CurrentWindow = index;
            if (Repaint)
            {
                Window.Draw(rect, GUIContent.none, false, false, false, false);
                WindowTitle.Draw(new Rect(rect.x, rect.y, rect.width, TitleHeight), new GUIContent(title), false, false, false, false);
            }
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + TitleHeight + 6f, rect.width - 20f, rect.height - TitleHeight - 14f));
            body();
            GUILayout.EndArea();
            UiInput.CurrentWindow = 0;
        }

        public static bool Press(string text, GUIStyle style, params GUILayoutOption[] opts)
        {
            var content = new GUIContent(text);
            Rect r = GUILayoutUtility.GetRect(content, style, opts);
            bool fired = UiInput.Clickable(r, out bool hover);
            if (Repaint) style.Draw(r, content, hover, hover && UiInput.Held, false, false);
            return fired;
        }

        public static bool Tab(string text, bool on, params GUILayoutOption[] opts) => Press(text, on ? ToolbarOn : Toolbar, opts);

        public static bool Btn(string text, params GUILayoutOption[] opts) => Press(text, Button, opts);

        public static bool BtnOn(string text, bool on, params GUILayoutOption[] opts) => Press(text, on ? ButtonOn : Button, opts);

        private static void DrawCheck(Rect box, bool value, bool hover)
        {
            var old = GUI.color;
            GUI.color = hover ? new Color(1f, 0.8f, 0.5f) : new Color(0.55f, 0.52f, 0.48f);
            GUI.DrawTexture(box, White);
            GUI.color = new Color(0.05f, 0.05f, 0.05f);
            GUI.DrawTexture(new Rect(box.x + 1, box.y + 1, box.width - 2, box.height - 2), White);
            if (value)
            {
                GUI.color = Accent;
                GUI.DrawTexture(new Rect(box.x + 3, box.y + 3, box.width - 6, box.height - 6), White);
            }
            GUI.color = old;
        }

        public static bool Toggle(string text, bool value, string hint = null)
        {
            var content = new GUIContent(text);
            Vector2 size = Label.CalcSize(content);
            Rect r = GUILayoutUtility.GetRect(size.x + 22f, Mathf.Max(18f, size.y), GUILayout.ExpandWidth(false));
            bool fired = UiInput.Clickable(r, out bool hover);
            if (Repaint)
            {
                DrawCheck(new Rect(r.x, r.y + (r.height - 14f) * 0.5f, 14f, 14f), value, hover);
                Label.Draw(new Rect(r.x + 20f, r.y, r.width - 20f, r.height), content, hover, false, false, false);
            }
            if (!string.IsNullOrEmpty(hint)) GUILayout.Label(hint, Muted);
            return fired ? !value : value;
        }

        public static bool ToggleBox(bool value, float width)
        {
            Rect r = GUILayoutUtility.GetRect(width, 18f, GUILayout.Width(width), GUILayout.Height(18f));
            bool fired = UiInput.Clickable(r, out bool hover);
            if (Repaint) DrawCheck(new Rect(r.x + (r.width - 14f) * 0.5f, r.y + 2f, 14f, 14f), value, hover);
            return fired ? !value : value;
        }

        public static int Choice(string label, int value, string[] names, float labelWidth = 170f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Label, GUILayout.Width(labelWidth));
            for (int i = 0; i < names.Length; i++)
                if (BtnOn(names[i], value == i)) value = i;
            GUILayout.EndHorizontal();
            return value;
        }

        public static int Cycle(string label, int value, IList<string> names, float labelWidth = 170f, float valueWidth = 220f)
        {
            int n = names.Count;
            if (n == 0) return value;
            value = Mathf.Clamp(value, 0, n - 1);
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(label)) GUILayout.Label(label, Label, GUILayout.Width(labelWidth));
            if (Btn("◀", GUILayout.Width(26))) value = (value - 1 + n) % n;
            GUILayout.Label(names[value], Center, GUILayout.Width(valueWidth));
            if (Btn("▶", GUILayout.Width(26))) value = (value + 1) % n;
            GUILayout.EndHorizontal();
            return value;
        }

        public static float SliderBar(float value, float min, float max)
        {
            Rect r = GUILayoutUtility.GetRect(120f, 20f, GUILayout.MinWidth(120f), GUILayout.ExpandWidth(true), GUILayout.Height(20f));
            var frac = UiInput.Drag(r);
            if (frac.HasValue) value = Mathf.Lerp(min, max, frac.Value);
            if (Repaint)
            {
                bool hover = UiInput.IsHover(r);
                float f = Mathf.Approximately(max, min) ? 0f : Mathf.Clamp01((value - min) / (max - min));
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.DrawTexture(new Rect(r.x, r.y + 8f, r.width, 4f), White);
                GUI.color = new Color(Accent.r, Accent.g, Accent.b, 0.8f);
                GUI.DrawTexture(new Rect(r.x, r.y + 8f, r.width * f, 4f), White);
                GUI.color = hover || UiInput.DragId >= 0 ? Color.white : new Color(0.85f, 0.8f, 0.72f);
                GUI.DrawTexture(new Rect(r.x + r.width * f - 5f, r.y + 3f, 10f, 14f), White);
                GUI.color = old;
            }
            return value;
        }

        public static float Slider(string label, float value, float min, float max, string id, string fmt = "0.##",
            float labelWidth = 170f, string suffix = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Label, GUILayout.Width(labelWidth));
            float nv = SliderBar(value, min, max);
            if (!Mathf.Approximately(nv, value)) { value = nv; Buffers.Remove(id); }
            value = NumberBox(value, id, fmt, 64f);
            if (!string.IsNullOrEmpty(suffix)) GUILayout.Label(suffix, Small, GUILayout.Width(22));
            GUILayout.EndHorizontal();
            return value;
        }

        public static float Number(string label, float value, string id, string fmt = "0.##", float labelWidth = 170f, float fieldWidth = 80f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Label, GUILayout.Width(labelWidth));
            value = NumberBox(value, id, fmt, fieldWidth);
            GUILayout.EndHorizontal();
            return value;
        }

        public static float NumberBox(float value, string id, string fmt, float width)
        {
            string shown = value.ToString(fmt, Inv);
            bool focused = UiInput.FocusId == id;
            string buf = focused && Buffers.TryGetValue(id, out var b) ? b : shown;
            string nb = TextBox(id, buf, GUILayout.Width(width));
            if (UiInput.FocusId == id)
            {
                if (nb != buf)
                {
                    Buffers[id] = nb;
                    if (float.TryParse(nb.Replace(',', '.'), NumberStyles.Float, Inv, out var parsed)) value = parsed;
                }
                else if (!Buffers.ContainsKey(id)) Buffers[id] = buf;
            }
            else Buffers.Remove(id);
            return value;
        }

        public static int Stepper(string label, int value, int min, int max, float labelWidth = 170f, int bigStep = 5)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Label, GUILayout.Width(labelWidth));
            if (Btn("−" + bigStep, GUILayout.Width(36))) value -= bigStep;
            if (Btn("−", GUILayout.Width(26))) value--;
            GUILayout.Label(value.ToString(), Center, GUILayout.Width(44));
            if (Btn("+", GUILayout.Width(26))) value++;
            if (Btn("+" + bigStep, GUILayout.Width(36))) value += bigStep;
            GUILayout.EndHorizontal();
            return Mathf.Clamp(value, min, max);
        }

        public static string TextBox(string id, string value, params GUILayoutOption[] opts)
        {
            value = value ?? "";
            var content = new GUIContent(value.Length > 0 ? value : " ");
            Rect r = GUILayoutUtility.GetRect(content, Field, opts);
            if (UiInput.Clickable(r, out bool hover)) UiInput.Focus(id);
            value = UiInput.ApplyTyping(id, value, out _);
            if (Repaint)
            {
                bool focused = UiInput.FocusId == id;
                bool caret = focused && (Time.unscaledTime % 1f) < 0.55f;
                Field.Draw(r, new GUIContent(value + (caret ? "|" : "")), hover, false, false, focused);
            }
            return value;
        }

        public static string TextField(string label, string value, string id, float labelWidth = 170f)
        {
            GUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(label)) GUILayout.Label(label, Label, GUILayout.Width(labelWidth));
            value = TextBox(id, value, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            return value;
        }

        public static Vector2 BeginScroll(Vector2 pos, params GUILayoutOption[] opts) => UiInput.BeginScroll(pos, opts);

        public static void EndScroll() => UiInput.EndScroll();

        public static void Bar(Rect r, float frac, Color fill, Color back)
        {
            var old = GUI.color;
            GUI.color = back;
            GUI.DrawTexture(r, White);
            GUI.color = fill;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), White);
            GUI.color = old;
        }

        public static void LayoutBar(float frac, Color fill, float height = 6f, float width = -1f)
        {
            var r = width > 0 ? GUILayoutUtility.GetRect(width, height, GUILayout.Width(width)) : GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));
            if (Repaint) Bar(r, frac, fill, new Color(0f, 0f, 0f, 0.6f));
        }

        public static void ShadowLabel(Rect r, string text, GUIStyle style, GUIStyle shadow)
        {
            var sr = r;
            sr.x += 1f; sr.y += 1f;
            GUI.Label(sr, StripColor(text), shadow);
            GUI.Label(r, text, style);
        }

        private static string StripColor(string s)
        {
            if (s.IndexOf('<') < 0) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            bool tag = false;
            foreach (var ch in s)
            {
                if (ch == '<') { tag = true; continue; }
                if (ch == '>') { tag = false; continue; }
                if (!tag) sb.Append(ch);
            }
            return sb.ToString();
        }

        public static void ResetBuffers() => Buffers.Clear();

        public static string Fmt(float v) => v.ToString("0.##", Inv);

        public static string FmtTime(float seconds)
        {
            var ts = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
        }
    }
}
