using System.Collections.Generic;
using UnityEngine;
using ValheimArena.Profiles;

namespace ValheimArena.Core
{
    /// <summary>
    /// Боец арены. Вешается на существо при появлении; всё, что нужно после перезагрузки мира или на другом
    /// клиенте, лежит в ZDO (ключи va_*), поэтому компонент восстанавливается из Character.Awake у любого игрока с модом,
    /// а метка, поставленная уже после появления существа, подхватывается ежесекундной сверкой (<see cref="SyncWithZdo"/>).
    /// Правила берутся из сессии его арены (<see cref="Session"/>), а не из локальных настроек клиента.
    /// </summary>
    public class ArenaUnit : MonoBehaviour
    {
        public const string ZUnit = "va_unit";
        public const string ZTeam = "va_team";
        public const string ZGroup = "va_group";
        public const string ZFaction = "va_faction";
        public const string ZProfile = "va_profile";
        public const string ZSession = "va_session";
        public const string ZPrefab = "va_prefab";
        public const string ZHost = "va_host";
        public const string ZCreator = "va_creator";
        public const string ZBehavior = "va_behavior";
        public const string ZOrigin = "va_origin";
        public const string ZAdopted = "va_adopted";
        /// <summary>Максимум HP бойца по профилю и уровню (см. ProfileLogic.RecalculateHealth).</summary>
        public const string ZMaxHealth = "va_maxhp";
        /// <summary>Флаги мира вовлечённого существа до арены: 1 — охотится на игроков, 2 — исчезает днём, 4 — из набега.</summary>
        public const string ZWorldFlags = "va_wflags";

        /// <summary>Как боец попал на арену.</summary>
        public const int Spawned = 0, Summoned = 1, Intruder = 2;

        private const string RpcSetProfile = "VA_SetProfile";

        public static readonly List<ArenaUnit> All = new List<ArenaUnit>();
        private static readonly Dictionary<Character, ArenaUnit> ByCharacter = new Dictionary<Character, ArenaUnit>();
        private static readonly Dictionary<BaseAI, ArenaUnit> ByAI = new Dictionary<BaseAI, ArenaUnit>();

        public Character Character;
        public ZNetView NView;
        public BaseAI AI;
        public MonsterAI Monster;
        public string PrefabName = "";
        public int Team = 1;
        public int Group;
        public int FactionOverride = -1;
        public string SessionId = "";
        public long HostPlayerId;
        public long CreatorPlayerId;
        public string Key = "";
        public CreatureProfile Profile;
        public Vector3 BaseScale = Vector3.one;
        public Character.Faction OriginalFaction;

        public float DamageDealtMultiplier = 1f;
        public float DamageTakenMultiplier = 1f;
        public readonly HashSet<string> DisabledAttacks = new HashSet<string>();
        public readonly Dictionary<string, EffectOverride> Effects = new Dictionary<string, EffectOverride>();
        public readonly Dictionary<ItemDrop.ItemData, ItemDrop.ItemData.SharedData> OriginalShared =
            new Dictionary<ItemDrop.ItemData, ItemDrop.ItemData.SharedData>();

        public int SerialNumber;
        public bool ItemsApplied;
        public float OrphanSince = -1f;
        public bool AlertPending;

        /// <summary>-1 — поведение арены, иначе <see cref="ArenaBehavior"/> группы.</summary>
        public int BehaviorOverride = -1;
        /// <summary>Место появления — «алтарь» бойца: вокруг него он бродит без цели, от него считается поводок.</summary>
        public Vector3 Origin;
        public int Adopted;
        /// <summary>Летает по прототипу (Character.m_flying у свежего экземпляра).</summary>
        public bool PrefabFlies;
        public float GroundedFor;
        /// <summary>Дальность зрения по прототипу/профилю — к ней возвращается «свободное» поведение.</summary>
        public float BaseViewRange;
        private float _originalMaxChase;
        private bool _originalFleeIfHurt, _originalFleeIfNotAlerted, _originalAttackPlayerObjects;
        private float _originalFleeIfLowHealth;
        private float _originalFallAsleepDistance;
        private EffectList.EffectData[] _originalDeathEffects;
        private string _originalDefeatKey = "";
        private string _originalDreamCinematic = "";

        private string _appliedProfileJson;
        private int _behaviourVersion = -1;
        private ArenaSession _session;
        private int _sessionVersion = -1;
        private bool _started;

