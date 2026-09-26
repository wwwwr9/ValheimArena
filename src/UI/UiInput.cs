using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ValheimArena.UI
{
    /// <summary>
    /// Собственный ввод окон мода. В Valheim на Unity 6 события IMGUI MouseDown/KeyDown до OnGUI не доходят,
    /// а подмешанные через Event.QueueEvent тоже не срабатывают. Поэтому окна используют IMGUI только для рисования и раскладки,
    /// а мышь/клавиатуру читают из Input System — так же, как сама игра.
    ///
    /// Схема: щелчок, случившийся в кадре N, привязывается к элементу в проходе Repaint кадра N (там известны
    /// прямоугольники и позиция мыши в координатах окна); срабатывает элемент в первом проходе OnGUI кадра N+1
    /// (Layout). Срабатывает он посреди раскладки: всё, что разложено выше, осталось по старому состоянию, поэтому
    /// после такого прохода кадр раскладывается заново (<see cref="InputApplied"/>, ArenaUI.OnGUI).
    ///
    /// Элемент опознаётся не порядковым номером, а ключом (<see cref="ControlId"/>): если между кадрами в списке
    /// выше появилась или пропала строка, номера сдвигаются, и щелчок достался бы соседнему элементу.
    /// </summary>
    public static class UiInput
    {
        private static readonly PropertyInfo VisibleRectProp =
            typeof(GUI).Assembly.GetType("UnityEngine.GUIClip")?.GetProperty("visibleRect", BindingFlags.Static | BindingFlags.NonPublic);

        // --- состояние из Update
        public static Vector2 MouseScreen { get; private set; }
        public static bool Held { get; private set; }
        private static bool _pressPending;
        private static float _wheelPending;
        private static readonly List<char> TypedPending = new List<char>();
        private static bool _backspace, _delete, _submit, _cancel, _clear, _paste;
        private static Keyboard _hookedKeyboard;

        // --- проходы OnGUI
        private const int NoControl = -1;
        private const int KindClick = 0, KindScroll = 1, KindDrag = 2;
        public const int KindText = 3;

        private static int _passFrame = -1;
        private static int _scope;
        private static readonly Stack<int> ScopeStack = new Stack<int>();
        private static readonly Dictionary<int, int> Seen = new Dictionary<int, int>();
        private static int _armedId = NoControl;
        private static int _candidateId = NoControl;
        private static bool _pressResolved;
        private static int _armedScrollId = NoControl;
        private static float _armedScrollDelta;
        private static int _scrollCandidate = NoControl;
        private static float _scrollDelta;
        private static readonly Stack<int> ScrollStack = new Stack<int>();

        private static int FireId { get; set; } = NoControl;
        public static bool FirstPass { get; private set; }
        /// <summary>
        /// В первом проходе кадра сработал элемент, применён набор или ползунок: состояние окна поменялось посреди
        /// раскладки, и кадр нужно разложить заново (<see cref="ArenaUI.OnGUI"/>).
        /// </summary>
        public static bool InputApplied { get; private set; }
        public static bool IsRepaint { get; private set; }

        // --- текстовый фокус и перетаскивание ползунков
        public static string FocusId { get; private set; }
        private static readonly List<char> FrameTyped = new List<char>();
        private static bool _fBackspace, _fDelete, _fSubmit, _fCancel, _fClear, _fPaste;
        private static int _dragId = NoControl;
        private static float _dragFrac;
        private static bool _dragArmed;

        // --- окна
        public static int CurrentWindow;
        public static int TopWindowUnderMouse;

        public static bool TextFocused => FocusId != null;

        // ================================================================== Update

        public static void Update(bool windowsOpen)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 raw = mouse.position.ReadValue();
                MouseScreen = new Vector2(raw.x, Screen.height - raw.y);
                Held = mouse.leftButton.isPressed;
                if (windowsOpen && mouse.leftButton.wasPressedThisFrame) _pressPending = true;
                float s = mouse.scroll.ReadValue().y;
                if (windowsOpen && Mathf.Abs(s) > 0.01f) _wheelPending += -Mathf.Sign(s) * 40f * Mathf.Max(1f, Mathf.Abs(s) / 120f);
            }
            if (!Held) _dragId = NoControl;

            HookKeyboard();
            var kb = Keyboard.current;
            if (!windowsOpen)
            {
                FocusId = null;
                TypedPending.Clear();
                _pressPending = false;
                _wheelPending = 0f;
                return;
            }
            if (kb != null && FocusId != null)
            {
                bool ctrl = kb.ctrlKey.isPressed && !kb.altKey.isPressed;
                if (kb.backspaceKey.wasPressedThisFrame) { if (ctrl) _clear = true; else _backspace = true; }
                if (kb.deleteKey.wasPressedThisFrame) _clear = true;
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame) _submit = true;
                if (kb.escapeKey.wasPressedThisFrame) _cancel = true;
                if (ctrl && kb.vKey.wasPressedThisFrame) _paste = true;
            }
        }

        private static void HookKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == _hookedKeyboard) return;
            if (_hookedKeyboard != null) _hookedKeyboard.onTextInput -= OnTextInput;
            _hookedKeyboard = kb;
            if (kb != null) kb.onTextInput += OnTextInput;
        }

        private static void OnTextInput(char c)
        {
            if (FocusId == null || c < 32 || c == 127) return;
            // Ctrl+буква — сочетание, а не символ. AltGr в Windows приходит как Ctrl+Alt и набирает символы (@, €, {).
            if (_hookedKeyboard != null && _hookedKeyboard.ctrlKey.isPressed && !_hookedKeyboard.altKey.isPressed) return;
            if (TypedPending.Count < 256) TypedPending.Add(c);
        }

        // ================================================================== проходы OnGUI

        /// <summary>В начале OnGUI мода, на каждом событии.</summary>
        public static void BeginPass(Vector2 guiMouse, Rect menu, bool menuOpen, Rect editor, bool editorOpen)
        {
            _scope = 0;
            ScopeStack.Clear();
            Seen.Clear();
            ScrollStack.Clear();
            IsRepaint = Event.current.type == EventType.Repaint;
            FirstPass = Time.frameCount != _passFrame;
            _passFrame = Time.frameCount;

            TopWindowUnderMouse = editorOpen && editor.Contains(guiMouse) ? 2 : menuOpen && menu.Contains(guiMouse) ? 1 : 0;

            if (FirstPass)
            {
                InputApplied = false;
                FireId = _armedId;
                _armedId = NoControl;
                if (_pressResolved)
                {
                    // Щелчок мимо поля ввода снимает фокус; поле, по которому щёлкнули, вернёт его себе.
                    FocusId = null;
                    _pressResolved = false;
                }
                FrameTyped.Clear();
                FrameTyped.AddRange(TypedPending);
                TypedPending.Clear();
                _fBackspace = _backspace; _fDelete = _delete; _fSubmit = _submit; _fCancel = _cancel; _fClear = _clear; _fPaste = _paste;
                _backspace = _delete = _submit = _cancel = _clear = _paste = false;
            }
            else
            {
                FireId = NoControl;
            }
            if (IsRepaint)
            {
                _candidateId = NoControl;
                _scrollCandidate = NoControl;
                _scrollDelta = _wheelPending;
            }
        }

        /// <summary>В конце OnGUI мода.</summary>
        public static void EndPass()
        {
            if (!IsRepaint) return;
            if (_pressPending)
            {
                _pressPending = false;
                _armedId = _candidateId;
                _pressResolved = true;
            }
            if (Mathf.Abs(_wheelPending) > 0.01f)
            {
                _armedScrollId = _scrollCandidate;
                _armedScrollDelta = _wheelPending;
                _wheelPending = 0f;
            }
            else
            {
                _armedScrollId = NoControl;
            }
        }

        /// <summary>
        /// Опознание элемента: ключ (подпись кнопки, имя поля) в пределах окна и области <see cref="PushScope"/>
        /// плюс номер среди элементов с тем же ключом в этом проходе. Сдвиг соседних элементов его не меняет.
        /// </summary>
        public static int ControlId(string key, int kind = KindClick)
        {
            int h = Mix(Mix(Mix(_scope, CurrentWindow), kind), key == null ? 0 : key.GetHashCode());
            Seen.TryGetValue(h, out int n);
            Seen[h] = n + 1;
            int id = Mix(h, n);
            return id == NoControl ? 1 : id;
        }

        private static int Mix(int h, int v) => unchecked((h ^ v) * 16777619 + 1);

        /// <summary>
        /// Область ключей для строки списка: одинаковые кнопки разных строк («✕», «Изм.») привязаны к своей строке,
        /// а не к позиции в списке.
        /// </summary>
        public static void PushScope(int key)
        {
            ScopeStack.Push(_scope);
            _scope = Mix(_scope, key);
        }

        public static void PopScope()
        {
            if (ScopeStack.Count > 0) _scope = ScopeStack.Pop();
        }

        /// <summary>Мышь над прямоугольником (координаты текущей группы/области) и не скрыта прокруткой или другим окном.</summary>
        public static bool IsHover(Rect r)
        {
            if (!ArenaUI.WantsCursor || CurrentWindow != TopWindowUnderMouse) return false;
            Vector2 m = Event.current.mousePosition;
            if (!r.Contains(m)) return false;
            if (VisibleRectProp != null)
            {
                try
                {
                    var vis = (Rect)VisibleRectProp.GetValue(null, null);
                    if (!vis.Contains(m)) return false;
                }
                catch { }
            }
            return true;
        }

        /// <summary>
        /// Кликабельный элемент с ключом <paramref name="key"/> (см. <see cref="ControlId"/>): регистрирует попадание
        /// в Repaint и возвращает true в кадр срабатывания.
        /// </summary>
        public static bool Clickable(Rect r, string key, out bool hover, int kind = KindClick)
        {
            int id = ControlId(key, kind);
            hover = IsRepaint && GUI.enabled && IsHover(r);
            if (hover && _pressPending) _candidateId = id;
            if (!GUI.enabled || FireId != id) return false;
            InputApplied = true;
            return true;
        }

        // ------------------------------------------------------------------ прокрутка

        public static Vector2 BeginScroll(Vector2 pos, params GUILayoutOption[] options)
        {
            int id = ControlId(null, KindScroll);
            ScrollStack.Push(id);
            if (FirstPass && _armedScrollId == id)
            {
                pos.y = Mathf.Max(0f, pos.y + _armedScrollDelta);
                _armedScrollId = NoControl;
            }
            return GUILayout.BeginScrollView(pos, options);
        }

        public static void EndScroll()
        {
            GUILayout.EndScrollView();
            int id = ScrollStack.Count > 0 ? ScrollStack.Pop() : NoControl;
            if (!IsRepaint || _scrollCandidate != NoControl || Mathf.Abs(_scrollDelta) < 0.01f) return;
            // Внутренняя прокрутка закрывается раньше внешней — она и забирает колесо.
            if (IsHover(GUILayoutUtility.GetLastRect())) _scrollCandidate = id;
        }

        // ------------------------------------------------------------------ ползунок

        /// <summary>Доля 0…1 от перетаскивания; null — ползунок не тянут. <paramref name="active"/> — тянут именно этот.</summary>
        public static float? Drag(Rect r, string key, out bool active)
        {
            int id = ControlId(key, KindDrag);
            if (IsRepaint && GUI.enabled)
            {
                if (_pressPending && IsHover(r))
                {
                    _dragId = id;
                    _candidateId = NoControl;
                }
                if (_dragId == id && Held)
                {
                    _dragFrac = Mathf.Clamp01((Event.current.mousePosition.x - r.x) / Mathf.Max(1f, r.width));
                    _dragArmed = true;
                }
            }
            active = _dragId == id && Held;
            if (FirstPass && _dragId == id && _dragArmed)
            {
                _dragArmed = false;
                InputApplied = true;
                return _dragFrac;
            }
            return null;
        }

        // ------------------------------------------------------------------ текст

        public static void Focus(string id) => FocusId = id;

        public static void Unfocus() => FocusId = null;

        /// <summary>Применить набранное за кадр к строке фокусного поля (только в первом проходе кадра).</summary>
        public static string ApplyTyping(string id, string value, out bool submitted)
        {
            submitted = false;
            if (!FirstPass || FocusId != id) return value;
            if (FrameTyped.Count > 0 || _fClear || _fPaste || _fBackspace || _fSubmit || _fCancel) InputApplied = true;
            value = value ?? "";
            if (_fClear) value = "";
            if (_fPaste)
            {
                string clip = GUIUtility.systemCopyBuffer ?? "";
                clip = clip.Replace("\r", "").Replace("\n", " ");
                value += clip;
            }
            if (_fBackspace && value.Length > 0) value = value.Substring(0, value.Length - 1);
            foreach (var c in FrameTyped) value += c;
            if (_fSubmit || _fCancel)
            {
                submitted = true;
                FocusId = null;
            }
            _fClear = _fPaste = _fBackspace = _fSubmit = _fCancel = false;
            FrameTyped.Clear();
            return value;
        }
    }
}
