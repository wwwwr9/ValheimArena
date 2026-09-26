using System;
using System.Collections.Generic;
using UnityEngine;
using ValheimArena.Profiles;

namespace ValheimArena.Core
{
    /// <summary>
    /// Арены мира и ход боя. Сетевая модель:
    /// • у арены есть хозяин (кто её развернул) — он ведёт состояние (подготовка/отсчёт/бой/пауза/итоги),
    ///   правила и итоговую статистику и рассылает их всем (<see cref="ArenaNet"/>);
    /// • существом в Valheim управляет клиент-владелец, и владение переходит между игроками — поэтому каждый
    ///   клиент сам применяет правила арены (заморозка, граница, поиск цели) к бойцам, которыми владеет;
    /// • гости (если хозяин разрешил) начинают/останавливают бой через команды хозяину и выпускают своих бойцов.
    /// </summary>
    public static class Arena
    {
        public static readonly Dictionary<string, ArenaSession> Sessions = new Dictionary<string, ArenaSession>();

        /// <summary>Меняется при любом изменении сессий/правил — кэши бойцов по нему обновляются.</summary>
        public static int SessionsVersion { get; private set; }

        /// <summary>Арена, которую ведёт этот клиент.</summary>
        public static ArenaSession Local { get; private set; }

        /// <summary>Арена, которую показывает интерфейс: своя или ближайшая чужая.</summary>
        public static ArenaSession Current { get; private set; }

        public static float TimeScale { get; private set; } = 1f;

        /// <summary>Доля периметра текущей арены вне зоны, которую игра обновляет вокруг персонажа (0…1).</summary>
        public static float OutsideActive { get; private set; }

        public static ArenaSettings S => ArenaSettingsStore.Current;

        public static bool Active => Current != null;
        public static ArenaState State => Current?.State ?? ArenaState.Inactive;
        public static string StateName => Current?.StateName ?? L.T("не активна");
        public static bool IsHost => Current != null && Current.IsLocal;
        public static bool CanControl => Current != null && (Current.IsLocal || Current.Rules.allowGuests);
        public static bool CanEditRules => Current == null || Current.IsLocal;

        private static ZNet _znetSeen;
        private static float _worldEnterTime;
        private static float _tickSecond, _tickHalf, _heartbeat, _statsBroadcast, _orphanTick;
        private static string _lastRulesJson = "";

        private static readonly Dictionary<string, Dictionary<string, float>> PendingDealt = new Dictionary<string, Dictionary<string, float>>();
        private static readonly Dictionary<string, Dictionary<string, float>> PendingTaken = new Dictionary<string, Dictionary<string, float>>();

        public static long LocalPlayerId => Player.m_localPlayer ? Player.m_localPlayer.GetPlayerID() : 0L;
        public static string LocalPlayerName => Player.m_localPlayer ? Player.m_localPlayer.GetPlayerName() : "?";

        public static ArenaSession FindSession(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Sessions.TryGetValue(id, out var s);
            return s;
        }

        private static void Bump() => SessionsVersion++;

        public static bool HasPeers => ZNet.instance && ZNet.instance.GetPeerConnections() > 0;

        // ================================================================== хозяин: жизненный цикл

        public static void Activate(Vector3 center)
        {
            if (!Player.m_localPlayer) return;
            if (Local != null)
            {
                MoveCenter(center);
                return;
            }
            Local = new ArenaSession
            {
                Id = Guid.NewGuid().ToString("N"),
                IsLocal = true,
                HostPlayerId = LocalPlayerId,
                HostPeer = ZDOMan.GetSessionID(),
                HostName = LocalPlayerName,
                Center = center,
                State = ArenaState.Preparing,
                Rules = ArenaRules.From(S),
                Revision = 1
            };
            _lastRulesJson = JsonIO.ToJson(Local.Rules);
            Sessions[Local.Id] = Local;
            Bump();
            Current = Local;
            ArenaBoundary.Rebuild();
            ArenaNet.SendState(Local);
            Message(L.T("Арена развёрнута. Радиус ") + S.radius.ToString("0") + L.T(" м.") + (HasPeers ? L.T(" Игроки с модом видят её и её правила.") : ""));
        }

        public static void MoveCenter(Vector3 center)
        {
            if (Local == null) return;
            Local.Center = center;
            ChangedLocal();
            ArenaBoundary.Rebuild();
        }

        /// <summary>Свернуть свою арену (хозяин).</summary>
        public static void Deactivate(bool despawn)
        {
            if (Local == null) return;
            if (despawn) HostClear(ArenaCleanup.FromSettings(S) | ClearWhat.Creatures);
            Local.State = ArenaState.Inactive;
            Local.Revision++;
            ArenaNet.SendState(Local);
            Sessions.Remove(Local.Id);
            Local = null;
            Bump();
            SetTimeScale(1f);
            Current = PickCurrent();
            if (Current == null) ArenaBoundary.Hide();
            else ArenaBoundary.Rebuild();
            ArenaCamera.OnArenaClosed();
        }

        private static void ChangedLocal()
        {
            if (Local == null) return;
            Local.Revision++;
            Bump();
            ArenaNet.SendState(Local);
        }

        // ================================================================== команды (хозяин и гости)

        public static void StartFight() => Command("start");
        public static void Pause() => Command("pause");
        public static void Resume() => Command("resume");
        /// <summary>Очистить арену по выбору в настройках (существа / ресурсы / надгробия / гаммельтролли / деревья).</summary>
        public static void ClearUnits() => ClearArena(ArenaCleanup.FromSettings(S));

        public static void ClearArena(ClearWhat what)
        {
            if (what == ClearWhat.None) { Message(L.T("Очистка: всё отмечено «оставить» — убирать нечего.")); return; }
            Command("clear:" + (int)what);
        }

        public static void ToggleFight()
        {
            var s = Current;
            if (s == null)
            {
                if (Player.m_localPlayer) Activate(Player.m_localPlayer.transform.position);
                return;
            }
            switch (s.State)
            {
                case ArenaState.Fighting: Pause(); break;
                case ArenaState.Paused: Resume(); break;
                case ArenaState.Countdown: break;
                default: StartFight(); break;
            }
        }