        public string DisplayName => Character ? ProfileLogic.Loc(Character.m_name) : PrefabName;

        public int Stars => Character ? Mathf.Max(0, Character.GetLevel() - 1) : 0;

        public bool Alive => Character && !Character.IsDead() && NView && NView.IsValid();

        public bool IsOwner => NView && NView.IsValid() && NView.IsOwner();

        /// <summary>Сессия арены бойца (кэш сбрасывается при любом изменении списка сессий).</summary>
        public ArenaSession Session
        {
            get
            {
                if (_sessionVersion != Arena.SessionsVersion)
                {
                    _session = Arena.FindSession(SessionId);
                    _sessionVersion = Arena.SessionsVersion;
                }
                return _session;
            }
        }

        public ArenaRules Rules => Session?.Rules;

        public ArenaBehavior Behavior
        {
            get
            {
                if (BehaviorOverride >= 0) return (ArenaBehavior)BehaviorOverride;
                var r = Rules;
                return r != null ? (ArenaBehavior)r.behavior : ArenaBehavior.Free;
            }
        }

        public static ArenaUnit GetByAI(BaseAI ai)
        {
            if ((object)ai == null) return null;
            ByAI.TryGetValue(ai, out var u);
            return u;
        }

        public UnitStat Stat
        {
            get
            {
                var s = Session;
                if (s == null) return null;
                s.Stats.TryGetValue(Key, out var st);
                return st;
            }
        }

        public static ArenaUnit Get(Character c)
        {
            if ((object)c == null) return null;
            ByCharacter.TryGetValue(c, out var u);
            return u;
        }

        /// <summary>
        /// Раз в секунду из Arena: сверить компоненты бойцов с метками в ZDO. Метку ставит владелец существа
        /// (вовлечь чужака, принять призыв), а из Character.Awake компонент получают только те, кто уже пришёл
        /// с меткой; боец, вовлечённый другим игроком, получает компонент здесь, вместе с табличкой, командой,
        /// правилами арены и учётом в итоге. Снятая владельцем метка (отпущенный чужак) так же снимает компонент у всех.
        /// </summary>
        public static void SyncWithZdo()
        {
            foreach (var c in Character.GetAllCharacters())
            {
                if (!c || c.IsPlayer() || ByCharacter.ContainsKey(c)) continue;
                TryAttachFromZdo(c);
            }
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var u = All[i];
                if (!u || u.IsOwner || !u.NView || !u.NView.IsValid()) continue;
                if (!u.NView.GetZDO().GetBool(ZUnit)) u.Release();
            }
        }

        /// <summary>Из Character.Awake и сверки: если в ZDO есть метка арены — восстановить бойца.</summary>
        public static void TryAttachFromZdo(Character c)
        {
            if (!c || c.IsPlayer()) return;
            var nview = c.GetComponent<ZNetView>();
            if (!nview || !nview.IsValid()) return;
            var zdo = nview.GetZDO();
            if (zdo == null || !zdo.GetBool(ZUnit)) return;
            Attach(c);
        }

        /// <summary>Создать/обновить компонент по данным ZDO и применить настройки арены и профиля.</summary>
        public static ArenaUnit Attach(Character c)
        {
            var unit = c.GetComponent<ArenaUnit>();
            bool fresh = unit == null;
            if (fresh) unit = c.gameObject.AddComponent<ArenaUnit>();
            unit.Init(c, fresh);
            return unit;
        }

