using System.Collections.Generic;
using UnityEngine;
using ValheimArena.UI;

namespace ValheimArena.Core
{
    public enum CameraMode { Player = 0, Free = 1, Follow = 2 }

    /// <summary>
    /// Свободная камера и слежение за бойцами. Работает вместо GameCamera.LateUpdate (патч),
    /// пока режим не «Игрок»; GameCamera.InFreeFly() при этом отвечает true — игра сама
    /// отключает управление персонажем, хотбар и центрирует травяной покров по камере.
    /// </summary>
    public static class ArenaCamera
    {
        public static CameraMode Mode { get; private set; } = CameraMode.Player;
        public static ArenaUnit Target { get; private set; }

        private static Vector3 _pos;
        private static float _yaw;
        private static float _pitch;
        private static float _orbitYaw;
        private static float _orbitPitch = 20f;
        private static float _zoom = 1f;
        private static Vector3 _smoothedFocus;
        private static Vector3 _camVel;
        private static float _directorTimer;
        private static bool _initialized;
        private static int _collisionMask;

        public static bool Active => Mode != CameraMode.Player && GameCamera.instance && Player.m_localPlayer;

        public static string ModeName(CameraMode m)
        {
            switch (m)
            {
                case CameraMode.Player: return L.T("Игрок");
                case CameraMode.Free: return L.T("Свободная");
                default: return L.T("Слежение");
            }
        }

        public static void SetMode(CameraMode mode)
        {
            if (mode == Mode) return;
            var gc = GameCamera.instance;
            if (mode != CameraMode.Player && gc)
            {
                var t = gc.transform;
                if (Mode == CameraMode.Player || !_initialized)
                {
                    _pos = t.position;
                    var e = t.rotation.eulerAngles;
                    _yaw = e.y;
                    _pitch = NormalizePitch(e.x);
                    _initialized = true;
                }
                if (mode == CameraMode.Follow)
                {
                    if (!Target || !Target.Alive) PickFirstTarget();
                    _orbitYaw = t.rotation.eulerAngles.y;
                    _smoothedFocus = Target ? Target.Character.GetCenterPoint() : _pos;
                    if (!Target) Arena.Message(L.T("Некого отслеживать — выпустите бойцов."));
                }
                var cam = Reflect.Camera(gc);
                if (cam) cam.fieldOfView = ArenaSettingsStore.Current.cameraFov;
                var sky = Reflect.SkyCamera(gc);
                if (sky) sky.fieldOfView = ArenaSettingsStore.Current.cameraFov;
            }
            Mode = mode;
        }

        public static void CycleMode()
        {
            SetMode(Mode == CameraMode.Player ? CameraMode.Free : Mode == CameraMode.Free ? CameraMode.Follow : CameraMode.Player);
        }

        public static void SetTarget(ArenaUnit u)
        {
            Target = u;
            _directorTimer = ArenaSettingsStore.Current.directorInterval;
            if (u && Mode != CameraMode.Follow) SetMode(CameraMode.Follow);
        }

        public static List<ArenaUnit> Candidates()
        {
            var list = Arena.SessionUnits(true);
            list.Sort((a, b) => a.Team != b.Team ? a.Team.CompareTo(b.Team) : a.SerialNumber.CompareTo(b.SerialNumber));
            return list;
        }

        public static void Next(int dir)
        {
            var list = Candidates();
            if (list.Count == 0) { Target = null; return; }
            int idx = Target ? list.IndexOf(Target) : -1;
            idx = idx < 0 ? (dir > 0 ? 0 : list.Count - 1) : (idx + dir + list.Count) % list.Count;
            SetTarget(list[idx]);
        }

        private static void PickFirstTarget()
        {
            var list = Candidates();
            Target = list.Count > 0 ? list[0] : null;
        }

        public static void OnUnitRemoved(ArenaUnit u)
        {
            if (u != Target) return;
            Target = null;
            if (Mode == CameraMode.Follow && ArenaSettingsStore.Current.autoSwitchOnDeath) _directorTimer = 1.2f;
        }

        public static void OnArenaClosed()
        {
            Target = null;
        }

