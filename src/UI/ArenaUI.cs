using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.UI
{
    /// <summary>Окна мода, горячие клавиши и признаки «мод забрал мышь/клавиатуру».</summary>
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

        /// <summary>Курсор нужен моду (открыто окно).</summary>
        public static bool WantsCursor => (MenuOpen || EditorOpen) && Player.m_localPlayer;

        /// <summary>Мышь над окном мода — колесо и обзор камерой не трогаем.</summary>
        public static bool WantsMouse => WantsCursor && _mouseOverWindow;

        /// <summary>В поле ввода мода набирается текст — WASD не двигают камеру и горячие клавиши молчат.</summary>
        public static bool WantsKeyboard => WantsCursor && UiInput.TextFocused;

        /// <summary>Автомасштаб по высоте экрана (1080p = 1) × множитель из настроек.</summary>
        public static float Scale => Mathf.Clamp(Screen.height / 1080f, 0.5f, 4f) * ArenaPlugin.UiScale.Value;

        private static UiLanguage? _pendingLanguage;

        /// <summary>
        /// Сменить язык со следующего кадра: посреди прохода OnGUI раскладка (Layout) и отрисовка (Repaint)
        /// разошлись бы по ширине подписей.
        /// </summary>
        public static void RequestLanguage(UiLanguage language) => _pendingLanguage = language;

        public static void Update()
        {
            if (_pendingLanguage.HasValue)
            {
                ArenaPlugin.Language.Value = _pendingLanguage.Value;   // сохраняется в cfg, L перечитывает язык
                _pendingLanguage = null;
            }
            if (!Player.m_localPlayer)
            {
                if (ArenaCamera.Mode != CameraMode.Player) ArenaCamera.SetMode(CameraMode.Player);
                return;
            }
            UpdateWindowDrag();
            if (Console.IsVisible() || (Chat.instance && Chat.instance.HasFocus()) || TextInput.IsVisible())
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
            if (Pressed(ArenaPlugin.KeyNextTarget)) ArenaCamera.Next(+1);
            if (Pressed(ArenaPlugin.KeyPrevTarget)) ArenaCamera.Next(-1);
            if (Pressed(ArenaPlugin.KeySlower)) Arena.StepTimeScale(-1);
            if (Pressed(ArenaPlugin.KeyFaster)) Arena.StepTimeScale(+1);
            if (Pressed(ArenaPlugin.KeyOverlay))
                ArenaSettingsStore.Current.overlay = !ArenaSettingsStore.Current.overlay;
            if (Pressed(ArenaPlugin.KeyTags)) ToggleNameTags();
        }

        /// <summary>
        /// Таблички над бойцами (имя, звёзды, полоска здоровья) вкл/выкл. Показать таблички при выключенном
        /// оверлее — значит включить и его: без оверлея таблички не рисуются.
        /// </summary>
        public static void ToggleNameTags()
        {
            var s = ArenaSettingsStore.Current;
            s.nameTags = !s.nameTags;
            if (s.nameTags) s.overlay = true;
            if (MessageHud.instance)
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft,
                    s.nameTags ? L.T("Таблички над бойцами показаны") : L.T("Таблички над бойцами скрыты"));
        }

        /// <summary>
        /// Клавиши-символы молчат, пока набирается текст в поле мода; F-клавиши работают всегда,
        /// чтобы окно можно было закрыть тем же F7, не снимая фокус с поля.
        /// </summary>
        private static bool Pressed(BepInEx.Configuration.ConfigEntry<KeyCode> key)
        {
            var k = key.Value;
            if (k == KeyCode.None) return false;
            bool fKey = k >= KeyCode.F1 && k <= KeyCode.F15;
            if (WantsKeyboard && !fKey) return false;
            return ZInput.GetKeyDown(k, false);
        }

        /// <summary>Перетаскивание окон за заголовок — по мыши Input System (события IMGUI не приходят).</summary>
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

            Overlay.Draw(scale);

            // Порядок рисования = порядок наложения: редактор поверх меню.
            if (MenuOpen) GuiKit.DrawWindow(1, MenuRect, L.T("АРЕНА  ·  ") + ArenaPlugin.Version, MainWindow.Draw);
            if (EditorOpen) GuiKit.DrawWindow(2, EditorRect, L.T("РЕДАКТОР СУЩЕСТВ"), EditorWindow.Draw);
            _mouseOverWindow = (MenuOpen && MenuRect.Contains(mouse)) || (EditorOpen && EditorRect.Contains(mouse));

            UiInput.EndPass();
            GUI.matrix = oldMatrix;
        }

        /// <summary>Размер и место окон — в долях виртуального экрана; при смене разрешения раскладка пересчитывается.</summary>
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