        private static void Command(string cmd)
        {
            var s = Current;
            if (s == null) return;
            if (s.IsLocal) ExecuteHostCommand(cmd, LocalPlayerName, false);
            else if (!s.Rules.allowGuests) Message(L.T("Хозяин арены (") + s.HostName + L.T(") не разрешил гостям управлять боем."));
            else ArenaNet.SendCommand(s, cmd, LocalPlayerName);
        }

        /// <summary>Команда на клиенте хозяина: своя или присланная гостем.</summary>
        public static void ExecuteHostCommand(string cmd, string who, bool fromGuest)
        {
            var s = Local;
            if (s == null) return;
            if (fromGuest && !s.Rules.allowGuests) return;
            var what = ClearWhat.Creatures;
            if (cmd.StartsWith("clear:"))
            {
                int.TryParse(cmd.Substring(6), out var mask);
                what = (ClearWhat)mask & ClearWhat.All;
                // Надгробия — это вещи других игроков: гость убирает их, только если это разрешает и сам хозяин.
                if (fromGuest && !S.clearTombstones) what &= ~ClearWhat.Tombstones;
                cmd = "clear";
            }
            switch (cmd)
            {
                case "start":
                    HostStart();
                    break;
                case "pause":
                    if (s.State == ArenaState.Fighting) { s.State = ArenaState.Paused; ChangedLocal(); }
                    break;
                case "resume":
                    if (s.State == ArenaState.Paused) { s.State = ArenaState.Fighting; ChangedLocal(); }
                    break;
                case "clear":
                    HostClear(what);
                    break;
                default:
                    return;
            }
            if (fromGuest) Message($"{who}: {CommandName(cmd)}" + (cmd == "clear" ? " (" + ArenaCleanup.Describe(what) + ")" : ""));
        }

        private static string CommandName(string cmd)
        {
            switch (cmd)
            {
                case "start": return L.T("старт боя");
                case "pause": return L.T("пауза");
                case "resume": return L.T("продолжение");
                case "clear": return L.T("очистка арены");
                default: return cmd;
            }
        }

        private static void HostStart()
        {
            var s = Local;
            if (s.State == ArenaState.Paused) { s.State = ArenaState.Fighting; ChangedLocal(); return; }
            if (s.State == ArenaState.Fighting || s.State == ArenaState.Countdown) return;
            if (!HasHostilePair(SessionUnits(s, true)))
            {
                Message(L.T("Некому сражаться: нет враждующих сторон. Проверьте состав и вкладку «Столкновения»."));
                return;
            }
            s.Result = "";
            if (S.countdown > 0.05f)
            {
                s.State = ArenaState.Countdown;
                s.CountdownEnd = ArenaSession.Now + S.countdown;
                ChangedLocal();
            }
            else BeginFighting(s);
        }

        private static void BeginFighting(ArenaSession s)
        {
            s.State = ArenaState.Fighting;
            s.FightStart = ArenaSession.Now;
            ChangedLocal();
            // Гости поднимут своих бойцов по смене состояния; своих — здесь (своё эхо хозяин не обрабатывает).
            foreach (var u in ArenaUnit.All)
                if (u && u.SessionId == s.Id) u.AlertPending = true;
            Message(L.T("В бой!"));
        }

        private static void HostClear(ClearWhat what)
        {
            var s = Local;
            if (s == null) return;
            // Рассылка всем (и себе): каждый клиент убирает то, чем управляет, хозяин — ещё и остальное.
            if (ArenaNet.Ready) ArenaNet.SendClear(s.Id, what);
            else OnClear(s.Id, what);
            if ((what & ClearWhat.Creatures) != 0)
            {
                s.ClearStats();
                if (s.State != ArenaState.Preparing) s.State = ArenaState.Preparing;
            }
            ChangedLocal();
        }

        /// <summary>У всех клиентов: убрать выбранное внутри границы арены.</summary>
        public static void OnClear(string sessionId, ClearWhat what)
        {
            var s = FindSession(sessionId);
            if (s == null) return;
            var counts = ArenaCleanup.Run(s, what, claimOthers: s.IsLocal);
            if ((what & ClearWhat.Creatures) != 0)
            {
                if (!s.IsLocal) s.ClearStats();
                DropPending(sessionId);
            }
            if (s.IsLocal) Message(L.T("Арена очищена: ") + ArenaCleanup.Report(counts) + ".");
        }

        public static void DespawnUnit(ArenaUnit u, bool adjustBossKey = true) => Despawn(u, adjustBossKey);

        /// <summary>
        /// Снять с ключа мира activeBosses сразу N боссов. SetGlobalKey уходит на сервер, и локальная копия
        /// обновляется только его ответом — N удалений за один проход прочитали бы одно значение и сняли бы одного.
        /// </summary>
        public static void ReleaseActiveBosses(int n)
        {
            if (n <= 0 || !ZoneSystem.instance) return;
            ZoneSystem.instance.GetGlobalKey(GlobalKeys.activeBosses, out float value);
            ZoneSystem.instance.SetGlobalKey(GlobalKeys.activeBosses, Mathf.Max(0f, value - n));
        }

        private static void Despawn(ArenaUnit u, bool adjustBossKey = true)
        {
            if (!u || !u.NView || !u.NView.IsValid()) return;
            if (!u.NView.IsOwner()) u.NView.ClaimOwnership();
            // Встревоженный босс учтён в ключе мира activeBosses (мешает порталам при «NoBossPortals») — снимаем, как это делает смерть.
            var zdo = u.NView.GetZDO();
            if (adjustBossKey && zdo.GetBool(ZDOVars.s_bossCount)) ReleaseActiveBosses(1);
            ZNetScene.instance.Destroy(u.gameObject);
        }

        // ================================================================== сеть: входящее

