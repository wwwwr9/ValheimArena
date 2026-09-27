using System;
using System.Reflection;
using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.UI
{
    public static class ArenaUI
    {
        public static bool MenuOpen;
        public static bool EditorOpen;

        public static Rect MenuRect = new Rect(20, 40, 780, 760);
        public static Rect EditorRect = new Rect(820, 40, 920, 780);
        private static bool _rectsPlaced;
        private static Vector2 _lastVirtualScreen;

        private static bool _mouseOverWindow;
        private static int _dragWindow;
        private static Vector2 _dragLast;

        public static bool WantsCursor => (MenuOpen || EditorOpen) && Player.m_localPlayer;

        public static bool WantsMouse => WantsCursor && _mouseOverWindow;

        public static bool WantsKeyboard => WantsCursor && UiInput.TextFocused;

        public static float Scale => Mathf.Clamp(Screen.height / 1080f, 0.5f, 4f) * ArenaPlugin.UiScale.Value;

        private static UiLanguage? _pendingLanguage;

        public static void RequestLanguage(UiLanguage language) => _pendingLanguage = language;

        public static void Update()
        {
            if (_pendingLanguage.HasValue)
            {
                ArenaPlugin.Language.Value = _pendingLanguage.Value;
                _pendingLanguage = null;
            }
            if (!Player.m_localPlayer)
            {
                if (ArenaCamera.Mode != CameraMode.Player) ArenaCamera.SetMode(CameraMode.Player);
                return;
            }
            UpdateWindowDrag();
            if (Console.IsVisible() || (Chat.instance && Chat.instance.HasFocus()) || TextInput.IsVisible() || Minimap.InTextInput()
                || (Hud.instance && Hud.instance.m_buildUi && Hud.instance.m_buildUi.SearchFieldFocused))
                return;

            if (Pressed(ArenaPlugin.KeyMenu))
            {
                MenuOpen = !MenuOpen;
                if (!MenuOpen) OnWindowsClosed();
            }
            if (Pressed(ArenaPlugin.KeyEditor))
            {
                EditorOpen = !EditorOpen;
                if (!EditorOpen) OnWindowsClosed();
            }
            if (Pressed(ArenaPlugin.KeyCamera)) ArenaCamera.CycleMode();
            if (Pressed(ArenaPlugin.KeyFight)) Arena.ToggleFight();
            if (Arena.Active)
            {
                if (Pressed(ArenaPlugin.KeyNextTarget)) ArenaCamera.Next(+1);
                if (Pressed(ArenaPlugin.KeyPrevTarget)) ArenaCamera.Next(-1);
                if (Pressed(ArenaPlugin.KeySlower)) Arena.StepTimeScale(-1);
                if (Pressed(ArenaPlugin.KeyFaster)) Arena.StepTimeScale(+1);
            }
            if (Pressed(ArenaPlugin.KeyOverlay))
                ArenaSettingsStore.Current.overlay = !ArenaSettingsStore.Current.overlay;
            if (Pressed(ArenaPlugin.KeyTags)) ToggleNameTags();
        }

        public static void ToggleNameTags()
        {
            var s = ArenaSettingsStore.Current;
            s.nameTags = !s.nameTags;
            if (s.nameTags) s.overlay = true;
            if (MessageHud.instance)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft,
                    s.nameTags ? L.T("Таблички над бойцами показаны") : L.T("Таблички над бойцами скрыты"));
        }

        private static bool Pressed(BepInEx.Configuration.ConfigEntry<KeyCode> key)
        {
            var k = key.Value;
            if (k == KeyCode.None) return false;
            bool fKey = k >= KeyCode.F1 && k <= KeyCode.F15;
            if (WantsKeyboard && !fKey) return false;
            return ZInput.GetKeyDown(k, false);
        }

        private static void UpdateWindowDrag()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || !WantsCursor) { _dragWindow = 0; return; }
            Vector2 m = UiInput.MouseScreen / Scale;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _dragWindow = 0;
                if (EditorOpen && TitleRect(EditorRect).Contains(m)) _dragWindow = 2;
                else if (MenuOpen && !(EditorOpen && EditorRect.Contains(m)) && TitleRect(MenuRect).Contains(m)) _dragWindow = 1;
                _dragLast = m;
            }
            else if (_dragWindow != 0 && mouse.leftButton.isPressed)
            {
                Vector2 d = m - _dragLast;
                _dragLast = m;
                if (_dragWindow == 1) MenuRect.position += d;
                else EditorRect.position += d;
            }
            else _dragWindow = 0;
        }

        private static Rect TitleRect(Rect r) => new Rect(r.x, r.y, r.width, GuiKit.TitleHeight);

        private static void OnWindowsClosed()
        {
            UiInput.Unfocus();
            GuiKit.ResetBuffers();
            ArenaSettingsStore.Save();
        }

        public static void OnGUI()
        {
            if (!Player.m_localPlayer) return;
            GuiKit.Ensure();

            var oldMatrix = GUI.matrix;
            float scale = Scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            Vector2 mouse = UiInput.MouseScreen / scale;
            PlaceWindows(scale);
            MenuRect = ClampToScreen(MenuRect, scale);
            EditorRect = ClampToScreen(EditorRect, scale);
            UiInput.BeginPass(mouse, MenuRect, MenuOpen, EditorRect, EditorOpen);
            DrawAll(scale, mouse);

            if (UiInput.InputApplied && Event.current.type == EventType.Layout && ResetLayout())
            {
                GUI.enabled = true;
                GUI.color = Color.white;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
                UiInput.BeginPass(mouse, MenuRect, MenuOpen, EditorRect, EditorOpen);
                DrawAll(scale, mouse);
            }

            UiInput.EndPass();
            GUI.matrix = oldMatrix;
        }

        private static void DrawAll(float scale, Vector2 mouse)
        {
            Overlay.Draw(scale);

            if (MenuOpen) GuiKit.DrawWindow(1, MenuRect, L.T("АРЕНА  ·  ") + ArenaPlugin.Version, MainWindow.Draw);
            if (EditorOpen) GuiKit.DrawWindow(2, EditorRect, L.T("РЕДАКТОР СУЩЕСТВ"), EditorWindow.Draw);
            _mouseOverWindow = (MenuOpen && MenuRect.Contains(mouse)) || (EditorOpen && EditorRect.Contains(mouse));
        }

        private static readonly FieldInfo OriginalGuiId =
            typeof(GUIUtility).GetField("s_OriginalID", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo LayoutBegin =
            typeof(GUILayoutUtility).GetMethod("Begin", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
        private static bool _layoutResetWarned;

        private static bool ResetLayout()
        {
            if (OriginalGuiId == null || LayoutBegin == null)
            {
                if (!_layoutResetWarned)
                {
                    _layoutResetWarned = true;
                    ArenaPlugin.Log.LogWarning("GUILayoutUtility.Begin / GUIUtility.s_OriginalID not found: windows are not re-laid out after a click.");
                }
                return false;
            }
            try
            {
                LayoutBegin.Invoke(null, new object[] { (int)OriginalGuiId.GetValue(null) });
                return true;
            }
            catch (Exception e)
            {
                if (!_layoutResetWarned)
                {
                    _layoutResetWarned = true;
                    ArenaPlugin.Log.LogWarning("Layout reset failed: " + e.Message);
                }
                return false;
            }
        }

        private static void PlaceWindows(float scale)
        {
            var vs = new Vector2(Screen.width / scale, Screen.height / scale);
            if (_rectsPlaced && (vs - _lastVirtualScreen).sqrMagnitude < 1f) return;
            _lastVirtualScreen = vs;
            _rectsPlaced = true;
            float h = Mathf.Min(820f, vs.y - 60f);
            float mw = Mathf.Min(780f, vs.x * 0.48f);
            float ew = Mathf.Min(920f, vs.x - mw - 60f);
            MenuRect = new Rect(20f, 40f, mw, h);
            EditorRect = new Rect(Mathf.Max(20f, vs.x - ew - 20f), 40f, Mathf.Max(ew, 560f), h);
        }

        private static Rect ClampToScreen(Rect r, float scale)
        {
            float w = Screen.width / scale, h = Screen.height / scale;
            r.width = Mathf.Min(r.width, w);
            r.height = Mathf.Min(r.height, h);
            r.x = Mathf.Clamp(r.x, 0f, Mathf.Max(0f, w - r.width));
            r.y = Mathf.Clamp(r.y, 0f, Mathf.Max(0f, h - r.height));
            return r;
        }

        public static void CloseMenu()
        {
            MenuOpen = false;
            OnWindowsClosed();
        }

        public static void CloseEditor()
        {
            EditorOpen = false;
            OnWindowsClosed();
        }
    }
}
