using System.Collections.Generic;
using UnityEngine;
using ValheimArena.Profiles;

namespace ValheimArena.Core
{
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
        public const string ZMaxHealth = "va_maxhp";

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

        public int BehaviorOverride = -1;
        public Vector3 Origin;
        public int Adopted;
        public bool PrefabFlies;
        public float GroundedFor;
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

        public static void TryAttachFromZdo(Character c)
        {
            if (!c || c.IsPlayer()) return;
            var nview = c.GetComponent<ZNetView>();
            if (!nview || !nview.IsValid()) return;
            var zdo = nview.GetZDO();
            if (zdo == null || !zdo.GetBool(ZUnit)) return;
            Attach(c);
        }

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
                NView.Register<string>(RpcSetProfile, RPC_SetProfile);
            }

            if (!ByCharacter.ContainsKey(c))
            {
                ByCharacter[c] = this;
                All.Add(this);
            }
            if (AI) ByAI[AI] = this;

            c.m_faction = FactionOverride >= 0 ? (Character.Faction)FactionOverride : OriginalFaction;

            c.m_defeatSetGlobalKey = "";
            c.m_dreamCinematic = "";

            SyncProfileFromZdo(force: true);

            if (_started) ApplyArenaBehaviour();
            TryApplyItems();
        }

        private void Start()
        {
            _started = true;
            ApplyArenaBehaviour();
            if (!ItemsApplied) TryApplyItems();
        }

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
            if (_started) ApplyArenaBehaviour();
        }

        public void ApplyArenaBehaviour()
        {
            var r = Rules;
            _behaviourVersion = Arena.SessionsVersion;
            var drop = GetComponent<CharacterDrop>();
            bool loot = r != null && r.dropLoot;
            if (drop) drop.SetDropsEnabled(loot);
            if (Character && Character.m_deathEffects != null && _originalDeathEffects != null)
                Character.m_deathEffects.m_effectPrefabs = loot ? _originalDeathEffects : WithoutLoot(_originalDeathEffects);
            if (r == null) return;
            var behavior = Behavior;

            if (AI)
            {
                float need = r.radius * 2.2f + 10f;
                AI.m_viewRange = behavior == ArenaBehavior.Free ? BaseViewRange : Mathf.Max(BaseViewRange, need);
                Reflect.SpawnPoint(AI) = Origin;
                if (IsOwner)
                {
                    AI.SetHuntPlayer(false);
                    if (Character.IsTamed() && behavior != ArenaBehavior.Free) AI.ResetPatrolPoint();
                    else Reflect.SetPatrolPoint(AI, Origin);
                }
            }
            if (Monster)
            {
                Monster.m_attackPlayerObjects = !r.noBuildingAttacks && _originalAttackPlayerObjects;
                Monster.m_fleeIfHurtWhenTargetCantBeReached = !r.noFlee && _originalFleeIfHurt;
                Monster.m_fleeIfNotAlerted = !r.noFlee && _originalFleeIfNotAlerted;
                Monster.m_fleeIfLowHealth = r.noFlee ? 0f
                    : Profile != null && Profile.hasMonsterAI ? Profile.fleeIfLowHealth : _originalFleeIfLowHealth;
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

        public void Release()
        {
            if (IsOwner)
            {
                var zdo = NView.GetZDO();
                zdo.Set(ZUnit, false);
                zdo.Set(ZSession, "");
                if (AI) AI.ResetPatrolPoint();
            }
            if (Character)
            {
                Character.m_faction = OriginalFaction;
                Character.m_defeatSetGlobalKey = _originalDefeatKey;
                Character.m_dreamCinematic = _originalDreamCinematic;
            }
            if (AI) AI.m_viewRange = BaseViewRange;
            if (Monster) Monster.m_maxChaseDistance = _originalMaxChase;
            var drop = GetComponent<CharacterDrop>();
            if (drop) drop.SetDropsEnabled(true);
            Destroy(this);
        }

        private void OnDestroy()
        {
            if ((object)Character != null) ByCharacter.Remove(Character);
            if ((object)AI != null) ByAI.Remove(AI);
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
