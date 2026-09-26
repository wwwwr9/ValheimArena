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
    /// (обычно Layout) — до раскладки, поэтому изменения состава элементов не ломают GUILayout.
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
        private static int _passFrame = -1;
        private static int _id;
        private static int _armedId = -1;
        private static int _candidateId = -1;
        private static bool _pressResolved;
        private static int _armedScrollId = -1;
        private static float _armedScrollDelta;
        private static int _scrollCandidate = -1;
        private static float _scrollDelta;
        private static readonly Stack<int> ScrollStack = new Stack<int>();

        public static int FireId { get; private set; } = -1;
        public static bool FirstPass { get; private set; }
        public static bool IsRepaint { get; private set; }

        // --- текстовый фокус и перетаскивание ползунков
        public static string FocusId { get; private set; }
        private static readonly List<char> FrameTyped = new List<char>();
        private static bool _fBackspace, _fDelete, _fSubmit, _fCancel, _fClear, _fPaste;
        public static int DragId { get; private set; } = -1;
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
            if (!Held) DragId = -1;

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
                bool ctrl = kb.ctrlKey.isPressed;
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
            if (_hookedKeyboard != null && _hookedKeyboard.ctrlKey.isPressed) return;
            if (TypedPending.Count < 256) TypedPending.Add(c);
        }

        // ================================================================== проходы OnGUI

        /// <summary>В начале OnGUI мода, на каждом событии.</summary>
        public static void BeginPass(Vector2 guiMouse, Rect menu, bool menuOpen, Rect editor, bool editorOpen)
        {
            _id = 0;
            ScrollStack.Clear();
            IsRepaint = Event.current.type == EventType.Repaint;
            FirstPass = Time.frameCount != _passFrame;
            _passFrame = Time.frameCount;

            TopWindowUnderMouse = editorOpen && editor.Contains(guiMouse) ? 2 : menuOpen && menu.Contains(guiMouse) ? 1 : 0;

            if (FirstPass)
            {
                FireId = _armedId;
                _armedId = -1;
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
                FireId = -1;
            }
            if (IsRepaint)
            {
                _candidateId = -1;
                _scrollCandidate = -1;
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
                _armedScrollId = -1;
            }
        }

        public static int NextId() => ++_id;

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

        /// <summary>Кликабельный элемент: регистрирует попадание в Repaint и возвращает true в кадр срабатывания.</summary>
        public static bool Clickable(Rect r, out bool hover)
        {
            int id = NextId();
            hover = IsRepaint && GUI.enabled && IsHover(r);
            if (hover && _pressPending) _candidateId = id;
            return GUI.enabled && FireId == id;
        }

        // ------------------------------------------------------------------ прокрутка

        public static Vector2 BeginScroll(Vector2 pos, params GUILayoutOption[] options)
        {
            int id = NextId();
            ScrollStack.Push(id);
            if (FirstPass && _armedScrollId == id)
            {
                pos.y = Mathf.Max(0f, pos.y + _armedScrollDelta);
                _armedScrollId = -1;
            }
            return GUILayout.BeginScrollView(pos, options);
        }

        public static void EndScroll()
        {
            GUILayout.EndScrollView();
            int id = ScrollStack.Count > 0 ? ScrollStack.Pop() : -1;
            if (!IsRepaint || _scrollCandidate >= 0 || Mathf.Abs(_scrollDelta) < 0.01f) return;
            // Внутренняя прокрутка закрывается раньше внешней — она и забирает колесо.
            if (IsHover(GUILayoutUtility.GetLastRect())) _scrollCandidate = id;
        }

        // ------------------------------------------------------------------ ползунок

        /// <summary>Доля 0…1 от перетаскивания; null — ползунок не тянут.</summary>
        public static float? Drag(Rect r)
        {
            int id = NextId();
            if (IsRepaint && GUI.enabled)
            {
                if (_pressPending && IsHover(r))
                {
                    DragId = id;
                    _candidateId = -1;
                }
                if (DragId == id && Held)
                {
                    _dragFrac = Mathf.Clamp01((Event.current.mousePosition.x - r.x) / Mathf.Max(1f, r.width));
                    _dragArmed = true;
                }
            }
            if (FirstPass && DragId == id && _dragArmed)
            {
                _dragArmed = false;
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