        public static void OnRemoteState(SessionState st)
        {
            if (st == null || string.IsNullOrEmpty(st.id)) return;
            if (Local != null && st.id == Local.Id) return;
            var s = FindSession(st.id);
            if ((ArenaState)st.state == ArenaState.Inactive)
            {
                if (s != null)
                {
                    Sessions.Remove(st.id);
                    Bump();
                    if (Current == s)
                    {
                        Current = PickCurrent();
                        Message(L.F("Арена игрока {0} свёрнута.", s.HostName));
                    }
                }
                return;
            }
            bool isNew = s == null;
            if (isNew)
            {
                s = new ArenaSession();
                Sessions[st.id] = s;
            }
            int oldRevision = s.Revision;
            var oldState = s.Apply(st);
            if (isNew || oldRevision != st.revision)
            {
                Bump();
                if (Current == s) ArenaBoundary.Rebuild();
            }
            if (oldState != ArenaState.Fighting && s.State == ArenaState.Fighting)
                foreach (var u in ArenaUnit.All)
                    if (u && u.SessionId == s.Id) u.AlertPending = true;
        }

        public static void OnHello()
        {
            if (Local == null) return;
            ArenaNet.SendState(Local);
            if (Local.Stats.Count > 0) ArenaNet.SendStats(Local);
        }

        public static void OnDamageReport(string sessionId, Dictionary<string, float> dealt, Dictionary<string, float> taken)
        {
            var s = Local;
            if (s == null || s.Id != sessionId) return;
            foreach (var kv in dealt) { var st = s.Stat(kv.Key); if (st != null) st.dealt += kv.Value; }
            foreach (var kv in taken) { var st = s.Stat(kv.Key); if (st != null) st.taken += kv.Value; }
            s.StatsDirty = true;
        }

        public static void OnKill(KillEvent e)
        {
            var s = FindSession(e.session);
            if (s == null) return;
            s.AddFeed(e);
            if (!s.IsLocal) return;
            var v = s.Stat(e.victimKey);
            if (v == null) return;
            if (string.IsNullOrEmpty(v.label))
            {
                v.label = e.victim;
                v.color = e.victimColor;
                v.team = e.victimTeam;
                v.faction = e.victimFaction;
                FillSide(s, v, e.victim);
            }
            if (v.alive)
            {
                v.alive = false;
                v.killedBy = e.killer;
                v.deathTime = ArenaSession.Now;
                if (!string.IsNullOrEmpty(e.killerKey)) s.Stat(e.killerKey).kills++;
            }
            s.StatsDirty = true;
            s.RebuildSides();
        }

        public static void OnStatsSummary(StatsSummary sum)
        {
            var s = FindSession(sum.session);
            if (s == null || s.IsLocal) return;
            s.ReplaceStats(sum.units);
        }

        // ================================================================== спавн

        public static bool CanSpawn(out string reason)
        {
            reason = "";
            var s = Current;
            if (s == null) { reason = L.T("Арена не развёрнута."); return false; }
            if (!s.IsLocal && !s.Rules.allowGuests) { reason = L.T("Хозяин арены (") + s.HostName + L.T(") не разрешил гостям выпускать бойцов."); return false; }
            return true;
        }

        public static List<ArenaUnit> SpawnRoster()
        {
            var result = new List<ArenaUnit>();
            if (!CanSpawn(out var why)) { Message(why); return result; }
            for (int i = 0; i < S.roster.Count; i++)
                result.AddRange(SpawnGroup(i));
            if (S.autoStart && result.Count > 0 && Current.State == ArenaState.Preparing) StartFight();
            return result;
        }

        public static List<ArenaUnit> SpawnGroup(int index)
        {
            var list = new List<ArenaUnit>();
            if (!CanSpawn(out var why)) { Message(why); return list; }
            if (index < 0 || index >= S.roster.Count) return list;
            var s = Current;
            var g = S.roster[index];
            var entry = CreatureCatalog.Get(g.prefab);
            if (entry == null)
            {
                Message(L.T("Нет такого существа: ") + g.prefab);
                return list;
            }
            CreatureProfile profile = null;
            if (!string.IsNullOrEmpty(g.profile))
            {
                profile = ProfileStore.Load(g.prefab, g.profile);
                if (profile == null) Message(L.F("Профиль «{0}» для {1} не найден — ванильные параметры.", g.profile, entry.Display));
            }
            Vector3 anchor = GroupAnchor(s, g);
            for (int n = 0; n < g.count; n++)
            {
                Vector3 pos = (Placement)g.placement == Placement.Random
                    ? s.RandomPointInside(0.85f)
                    : s.ClampInside(anchor + RandomXZ(g.spread), 0.92f);
                int stars = UnityEngine.Random.Range(g.starsMin, g.starsMax + 1);
                var u = SpawnOne(s, entry, pos, g, index, stars, profile);
                if (u) list.Add(u);
            }
            if (list.Count > 0)
                Message(L.F("Выпущено: {0} ×{1} ({2})", entry.Display, list.Count, Hostility.TeamName(g.team)));
            return list;
        }

        private static ArenaUnit SpawnOne(ArenaSession s, CreatureEntry entry, Vector3 pos, SpawnGroup g, int groupIndex, int stars, CreatureProfile profile)
        {
            float y = GroundHeight(pos);
            if (entry.Swimmer && ZoneSystem.instance) y = Mathf.Max(y, ZoneSystem.instance.m_waterLevel - 0.5f);
            pos.y = y + (entry.Flying ? ArenaFlight.SpawnAltitude(entry.GameObject) : 0.3f);
            Vector3 look = s.Center - pos;
            look.y = 0f;
            var rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;

            var go = UnityEngine.Object.Instantiate(entry.GameObject, pos, rot);
            var ch = go.GetComponent<Character>();
            var nview = go.GetComponent<ZNetView>();
            if (!ch || !nview || !nview.IsValid())
            {
                if (go) UnityEngine.Object.Destroy(go);
                return null;
            }
            var zdo = nview.GetZDO();
            zdo.Set(ArenaUnit.ZUnit, true);
            zdo.Set(ArenaUnit.ZPrefab, entry.Prefab);
            zdo.Set(ArenaUnit.ZTeam, g.team);
            zdo.Set(ArenaUnit.ZGroup, groupIndex);
            zdo.Set(ArenaUnit.ZFaction, g.factionOverride);
            zdo.Set(ArenaUnit.ZSession, s.Id);
            zdo.Set(ArenaUnit.ZHost, s.HostPlayerId);
            zdo.Set(ArenaUnit.ZCreator, LocalPlayerId);
            zdo.Set(ArenaUnit.ZProfile, profile != null ? ProfileStore.ToJson(profile) : "");
            zdo.Set(ArenaUnit.ZBehavior, g.behavior);
            zdo.Set(ArenaUnit.ZAdopted, ArenaUnit.Spawned);
            zdo.Set(ArenaUnit.ZOrigin, pos);
            zdo.Set(ZDOVars.s_spawnPoint, pos);

            var unit = ArenaUnit.Attach(ch);
            ch.SetLevel(Mathf.Clamp(stars + 1, 1, 21));
            ch.SetHealth(ch.GetMaxHealth());
            ProfileLogic.RecalculateHealth(unit);   // полное HP и свой максимум в ZDO — уже с уровнем
            return unit;
        }

