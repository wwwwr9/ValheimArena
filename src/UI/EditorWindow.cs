using System.Collections.Generic;
using UnityEngine;
using ValheimArena.Core;
using ValheimArena.Profiles;

namespace ValheimArena.UI
{
    public static class EditorWindow
    {
        private static string _prefab = "";
        private static CreatureProfile _work;
        private static CreatureProfile _vanilla;
        private static string _loaded = "";
        private static string _saveAs = "";
        private static bool _dirtyHint;

        private static string _search = "";
        private static bool _onlyWithProfiles;
        private static Vector2 _listScroll, _editScroll;
        private static readonly bool[] Sections = { true, true, false, false, true };
        private static readonly HashSet<string> OpenAttacks = new HashSet<string>();
        private static bool _showZeroDamage;
        private static string _confirmDelete = "";

        private const float LW = 190f;

        public static void Draw()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.T("Профили: ") + ProfileStore.Root, GuiKit.Muted);
            GUILayout.FlexibleSpace();
            if (GuiKit.Btn("✕", GUILayout.Width(30))) ArenaUI.CloseEditor();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawList();
            GUILayout.BeginVertical();
            DrawEditor();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        public static void Open(string prefab, string profile)
        {
            ArenaUI.EditorOpen = true;
            Select(prefab);
            if (!string.IsNullOrEmpty(profile)) LoadProfile(profile);
        }

        private static void DrawList()
        {
            GUILayout.BeginVertical(GUILayout.Width(250));
            _search = GuiKit.TextField("", _search, "editor_search");
            _onlyWithProfiles = GuiKit.Toggle(L.T("Только с профилями"), _onlyWithProfiles);
            HashSet<string> withProfiles = _onlyWithProfiles ? new HashSet<string>(ProfileStore.PrefabsWithProfiles()) : null;
            _listScroll = GuiKit.BeginScroll(_listScroll);
            string q = (_search ?? "").Trim().ToLowerInvariant();
            int row = 0;
            foreach (var e in CreatureCatalog.All)
            {
                if (q.Length > 0 && !e.SearchKey.Contains(q)) continue;
                if (withProfiles != null && !withProfiles.Contains(FileNames.Sanitize(e.Prefab))) continue;
                bool sel = e.Prefab == _prefab;
                string text = (e.Boss ? GuiKit.Colored("♛ ", GuiKit.Accent) : "") + e.Display + "\n" + GuiKit.Colored(e.Prefab, GuiKit.MutedColor);
                if (GuiKit.Press(text, sel ? GuiKit.ButtonOn : (row++ % 2 == 0 ? GuiKit.Row : GuiKit.RowAlt)))
                    Select(e.Prefab);
            }
            if (CreatureCatalog.All.Count == 0) GUILayout.Label(L.T("Каталог появится после входа в мир."), GuiKit.Muted);
            GuiKit.EndScroll();
            GUILayout.EndVertical();
        }

        private static void Select(string prefab)
        {
            if (prefab == _prefab && _work != null) return;
            var entry = CreatureCatalog.Get(prefab);
            if (entry == null) return;
            _prefab = prefab;
            _vanilla = ProfileLogic.Capture(entry.GameObject);
            _work = _vanilla?.Clone();
            _loaded = "";
            _saveAs = "";
            _dirtyHint = false;
            _confirmDelete = "";
            OpenAttacks.Clear();
            GuiKit.ResetBuffers();
            _editScroll = Vector2.zero;
        }

        private static void LoadProfile(string name)
        {
            var p = ProfileStore.Load(_prefab, name);
            if (p == null) return;
            _work = Merge(p.Clone());
            _loaded = name;
            _saveAs = name;
            _dirtyHint = false;
            GuiKit.ResetBuffers();
        }

