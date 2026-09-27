using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using ValheimArena.Core;
using ValheimArena.Profiles;

namespace ValheimArena.UI
{
    public static class MainWindow
    {
        private static int _tab;
        private static string[] Tabs => new[] { L.T("Арена"), L.T("Бойцы"), L.T("Столкновения"), L.T("Камера"), L.T("Персонаж"), L.T("Итоги") };
        private static Vector2 _scroll;
        private static Vector2 _listScroll;
        private static Vector2 _rosterScroll;

        private static SpawnGroup _draft = new SpawnGroup();
        private static int _editingIndex = -1;
        private static string _search = "";
        private static int _category;
        private static int _factionFilter;
        private static int _filterFaction = -1;
        private static string[] Categories => new[] { L.T("Все"), L.T("Боссы"), L.T("Летающие"), L.T("Водные") };

        private static string _scenarioName = "";
        private static bool _bossReciprocal = true;

        private static ArenaSettings S => ArenaSettingsStore.Current;

        public static void Draw()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Tabs.Length; i++)
                if (GuiKit.Tab(Tabs[i], _tab == i)) { _tab = i; UiInput.Unfocus(); }
            GUILayout.FlexibleSpace();
            LanguageButtons();
            GUILayout.Space(6);
            if (GuiKit.Btn("✕", GUILayout.Width(30))) ArenaUI.CloseMenu();
            GUILayout.EndHorizontal();

            StatusLine();

            _scroll = GuiKit.BeginScroll(_scroll);
            switch (_tab)
            {
                case 0: TabArena(); break;
                case 1: TabFighters(); break;
                case 2: TabHostility(); break;
                case 3: TabCamera(); break;
                case 4: TabPlayer(); break;
                case 5: TabResults(); break;
            }
            GuiKit.EndScroll();