        // ================================================================== призванные и посторонние

        /// <summary>
        /// Существо, созданное бойцом арены (призыв босса, порождение атаки, снаряда, эффекта), встаёт на его сторону:
        /// та же арена, команда и фракция, поведение — как у призвавшего.
        /// </summary>
        public static void AdoptSummon(Character spawned, ArenaUnit summoner)
        {
            if (!spawned || spawned.IsPlayer() || !summoner || ArenaUnit.Get(spawned)) return;
            var s = summoner.Session;
            if (s == null) return;
            int faction = summoner.FactionOverride >= 0 ? summoner.FactionOverride : (int)summoner.Character.GetFaction();
            Adopt(spawned, s, summoner.Team, faction, summoner.BehaviorOverride, ArenaUnit.Summoned);
        }

        private static ArenaUnit Adopt(Character c, ArenaSession s, int team, int factionOverride, int behavior, int kind)
        {
            var nview = c.GetComponent<ZNetView>();
            if (!nview || !nview.IsValid() || !nview.IsOwner() || !c.GetComponent<BaseAI>()) return null;
            var zdo = nview.GetZDO();
            zdo.Set(ArenaUnit.ZUnit, true);
            zdo.Set(ArenaUnit.ZPrefab, Utils.GetPrefabName(c.gameObject));
            zdo.Set(ArenaUnit.ZTeam, team);
            zdo.Set(ArenaUnit.ZGroup, -1);
            zdo.Set(ArenaUnit.ZFaction, factionOverride);
            zdo.Set(ArenaUnit.ZSession, s.Id);
            zdo.Set(ArenaUnit.ZHost, s.HostPlayerId);
            zdo.Set(ArenaUnit.ZCreator, LocalPlayerId);
            zdo.Set(ArenaUnit.ZBehavior, behavior);
            zdo.Set(ArenaUnit.ZAdopted, kind);
            zdo.Set(ArenaUnit.ZOrigin, c.transform.position);
            ArenaUnit.RememberWorldFlags(c, zdo);
            var u = ArenaUnit.Attach(c);
            if (s.State == ArenaState.Fighting) u.AlertPending = true;
            return u;
        }

        /// <summary>
        /// Посторонние внутри границы (забредшие дикие, чужие призывы): по правилу арены — не замечать, вовлечь в бой
        /// как команду «Посторонние» или убрать. Решает клиент-владелец существа.
        /// </summary>
        private static void HandleIntruders()
        {
            if (Sessions.Count == 0) return;
            foreach (var c in new List<Character>(Character.GetAllCharacters()))
            {
                if (!c || c.IsPlayer() || c.IsDead() || c.IsTamed() || ArenaUnit.Get(c)) continue;
                // Боссы и существа прогресса мира (ключ победы) не втягиваются: у бойца ключ и катсцена стираются,
                // добыча по правилу арены, очистка удаляет — настоящий босс с алтаря у арены пропал бы для мира.
                if (c.IsBoss() || !string.IsNullOrEmpty(c.m_defeatSetGlobalKey)) continue;
                var nview = c.GetComponent<ZNetView>();
                if (!nview || !nview.IsValid() || !nview.IsOwner() || !c.GetComponent<BaseAI>()) continue;
                Vector3 pos = c.transform.position;
                foreach (var s in Sessions.Values)
                {
                    if (!s.Live || s.State == ArenaState.Inactive || !s.IsInside(pos, -0.5f)) continue;
                    var policy = (IntruderPolicy)s.Rules.intruders;
                    if (policy == IntruderPolicy.Involve)
                    {
                        var u = Adopt(c, s, 0, -1, -1, ArenaUnit.Intruder);
                        if (u) Message(L.T("Чужак на арене: ") + Label(u) + L.T(" — вступает в бой."));
                    }
                    else if (policy == IntruderPolicy.Remove)
                    {
                        ZNetScene.instance.Destroy(c.gameObject);
                    }
                    break;
                }
            }
        }

        private static readonly float[] TeamSideAngles = { 90f, 270f, 0f, 180f, 45f, 225f, 135f, 315f };

        private static Vector3 GroupAnchor(ArenaSession s, SpawnGroup g)
        {
            switch ((Placement)g.placement)
            {
                case Placement.Center:
                    return s.Center;
                case Placement.CameraAim:
                    return s.ClampInside(CameraAimPoint(), 0.9f);
                case Placement.TeamSide:
                    // Сторона — по номеру команды, а не по порядку в составе: у хозяина и гостей одна и та же.
                    // 1 и 2 — друг напротив друга, 3 и 4 — поперёк, 5–8 — по диагоналям.
                    float ang = TeamSideAngles[Mathf.Clamp(g.team, 1, Hostility.TeamCount) - 1] * Mathf.Deg2Rad;
                    return s.Center + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (s.Rules.radius * 0.6f);
                default:
                    return s.Center;
            }
        }