        private static CreatureProfile Merge(CreatureProfile p)
        {
            if (_vanilla == null) return p;
            if (p.version < 2)
            {
                p.flyTurnSpeed = _vanilla.flyTurnSpeed;
                p.swimSpeed = _vanilla.swimSpeed;
                p.swimTurnSpeed = _vanilla.swimTurnSpeed;
                p.version = 2;
            }
            foreach (var a in p.attacks)
            {
                var va = _vanilla.FindAttack(a.item, a.sharedName);
                if (a.captured >= 2 || va == null) continue;
                a.captured = 2;
                a.attackType = va.attackType;
                a.attackRayWidth = va.attackRayWidth;
                a.statusEffect = va.statusEffect;
                a.statusEffectChance = va.statusEffectChance;
                a.aiTargetType = va.aiTargetType;
                a.aiInDungeonOnly = va.aiInDungeonOnly;
                a.aiInMistOnly = va.aiInMistOnly;
                a.aiWhenFlying = va.aiWhenFlying;
                a.aiWhenWalking = va.aiWhenWalking;
                a.aiWhenSwiming = va.aiWhenSwiming;
            }
            foreach (var va in _vanilla.attacks)
                if (p.FindAttack(va.item, null) == null) p.attacks.Add(va.Clone());
            foreach (var ve in _vanilla.effects)
                if (p.FindEffect(ve.prefab) == null) p.effects.Add(ve.Clone());
            if (p.weakSpots == null) p.weakSpots = new List<WeakSpotOverride>();
            foreach (var vw in _vanilla.weakSpots)
                if (p.weakSpots.Find(x => x.name == vw.name) == null) p.weakSpots.Add(vw.Clone());
            return p;
        }