        // ------------------------------------------------------------------ кадр

        /// <summary>Вызывается вместо GameCamera.LateUpdate.</summary>
        public static void UpdateCamera(GameCamera gc)
        {
            float dt = Time.unscaledDeltaTime;
            var t = gc.transform;
            var s = ArenaSettingsStore.Current;

            gc.UpdateMouseCapture();
            var listener = Reflect.Listener(gc);
            if (listener) listener.transform.localPosition = Vector3.zero;

            bool typing = ArenaUI.WantsKeyboard || Console.IsVisible() || (Chat.instance && Chat.instance.HasFocus());
            // При открытом окне мода обзор — только с зажатой ПКМ: иначе камера крутится, пока ведёшь мышь к кнопке.
            bool look = !ArenaUI.WantsCursor || ZInput.GetMouseButton(1);
            Vector2 md = look && !Menu.IsVisible() ? ZInput.GetMouseDelta() * ArenaPlugin.MouseSensitivity.Value : Vector2.zero;
            float invertY = PlayerController.m_invertCameraY ? -1f : 1f;
            float scroll = ArenaUI.WantsMouse ? 0f : ZInput.GetMouseScrollWheel();

            if (Mode == CameraMode.Free)
            {
                _yaw += md.x;
                _pitch = Mathf.Clamp(_pitch - md.y * invertY, -89f, 89f);
                var rot = Quaternion.Euler(_pitch, _yaw, 0f);

                if (scroll != 0f) s.freeCamSpeed = Mathf.Clamp(s.freeCamSpeed * (scroll > 0f ? 1.15f : 0.87f), 1f, 200f);
                Vector3 move = Vector3.zero;
                if (!typing)
                {
                    if (ZInput.GetButton("Forward")) move += Vector3.forward;
                    if (ZInput.GetButton("Backward")) move += Vector3.back;
                    if (ZInput.GetButton("Left")) move += Vector3.left;
                    if (ZInput.GetButton("Right")) move += Vector3.right;
                    Vector2 stick = ZInput.GetJoyLeftStick();
                    move += new Vector3(stick.x, 0f, stick.y);
                }
                Vector3 world = rot * move;
                if (!typing)
                {
                    if (ZInput.GetButton("Jump") || ZInput.GetKey(KeyCode.E, false)) world += Vector3.up;
                    if (ZInput.GetButton("Crouch") || ZInput.GetKey(KeyCode.Q, false)) world += Vector3.down;
                }
                if (world.sqrMagnitude > 1f) world.Normalize();
                float speed = s.freeCamSpeed;
                if (!typing && ZInput.GetKey(KeyCode.LeftShift, false)) speed *= 3f;
                if (!typing && ZInput.GetKey(KeyCode.LeftAlt, false)) speed *= 0.3f;
                _pos += world * speed * dt;
                _pos = LimitPosition(_pos);
                t.SetPositionAndRotation(_pos, rot);
            }
            else if (Mode == CameraMode.Follow)
            {
                UpdateDirector(dt);
                if (!Target || !Target.Alive)
                {
                    // Цели нет — стоим на месте, но даём оглядеться.
                    _yaw += md.x;
                    _pitch = Mathf.Clamp(_pitch - md.y * invertY, -89f, 89f);
                    t.SetPositionAndRotation(_pos, Quaternion.Euler(_pitch, _yaw, 0f));
                    return;
                }
                var ch = Target.Character;
                Vector3 focus = ch.GetCenterPoint();
                _smoothedFocus = Vector3.Lerp(_smoothedFocus, focus, 1f - Mathf.Exp(-10f * dt));

                if (scroll != 0f) _zoom = Mathf.Clamp(_zoom * (scroll > 0f ? 0.88f : 1.13f), 0.25f, 6f);
                float size = Mathf.Max(ch.GetHeight(), ch.GetRadius() * 2f);
                float dist = Mathf.Max(2.5f, size * 2.2f + 2f) * _zoom * s.followDistanceScale;

                var style = (FollowStyle)s.followStyle;
                if (style == FollowStyle.Chase)
                {
                    float targetYaw = ch.transform.rotation.eulerAngles.y;
                    _orbitYaw = Mathf.LerpAngle(_orbitYaw, targetYaw, 1f - Mathf.Exp(-2.5f * dt));
                    _orbitPitch = Mathf.Clamp(_orbitPitch - md.y * invertY * 0.5f, -10f, 60f);
                }
                else if (style == FollowStyle.TopDown)
                {
                    _orbitYaw += md.x;
                    _orbitPitch = 72f;
                    dist *= 1.6f;
                }
                else
                {
                    _orbitYaw += md.x;
                    _orbitPitch = Mathf.Clamp(_orbitPitch - md.y * invertY, -20f, 85f);
                }

                var rot = Quaternion.Euler(_orbitPitch, _orbitYaw, 0f);
                Vector3 desired = _smoothedFocus - rot * Vector3.forward * dist;
                desired = AvoidObstacles(_smoothedFocus, desired);
                _pos = Vector3.SmoothDamp(_pos, desired, ref _camVel, 0.08f, 500f, dt);
                _pos = KeepAboveGround(_pos);
                Vector3 lookDir = _smoothedFocus - _pos;
                t.SetPositionAndRotation(_pos, lookDir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(lookDir, Vector3.up) : t.rotation);
                _yaw = t.rotation.eulerAngles.y;
                _pitch = NormalizePitch(t.rotation.eulerAngles.x);
            }
        }