        private void Init(Character c, bool fresh)
        {
            Character = c;
            NView = c.GetComponent<ZNetView>();
            AI = c.GetComponent<BaseAI>();
            Monster = AI as MonsterAI;
            var zdo = NView.GetZDO();
            PrefabName = zdo.GetString(ZPrefab, Utils.GetPrefabName(c.gameObject));
            // Команда 0 — посторонние, вовлечённые в бой (враги всем командам).
            Team = Mathf.Clamp(zdo.GetInt(ZTeam, 1), 0, Hostility.TeamCount);
            Group = zdo.GetInt(ZGroup, 0);
            FactionOverride = zdo.GetInt(ZFaction, -1);
            SessionId = zdo.GetString(ZSession, "");
            HostPlayerId = zdo.GetLong(ZHost, 0L);
            CreatorPlayerId = zdo.GetLong(ZCreator, 0L);
            BehaviorOverride = zdo.GetInt(ZBehavior, -1);
            Adopted = zdo.GetInt(ZAdopted, Spawned);
            Origin = zdo.GetVec3(ZOrigin, c.transform.position);
            Key = zdo.m_uid.ToString();
            SerialNumber = (int)(zdo.m_uid.ID % 10000u);
            _sessionVersion = -1;
            _behaviourVersion = -1;
            if (fresh)
            {
                BaseScale = transform.localScale;
                OriginalFaction = c.m_faction;
                PrefabFlies = c.m_flying;
                _originalDefeatKey = c.m_defeatSetGlobalKey ?? "";
                _originalDreamCinematic = c.m_dreamCinematic ?? "";
                BaseViewRange = AI ? AI.m_viewRange : 0f;
                _originalMaxChase = Monster ? Monster.m_maxChaseDistance : 0f;
                if (Monster)
                {
                    _originalFleeIfHurt = Monster.m_fleeIfHurtWhenTargetCantBeReached;
                    _originalFleeIfNotAlerted = Monster.m_fleeIfNotAlerted;
                    _originalFleeIfLowHealth = Monster.m_fleeIfLowHealth;
                    _originalAttackPlayerObjects = Monster.m_attackPlayerObjects;
                    _originalFallAsleepDistance = Monster.m_fallAsleepDistance;
                }
                _originalDeathEffects = c.m_deathEffects?.m_effectPrefabs;
                // Смену профиля с чужого клиента выполняет владелец существа (он же пишет ZDO). Регистрация живёт
                // на ZNetView, а не на компоненте: у вовлечённого повторно чужака прежнюю снимаем перед новой.
                NView.Unregister(RpcSetProfile);
                NView.Register<string>(RpcSetProfile, RPC_SetProfile);
            }

            if (!ByCharacter.ContainsKey(c))
            {
                ByCharacter[c] = this;
                All.Add(this);
            }
            if (AI) ByAI[AI] = this;

            c.m_faction = FactionOverride >= 0 ? (Character.Faction)FactionOverride : OriginalFaction;

            // Бойцы арены не двигают прогресс мира и не выдают катсцены — у любого клиента, который ими владеет.
            c.m_defeatSetGlobalKey = "";
            c.m_dreamCinematic = "";

            // Character.Awake уже выставил максимум HP по ванильному m_health — пересчитываем с сохранением доли.
            SyncProfileFromZdo(force: true);

            // При восстановлении из Character.Awake компоненты ИИ ещё не прошли Awake — поведение настраивается в Start.
            if (_started) ApplyArenaBehaviour();
            TryApplyItems();
        }

        private void Start()
        {
            _started = true;
            ApplyArenaBehaviour();
            if (!ItemsApplied) TryApplyItems();
        }

        /// <summary>Профиль в ZDO поменялся (его сменил владелец) — применить у себя: размер, HP, атаки.</summary>
        public void SyncProfileFromZdo(bool force)
        {
            if (!NView || !NView.IsValid()) return;
            string json = NView.GetZDO().GetString(ZProfile, "");
            if (!force && ReferenceEquals(json, _appliedProfileJson)) return;
            if (!force && json == _appliedProfileJson) { _appliedProfileJson = json; return; }
            _appliedProfileJson = json;
            var p = ProfileStore.FromJson(json);
            if (p != null)
            {
                Profile = p;
                ProfileLogic.ApplyBase(this, p, keepHealthFraction: true);
                if (ItemsApplied) ProfileLogic.ApplyItems(this, p);
            }
            else if (Profile != null)
            {
                ResetToVanilla();
            }
            // Профиль переписал зрение и скорости — вернуть поправки режима поведения.
            if (_started) ApplyArenaBehaviour();
        }