        private static void DrawEditor()
        {
            var entry = CreatureCatalog.Get(_prefab);
            if (entry == null || _work == null)
            {
                GUILayout.Label(L.T("Выберите существо слева. Изменения собираются в профиль; профиль выбирается в группе состава ") +
                                L.T("(вкладка «Бойцы») или применяется к уже выпущенным бойцам."), GuiKit.Muted);
                return;
            }

            GUILayout.Label(GuiKit.Colored(entry.Display, Color.white) + GuiKit.Colored("  " + entry.Prefab + " · " + Hostility.FactionName((int)entry.Faction), GuiKit.MutedColor), GuiKit.Header);

            GUILayout.BeginVertical(GuiKit.Box);
            var names = new List<string> { L.T("— ванильные параметры —") };
            names.AddRange(ProfileStore.List(_prefab));
            int cur = string.IsNullOrEmpty(_loaded) ? 0 : Mathf.Max(0, names.IndexOf(_loaded));
            int pick = GuiKit.Cycle(L.T("Профиль"), cur, names, 80f, 260f);
            if (pick != cur)
            {
                if (pick == 0) { _work = _vanilla.Clone(); _loaded = ""; _saveAs = ""; GuiKit.ResetBuffers(); }
                else LoadProfile(names[pick]);
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(L.T("Имя"), GuiKit.Label, GUILayout.Width(80));
            _saveAs = GuiKit.TextField("", _saveAs, "profile_name");
            if (GuiKit.Btn(L.T("Сохранить"), GUILayout.Width(100)))
            {
                string n = FileNames.Sanitize(_saveAs);
                if (n.Length == 0) Arena.Message(L.T("Задайте имя профиля."));
                else
                {
                    _work.prefab = _prefab;
                    _work.name = n;
                    ProfileStore.Save(_work);
                    _loaded = n;
                    _saveAs = n;
                    _dirtyHint = false;
                    Arena.Message(L.F("Профиль «{0}» сохранён для {1}.", n, entry.Display));
                }
            }
            GUI.enabled = !string.IsNullOrEmpty(_loaded);
            if (GuiKit.Btn(_confirmDelete == _loaded && _loaded != "" ? L.T("Точно удалить?") : L.T("Удалить"), GUILayout.Width(110)))
            {
                if (_confirmDelete == _loaded)
                {
                    ProfileStore.Delete(_prefab, _loaded);
                    _loaded = "";
                    _confirmDelete = "";
                    _work = _vanilla.Clone();
                    GuiKit.ResetBuffers();
                }
                else _confirmDelete = _loaded;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GuiKit.Btn(L.T("Сбросить к ванили"))) { _work = _vanilla.Clone(); _dirtyHint = true; GuiKit.ResetBuffers(); }
            int onArena = CountOnArena();
            GUI.enabled = onArena > 0 && Arena.CanControl;
            if (GuiKit.Btn(L.F("Применить к бойцам этого вида ({0})", onArena))) ApplyToArena(false);
            if (GuiKit.Btn(L.T("Вернуть им ваниль"))) ApplyToArena(true);
            GUI.enabled = true;
            var t = ArenaCamera.Target;
            GUI.enabled = t && t.PrefabName == _prefab && Arena.CanControl;
            if (GuiKit.Btn(L.T("Применить к цели камеры"))) t.SetProfile(_work);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (_dirtyHint) GUILayout.Label(L.T("Есть несохранённые изменения. Составу нужен сохранённый профиль; к живым бойцам можно применить сразу."), GuiKit.Muted);
            GUILayout.EndVertical();

            _editScroll = GuiKit.BeginScroll(_editScroll);
            var before = JsonUtility.ToJson(_work);
            if (Section(0, L.T("Живучесть"))) DrawVitality();
            if (Section(1, L.T("Сопротивления"))) DrawResists();
            if (Section(2, L.T("Движение"))) DrawMovement();
            if (Section(3, L.T("Поведение ИИ"))) DrawBehaviour();
            if (Section(4, L.F("Атаки ({0})", _work.attacks.Count))) DrawAttacks();
            if (!_dirtyHint && UiInput.FirstPass && JsonUtility.ToJson(_work) != before) _dirtyHint = true;
            GuiKit.EndScroll();
        }

        private static bool Section(int i, string title)
        {
            if (GuiKit.Press((Sections[i] ? "▼ " : "► ") + title, GuiKit.SubHeader)) Sections[i] = !Sections[i];
            return Sections[i];
        }

        private static int CountOnArena()
        {
            int n = 0;
            foreach (var u in ArenaUnit.All)
                if (u && u.Alive && u.PrefabName == _prefab && Arena.Current != null && u.SessionId == Arena.Current.Id) n++;
            return n;
        }

        private static void ApplyToArena(bool vanilla)
        {
            int n = 0;
            foreach (var u in new List<ArenaUnit>(ArenaUnit.All))
            {
                if (!u || !u.Alive || u.PrefabName != _prefab || Arena.Current == null || u.SessionId != Arena.Current.Id) continue;
                u.SetProfile(vanilla ? null : _work);
                n++;
            }
            Arena.Message(vanilla ? L.F("Ванильные параметры возвращены: {0}", n) : L.F("Профиль применён к бойцам: {0}", n));
        }

        private static float Num(string label, float value, float vanilla, string id, string fmt = "0.##")
        {
            bool changed = Mathf.Abs(value - vanilla) > 0.0001f;
            GUILayout.BeginHorizontal();
            GUILayout.Label(changed ? GuiKit.Colored(label, GuiKit.Accent) : label, GuiKit.Label, GUILayout.Width(LW));
            float nv = GuiKit.NumberBox(value, id, fmt, 90f);
            GUILayout.Label(changed ? GuiKit.Colored(L.T("ваниль ") + vanilla.ToString(fmt), GuiKit.MutedColor) : "", GuiKit.Small, GUILayout.Width(110));
            GUILayout.EndHorizontal();
            return nv;
        }

        private static int NumInt(string label, int value, int vanilla, string id)
        {
            return Mathf.RoundToInt(Num(label, value, vanilla, id, "0"));
        }

        private static bool Flag(string label, bool value, bool vanilla)
        {
            string text = value != vanilla ? GuiKit.Colored(label, GuiKit.Accent) : label;
            return GuiKit.Toggle(text, value);
        }

        private static void DrawVitality()
        {
            var v = _vanilla;
            _work.health = Mathf.Max(1f, Num(L.T("Здоровье (база)"), _work.health, v.health, "hp", "0"));
            float wl = Game.m_worldLevel > 0 && Game.instance ? Game.m_worldLevel * Game.instance.m_worldLevelEnemyHPMultiplier : 1f;
            float hp = _work.health * wl;
            GUILayout.Label(L.F("С учётом звёзд: 0★ {0:0} · 1★ {1:0} · 2★ {2:0} · 5★ {3:0}", hp, hp * 2, hp * 3, hp * 6) +
                            (wl > 1f ? L.F(" (мир NG+{0}: ×{1:0.#})", Game.m_worldLevel, wl) : ""), GuiKit.Muted);
            _work.scale = Mathf.Clamp(Num(L.T("Размер (множитель)"), _work.scale, 1f, "scale"), 0.1f, 10f);
            _work.damageDealtMultiplier = Mathf.Max(0f, Num(L.T("Наносимый урон ×"), _work.damageDealtMultiplier, 1f, "dmgout"));
            _work.damageTakenMultiplier = Mathf.Max(0f, Num(L.T("Получаемый урон ×"), _work.damageTakenMultiplier, 1f, "dmgin"));
            GUILayout.Label(L.T("Множители действуют на весь урон: оружие, снаряды, площадные удары, эффекты, горение и яд."), GuiKit.Muted);
            _work.staggerDamageFactor = Mathf.Max(0f, Num(L.T("Порог ошеломления (доля HP)"), _work.staggerDamageFactor, v.staggerDamageFactor, "stagger", "0.###"));
            GUILayout.Label(L.T("0 — не ошеломляется уроном."), GuiKit.Muted);
            _work.staggerWhenBlocked = Flag(L.T("Ошеломляется, когда удар заблокирован"), _work.staggerWhenBlocked, v.staggerWhenBlocked);
            _work.regenAllHPTime = Mathf.Max(1f, Num(L.T("Полное восстановление HP, с"), _work.regenAllHPTime, v.regenAllHPTime, "regen", "0"));
        }

        private static void DrawResists()
        {
            DrawResistRows(_work.resists, _vanilla.resists);
            var r = _work.resists;
            GUILayout.BeginHorizontal();
            if (GuiKit.Btn(L.T("Все — обычные"))) for (int i = 0; i < ResistSet.Labels.Length; i++) r.Set(i, (int)HitData.DamageModifier.Normal);
            if (GuiKit.Btn(L.T("Все — стойкие"))) for (int i = 0; i < ResistSet.Labels.Length; i++) r.Set(i, (int)HitData.DamageModifier.Resistant);
            if (GuiKit.Btn(L.T("Как в игре"))) _work.resists = _vanilla.resists.Clone();
            GUILayout.EndHorizontal();
            GUILayout.Label(L.T("«−» — уязвимее, «+» — стойче. «Игнор» и «Иммунитет» обнуляют урон своего типа; горение, мороз, ") +
                            L.T("молнию и яд снимает только «Иммунитет», отбрасывание проходит всегда. «Чистый» урон сопротивления не учитывают."), GuiKit.Muted);

            foreach (var w in _work.weakSpots)
            {
                var vw = _vanilla.weakSpots.Find(x => x.name == w.name) ?? w;
                GUILayout.Space(6);
                GUILayout.Label(L.F("Слабое место «{0}»", w.name), GuiKit.SubHeader);
                GUILayout.Label(L.T("Попадание в слабое место игра считает по его сопротивлениям вместо основных."), GuiKit.Muted);
                DrawResistRows(w.resists, vw.resists);
            }
        }

        private static void DrawResistRows(ResistSet r, ResistSet vanilla)
        {
            for (int i = 0; i < ResistSet.Labels.Length; i++)
            {
                int val = r.Get(i);
                int van = vanilla.Get(i);
                GUILayout.BeginHorizontal();
                GUILayout.Label(val != van ? GuiKit.Colored(ResistSet.Labels[i], GuiKit.Accent) : ResistSet.Labels[i], GuiKit.Label, GUILayout.Width(LW - 40f));
                int idx = System.Array.IndexOf(ResistSet.Ladder, (HitData.DamageModifier)val);
                if (idx < 0) idx = 3;
                if (GuiKit.Btn("−", GUILayout.Width(26))) idx = Mathf.Max(0, idx - 1);
                GUILayout.Label(ResistSet.ModifierName((int)ResistSet.Ladder[idx]), GuiKit.Center, GUILayout.Width(170));
                if (GuiKit.Btn("+", GUILayout.Width(26))) idx = Mathf.Min(ResistSet.Ladder.Length - 1, idx + 1);
                r.Set(i, (int)ResistSet.Ladder[idx]);
                if (val != van) GUILayout.Label(GuiKit.Colored(L.T("ваниль: ") + ResistSet.ModifierName(van), GuiKit.MutedColor), GuiKit.Small);
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawMovement()
        {
            var v = _vanilla;
            _work.walkSpeed = Mathf.Max(0f, Num(L.T("Шаг"), _work.walkSpeed, v.walkSpeed, "walk"));
            _work.speed = Mathf.Max(0f, Num(L.T("Обычная скорость"), _work.speed, v.speed, "speed"));
            _work.runSpeed = Mathf.Max(0f, Num(L.T("Бег"), _work.runSpeed, v.runSpeed, "run"));
            _work.turnSpeed = Mathf.Max(0f, Num(L.T("Поворот"), _work.turnSpeed, v.turnSpeed, "turn"));
            _work.runTurnSpeed = Mathf.Max(0f, Num(L.T("Поворот на бегу"), _work.runTurnSpeed, v.runTurnSpeed, "runturn"));
            _work.acceleration = Mathf.Max(0f, Num(L.T("Ускорение"), _work.acceleration, v.acceleration, "accel"));
            _work.jumpForce = Mathf.Max(0f, Num(L.T("Сила прыжка"), _work.jumpForce, v.jumpForce, "jump"));
            if (_work.flying || v.flying)
            {
                _work.flySlowSpeed = Mathf.Max(0f, Num(L.T("Полёт медленно"), _work.flySlowSpeed, v.flySlowSpeed, "flyslow"));
                _work.flyFastSpeed = Mathf.Max(0f, Num(L.T("Полёт быстро"), _work.flyFastSpeed, v.flyFastSpeed, "flyfast"));
                _work.flyTurnSpeed = Mathf.Max(0f, Num(L.T("Поворот в полёте"), _work.flyTurnSpeed, v.flyTurnSpeed, "flyturn"));
            }
            _work.swimSpeed = Mathf.Max(0f, Num(L.T("Плавание"), _work.swimSpeed, v.swimSpeed, "swim"));
            _work.swimTurnSpeed = Mathf.Max(0f, Num(L.T("Поворот в воде"), _work.swimTurnSpeed, v.swimTurnSpeed, "swimturn"));
        }

        private static void DrawBehaviour()
        {
            var v = _vanilla;
            _work.viewRange = Mathf.Max(0f, Num(L.T("Дальность зрения"), _work.viewRange, v.viewRange, "view"));
            _work.viewAngle = Mathf.Clamp(Num(L.T("Угол зрения от взгляда, °"), _work.viewAngle, v.viewAngle, "viewang"), 0f, 180f);
            _work.hearRange = Mathf.Max(0f, Num(L.T("Дальность слуха"), _work.hearRange, v.hearRange, "hear"));
            _work.mistVision = Flag(L.T("Видит сквозь туман"), _work.mistVision, v.mistVision);
            GUILayout.Label(L.T("На арене зрение не ниже диаметра арены — иначе бойцы не находят друг друга."), GuiKit.Muted);
            if (_work.hasMonsterAI)
            {
                _work.alertRange = Mathf.Max(0f, Num(L.T("Дальность тревоги"), _work.alertRange, v.alertRange, "alert"));
                _work.minAttackInterval = Mathf.Max(0f, Num(L.T("Мин. пауза между атаками, с"), _work.minAttackInterval, v.minAttackInterval, "minatk"));
                _work.circleTargetInterval = Mathf.Max(0f, Num(L.T("Кружить вокруг цели каждые, с"), _work.circleTargetInterval, v.circleTargetInterval, "circle"));
                _work.fleeIfLowHealth = Mathf.Clamp01(Num(L.T("Бежать при доле HP ниже"), _work.fleeIfLowHealth, v.fleeIfLowHealth, "flee", "0.##"));
                GUILayout.Label(L.T("Бегство отключается настройкой арены «Не убегать при ранении»."), GuiKit.Muted);
            }
        }

        private static void DrawAttacks()
        {
            if (_work.attacks.Count == 0)
            {
                GUILayout.Label(_work.hasMonsterAI
                    ? L.T("У существа нет атак-оружия. Урон регулируется множителями в «Живучести».")
                    : L.T("Это животное: ИИ животных не атакует, а только убегает."), GuiKit.Muted);
                return;
            }
            _showZeroDamage = GuiKit.Toggle(L.T("Показывать нулевые типы урона"), _showZeroDamage);
            for (int i = 0; i < _work.attacks.Count; i++)
            {
                var a = _work.attacks[i];
                var va = _vanilla.FindAttack(a.item, null) ?? a;
                GUILayout.BeginVertical(GuiKit.Box);
                GUILayout.BeginHorizontal();
                a.enabled = GuiKit.ToggleBox(a.enabled, 18f);
                bool open = OpenAttacks.Contains(a.item);
                string title = (open ? "▼ " : "► ") + (string.IsNullOrEmpty(a.displayName) ? a.item : a.displayName) +
                               GuiKit.Colored(L.F("   {0} · {1} · урон {2:0}", a.item, SourceLabel(a.source), a.damage.Total()) + AttackBadges(a), GuiKit.MutedColor) +
                               (a.enabled ? "" : GuiKit.Colored(L.T("  ОТКЛЮЧЕНА"), new Color(1f, 0.4f, 0.3f)));
                if (GuiKit.Press(title, GuiKit.Label))
                {
                    if (open) OpenAttacks.Remove(a.item); else OpenAttacks.Add(a.item);
                }
                GUILayout.EndHorizontal();
                if (open) DrawAttack(a, va, "a" + i + "_");
                GUILayout.EndVertical();
            }
        }

        private static string SourceLabel(string source)
        {
            if (string.IsNullOrEmpty(source)) return "";
            if (source.StartsWith("набор «") && source.EndsWith("»"))
                return L.F("набор «{0}»", source.Substring(7, source.Length - 8));
            return L.T(source);
        }

        private static string AttackBadges(AttackOverride a)
        {
            if (a.captured < 2) return "";
            var parts = new List<string>();
            if (a.aiTargetType == (int)ItemDrop.ItemData.AiTarget.FriendHurt) parts.Add(L.T("лечит союзников"));
            if (a.aiTargetType == (int)ItemDrop.ItemData.AiTarget.Friend) parts.Add(L.T("на союзников"));
            if (a.aiInMistOnly) parts.Add(L.T("только в тумане"));
            if (a.aiInDungeonOnly) parts.Add(L.T("только в подземелье"));
            if (!a.aiWhenWalking && a.aiWhenFlying) parts.Add(L.T("только в полёте"));
            if (a.aiWhenWalking && !a.aiWhenFlying) parts.Add(L.T("только на земле"));
            if (!a.aiWhenSwiming) parts.Add(L.T("не в воде"));
            return parts.Count == 0 ? "" : " · " + string.Join(", ", parts);
        }

        private static void DrawAttack(AttackOverride a, AttackOverride v, string p)
        {
            if (!string.IsNullOrEmpty(a.animation)) GUILayout.Label(L.T("Анимация: ") + a.animation, GuiKit.Muted);

            GUILayout.Label(L.T("Урон"), GuiKit.SubHeader);
            DamageGrid(a.damage, v.damage, p + "d_");
            GUILayout.Label(L.T("Со звёздами урон атаки растёт: ×(1 + 0,5 · звёзды), и ещё на «Множитель урона атаки» ниже."), GuiKit.Muted);

            a.attackForce = Num(L.T("Отбрасывание"), a.attackForce, v.attackForce, p + "force");
            a.backstabBonus = Num(L.T("Множитель удара в спину"), a.backstabBonus, v.backstabBonus, p + "backstab");
            a.blockable = Flag(L.T("Можно заблокировать"), a.blockable, v.blockable);
            a.dodgeable = Flag(L.T("Можно увернуться"), a.dodgeable, v.dodgeable);

            GUILayout.Label(L.T("Применение ИИ"), GuiKit.SubHeader);
            a.aiAttackRange = Mathf.Max(0f, Num(L.T("Дальность применения"), a.aiAttackRange, v.aiAttackRange, p + "range"));
            a.aiAttackRangeMin = Num(L.T("Мин. дистанция"), a.aiAttackRangeMin, v.aiAttackRangeMin, p + "rangemin");
            a.aiAttackInterval = Mathf.Max(0f, Num(L.T("Перезарядка, с"), a.aiAttackInterval, v.aiAttackInterval, p + "interval"));
            a.aiAttackMaxAngle = Mathf.Clamp(Num(L.T("Макс. угол к цели"), a.aiAttackMaxAngle, v.aiAttackMaxAngle, p + "angle"), 0.1f, 360f);
            a.aiPrioritized = Flag(L.T("Приоритетная атака"), a.aiPrioritized, v.aiPrioritized);
            if (a.captured >= 2)
            {
                if (a.aiInMistOnly || v.aiInMistOnly)
                    a.aiInMistOnly = Flag(L.T("Только в тумане (на арене без тумана не сработает)"), a.aiInMistOnly, v.aiInMistOnly);
                if (a.aiInDungeonOnly || v.aiInDungeonOnly)
                    a.aiInDungeonOnly = Flag(L.T("Только в подземелье (на арене не сработает)"), a.aiInDungeonOnly, v.aiInDungeonOnly);
                if (!string.IsNullOrEmpty(a.statusEffect))
                    a.statusEffectChance = Mathf.Clamp01(Num(L.F("Шанс эффекта «{0}»", a.statusEffect), a.statusEffectChance, v.statusEffectChance, p + "sechance", "0.##"));
            }
            a.aiMinHealthPercentage = Mathf.Clamp01(Num(L.T("Только при HP не ниже доли"), a.aiMinHealthPercentage, v.aiMinHealthPercentage, p + "hpmin"));
            a.aiMaxHealthPercentage = Mathf.Clamp01(Num(L.T("Только при HP не выше доли"), a.aiMaxHealthPercentage, v.aiMaxHealthPercentage, p + "hpmax"));

            GUILayout.Label(L.T("Удар"), GuiKit.SubHeader);
            a.damageMultiplier = Mathf.Max(0f, Num(L.T("Множитель урона атаки"), a.damageMultiplier, v.damageMultiplier, p + "mult"));
            a.forceMultiplier = Mathf.Max(0f, Num(L.T("Множитель отбрасывания"), a.forceMultiplier, v.forceMultiplier, p + "fmult"));
            a.staggerMultiplier = Mathf.Max(0f, Num(L.T("Множитель ошеломления"), a.staggerMultiplier, v.staggerMultiplier, p + "smult"));
            var type = (Attack.AttackType)a.attackType;
            if (a.captured >= 2 && type == Attack.AttackType.Area)
            {
                a.attackRayWidth = Mathf.Max(0.1f, Num(L.T("Радиус удара по площади"), a.attackRayWidth, v.attackRayWidth, p + "arearad"));
                a.attackRange = Mathf.Max(0f, Num(L.T("Центр площади впереди, м"), a.attackRange, v.attackRange, p + "hitrange"));
            }
            else if (a.captured >= 2 && (type == Attack.AttackType.Projectile || type == Attack.AttackType.TriggerProjectile))
            {
                a.attackRange = Mathf.Max(0f, Num(L.T("Точка вылета снаряда впереди, м"), a.attackRange, v.attackRange, p + "hitrange"));
            }
            else
            {
                a.attackRange = Mathf.Max(0f, Num(L.T("Дальность удара"), a.attackRange, v.attackRange, p + "hitrange"));
                a.attackAngle = Mathf.Clamp(Num(L.T("Сектор удара, °"), a.attackAngle, v.attackAngle, p + "hitangle"), 0f, 360f);
            }

            if (a.hasProjectile)
            {
                GUILayout.Label(L.T("Снаряд"), GuiKit.SubHeader);
                a.projectileVel = Mathf.Max(0f, Num(L.T("Скорость"), a.projectileVel, v.projectileVel, p + "pvel"));
                a.projectileVelMin = Mathf.Max(0f, Num(L.T("Мин. скорость"), a.projectileVelMin, v.projectileVelMin, p + "pvelmin"));
                a.projectiles = Mathf.Clamp(NumInt(L.T("Снарядов за выстрел"), a.projectiles, v.projectiles, p + "pcount"), 1, 50);
                a.projectileBursts = Mathf.Clamp(NumInt(L.T("Очередей"), a.projectileBursts, v.projectileBursts, p + "pburst"), 1, 20);
            }

            if (a.effects.Count > 0)
            {
                GUILayout.Label(L.T("Эффекты атаки с собственным уроном"), GuiKit.SubHeader);
                GUILayout.Label(L.T("Площадные удары, взрывы, лужи, осколки. Урон эффекта задаётся в нём самом и не зависит ни от урона атаки выше, ни от звёзд."), GuiKit.Muted);
                foreach (var name in a.effects)
                {
                    var e = _work.FindEffect(name);
                    var ve = _vanilla.FindEffect(name) ?? e;
                    if (e == null) continue;
                    GUILayout.BeginVertical(GuiKit.Box);
                    e.modified = GuiKit.Toggle(L.F("{0} ({1}) — изменить урон", name, L.T(e.kind)), e.modified);
                    if (e.modified)
                    {
                        DamageGrid(e.damage, ve.damage, p + "fx_" + name + "_");
                        e.attackForce = Num(L.T("Отбрасывание"), e.attackForce, ve.attackForce, p + "fx_" + name + "_force");
                    }
                    else
                    {
                        GUILayout.Label(L.T("Урон в игре: ") + DamageSummary(e.damage), GuiKit.Muted);
                    }
                    GUILayout.EndVertical();
                }
            }

            if (GuiKit.Btn(L.T("Вернуть этой атаке ванильные значения")))
            {
                int idx = _work.attacks.IndexOf(a);
                var fresh = v.Clone();
                fresh.enabled = a.enabled;
                if (idx >= 0) _work.attacks[idx] = fresh;
                foreach (var name in fresh.effects)
                {
                    var ve = _vanilla.FindEffect(name);
                    int ei = _work.effects.FindIndex(x => x.prefab == name);
                    if (ve != null && ei >= 0) _work.effects[ei] = ve.Clone();
                }
                GuiKit.ResetBuffers();
            }
        }

        private static void DamageGrid(DamageSet d, DamageSet v, string p)
        {
            int col = 0;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < DamageSet.Labels.Length; i++)
            {
                float val = d.Get(i);
                float van = v.Get(i);
                if (!_showZeroDamage && Mathf.Approximately(val, 0f) && Mathf.Approximately(van, 0f)) continue;
                bool changed = Mathf.Abs(val - van) > 0.0001f;
                GUILayout.Label(changed ? GuiKit.Colored(DamageSet.Labels[i], GuiKit.Accent) : DamageSet.Labels[i], GuiKit.Small, GUILayout.Width(118));
                d.Set(i, Mathf.Max(0f, GuiKit.NumberBox(val, p + i, "0.##", 62f)));
                GUILayout.Space(8);
                if (++col % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            if (col == 0) GUILayout.Label(L.T("нет урона (включите «Показывать нулевые типы», чтобы добавить)"), GuiKit.Muted);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static string DamageSummary(DamageSet d)
        {
            var parts = new List<string>();
            for (int i = 0; i < DamageSet.Labels.Length; i++)
                if (d.Get(i) > 0f) parts.Add(DamageSet.Labels[i].ToLowerInvariant() + " " + d.Get(i).ToString("0.#"));
            return parts.Count == 0 ? L.T("нет") : string.Join(", ", parts);
        }
    }
}