        private static void UpdateDirector(float dt)
        {
            var s = ArenaSettingsStore.Current;
            _directorTimer -= dt;
            if (_directorTimer > 0f) return;
            bool lost = !Target || !Target.Alive;
            if (lost && !s.autoSwitchOnDeath && !s.director) return;
            if (!lost && !s.director) return;

            // Режиссёр: предпочтение тем, кто сейчас дерётся и наносит больше урона.
            ArenaUnit best = null;
            float bestScore = float.MinValue;
            foreach (var u in Candidates())
            {
                if (u == Target && !lost) continue;
                float score = (u.Stat?.dealt ?? 0f) * 0.01f + Random.value * 2f;
                if (u.Monster && Reflect.TargetCreature(u.Monster)) score += 5f;
                if (u.Character.IsBoss()) score += 3f;
                if (score > bestScore) { bestScore = score; best = u; }
            }
            if (best)
            {
                Target = best;
                _smoothedFocus = best.Character.GetCenterPoint();
            }
            _directorTimer = best ? s.directorInterval : 1f;
        }

        private static Vector3 LimitPosition(Vector3 p)
        {
            var s = ArenaSettingsStore.Current;
            var arena = Arena.Current;
            if (s.limitCamera && arena != null)
            {
                Vector3 c = arena.Center;
                Vector3 d = p - c;
                float max = arena.Rules.radius + 80f;
                Vector2 h = new Vector2(d.x, d.z);
                if (h.magnitude > max)
                {
                    h = h.normalized * max;
                    p.x = c.x + h.x;
                    p.z = c.z + h.y;
                }
                p.y = Mathf.Clamp(p.y, c.y - 50f, c.y + 250f);
            }
            return KeepAboveGround(p);
        }

        private static Vector3 KeepAboveGround(Vector3 p)
        {
            if (!ZoneSystem.instance) return p;
            if (ZoneSystem.instance.GetGroundHeight(p, out float h) && p.y < h + 0.4f) p.y = h + 0.4f;
            return p;
        }

        private static Vector3 AvoidObstacles(Vector3 from, Vector3 to)
        {
            if (_collisionMask == 0) _collisionMask = LayerMask.GetMask("terrain", "static_solid", "Default", "piece");
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.01f) return to;
            if (Physics.SphereCast(from, 0.3f, dir / len, out var hit, len, _collisionMask))
                return from + dir / len * Mathf.Max(0.8f, hit.distance - 0.2f);
            return to;
        }

        private static float NormalizePitch(float x)
        {
            if (x > 180f) x -= 360f;
            return Mathf.Clamp(x, -89f, 89f);
        }
    }
}