        public static Vector3 CameraAimPoint()
        {
            var cam = Utils.GetMainCamera();
            Vector3 fallback = Current != null ? Current.Center : (Player.m_localPlayer ? Player.m_localPlayer.transform.position : Vector3.zero);
            if (!cam) return fallback;
            int mask = LayerMask.GetMask("terrain", "Default", "static_solid", "piece", "Default_small");
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 500f, mask))
                return hit.point;
            return fallback;
        }

        private static Vector3 RandomXZ(float spread)
        {
            var c = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, spread);
            return new Vector3(c.x, 0f, c.y);
        }

        private static int _groundMask;

        public static float GroundHeight(Vector3 p)
        {
            // Маска — один раз: GroundHeight зовётся каждый физический такт для каждого летуна под потолком.
            if (_groundMask == 0) _groundMask = LayerMask.GetMask("terrain", "Default", "static_solid", "piece");
            if (Physics.Raycast(new Vector3(p.x, p.y + 400f, p.z), Vector3.down, out var hit, 1000f, _groundMask))
                return hit.point.y;
            return ZoneSystem.instance ? ZoneSystem.instance.GetGroundHeight(p) : p.y;
        }

        // ================================================================== такты

        public static void Update()
        {
            if (_znetSeen != ZNet.instance || (ZNet.instance == null && Sessions.Count > 0))
            {
                _znetSeen = ZNet.instance;
                ResetWorld();
            }
            ArenaNet.Update();

            float dt = Time.unscaledDeltaTime;
            _tickHalf -= dt;
            if (_tickHalf <= 0f)
            {
                _tickHalf = 0.5f;
                HostSyncRules();
            }

            if (Local != null && Local.State == ArenaState.Countdown && ArenaSession.Now >= Local.CountdownEnd)
                BeginFighting(Local);

            _tickSecond -= dt;
            if (_tickSecond <= 0f)
            {
                _tickSecond = 1f;
                ArenaUnit.PruneDead();
                ArenaUnit.SyncWithZdo();
                SyncTimeScale();
                foreach (var u in ArenaUnit.All)
                {
                    if (!u) continue;
                    u.Refresh();
                    // Замороженному ИИ не копить «время мира»: после паузы он разом вылечился бы за всё время.
                    if (u.IsOwner && ShouldFreeze(u)) u.NView.GetZDO().Set(ZDOVars.s_worldTimeHash, ZNet.instance.GetTime().Ticks);
                    EnsureDeath(u);
                    var us = u.Session;
                    if (us != null && us.Live && u.Alive) ArenaFlight.EnsureAirborne(u, 1f);
                    if (u.AlertPending)
                    {
                        // Поднимает по тревоге только владелец — у него считается ИИ.
                        if (u.IsOwner && u.AI) u.AI.Alert();
                        u.AlertPending = false;
                    }
                }
                ClaimOwnerless();
                HandleIntruders();
                WatchEscapes();
                ForceTargets();
                FlushDamage();
                if (Local != null)
                {
                    HostRefreshStats();
                    if (Local.State == ArenaState.Fighting) CheckFinish();
                }
                var picked = PickCurrent();
                if (picked != Current)
                {
                    Current = picked;
                    ArenaBoundary.Rebuild();
                    if (picked != null && !picked.IsLocal)
                        Message(L.F("Рядом арена игрока {0} — вы гость", picked.HostName) + (picked.Rules.allowGuests ? L.T(" и можете управлять боем.") : "."));
                }
                OutsideActive = ComputeOutsideActive();
            }

            _heartbeat -= dt;
            if (_heartbeat <= 0f)
            {
                _heartbeat = 3f;
                if (Local != null && HasPeers) ArenaNet.SendState(Local);
            }

            _statsBroadcast -= dt;
            if (_statsBroadcast <= 0f)
            {
                _statsBroadcast = 2f;
                if (Local != null && Local.StatsDirty && HasPeers)
                {
                    Local.StatsDirty = false;
                    ArenaNet.SendStats(Local);
                }
            }

            _orphanTick -= dt;
            if (_orphanTick <= 0f)
            {
                _orphanTick = 3f;
                if (S.cleanupOrphans) CleanupOrphans();
            }

            PlayerPresence.Update();
            ArenaBoundary.Update();
        }

        public static void FixedUpdate()
        {
            foreach (var u in ArenaUnit.All)
            {
                if (!u || !u.Alive || !u.IsOwner) continue;
                var s = u.Session;
                if (s == null || !s.Live) continue;
                ArenaBoundary.Enforce(u.Character, s, (BoundaryMode)s.Rules.boundaryMode);
            }
            var cur = Current;
            if (cur != null && S.boundaryAffectsPlayer && Player.m_localPlayer && (BoundaryMode)cur.Rules.boundaryMode != BoundaryMode.None)
                ArenaBoundary.Enforce(Player.m_localPlayer, cur, (BoundaryMode)cur.Rules.boundaryMode);
        }

        private static void ResetWorld()
        {
            Sessions.Clear();
            Local = null;
            Current = null;
            PendingDealt.Clear();
            PendingTaken.Clear();
            TimeScale = 1f;
            _worldEnterTime = Time.unscaledTime;
            Bump();
            ArenaBoundary.Hide();
            ArenaCamera.OnArenaClosed();
            CreatureCatalog.Invalidate();
        }

        /// <summary>Хозяин: правила из своих настроек; изменились — разослать.</summary>
        private static void HostSyncRules()
        {
            if (Local == null) return;
            var r = ArenaRules.From(S);
            var json = JsonIO.ToJson(r);
            if (json == _lastRulesJson) return;
            _lastRulesJson = json;
            Local.Rules = r;
            ArenaBoundary.Rebuild();
            ChangedLocal();
        }

        /// <summary>Своя арена, иначе ближайшая живая чужая рядом с персонажем.</summary>
        private static ArenaSession PickCurrent()
        {
            if (Local != null) return Local;
            var p = Player.m_localPlayer;
            if (!p) return null;
            ArenaSession best = null;
            float bestD = float.MaxValue;
            foreach (var s in Sessions.Values)
            {
                if (s.IsLocal || !s.Live) continue;
                float d = Vector3.Distance(p.transform.position, s.Center) - s.Rules.radius;
                if (d > 200f) continue;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>
        /// Бойцы, чья арена исчезла. Свою старую арену убираем сразу; чужую — только если о ней нет вестей
        /// дольше 30 с и мы в мире не меньше 30 с (иначе удалили бы бойцов арены, чьё состояние ещё не пришло).
        /// </summary>
        private static void CleanupOrphans()
        {
            float now = Time.unscaledTime;
            long me = LocalPlayerId;
            foreach (var u in new List<ArenaUnit>(ArenaUnit.All))
            {
                if (!u || !u.IsOwner) continue;
                var s = u.Session;
                if (s != null && s.Live)
                {
                    u.OrphanSince = -1f;
                    continue;
                }
                bool myOldArena = me != 0 && u.HostPlayerId == me && (Local == null || Local.Id != u.SessionId);
                if (u.OrphanSince < 0f) u.OrphanSince = now;
                bool expired = now - u.OrphanSince > 30f && now - _worldEnterTime > 30f;
                if (!myOldArena && !expired) continue;
                // Забредших диких отпускаем в мир, выпущенных и призванных — убираем.
                if (u.Adopted == ArenaUnit.Intruder) u.Release();
                else Despawn(u);
            }
        }

        private static readonly System.Reflection.MethodInfo CheckDeathMethod =
            HarmonyLib.AccessTools.Method(typeof(Character), "CheckDeath");

        /// <summary>
        /// Существо с нулевым здоровьем, которое не умерло, игра больше не ранит (RPC_Damage выходит при HP ≤ 0):
        /// оно стоит бессмертным. Смерть проверяет владелец каждый физический такт; если этого не произошло,
        /// владелец бойца запускает ту же проверку сам и пишет в лог, кто это был.
        /// </summary>
        private static void EnsureDeath(ArenaUnit u)
        {
            var ch = u.Character;
            if (!u.IsOwner || !ch || ch.IsDead() || ch.GetHealth() > 0f || CheckDeathMethod == null) return;
            ArenaPlugin.Log.LogWarning(L.F("Боец с нулевым здоровьем не погиб, смерть запущена принудительно: {0}", Label(u)));
            try { CheckDeathMethod.Invoke(ch, null); }
            catch (Exception e) { ArenaPlugin.Log.LogWarning(e.GetBaseException().ToString()); }
        }

        private static readonly HashSet<string> OwnerlessLogged = new HashSet<string>();

        /// <summary>
        /// Боец без владельца (вышел из активной зоны всех игроков или его владелец отключился) у игры невидим и
        /// заморожен, но жив: висят метка и полоска босса, бой не кончается. Забирает его сервер мира — его сторож
        /// (<see cref="Patches.KeepArenaOwnershipPatch"/>) потом держит владение. Остальные клиенты не забирают:
        /// без сторожа сервер снова снял бы владельца через 2 с, и боец мигал бы.
        /// </summary>
        private static void ClaimOwnerless()
        {
            if (!ZNet.instance || !ZNet.instance.IsServer()) return;
            foreach (var u in ArenaUnit.All)
            {
                // Владелец отключился — игра с постоянного объекта владельца не снимает: он такой же «ничей».
                if (!u || !u.NView || !u.NView.IsValid() || Patches.KeepArenaOwnershipPatch.Connected(u.NView.GetZDO().GetOwner())) continue;
                u.NView.ClaimOwnership();
                if (OwnerlessLogged.Add(u.Key))
                    ArenaPlugin.Log.LogInfo(L.F("Боец без владельца (вне активной зоны игроков): {0} — управление взял сервер.", Label(u)));
            }
            if (OwnerlessLogged.Count > 512) OwnerlessLogged.Clear();
        }

        private static readonly Dictionary<string, float> EscapeLogged = new Dictionary<string, float>();

        /// <summary>
        /// Диагностика: боец дальше 3 м за границей. Своих бойцов стена держит каждый физический такт, поэтому
        /// такая запись — либо боец под управлением другого игрока, либо сбой; в лог пишется, кто им управляет.
        /// </summary>
        private static void WatchEscapes()
        {
            float now = Time.unscaledTime;
            foreach (var u in ArenaUnit.All)
            {
                if (!u || !u.Alive) continue;
                var s = u.Session;
                if (s == null || !s.Live || (BoundaryMode)s.Rules.boundaryMode == BoundaryMode.None) continue;
                Vector3 pos = u.transform.position;
                if (s.IsInside(pos, 3f)) continue;
                if (EscapeLogged.TryGetValue(u.Key, out var last) && now - last < 30f) continue;
                EscapeLogged[u.Key] = now;
                Vector3 d = pos - s.Center;
                d.y = 0f;
                string owner = u.IsOwner ? L.T("этот клиент") : L.T("другой клиент (владелец ") + u.NView.GetZDO().GetOwner() + ")";
                ArenaPlugin.Log.LogWarning(L.F("Вне границы: {0} — {1:0.#} м за кольцом арены ", Label(u), d.magnitude - s.Rules.radius) +
                                           L.F("{0}; управляет {1}; поведение {2}.", (s.IsLocal ? L.T("(своей)") : s.HostName), owner, u.Behavior));
            }
            if (EscapeLogged.Count > 512) EscapeLogged.Clear();
        }

        /// <summary>
        /// Режим «идёт к врагам»: цель есть всегда — ближайший враждебный боец той же арены или игрок-участник,
        /// и её место известно всегда (иначе существо дошло бы до места, где видело врага, и встало).
        /// Каждый клиент — для бойцов, которыми владеет.
        /// </summary>
        private static void ForceTargets()
        {
            foreach (var s in Sessions.Values)
            {
                if (!s.Live || s.State != ArenaState.Fighting) continue;
                var units = SessionUnits(s, true);
                foreach (var u in units)
                {
                    var mai = u.Monster;
                    if (!mai || !u.IsOwner || u.Character.m_aiCannotTargetOthers || u.Behavior != ArenaBehavior.Hunt) continue;
                    var cur = Reflect.TargetCreature(mai);
                    if (cur && !cur.IsDead() && BaseAI.IsEnemy(u.Character, cur))
                    {
                        Reflect.LastKnownTargetPos(mai) = cur.transform.position;
                        Reflect.BeenAtLastPos(mai) = false;
                        Reflect.TimeSinceSensedTarget(mai) = 0f;
                        if (!mai.IsAlerted()) Reflect.SetAlerted(mai, true);
                        continue;
                    }

                    Character best = null;
                    float bestD = float.MaxValue;
                    Vector3 pos = u.transform.position;
                    foreach (var v in units)
                    {
                        if (v == u || v.Character.m_aiSkipTarget) continue;
                        if (!BaseAI.IsEnemy(u.Character, v.Character)) continue;
                        float d = (v.transform.position - pos).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = v.Character; }
                    }
                    foreach (var p in Player.GetAllPlayers())
                    {
                        if (!p || p.IsDead() || p.InGhostMode() || p.InDebugFlyMode() || !s.IsInside(p.transform.position, 5f)) continue;
                        if (!BaseAI.IsEnemy(u.Character, p)) continue;
                        float d = (p.transform.position - pos).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = p; }
                    }
                    if (!best) continue;
                    Reflect.TargetCreature(mai) = best;
                    Reflect.TargetStatic(mai) = null;
                    Reflect.LastKnownTargetPos(mai) = best.transform.position;
                    Reflect.BeenAtLastPos(mai) = false;
                    Reflect.TimeSinceSensedTarget(mai) = 0f;
                    Reflect.TimeSinceAttacking(mai) = 0f;
                    if (!mai.IsAlerted()) Reflect.SetAlerted(mai, true);
                }
            }
        }

        public static bool ShouldFreeze(ArenaUnit u)
        {
            var s = u.Session;
            if (s == null || !s.Live) return false;
            switch (s.State)
            {
                case ArenaState.Preparing:
                case ArenaState.Countdown:
                    return s.Rules.freezeBeforeStart;
                case ArenaState.Paused:
                    return true;
                case ArenaState.Finished:
                    return s.Rules.freezeAfterFinish;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Бойцы вне активной зоны вокруг персонажа (зависит от дальности симуляции) игра не обновляет и выгружает;
        /// у хозяина это ещё и ломает подсчёт итога — предупреждаем, если граница арены туда заходит.
        /// </summary>
        private static float ComputeOutsideActive()
        {
            var s = Current;
            if (s == null || !ZNet.instance || !ZoneSystem.instance || !ZNetScene.instance) return 0f;
            Vector3 refPos = ZNet.instance.GetReferencePosition();
            const int n = 32;
            int outside = 0;
            for (int i = 0; i < n; i++)
                if (!ZNetScene.InActiveArea(s.PerimeterPoint(i / (float)n), refPos)) outside++;
            return outside / (float)n;
        }

        // ================================================================== итог и статистика

        public static List<ArenaUnit> SessionUnits(bool aliveOnly) => SessionUnits(Current, aliveOnly);

        public static List<ArenaUnit> SessionUnits(ArenaSession s, bool aliveOnly)
        {
            var list = new List<ArenaUnit>();
            if (s == null) return list;
            foreach (var u in ArenaUnit.All)
            {
                if (!u || u.SessionId != s.Id) continue;
                if (aliveOnly && !u.Alive) continue;
                list.Add(u);
            }
            return list;
        }

        /// <summary>
        /// Есть ли пара, где один может напасть на другого. Нападать умеет только ИИ монстров: ИИ животных (олень,
        /// заяц) лишь убегает, m_aiCannotTargetOthers не выбирает цель; m_aiSkipTarget целью не становится.
        /// Иначе «олень против зайца» не кончился бы никогда.
        /// </summary>
        private static bool HasHostilePair(List<ArenaUnit> alive)
        {
            for (int i = 0; i < alive.Count; i++)
                for (int j = i + 1; j < alive.Count; j++)
                    if (CanFight(alive[i], alive[j]) || CanFight(alive[j], alive[i]))
                        return true;
            return false;
        }

        private static bool CanFight(ArenaUnit a, ArenaUnit b) =>
            a.Monster && a.Character && !a.Character.m_aiCannotTargetOthers &&
            b.Character && !b.Character.m_aiSkipTarget && Hostility.UnitsHostile(a, b);

        /// <summary>Хозяин: итог, когда враждующих сторон не осталось (по бойцам, видимым у хозяина).</summary>
        private static void CheckFinish()
        {
            var s = Local;
            var alive = SessionUnits(s, true);
            if (HasHostilePair(alive)) return;

            s.State = ArenaState.Finished;
            s.FightEnd = ArenaSession.Now;
            string text;
            if (alive.Count == 0)
            {
                text = L.T("Ничья: не выжил никто.");
            }
            else
            {
                var names = new List<string>();
                var seen = new HashSet<int>();
                foreach (var u in alive)
                {
                    int side = Hostility.SideOf(u);
                    if (seen.Add(side)) names.Add(Hostility.SideName(side, Label(u)));
                }
                text = (names.Count == 1 ? L.T("Победа: ") : L.T("Победители: ")) + string.Join(", ", names) + L.F(" — выжило {0}.", alive.Count);
            }
            text += L.F(" Бой длился {0:0} с.", s.FightSeconds);
            s.Result = text;
            ChangedLocal();
            s.StatsDirty = true;
            Message(text);
        }

        /// <summary>Хозяин: строки статистики по видимым бойцам (подписи, стороны, живы ли).</summary>
        private static void HostRefreshStats()
        {
            var s = Local;
            bool changed = false;
            foreach (var u in SessionUnits(s, false))
            {
                var st = s.Stat(u.Key);
                string label = Label(u);
                int faction = (int)u.Character.GetFaction();
                if (st.label != label || st.team != u.Team || st.faction != faction)
                {
                    st.label = label;
                    st.color = Hostility.UnitColor(u);
                    st.team = u.Team;
                    st.faction = faction;
                    changed = true;
                }
                if (FillSide(s, st, label)) changed = true;
                if (u.Alive && !st.alive && st.deathTime <= 0) { st.alive = true; changed = true; }
            }
            if (changed) s.StatsDirty = true;
            s.RebuildSides();
        }

        private static bool FillSide(ArenaSession s, UnitStat st, string label)
        {
            var mode = (HostilityMode)s.Rules.hostilityMode;
            int serial = 0;
            int hash = st.key.LastIndexOf(':');
            if (hash >= 0) int.TryParse(st.key.Substring(hash + 1), out serial);
            int side = Hostility.SideOf(mode, st.team, st.faction, serial % 10000);
            if (side == st.side && !string.IsNullOrEmpty(st.sideName)) return false;
            st.side = side;
            st.sideName = Hostility.SideName(side, label);
            st.sideColor = Hostility.SideColor(side, st.team);
            return true;
        }

        /// <summary>Урон считается у владельца пострадавшего — копим и раз в секунду отправляем хозяину арены.</summary>
        public static void OnDamage(Character victim, Character attacker, float amount)
        {
            if (amount <= 0f) return;
            var v = ArenaUnit.Get(victim);
            if (v) Add(PendingTaken, v.SessionId, v.Key, amount);
            var a = ArenaUnit.Get(attacker);
            if (a) Add(PendingDealt, a.SessionId, a.Key, amount);
        }

        private static void Add(Dictionary<string, Dictionary<string, float>> map, string session, string key, float amount)
        {
            if (string.IsNullOrEmpty(session) || string.IsNullOrEmpty(key)) return;
            if (!map.TryGetValue(session, out var d)) map[session] = d = new Dictionary<string, float>();
            d.TryGetValue(key, out var cur);
            d[key] = cur + amount;
        }

        private static void FlushDamage()
        {
            if (PendingDealt.Count == 0 && PendingTaken.Count == 0) return;
            var ids = new HashSet<string>(PendingDealt.Keys);
            ids.UnionWith(PendingTaken.Keys);
            foreach (var id in ids)
            {
                var s = FindSession(id);
                PendingDealt.TryGetValue(id, out var dealt);
                PendingTaken.TryGetValue(id, out var taken);
                dealt = dealt ?? new Dictionary<string, float>();
                taken = taken ?? new Dictionary<string, float>();
                if (s == null) continue;
                if (s.IsLocal) OnDamageReport(id, dealt, taken);
                else ArenaNet.SendDamage(s, dealt, taken);
            }
            PendingDealt.Clear();
            PendingTaken.Clear();
        }

        private static void DropPending(string sessionId)
        {
            PendingDealt.Remove(sessionId);
            PendingTaken.Remove(sessionId);
        }

        /// <summary>Гибель считается у владельца погибшего — рассылаем событие всем (лента, итоги у хозяина).</summary>
        public static void OnDeath(Character victim, Character killer)
        {
            var v = ArenaUnit.Get(victim);
            if (!v || v.Session == null) return;
            FlushDamage();
            var k = ArenaUnit.Get(killer);
            string killerName;
            Color killerColor;
            if (k) { killerName = Label(k); killerColor = Hostility.UnitColor(k); }
            else if (killer && killer.IsPlayer()) { killerName = ((Player)killer).GetPlayerName(); killerColor = Color.white; }
            else if (killer) { killerName = ProfileLogic.Loc(killer.m_name); killerColor = Color.gray; }
            else { killerName = L.T("стихия"); killerColor = Color.gray; }
            ArenaNet.SendKill(new KillEvent
            {
                session = v.SessionId,
                victimKey = v.Key,
                victim = Label(v),
                victimColor = Hostility.UnitColor(v),
                victimTeam = v.Team,
                victimFaction = (int)v.Character.GetFaction(),
                killerKey = k ? k.Key : "",
                killer = killerName,
                killerColor = killerColor
            });
        }

        public static void OnUnitRemoved(ArenaUnit u)
        {
            ArenaCamera.OnUnitRemoved(u);
        }

        public static string Label(ArenaUnit u)
        {
            string stars = u.Stars <= 0 ? "" : u.Stars <= 5 ? " " + new string('★', u.Stars) : " " + u.Stars + "★";
            string kind = u.Adopted == ArenaUnit.Summoned ? L.T(" (призван)") : u.Adopted == ArenaUnit.Intruder ? L.T(" (чужак)") : "";
            return u.DisplayName + stars + " #" + u.SerialNumber + kind;
        }

        // ================================================================== время

        public static bool CanControlTime()
        {
            var z = ZNet.instance;
            return z && z.IsServer() && z.GetPeerConnections() == 0;
        }

        public static void SetTimeScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0f, 4f);
            if (Math.Abs(scale - 1f) > 0.001f && !CanControlTime())
            {
                if (TimeScale != 1f) { TimeScale = 1f; if (Game.instance) Game.FadeTimeScale(1f); }
                Message(L.T("Управление временем доступно только в одиночной игре."));
                return;
            }
            TimeScale = scale;
            if (Game.instance) Game.FadeTimeScale(scale);
        }

        /// <summary>Игра сняла паузу и поставила ×1 — вернуть ход времени арены (или признать ×1, если менять нельзя).</summary>
        public static void OnGameUnpaused()
        {
            if (Mathf.Abs(TimeScale - 1f) < 0.001f) return;
            if (CanControlTime() && Game.instance) Game.FadeTimeScale(TimeScale);
            else TimeScale = 1f;
        }

        /// <summary>Подключился игрок — игра сама идёт на ×1; показываем ×1 и не возвращаемся к остановке, когда он уйдёт.</summary>
        private static void SyncTimeScale()
        {
            if (Mathf.Abs(TimeScale - 1f) < 0.001f || CanControlTime()) return;
            TimeScale = 1f;
            if (Game.instance) Game.FadeTimeScale(1f);
        }

        public static void StepTimeScale(int dir)
        {
            float[] steps = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f };
            int idx = 0;
            float best = float.MaxValue;
            for (int i = 0; i < steps.Length; i++)
            {
                float d = Mathf.Abs(steps[i] - TimeScale);
                if (d < best) { best = d; idx = i; }
            }
            idx = Mathf.Clamp(idx + dir, 0, steps.Length - 1);
            SetTimeScale(steps[idx]);
        }

        public static void Message(string text)
        {
            // Сообщения арены видны и при скрытом интерфейсе игры: они относятся к арене, а не к игре.
            if (MessageHud.instance) MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text, 0, null, showDespiteHiddenHUD: true);
            ArenaPlugin.Log.LogInfo(text);
        }
    }
}