        /// <summary>
        /// Поведение по правилам арены: не бежать, не ломать постройки, не спать; режим «идёт к врагам / стоит /
        /// свободно». Повторяется при смене правил.
        /// </summary>
        public void ApplyArenaBehaviour()
        {
            var r = Rules;
            _behaviourVersion = Arena.SessionsVersion;
            // Добыча бойца — только по правилу арены; пока правила не пришли, её нет.
            var drop = GetComponent<CharacterDrop>();
            bool loot = r != null && r.dropLoot;
            if (drop) drop.SetDropsEnabled(loot);
            // Без добычи — и без того, что выпадает при гибели эффектом: ресурсы (статуя-жила гаммельтролля), предметы.
            if (Character && Character.m_deathEffects != null && _originalDeathEffects != null)
                Character.m_deathEffects.m_effectPrefabs = loot ? _originalDeathEffects : WithoutLoot(_originalDeathEffects);
            if (r == null) return;
            var behavior = Behavior;

            if (AI)
            {
                // Свободное поведение — со своим зрением; остальные видят арену целиком.
                float need = r.radius * 2.2f + 10f;
                AI.m_viewRange = behavior == ArenaBehavior.Free ? BaseViewRange : Mathf.Max(BaseViewRange, need);
                // «Алтарь» бойца — место его появления: вокруг него он бродит без цели, от него считается поводок.
                Reflect.SpawnPoint(AI) = Origin;
                if (IsOwner)
                {
                    AI.SetHuntPlayer(false);
                    // Приручённый с точкой патруля бросает цель дальше m_alertRange от неё (MonsterAI) — в бою без патруля.
                    if (Character.IsTamed() && behavior != ArenaBehavior.Free) AI.ResetPatrolPoint();
                    else Reflect.SetPatrolPoint(AI, Origin);
                }
            }
            if (Monster)
            {
                // Выключенное правило возвращает ванильное (или профильное) поведение, а не оставляет прежнее.
                Monster.m_attackPlayerObjects = !r.noBuildingAttacks && _originalAttackPlayerObjects;
                Monster.m_fleeIfHurtWhenTargetCantBeReached = !r.noFlee && _originalFleeIfHurt;
                Monster.m_fleeIfNotAlerted = !r.noFlee && _originalFleeIfNotAlerted;
                Monster.m_fleeIfLowHealth = r.noFlee ? 0f
                    : Profile != null && Profile.hasMonsterAI ? Profile.fleeIfLowHealth : _originalFleeIfLowHealth;
                // Поводок боссов (m_maxChaseDistance) остаётся только у свободного поведения — как у алтаря в игре.
                Monster.m_maxChaseDistance = behavior == ArenaBehavior.Free ? _originalMaxChase : 0f;
                Monster.m_fallAsleepDistance = r.wakeSleepers ? 0f : _originalFallAsleepDistance;
                if (IsOwner)
                {
                    Monster.SetDespawnInDay(false);
                    Monster.SetEventCreature(false);
                    if (r.wakeSleepers && Monster.IsSleeping()) Reflect.MonsterWakeup(Monster);
                }
            }
        }

        private static EffectList.EffectData[] WithoutLoot(EffectList.EffectData[] effects)
        {
            var list = new List<EffectList.EffectData>();
            foreach (var e in effects)
            {
                var go = e?.m_prefab;
                if (go && (go.GetComponent<ItemDrop>() || go.GetComponent<Destructible>() || go.GetComponent<MineRock>() || go.GetComponent<MineRock5>()))
                    continue;
                list.Add(e);
            }
            return list.ToArray();
        }

        /// <summary>Раз в секунду из Arena: догнать смену правил и профиля.</summary>
        public void Refresh()
        {
            if (_started && _behaviourVersion != Arena.SessionsVersion) ApplyArenaBehaviour();
            SyncProfileFromZdo(force: false);
        }

        public void TryApplyItems()
        {
            if (!(Character is Humanoid h)) return;
            var inv = h.GetInventory();
            if (inv == null || inv.GetAllItems().Count == 0) return;
            ProfileLogic.ApplyItems(this, Profile);
            ItemsApplied = true;
        }

        /// <summary>
        /// Сменить профиль живому бойцу (из редактора). null — вернуть ванильные параметры.
        /// Чужому бойцу команда уходит владельцу; остальные клиенты подхватят профиль из ZDO.
        /// </summary>
        public void SetProfile(CreatureProfile p)
        {
            string json = p != null ? ProfileStore.ToJson(p) : "";
            if (!IsOwner)
            {
                if (NView && NView.IsValid()) NView.InvokeRPC(RpcSetProfile, json);
                return;
            }
            ApplyProfileAsOwner(json);
        }

        private void RPC_SetProfile(long sender, string json)
        {
            if (IsOwner) ApplyProfileAsOwner(json);
        }