            GUILayout.Label(L.F("{0} меню · {1} редактор · {2} камера · ", ArenaPlugin.KeyMenu.Value, ArenaPlugin.KeyEditor.Value, ArenaPlugin.KeyCamera.Value) +
                            L.F("{0} бой/пауза · {1}/{2} цели · ", ArenaPlugin.KeyFight.Value, ArenaPlugin.KeyPrevTarget.Value, ArenaPlugin.KeyNextTarget.Value) +
                            L.F("{0}/{1} время · {2} таблички · {3} оверлей · {4} интерфейс игры · ПКМ — обзор при открытом меню", ArenaPlugin.KeySlower.Value,
                                ArenaPlugin.KeyFaster.Value, ArenaPlugin.KeyTags.Value, ArenaPlugin.KeyOverlay.Value, ArenaPlugin.KeyGameUi.Value), GuiKit.Muted);
        }

        private static void LanguageButtons()
        {
            var cur = ArenaPlugin.Language.Value;
            if (GuiKit.BtnOn("EN", cur == UiLanguage.English, GUILayout.Width(40))) ArenaUI.RequestLanguage(UiLanguage.English);
            if (GuiKit.BtnOn("RU", cur == UiLanguage.Russian, GUILayout.Width(40))) ArenaUI.RequestLanguage(UiLanguage.Russian);
            if (GuiKit.BtnOn(L.T("Авто"), cur == UiLanguage.Auto, GUILayout.Width(54))) ArenaUI.RequestLanguage(UiLanguage.Auto);
        }

        private static ArenaSettings _guestView;
        private static string _guestViewKey = "";

        private static ArenaSettings R
        {
            get
            {
                var cur = Arena.Current;
                if (Arena.CanEditRules || cur == null) return S;
                string key = cur.Id + "/" + cur.Revision;
                if (_guestView == null || key != _guestViewKey)
                {
                    _guestView = S.Clone();
                    cur.Rules.CopyTo(_guestView);
                    _guestViewKey = key;
                }
                return _guestView;
            }
        }

        private static void StatusLine()
        {
            var cur = Arena.Current;
            GUILayout.BeginHorizontal(GuiKit.Box);
            string state = GuiKit.Colored(Arena.StateName, GuiKit.Accent);
            GUILayout.Label(L.T("Арена: ") + state, GuiKit.Label);
            if (cur != null)
            {
                GUILayout.Label(L.F("  бойцов: {0}", Arena.SessionUnits(true).Count), GuiKit.Label);
                if (cur.State == ArenaState.Fighting || cur.State == ArenaState.Paused)
                    GUILayout.Label(L.T("  время: ") + GuiKit.FmtTime(cur.FightSeconds), GuiKit.Label);
                if (Mathf.Abs(Arena.TimeScale - 1f) > 0.001f)
                    GUILayout.Label("  ×" + GuiKit.Fmt(Arena.TimeScale), GuiKit.Label);
                GUILayout.Label(cur.IsLocal
                    ? (Arena.HasPeers ? GuiKit.Colored(L.T("  вы хозяин"), new Color(0.6f, 0.9f, 0.6f)) : "")
                    : GuiKit.Colored(L.T("  хозяин: ") + cur.HostName, new Color(0.6f, 0.85f, 1f)), GuiKit.Label);
            }
            if (cur != null && Arena.OutsideActive > 0f)
                GUILayout.Label(GuiKit.Colored(L.T("  ⚠ граница вне зоны прогрузки"), new Color(1f, 0.45f, 0.35f)), GuiKit.Label);
            GUILayout.FlexibleSpace();
            GUILayout.Label(L.T("Камера: ") + ArenaCamera.ModeName(ArenaCamera.Mode), GuiKit.Label);
            GUILayout.EndHorizontal();

            if (cur != null)
            {
                GUILayout.BeginHorizontal();
                string fightLabel;
                switch (cur.State)
                {
                    case ArenaState.Fighting: fightLabel = L.T("⏸ Пауза"); break;
                    case ArenaState.Paused: fightLabel = L.T("▶ Продолжить"); break;
                    case ArenaState.Countdown: fightLabel = L.T("Отсчёт…"); break;
                    case ArenaState.Finished: fightLabel = L.T("▶ Бой заново (выжившие)"); break;
                    default: fightLabel = L.T("▶ Начать бой"); break;
                }
                GUI.enabled = Arena.CanControl;
                if (GuiKit.Press(fightLabel, cur.State == ArenaState.Fighting ? GuiKit.ButtonOn : GuiKit.Button, "fight", GUILayout.Height(28)))
                    Arena.ToggleFight();
                if (GuiKit.Btn(L.T("Выпустить состав"), GUILayout.Height(28))) Arena.SpawnRoster();
                if (GuiKit.Btn(L.T("Очистить арену"), GUILayout.Height(28))) Arena.ClearUnits();
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                if (!Arena.CanControl)
                    GUILayout.Label(L.T("Хозяин арены не разрешил гостям управлять боем — можно смотреть и участвовать персонажем."), GuiKit.Muted);
            }
        }

        private static void TabArena()
        {
            var cur = Arena.Current;
            GUILayout.Label(L.T("Площадка"), GuiKit.Header);
            if (cur == null)
            {
                GUILayout.Label(L.T("Арена разворачивается вокруг точки включения; границы задаются от неё. ") +
                                L.T("В сетевой игре её правила и ход боя получают все игроки с модом."), GuiKit.Muted);
                GUILayout.BeginHorizontal();
                if (GuiKit.Btn(L.T("Развернуть арену здесь"), GUILayout.Height(30)) && Player.m_localPlayer)
                    Arena.Activate(Player.m_localPlayer.transform.position);
                if (GuiKit.Btn(L.T("…в точке прицела камеры"), GUILayout.Height(30)))
                    Arena.Activate(Arena.CameraAimPoint());
                GUILayout.EndHorizontal();
            }
            else if (!cur.IsLocal)
            {
                GUILayout.Label(GuiKit.Colored(L.F("Арена игрока {0}. Вы гость: правила и границы задаёт хозяин", cur.HostName) +
                                               (cur.Rules.allowGuests ? L.T(", управлять боем и выпускать бойцов можно.") : L.T(", управление боем закрыто.")),
                                               new Color(0.6f, 0.85f, 1f)), GuiKit.Label);
                var c = cur.Center;
                GUILayout.Label(L.F("Центр: X {0:0}  Y {1:0}  Z {2:0}", c.x, c.y, c.z), GuiKit.Label);
                if (GuiKit.Btn(L.T("Развернуть свою арену здесь")) && Player.m_localPlayer)
                    Arena.Activate(Player.m_localPlayer.transform.position);
            }
            else
            {
                var c = cur.Center;
                GUILayout.Label(L.F("Центр: X {0:0}  Y {1:0}  Z {2:0}", c.x, c.y, c.z), GuiKit.Label);
                GUILayout.BeginHorizontal();
                if (GuiKit.Btn(L.T("Центр — к персонажу")) && Player.m_localPlayer) Arena.MoveCenter(Player.m_localPlayer.transform.position);
                if (GuiKit.Btn(L.T("Центр — в прицел камеры"))) Arena.MoveCenter(Arena.CameraAimPoint());
                if (GuiKit.Btn(L.T("Свернуть арену"))) Arena.Deactivate(true);
                GUILayout.EndHorizontal();
                S.allowGuests = GuiKit.Toggle(L.T("Гости могут управлять боем и выпускать бойцов"), S.allowGuests,
                    L.T("Сетевая игра: старт, пауза, очистка и выпуск своих бойцов для других игроков с модом. Правила меняет только хозяин."));
            }

            bool edit = Arena.CanEditRules;
            var R = MainWindow.R;

            GUILayout.Space(4);
            GUILayout.Label(L.T("Границы") + (edit ? "" : L.T(" (правила хозяина)")), GuiKit.SubHeader);
            GUI.enabled = edit;
            float r = GuiKit.Slider(L.T("Радиус / полуширина"), R.radius, 5f, 150f, "radius", "0", suffix: L.T("м"));
            if (edit && !Mathf.Approximately(r, S.radius)) { S.radius = Mathf.Clamp(r, 5f, 300f); ArenaBoundary.Rebuild(); }
            int shape = GuiKit.Choice(L.T("Форма"), R.shape, new[] { L.T("Круг"), L.T("Квадрат") });
            if (edit && shape != S.shape) { S.shape = shape; ArenaBoundary.Rebuild(); }
            int bmode = GuiKit.Choice(L.T("Выход за границу"), R.boundaryMode, new[] { L.T("Свободно"), L.T("Стена"), L.T("Возврат к центру") });
            if (edit) S.boundaryMode = bmode;
            float ceil = GuiKit.Slider(L.T("Потолок для летающих"), R.heightLimit, 0f, 60f, "ceil", "0", suffix: L.T("м"));
            if (edit) S.heightLimit = ceil;
            GUILayout.Label(L.T("0 — без потолка."), GuiKit.Muted);
            float fly = GuiKit.Slider(L.T("Высота полёта летунов"), R.flyHeight, 0f, 40f, "flyheight", "0", suffix: L.T("м"));
            if (edit) S.flyHeight = fly;
            GUILayout.Label(L.T("Крейсерская высота над землёй в режиме «идёт к врагам»; у цели летун держит высоту, ") +
                            L.T("с которой достаёт его атака (стрелки — выше, кусачие пикируют). 0 — по существу и его атакам."), GuiKit.Muted);
            GUI.enabled = true;
            S.showBoundary = GuiKit.Toggle(L.T("Показывать границу"), S.showBoundary);
            S.boundaryAffectsPlayer = GuiKit.Toggle(L.T("Граница держит и моего персонажа"), S.boundaryAffectsPlayer);
            if (Arena.OutsideActive > 0f)
                GUILayout.Label(GuiKit.Colored(L.F("Внимание: {0:0}% границы вне зоны, которую игра обновляет вокруг персонажа. ", Arena.OutsideActive * 100f) +
                                               L.T("Существа там замрут или исчезнут — держите персонажа ближе к центру или уменьшите радиус.") +
                                               (cur != null && cur.IsLocal && Arena.HasPeers ? L.T(" Итог боя считается по бойцам, видимым у хозяина.") : ""),
                                               new Color(1f, 0.45f, 0.35f)), GuiKit.Muted);

            GUILayout.Space(4);
            DrawCleanup();

            GUILayout.Space(4);
            GUILayout.Label(L.T("Ход боя"), GuiKit.SubHeader);
            GUI.enabled = edit;
            float cd = GuiKit.Slider(L.T("Отсчёт перед боем"), R.countdown, 0f, 10f, "countdown", "0.#", suffix: L.T("с"));
            if (edit) S.countdown = cd;
            bool fbs = GuiKit.Toggle(L.T("Бойцы ждут команды «Начать бой»"), R.freezeBeforeStart);
            bool faf = GuiKit.Toggle(L.T("Замораживать выживших после итога"), R.freezeAfterFinish);
            if (edit) { S.freezeBeforeStart = fbs; S.freezeAfterFinish = faf; }
            GUI.enabled = true;
            S.autoStart = GuiKit.Toggle(L.T("Начинать бой сразу после «Выпустить состав»"), S.autoStart);

            GUILayout.Space(4);
            GUILayout.Label(L.T("Поведение бойцов") + (edit ? "" : L.T(" (правила хозяина)")), GuiKit.SubHeader);
            GUI.enabled = edit;
            int beh = GuiKit.Choice(L.T("Поведение в бою"), R.behavior, BehaviorNames, 130f);
            GUILayout.Label(BehaviorHints[Mathf.Clamp(R.behavior, 0, 2)], GuiKit.Muted);
            int intr = GuiKit.Choice(L.T("Посторонние на арене"), R.intruders, new[] { L.T("Не замечать"), L.T("Вовлекать в бой"), L.T("Убирать") }, 130f);
            GUILayout.Label(IntruderHints[Mathf.Clamp(R.intruders, 0, 2)], GuiKit.Muted);
            GUILayout.Label(L.T("Призванные бойцами существа (слуги боссов, порождения атак) всегда встают на сторону призвавшего."), GuiKit.Muted);
            bool nf = GuiKit.Toggle(L.T("Не убегать при ранении"), R.noFlee);
            bool nb = GuiKit.Toggle(L.T("Не ломать постройки"), R.noBuildingAttacks);
            bool ws = GuiKit.Toggle(L.T("Будить спящих при появлении"), R.wakeSleepers);
            bool dl = GuiKit.Toggle(L.T("Оставлять добычу"), R.dropLoot,
                L.T("Выключено — ни добычи, ни ресурсов из погибших (статуи-жилы гаммельтроллей). Включено — трофеи боссов, положенные на камни, двигают прогресс мира."));
            if (edit)
            {
                S.behavior = beh; S.intruders = intr; S.noFlee = nf; S.noBuildingAttacks = nb; S.wakeSleepers = ws; S.dropLoot = dl;
            }
            GUI.enabled = true;
            S.cleanupOrphans = GuiKit.Toggle(L.T("Убирать бойцов исчезнувших арен"), S.cleanupOrphans,
                L.T("Своей прошлой арены — сразу; чужой — если о ней нет вестей дольше 30 с (хозяин вышел или свернул арену)."));
            GUILayout.Label(L.T("Боссы на арене не засчитываются как побеждённые: ключи мира и катсцены у них отключены."), GuiKit.Muted);

            GUILayout.Space(4);
            GUILayout.Label(L.T("Время (только одиночная игра)"), GuiKit.SubHeader);
            GUI.enabled = Arena.CanControlTime();
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.T("Скорость: ×") + GuiKit.Fmt(Arena.TimeScale), GuiKit.Label, GUILayout.Width(170));
            foreach (var v in new[] { 0f, 0.1f, 0.25f, 0.5f, 1f, 2f })
                if (GuiKit.BtnOn(v == 0f ? L.T("стоп") : "×" + GuiKit.Fmt(v), Mathf.Approximately(Arena.TimeScale, v))) Arena.SetTimeScale(v);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (!Arena.CanControlTime())
                GUILayout.Label(L.T("Пока в мире есть другие игроки, игра не даёт менять ход времени."), GuiKit.Muted);

            GUILayout.Space(6);
            GUILayout.Label(L.T("Сценарии: состав и правила арены (персонаж, камера и оверлей не меняются)"), GuiKit.SubHeader);
            GUILayout.BeginHorizontal();
            _scenarioName = GuiKit.TextField("", _scenarioName, "scenario_name");
            if (GuiKit.Btn(L.T("Сохранить"), GUILayout.Width(100)) && !string.IsNullOrWhiteSpace(_scenarioName))
            {
                ArenaSettingsStore.SaveScenario(_scenarioName);
                Arena.Message(L.T("Сценарий сохранён: ") + _scenarioName);
            }
            GUILayout.EndHorizontal();
            foreach (var name in ArenaSettingsStore.ListScenarios())
            {
                GUILayout.BeginHorizontal(GuiKit.Row);
                UiInput.PushScope(name.GetHashCode());
                GUILayout.Label(name, GuiKit.Label);
                GUILayout.FlexibleSpace();
                if (GuiKit.Btn(L.T("Загрузить"), GUILayout.Width(90)) && ArenaSettingsStore.LoadScenario(name))
                {
                    _editingIndex = -1;
                    GuiKit.ResetBuffers();
                    ArenaBoundary.Rebuild();
                    Arena.Message(L.T("Сценарий загружен: ") + name + (Arena.CanEditRules ? "" : L.T(" (правила действуют на вашей арене, на чужой — правила хозяина)")));
                }
                if (GuiKit.Btn(L.T("Удалить"), GUILayout.Width(80))) ArenaSettingsStore.DeleteScenario(name);
                UiInput.PopScope();
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawCleanup()
        {
            GUILayout.Label(L.T("Очистка арены"), GuiKit.SubHeader);
            GUILayout.BeginHorizontal();
            var cur = ArenaCleanup.FromSettings(S);
            if (GuiKit.BtnOn(L.T("Полностью"), cur == ClearWhat.All)) ArenaCleanup.ToSettings(S, ClearWhat.All);
            if (GuiKit.BtnOn(L.T("Только существа"), cur == ClearWhat.Creatures)) ArenaCleanup.ToSettings(S, ClearWhat.Creatures);
            GUILayout.FlexibleSpace();
            GUI.enabled = Arena.CanControl;
            if (GuiKit.Btn(L.T("Очистить сейчас"))) Arena.ClearUnits();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            S.clearCreatures = CleanupRow(L.T("Существа"), L.T("бойцы арены (выпущенные, призванные, вовлечённые чужаки) и трупы"), S.clearCreatures);
            S.clearItems = CleanupRow(L.T("Ресурсы"), L.T("добыча и любые предметы, лежащие на земле"), S.clearItems);
            S.clearTombstones = CleanupRow(L.T("Надгробия"), L.T("надгробия игроков — вместе с вещами в них!"), S.clearTombstones);
            S.clearPetrified = CleanupRow(L.T("Окаменевшие гаммельтролли"), L.T("каменные статуи павших гаммельтроллей (золотые жилы) и их осколки"), S.clearPetrified);
            S.clearTrees = CleanupRow(L.T("Упавшие деревья"), L.T("стволы после рубки и ударов (пни и растущие деревья не трогаются)"), S.clearTrees);
            GUILayout.Label(L.T("Действует на кнопку «Очистить арену» и на сворачивание арены (существа при сворачивании убираются всегда). ") +
                            L.T("Бойцы арены — где бы ни были, остальное — только внутри границы; дикие существа и постройки не трогаются."), GuiKit.Muted);
        }

        private static bool CleanupRow(string name, string hint, bool clear)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GuiKit.Label, GUILayout.Width(200));
            if (GuiKit.BtnOn(L.T("убрать"), clear, GUILayout.Width(80))) clear = true;
            if (GuiKit.BtnOn(L.T("оставить"), !clear, GUILayout.Width(80))) clear = false;
            GUILayout.Label(hint, GuiKit.Muted);
            GUILayout.EndHorizontal();
            return clear;
        }

        private static void TabFighters()
        {
            bool canSpawn = Arena.CanSpawn(out var spawnWhy);
            if (!canSpawn)
                GUILayout.Label(GuiKit.Colored(Arena.Active ? spawnWhy : L.T("Арена не развёрнута — состав можно собрать, выпустить — после «Развернуть арену»."), GuiKit.Accent), GuiKit.Small);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(300));
            GUILayout.Label(L.T("Существа"), GuiKit.SubHeader);
            _search = GuiKit.TextField(L.T("Поиск"), _search, "catalog_search", 60f);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Categories.Length; i++)
                if (GuiKit.BtnOn(Categories[i], _category == i)) _category = i;
            GUILayout.EndHorizontal();
            var factions = CreatureCatalog.PresentFactions;
            var factionNames = new List<string> { L.T("Все фракции") };
            foreach (var f in factions) factionNames.Add(Hostility.FactionName((int)f));
            _factionFilter = GuiKit.Cycle("", Mathf.Clamp(_factionFilter, 0, factionNames.Count - 1), factionNames, 0f, 230f);
            _filterFaction = _factionFilter > 0 && _factionFilter <= factions.Count ? (int)factions[_factionFilter - 1] : -1;
            _listScroll = GuiKit.BeginScroll(_listScroll, GUILayout.Height(330));
            string q = (_search ?? "").Trim().ToLowerInvariant();
            int row = 0;
            foreach (var e in CreatureCatalog.All)
            {
                if (!PassCategory(e)) continue;
                if (q.Length > 0 && !e.SearchKey.Contains(q)) continue;
                bool sel = _draft.prefab == e.Prefab;
                string text = (e.Boss ? GuiKit.Colored("♛ ", GuiKit.Accent) : "") + e.Display + GuiKit.Colored("  " + e.Prefab, GuiKit.MutedColor);
                if (GuiKit.Press(text, sel ? GuiKit.ButtonOn : (row++ % 2 == 0 ? GuiKit.Row : GuiKit.RowAlt)))
                {
                    if (_draft.prefab != e.Prefab) _draft.profile = "";
                    _draft.prefab = e.Prefab;
                }
            }
            if (CreatureCatalog.All.Count == 0) GUILayout.Label(L.T("Каталог появится после входа в мир."), GuiKit.Muted);
            GuiKit.EndScroll();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GuiKit.Box);
            GUILayout.Label(_editingIndex >= 0 ? L.T("Изменение группы #") + (_editingIndex + 1) : L.T("Новая группа"), GuiKit.SubHeader);
            var entry = CreatureCatalog.Get(_draft.prefab);
            if (entry == null)
            {
                GUILayout.Label(L.T("Выберите существо в списке слева."), GuiKit.Muted);
            }
            else
            {
                GUILayout.Label(GuiKit.Colored(entry.Display, Color.white) + GuiKit.Colored($"  ({entry.Prefab}, HP {entry.Health:0}, {Hostility.FactionName((int)entry.Faction)})", GuiKit.MutedColor), GuiKit.Label);
                GUILayout.Label(VanillaSpawns.Describe(entry.Spawn), GuiKit.Muted);
                if (entry.Spawn != null && GuiKit.Btn(L.T("Как в игре: стая и звёзды"))) VanillaSpawns.ApplyTo(_draft, entry.Spawn);
                DrawGroupEditor(_draft, 110f);
                GUILayout.BeginHorizontal();
                if (_editingIndex >= 0 && _editingIndex < S.roster.Count)
                {
                    if (GuiKit.Btn(L.T("Сохранить группу"))) { S.roster[_editingIndex] = _draft.Clone(); _editingIndex = -1; }
                    if (GuiKit.Btn(L.T("Отмена"))) _editingIndex = -1;
                }
                else
                {
                    if (GuiKit.Btn(L.T("Добавить в состав"))) AddDraft(false);
                    GUI.enabled = canSpawn;
                    if (GuiKit.Btn(L.T("Выпустить сразу"))) AddDraft(true);
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.T("Состав арены"), GuiKit.Header);
            GUILayout.FlexibleSpace();
            GUI.enabled = canSpawn && S.roster.Count > 0;
            if (GuiKit.Btn(L.T("Выпустить весь состав"))) Arena.SpawnRoster();
            GUI.enabled = true;
            if (GuiKit.Btn(L.T("Очистить состав"))) { S.roster.Clear(); _editingIndex = -1; }
            GUILayout.EndHorizontal();

            _rosterScroll = GuiKit.BeginScroll(_rosterScroll, GUILayout.MinHeight(160));
            if (S.roster.Count == 0) GUILayout.Label(L.T("Пусто. Выберите существо, настройте группу и нажмите «Добавить в состав»."), GuiKit.Muted);
            for (int i = 0; i < S.roster.Count; i++)
            {
                var g = S.roster[i];
                GUILayout.BeginHorizontal(i % 2 == 0 ? GuiKit.Row : GuiKit.RowAlt);
                var old = GUI.color;
                GUI.color = Hostility.TeamColor(g.team);
                GUILayout.Label("■", GuiKit.Label, GUILayout.Width(14));
                GUI.color = old;
                string stars = g.starsMax == g.starsMin ? StarText(g.starsMin) : StarText(g.starsMin) + "–" + StarText(g.starsMax);
                string prof = string.IsNullOrEmpty(g.profile) ? "" : GuiKit.Colored("  [" + g.profile + "]", new Color(0.6f, 0.85f, 1f));
                string fac = g.factionOverride >= 0 ? GuiKit.Colored("  " + Hostility.FactionName(g.factionOverride), GuiKit.MutedColor) : "";
                if (g.behavior >= 0) fac += GuiKit.Colored("  " + BehaviorNames[Mathf.Clamp(g.behavior, 0, 2)].ToLowerInvariant(), GuiKit.MutedColor);
                GUILayout.Label($"{CreatureCatalog.DisplayName(g.prefab)} ×{g.count}  {stars}{prof}{fac}", GuiKit.Label);
                GUILayout.FlexibleSpace();
                UiInput.PushScope(RuntimeHelpers.GetHashCode(g));
                GUI.enabled = canSpawn;
                if (GuiKit.Btn(L.T("Выпустить"), GUILayout.Width(84))) Arena.SpawnGroup(i);
                GUI.enabled = true;
                if (GuiKit.Btn(L.T("Изм."), GUILayout.Width(44))) { _draft = g.Clone(); _editingIndex = i; }
                if (GuiKit.Btn("▲", GUILayout.Width(24)) && i > 0)
                {
                    S.roster[i] = S.roster[i - 1]; S.roster[i - 1] = g;
                    if (_editingIndex == i) _editingIndex = i - 1; else if (_editingIndex == i - 1) _editingIndex = i;
                }
                if (GuiKit.Btn("▼", GUILayout.Width(24)) && i < S.roster.Count - 1)
                {
                    S.roster[i] = S.roster[i + 1]; S.roster[i + 1] = g;
                    if (_editingIndex == i) _editingIndex = i + 1; else if (_editingIndex == i + 1) _editingIndex = i;
                }
                bool remove = GuiKit.Btn("✕", GUILayout.Width(24));
                UiInput.PopScope();
                if (remove)
                {
                    S.roster.RemoveAt(i);
                    if (_editingIndex == i) _editingIndex = -1; else if (_editingIndex > i) _editingIndex--;
                    GUILayout.EndHorizontal();
                    break;
                }
                GUILayout.EndHorizontal();
            }
            GuiKit.EndScroll();
        }

        private static void AddDraft(bool spawnNow)
        {
            S.roster.Add(_draft.Clone());
            if (spawnNow) Arena.SpawnGroup(S.roster.Count - 1);
            _draft.team = _draft.team % Hostility.TeamCount + 1;
        }

        private static bool PassCategory(CreatureEntry e)
        {
            if (_filterFaction >= 0 && (int)e.Faction != _filterFaction) return false;
            switch (_category)
            {
                case 1: return e.Boss;
                case 2: return e.CanFly;
                case 3: return e.Swimmer || e.Faction == Character.Faction.SeaMonsters;
                default: return true;
            }
        }

        public static string StarText(int stars) => stars <= 0 ? "0★" : stars + "★";

        private static string[] BehaviorNames => new[] { L.T("Идёт к врагам"), L.T("Стоит на месте"), L.T("Свободно") };

        private static string[] BehaviorHints => new[]
        {
            L.T("Во время боя всегда знает, где враги на арене (сквозь препятствия и туман), и идёт к ним; поводок боссов снят."),
            L.T("Не сходит с места: разворачивается к врагу и бьёт, когда тот подошёл на дистанцию атаки (стрелки стреляют издали)."),
            L.T("Как в игре: видит своими глазами, без цели бродит у места появления. Боссы держатся его как алтаря — ") +
            L.T("далеко не преследуют и возвращаются."),
        };

        private static string[] IntruderHints => new[]
        {
            L.T("Забредшие существа и чужие призывы бойцам не интересны, и сами их не трогают."),
            L.T("Существо, оказавшееся внутри границы, становится бойцом команды «Посторонние» — враг всем командам."),
            L.T("Существа, оказавшиеся внутри границы, удаляются."),
        };

        private static string[] PlacementNames => new[] { L.T("Сторона команды"), L.T("Случайно по арене"), L.T("Центр"), L.T("Прицел камеры") };

        private static void DrawGroupEditor(SpawnGroup g, float lw)
        {
            g.count = GuiKit.Stepper(L.T("Количество"), g.count, 1, 200, lw);
            g.starsMin = GuiKit.Stepper(L.T("Звёзды от"), g.starsMin, 0, 20, lw, 1);
            g.starsMax = GuiKit.Stepper(L.T("Звёзды до"), Mathf.Max(g.starsMax, g.starsMin), g.starsMin, 20, lw, 1);
            GUILayout.Label(L.T("Уровень = звёзды + 1. HP ×(звёзды + 1), урон атак ×(1 + 0,5 · звёзды); облик игра задаёт до 2★ — выше остаётся облик 2★."), GuiKit.Muted);

            GUILayout.BeginHorizontal();
            GUILayout.Label(L.T("Команда"), GuiKit.Label, GUILayout.Width(lw));
            for (int t = 1; t <= Hostility.TeamCount; t++)
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = Hostility.TeamColor(t);
                if (GuiKit.BtnOn(t.ToString(), g.team == t, GUILayout.Width(26))) g.team = t;
                GUI.backgroundColor = old;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(Hostility.TeamName(g.team) + ((HostilityMode)R.hostilityMode == HostilityMode.Teams ? "" : L.T(" (в этом режиме — только расстановка)")), GuiKit.Muted);

            var facNames = new List<string> { L.T("как у прототипа") };
            for (int f = 0; f < Hostility.FactionCount; f++) facNames.Add(Hostility.FactionName(f));
            g.factionOverride = GuiKit.Cycle(L.T("Фракция"), g.factionOverride + 1, facNames, lw, 160f) - 1;

            var profiles = new List<string> { L.T("ванильные параметры") };
            profiles.AddRange(ProfileStore.List(g.prefab));
            int pi = string.IsNullOrEmpty(g.profile) ? 0 : Mathf.Max(0, profiles.IndexOf(g.profile));
            pi = GuiKit.Cycle(L.T("Профиль"), pi, profiles, lw, 160f);
            g.profile = pi == 0 ? "" : profiles[pi];

            g.placement = GuiKit.Cycle(L.T("Размещение"), g.placement, PlacementNames, lw, 160f);
            var behNames = new List<string> { L.T("как у арены") };
            behNames.AddRange(BehaviorNames);
            g.behavior = GuiKit.Cycle(L.T("Поведение"), g.behavior + 1, behNames, lw, 160f) - 1;
            g.spread = GuiKit.Slider(L.T("Разброс"), g.spread, 0f, 20f, "spread", "0.#", lw, L.T("м"));
        }

        private static void TabHostility()
        {
            GUILayout.Label(L.T("Кто с кем сражается"), GuiKit.Header);
            bool edit = Arena.CanEditRules;
            if (!edit) GUILayout.Label(GuiKit.Colored(L.F("Правила арены игрока {0} — только просмотр.", Arena.Current.HostName), new Color(0.6f, 0.85f, 1f)), GuiKit.Label);
            GUI.enabled = edit;
            int mode = GuiKit.Choice(L.T("Режим"), R.hostilityMode, new[] { L.T("Команды"), L.T("Фракции"), L.T("Все против всех") }, 90f);
            if (edit) S.hostilityMode = mode;
            GUI.enabled = true;

            switch ((HostilityMode)mode)
            {
                case HostilityMode.Teams: TeamMatrix(); break;
                case HostilityMode.Factions: FactionMatrix(); break;
                default:
                    GUILayout.Label(L.T("Каждый боец — сам за себя, включая существ одного вида и одной команды."), GuiKit.Muted);
                    break;
            }

            GUILayout.Space(6);
            GUILayout.Label(L.T("Общие правила"), GuiKit.SubHeader);
            GUI.enabled = edit;
            bool ff = GuiKit.Toggle(L.T("Дружественный огонь"), R.friendlyFire,
                L.T("Выключен: союзники не ранят друг друга ни оружием, ни площадными ударами."));
            bool aoe = GuiKit.Toggle(L.T("Площадные удары бьют врагов того же вида"), R.aoeHitsSameKind,
                L.T("В игре AoE не задевает существ с тем же именем, что у владельца; для боя «тролль против тролля» это нужно снять."));
            bool iso = GuiKit.Toggle(L.T("Изолировать арену от посторонних"), R.isolateOutsiders,
                L.T("Бойцы не отвлекаются на диких существ и на игроков за границей арены, а дикие — на бойцов. ") +
                L.T("Игрок внутри границы участвует в бою, если на него реагируют существа (вкладка «Персонаж»)."));
            bool ips = GuiKit.Toggle(L.T("Урон не зависит от числа игроков рядом"), R.ignorePlayerScaling,
                L.T("В игре каждый следующий игрок в 100 м (до пяти) прибавляет ударам существ 4 %, а их живучести 30 % — ") +
                L.T("и в бою существ между собой. Включено — на арене урон как в одиночной игре."));
            if (edit) { S.friendlyFire = ff; S.aoeHitsSameKind = aoe; S.isolateOutsiders = iso; S.ignorePlayerScaling = ips; }
            GUI.enabled = true;
        }

        private static void TeamMatrix()
        {
            GUILayout.Label(L.T("Отметка — команды враждуют (симметрично). По умолчанию все команды враждуют между собой."), GuiKit.Muted);
            var used = new HashSet<int>();
            foreach (var g in S.roster) used.Add(g.team);
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(120));
            for (int j = 1; j <= Hostility.TeamCount; j++)
                GUILayout.Label(GuiKit.Colored(j.ToString(), Hostility.TeamColor(j)), GuiKit.Center, GUILayout.Width(30));
            GUILayout.EndHorizontal();
            for (int i = 1; i <= Hostility.TeamCount; i++)
            {
                GUILayout.BeginHorizontal();
                string name = Hostility.TeamName(i) + (used.Contains(i) ? " •" : "");
                GUILayout.Label(GuiKit.Colored(i + ". " + name, Hostility.TeamColor(i)), GuiKit.Label, GUILayout.Width(120));
                for (int j = 1; j <= Hostility.TeamCount; j++)
                {
                    if (j == i) { GUILayout.Label("—", GuiKit.Center, GUILayout.Width(30)); continue; }
                    bool v = Hostility.TeamHostile(R, i, j);
                    GUI.enabled = Arena.CanEditRules;
                    bool nv = GuiKit.ToggleBox(v, 30f);
                    GUI.enabled = true;
                    if (nv != v) Hostility.SetTeamHostile(S, i, j, nv);
                }
                GUILayout.EndHorizontal();
            }
            GUI.enabled = Arena.CanEditRules;
            GUILayout.BeginHorizontal();
            if (GuiKit.Btn(L.T("Все враждуют"))) Hostility.ResetTeamMatrix(S);
            if (GuiKit.Btn(L.T("Все союзники")))
                for (int i = 0; i < S.teamMatrix.Length; i++) S.teamMatrix[i] = false;
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Label(L.T("• — команда есть в составе."), GuiKit.Muted);

            GUILayout.Space(4);
            var names = new List<string> { L.T("вне команд (враг всем)") };
            for (int t = 1; t <= Hostility.TeamCount; t++) names.Add(t + ". " + Hostility.TeamName(t));
            S.playerTeam = GuiKit.Cycle(L.T("Мой персонаж в команде"), S.playerTeam, names, 170f, 200f);
            GUILayout.Label(L.T("У каждого игрока своя команда. Действует внутри границы и если существа реагируют на персонажа (вкладка «Персонаж»)."), GuiKit.Muted);
        }

        private static void FactionMatrix()
        {
            int n = Hostility.FactionCount;
            GUILayout.Label(L.T("Строка — кто нападает, столбец — на кого. Фракцию бойца можно переопределить в группе состава. ") +
                            L.T("Изначально — как в игре: боссы никого, кроме игроков, не трогают."), GuiKit.Muted);
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(130));
            for (int j = 0; j < n; j++)
                GUILayout.Label((j + 1).ToString(), GuiKit.Center, GUILayout.Width(26));
            GUILayout.EndHorizontal();
            for (int i = 0; i < n; i++)
            {
                GUILayout.BeginHorizontal(i % 2 == 0 ? GuiKit.Row : GuiKit.RowAlt);
                GUILayout.Label((i + 1) + ". " + Hostility.FactionName(i), GuiKit.Small, GUILayout.Width(130));
                for (int j = 0; j < n; j++)
                {
                    bool v = R.factionMatrix[i * n + j];
                    GUI.enabled = Arena.CanEditRules;
                    bool nv = GuiKit.ToggleBox(v, 26f);
                    GUI.enabled = true;
                    if (nv != v) S.factionMatrix[i * n + j] = nv;
                }
                GUILayout.EndHorizontal();
            }
            GUI.enabled = Arena.CanEditRules;
            GUILayout.BeginHorizontal();
            if (GuiKit.Btn(L.T("Как в игре"))) Hostility.ResetFactionMatrix(S);
            if (GuiKit.Btn(L.T("Боссы атакуют рядовых"))) Hostility.BossesAttackEveryone(S, _bossReciprocal);
            _bossReciprocal = GuiKit.Toggle(L.T("и рядовые отвечают"), _bossReciprocal);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GuiKit.Btn(L.T("Все фракции враждуют"))) Hostility.AllFactionsHostile(S, false);
            if (GuiKit.Btn(L.T("…и внутри фракций тоже"))) Hostility.AllFactionsHostile(S, true);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        private static string[] StyleNames => new[] { L.T("Орбита"), L.T("Погоня"), L.T("Сверху") };

        private static void TabCamera()
        {
            GUILayout.Label(L.T("Камера"), GuiKit.Header);
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.T("Режим"), GuiKit.Label, GUILayout.Width(170));
            for (int m = 0; m < 3; m++)
                if (GuiKit.BtnOn(ArenaCamera.ModeName((CameraMode)m), (int)ArenaCamera.Mode == m)) ArenaCamera.SetMode((CameraMode)m);
            GUILayout.EndHorizontal();
            GUILayout.Label(L.T("Свободная: WASD — движение, Пробел/E — вверх, Ctrl/Q — вниз, Shift — быстрее, Alt — медленнее, колесо — скорость. ") +
                            L.T("Слежение: мышь — облёт, колесо — дистанция. При открытом меню обзор — с зажатой ПКМ."), GuiKit.Muted);

            S.freeCamSpeed = GuiKit.Slider(L.T("Скорость свободной"), S.freeCamSpeed, 1f, 100f, "camspeed", "0.#", suffix: L.T("м/с"));
            float fov = GuiKit.Slider(L.T("Поле зрения"), S.cameraFov, 30f, 110f, "fov", "0");
            if (!Mathf.Approximately(fov, S.cameraFov))
            {
                S.cameraFov = fov;
                if (GameCamera.instance && ArenaCamera.Active)
                {
                    var cam = Reflect.Camera(GameCamera.instance);
                    var sky = Reflect.SkyCamera(GameCamera.instance);
                    if (cam) cam.fieldOfView = fov;
                    if (sky) sky.fieldOfView = fov;
                }
            }
            S.limitCamera = GuiKit.Toggle(L.T("Не улетать дальше 80 м за границу"), S.limitCamera,
                L.T("Мир прогружается вокруг персонажа; далеко от него существа и рельеф исчезают."));

            GUILayout.Space(4);
            GUILayout.Label(L.T("Слежение"), GuiKit.SubHeader);
            S.followStyle = GuiKit.Choice(L.T("Стиль"), S.followStyle, StyleNames);
            S.followDistanceScale = GuiKit.Slider(L.T("Дистанция"), S.followDistanceScale, 0.3f, 4f, "followdist", "0.##", suffix: "×");
            S.autoSwitchOnDeath = GuiKit.Toggle(L.T("Переключаться, когда цель гибнет"), S.autoSwitchOnDeath);
            S.director = GuiKit.Toggle(L.T("Режиссёр: сам переключает цели"), S.director, L.T("Предпочитает тех, кто в бою и наносит больше урона."));
            if (S.director) S.directorInterval = GuiKit.Slider(L.T("Смена цели каждые"), S.directorInterval, 3f, 30f, "dirint", "0", suffix: L.T("с"));

            GUILayout.Space(4);
            GUILayout.Label(L.T("Бойцы (щелчок — следить)"), GuiKit.SubHeader);
            var list = ArenaCamera.Candidates();
            if (list.Count == 0) GUILayout.Label(L.T("На арене никого."), GuiKit.Muted);
            int row = 0;
            foreach (var u in list)
            {
                GUILayout.BeginHorizontal(row++ % 2 == 0 ? GuiKit.Row : GuiKit.RowAlt);
                bool sel = ArenaCamera.Target == u && ArenaCamera.Mode == CameraMode.Follow;
                if (GuiKit.Press(GuiKit.Colored(Arena.Label(u), Hostility.UnitColor(u)), sel ? GuiKit.ButtonOn : GuiKit.Row, GUILayout.Width(300)))
                    ArenaCamera.SetTarget(u);
                GuiKit.LayoutBar(u.Character.GetHealthPercentage(), Color.Lerp(Color.red, Color.green, u.Character.GetHealthPercentage()), 8f, 160f);
                GUILayout.Label($"{u.Character.GetHealth():0}/{u.Character.GetMaxHealth():0}", GuiKit.Small, GUILayout.Width(90));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
            GUILayout.Label(L.T("Оверлей"), GuiKit.SubHeader);
            S.overlay = GuiKit.Toggle(L.T("Показывать оверлей арены (") + ArenaPlugin.KeyOverlay.Value + ")", S.overlay);
            S.nameTags = GuiKit.Toggle(L.F("Таблички над бойцами: имя, звёзды, здоровье ({0})", ArenaPlugin.KeyTags.Value), S.nameTags);
            bool hideGame = GuiKit.Toggle(L.F("Скрыть интерфейс игры ({0})", ArenaPlugin.KeyGameUi.Value), GameUi.Hidden,
                L.T("HUD, миникарта, полоски над существами, сообщения игры, чат и подсказки клавиш; оверлей и окна арены остаются."));
            if (hideGame != GameUi.Hidden) GameUi.SetHidden(hideGame);
            S.hpBars = GuiKit.Toggle(L.T("Полоски здоровья"), S.hpBars);
            S.killFeed = GuiKit.Toggle(L.T("Лента убийств"), S.killFeed);
            S.tagDistance = GuiKit.Slider(L.T("Дальность табличек"), S.tagDistance, 20f, 250f, "tagdist", "0", suffix: L.T("м"));
        }

        private static void TabPlayer()
        {
            GUILayout.Label(L.T("Персонаж в мире"), GuiKit.Header);
            GUILayout.BeginHorizontal();
            if (GuiKit.Btn(L.T("Наблюдатель"), GUILayout.Height(28)))
            {
                S.playerVisible = false;
                S.playerCollision = (int)CollisionMode.Disabled;
                S.creaturesReact = false;
                S.playerInvulnerable = true;
            }
            if (GuiKit.Btn(L.T("Участник"), GUILayout.Height(28)))
            {
                S.playerVisible = true;
                S.playerCollision = (int)CollisionMode.Normal;
                S.creaturesReact = true;
                S.playerInvulnerable = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(L.T("Наблюдатель: невидим, без столкновений (замирает на месте), существа его не замечают, урон не проходит."), GuiKit.Muted);

            GUILayout.Space(4);
            S.playerVisible = GuiKit.Toggle(L.T("Видимое отображение"), S.playerVisible);
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.T("Столкновения тела"), GuiKit.Label, GUILayout.Width(170));
            for (int m = 0; m < 3; m++)
                if (GuiKit.BtnOn(PlayerPresence.CollisionName((CollisionMode)m), S.playerCollision == m)) S.playerCollision = m;
            GUILayout.EndHorizontal();
            GUILayout.Label(L.T("«Сквозь существ» — персонаж ходит, а тела, удары, снаряды и площадные атаки существ проходят сквозь него; ") +
                            L.T("существа его не выбирают целью, и его оружие их тоже не задевает. «Отключена» — то же, но физика персонажа ") +
                            L.T("выключена и он висит на месте; удобно со свободной камерой."), GuiKit.Muted);
            bool ghost = (CollisionMode)S.playerCollision != CollisionMode.Normal;
            GUI.enabled = !ghost;
            bool react = GuiKit.Toggle(L.T("Существа реагируют на персонажа"), !ghost && S.creaturesReact,
                L.T("Выключено: ни бойцы арены, ни дикие существа его не замечают, их удары, площадные атаки и шальные снаряды ") +
                L.T("его не задевают; тело при этом твёрдое. Его оружие существ тоже не задевает."));
            GUI.enabled = true;
            if (!ghost) S.creaturesReact = react;
            else GUILayout.Label(L.T("Пока столкновения не «Обычные», существа персонажа не замечают в любом случае."), GuiKit.Muted);
            S.playerInvulnerable = GuiKit.Toggle(L.T("Неуязвимость"), S.playerInvulnerable,
                L.T("Никакого урона, ошеломления и отбрасывания — ни от существ, ни от игроков, огня, яда, дыма и падения. ") +
                L.T("Существа при этом замечают и атакуют как обычно."));
        }

        private static void TabResults()
        {
            var cur = Arena.Current;
            GUILayout.Label(L.T("Итоги"), GuiKit.Header);
            if (cur == null)
            {
                GUILayout.Label(L.T("Арена не развёрнута."), GuiKit.Muted);
                return;
            }
            if (!cur.IsLocal) GUILayout.Label(L.T("Статистику ведёт хозяин арены и присылает раз в пару секунд."), GuiKit.Muted);
            string result = cur.ResultText;
            if (!string.IsNullOrEmpty(result))
                GUILayout.Label(GuiKit.Colored(result, GuiKit.Accent), GuiKit.Label);
            else if (cur.State == ArenaState.Fighting)
                GUILayout.Label(L.T("Бой идёт."), GuiKit.Muted);

            GUILayout.Space(4);
            GUILayout.Label(L.T("Стороны"), GuiKit.SubHeader);
            HeaderRow(new[] { L.T("Сторона"), L.T("Живы"), L.T("Всего"), L.T("Убийств"), L.T("Урон") }, new[] { 260f, 60f, 60f, 70f, 90f });
            int row = 0;
            foreach (var s in cur.Sides.Values)
            {
                GUILayout.BeginHorizontal(row++ % 2 == 0 ? GuiKit.Row : GuiKit.RowAlt);
                GUILayout.Label(GuiKit.Colored(s.Name, s.Color), GuiKit.Label, GUILayout.Width(260));
                GUILayout.Label(s.Alive.ToString(), GuiKit.Label, GUILayout.Width(60));
                GUILayout.Label(s.Spawned.ToString(), GuiKit.Label, GUILayout.Width(60));
                GUILayout.Label(s.Kills.ToString(), GuiKit.Label, GUILayout.Width(70));
                GUILayout.Label(s.Damage.ToString("0"), GuiKit.Label, GUILayout.Width(90));
                GUILayout.EndHorizontal();
            }

            var alive = new List<UnitStat>();
            var fallen = new List<UnitStat>();
            foreach (var st in cur.Stats.Values)
            {
                if (string.IsNullOrEmpty(st.label)) continue;
                if (st.alive) alive.Add(st); else fallen.Add(st);
            }
            alive.Sort((a, b) => b.dealt.CompareTo(a.dealt));
            fallen.Sort((a, b) => b.deathTime.CompareTo(a.deathTime));

            GUILayout.Space(4);
            GUILayout.Label(L.T("Живые бойцы"), GuiKit.SubHeader);
            HeaderRow(new[] { L.T("Боец"), "HP", L.T("Урон"), L.T("Получено"), L.T("Убийств") }, new[] { 280f, 90f, 80f, 80f, 70f });
            row = 0;
            var byKey = new Dictionary<string, ArenaUnit>();
            foreach (var u in Arena.SessionUnits(true)) byKey[u.Key] = u;
            foreach (var st in alive)
            {
                GUILayout.BeginHorizontal(row++ % 2 == 0 ? GuiKit.Row : GuiKit.RowAlt);
                GUILayout.Label(GuiKit.Colored(st.label, st.color), GuiKit.Label, GUILayout.Width(280));
                string hp = byKey.TryGetValue(st.key, out var u) ? $"{u.Character.GetHealth():0}/{u.Character.GetMaxHealth():0}" : "—";
                GUILayout.Label(hp, GuiKit.Label, GUILayout.Width(90));
                GUILayout.Label(st.dealt.ToString("0"), GuiKit.Label, GUILayout.Width(80));
                GUILayout.Label(st.taken.ToString("0"), GuiKit.Label, GUILayout.Width(80));
                GUILayout.Label(st.kills.ToString(), GuiKit.Label, GUILayout.Width(70));
                GUILayout.EndHorizontal();
            }

            if (fallen.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label(L.T("Павшие"), GuiKit.SubHeader);
                HeaderRow(new[] { L.T("Боец"), L.T("Прожил"), L.T("Урон"), L.T("Убийств"), L.T("Кем убит") }, new[] { 280f, 70f, 80f, 70f, 200f });
                row = 0;
                foreach (var f in fallen)
                {
                    GUILayout.BeginHorizontal(row++ % 2 == 0 ? GuiKit.Row : GuiKit.RowAlt);
                    GUILayout.Label(GuiKit.Colored(f.label, f.color), GuiKit.Label, GUILayout.Width(280));
                    GUILayout.Label(GuiKit.FmtTime((float)(f.deathTime - f.spawnTime)), GuiKit.Label, GUILayout.Width(70));
                    GUILayout.Label(f.dealt.ToString("0"), GuiKit.Label, GUILayout.Width(80));
                    GUILayout.Label(f.kills.ToString(), GuiKit.Label, GUILayout.Width(70));
                    GUILayout.Label(string.IsNullOrEmpty(f.killedBy) ? L.T("стихия") : f.killedBy, GuiKit.Label, GUILayout.Width(200));
                    GUILayout.EndHorizontal();
                }
            }
        }

        private static void HeaderRow(string[] names, float[] widths)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < names.Length; i++)
                GUILayout.Label(GuiKit.Colored(names[i], GuiKit.MutedColor), GuiKit.Small, GUILayout.Width(widths[i]));
            GUILayout.EndHorizontal();
        }
    }
}