        private void ApplyProfileAsOwner(string json)
        {
            NView.GetZDO().Set(ZProfile, json ?? "");
            SyncProfileFromZdo(force: true);
            ApplyArenaBehaviour();
        }

        private void ResetToVanilla()
        {
            var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(PrefabName) : null;
            var vanilla = prefab ? ProfileLogic.Capture(prefab) : null;
            Profile = null;
            if (vanilla != null) ProfileLogic.ApplyBase(this, vanilla, keepHealthFraction: true);
            DamageDealtMultiplier = 1f;
            DamageTakenMultiplier = 1f;
            Effects.Clear();
            DisabledAttacks.Clear();
            ProfileLogic.RestoreAllItems(this);
        }

        public bool IsAttackDisabled(ItemDrop.ItemData item)
        {
            if (DisabledAttacks.Count == 0 || item == null) return false;
            return DisabledAttacks.Contains(ProfileLogic.ItemKey(item));
        }

        /// <summary>
        /// Флаги мира, которые арена у бойца снимает (охота на игроков, исчезновение днём, набег) — запомнить
        /// до вовлечения, чтобы отпущенный чужак снова стал ночным волком или участником набега, а не остался навсегда.
        /// </summary>
        public static void RememberWorldFlags(Character c, ZDO zdo)
        {
            var ai = c.GetComponent<BaseAI>();
            var m = ai as MonsterAI;
            int flags = (ai && ai.HuntPlayer() ? 1 : 0) | (m && m.DespawnInDay() ? 2 : 0) | (m && m.IsEventCreature() ? 4 : 0);
            zdo.Set(ZWorldFlags, flags);
        }

        /// <summary>
        /// Отпустить вовлечённого постороннего (арена исчезла): снять метку арены (это делает владелец, остальные
        /// клиенты снимают компонент по метке), вернуть фракцию, ключ победы, флаги мира, поведение и добычу.
        /// </summary>
        public void Release()
        {
            if (IsOwner)
            {
                var zdo = NView.GetZDO();
                zdo.Set(ZUnit, false);
                zdo.Set(ZSession, "");
                zdo.Set(ZProfile, "");
                if (AI) AI.ResetPatrolPoint();
                int flags = zdo.GetInt(ZWorldFlags, -1);
                if (flags >= 0)
                {
                    if (AI) AI.SetHuntPlayer((flags & 1) != 0);
                    if (Monster)
                    {
                        Monster.SetDespawnInDay((flags & 2) != 0);
                        Monster.SetEventCreature((flags & 4) != 0);
                    }
                }
            }
            // Профиль, применённый к чужаку из редактора, не уходит с ним в мир.
            if (Profile != null && Character) ResetToVanilla();
            if (Character)
            {
                Character.m_faction = OriginalFaction;
                Character.m_defeatSetGlobalKey = _originalDefeatKey;
                Character.m_dreamCinematic = _originalDreamCinematic;
                if (Character.m_deathEffects != null && _originalDeathEffects != null)
                    Character.m_deathEffects.m_effectPrefabs = _originalDeathEffects;
            }
            if (AI) AI.m_viewRange = BaseViewRange;
            if (Monster)
            {
                Monster.m_maxChaseDistance = _originalMaxChase;
                Monster.m_fleeIfHurtWhenTargetCantBeReached = _originalFleeIfHurt;
                Monster.m_fleeIfNotAlerted = _originalFleeIfNotAlerted;
                Monster.m_fleeIfLowHealth = _originalFleeIfLowHealth;
                Monster.m_attackPlayerObjects = _originalAttackPlayerObjects;
                Monster.m_fallAsleepDistance = _originalFallAsleepDistance;
            }
            var drop = GetComponent<CharacterDrop>();
            if (drop) drop.SetDropsEnabled(true);
            Destroy(this);
        }

        private void OnDestroy()
        {
            // Ключ — та же C#-ссылка, поэтому удаление работает и для уже уничтоженного Character.
            if ((object)Character != null) ByCharacter.Remove(Character);
            if ((object)AI != null) ByAI.Remove(AI);
            if (NView && NView.IsValid()) NView.Unregister(RpcSetProfile);
            All.Remove(this);
            Arena.OnUnitRemoved(this);
        }

        public static void PruneDead()
        {
            for (int i = All.Count - 1; i >= 0; i--)
                if (!All[i]) All.RemoveAt(i);
        }
    }
}
