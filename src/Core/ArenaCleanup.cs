using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimArena.Core
{
    [Flags]
    public enum ClearWhat
    {
        None = 0,
        Creatures = 1,
        Items = 2,
        Tombstones = 4,
        Petrified = 8,
        Trees = 16,
        All = Creatures | Items | Tombstones | Petrified | Trees
    }

    public static class ArenaCleanup
    {
        private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances =
            AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");

        private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> ObjectsById =
            AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");

        private static readonly int UnitHash = ArenaUnit.ZUnit.GetStableHashCode();
        private static readonly int SessionHash = ArenaUnit.ZSession.GetStableHashCode();

        private const string PetrifiedName = "$piece_deposit_goldvein";

        private static readonly HashSet<string> PetrifiedPrefabs = new HashSet<string>
        {
            "TrollFrost_Dead", "TrollFrost_Frac", "TrollFrost_Frac_arm", "TrollFrost_Frac_legs",
            "DN_gammeltrollFrac01", "DN_gammeltrollFrac02"
        };

        public static ClearWhat FromSettings(ArenaSettings s)
        {
            var w = ClearWhat.None;
            if (s.clearCreatures) w |= ClearWhat.Creatures;
            if (s.clearItems) w |= ClearWhat.Items;
            if (s.clearTombstones) w |= ClearWhat.Tombstones;
            if (s.clearPetrified) w |= ClearWhat.Petrified;
            if (s.clearTrees) w |= ClearWhat.Trees;
            return w;
        }

        public static void ToSettings(ArenaSettings s, ClearWhat w)
        {
            s.clearCreatures = (w & ClearWhat.Creatures) != 0;
            s.clearItems = (w & ClearWhat.Items) != 0;
            s.clearTombstones = (w & ClearWhat.Tombstones) != 0;
            s.clearPetrified = (w & ClearWhat.Petrified) != 0;
            s.clearTrees = (w & ClearWhat.Trees) != 0;
        }

        public static string Describe(ClearWhat w)
        {
            if (w == ClearWhat.None) return L.T("ничего");
            if (w == ClearWhat.All) return L.T("всё");
            var parts = new List<string>();
            if ((w & ClearWhat.Creatures) != 0) parts.Add(L.T("существа"));
            if ((w & ClearWhat.Items) != 0) parts.Add(L.T("ресурсы"));
            if ((w & ClearWhat.Tombstones) != 0) parts.Add(L.T("надгробия"));
            if ((w & ClearWhat.Petrified) != 0) parts.Add(L.T("окаменевшие гаммельтролли"));
            if ((w & ClearWhat.Trees) != 0) parts.Add(L.T("упавшие деревья"));
            return string.Join(", ", parts);
        }

        private static ClearWhat Classify(ZNetView nview, ZDO zdo)
        {
            var go = nview.gameObject;
            if (go.GetComponent<Character>()) return ClearWhat.None;
            if (go.GetComponent<Ragdoll>()) return ClearWhat.Creatures;
            if (go.GetComponent<TombStone>()) return ClearWhat.Tombstones;
            if (go.GetComponent<TreeLog>()) return ClearWhat.Trees;
            if (IsPetrified(go)) return ClearWhat.Petrified;
            if (go.GetComponent<ItemDrop>() && !zdo.GetBool(ZDOVars.s_piece)) return ClearWhat.Items;
            return ClearWhat.None;
        }

        private static bool IsPetrified(GameObject go, int depth = 0)
        {
            string prefab = Utils.GetPrefabName(go);
            if (PetrifiedPrefabs.Contains(prefab) || prefab.StartsWith("TrollFrost_Frac", StringComparison.Ordinal) ||
                prefab.IndexOf("gammeltroll", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            var mr5 = go.GetComponent<MineRock5>();
            if (mr5 && mr5.m_name == PetrifiedName) return true;
            var mr = go.GetComponent<MineRock>();
            if (mr && mr.m_name == PetrifiedName) return true;
            var hover = go.GetComponent<HoverText>();
            if (hover && hover.m_text == PetrifiedName) return true;
            var d = go.GetComponent<Destructible>();
            return depth == 0 && d && d.m_spawnWhenDestroyed && IsPetrified(d.m_spawnWhenDestroyed, 1);
        }

        public static Dictionary<ClearWhat, int> Run(ArenaSession s, ClearWhat what, bool claimOthers)
        {
            var counts = new Dictionary<ClearWhat, int>();
            if (s == null || what == ClearWhat.None || !ZNetScene.instance) return counts;

            if ((what & ClearWhat.Creatures) != 0)
            {
                int bosses = 0;
                if (claimOthers && ZDOMan.instance != null)
                {
                    long me = ZDOMan.GetSessionID();
                    foreach (var zdo in new List<ZDO>(ObjectsById(ZDOMan.instance).Values))
                    {
                        if (!zdo.IsValid() || !zdo.GetBool(UnitHash) || zdo.GetString(SessionHash) != s.Id) continue;
                        if (ZNetScene.instance.FindInstance(zdo)) continue;
                        if (zdo.GetBool(ZDOVars.s_bossCount)) bosses++;
                        zdo.SetOwner(me);
                        ZDOMan.instance.DestroyZDO(zdo);
                        Bump(counts, ClearWhat.Creatures);
                    }
                }

                foreach (var u in new List<ArenaUnit>(ArenaUnit.All))
                {
                    if (!u || u.SessionId != s.Id) continue;
                    if (!u.IsOwner && !claimOthers) continue;
                    if (u.NView && u.NView.IsValid() && u.NView.GetZDO().GetBool(ZDOVars.s_bossCount)) bosses++;
                    Arena.DespawnUnit(u, adjustBossKey: false);
                    Bump(counts, ClearWhat.Creatures);
                }
                Arena.ReleaseActiveBosses(bosses);
            }

            var map = Instances(ZNetScene.instance);
            if (map == null) return counts;
            foreach (var nview in new List<ZNetView>(map.Values))
            {
                if (!nview || !nview.IsValid()) continue;
                var zdo = nview.GetZDO();
                if (zdo == null || !s.IsInside(nview.transform.position, 2f)) continue;
                var kind = Classify(nview, zdo);
                if (kind == ClearWhat.None || (what & kind) == 0) continue;
                if (!zdo.IsOwner())
                {
                    if (!claimOthers) continue;
                    nview.ClaimOwnership();
                }
                ZNetScene.instance.Destroy(nview.gameObject);
                Bump(counts, kind);
            }
            return counts;
        }

        private static void Bump(Dictionary<ClearWhat, int> d, ClearWhat k)
        {
            d.TryGetValue(k, out var n);
            d[k] = n + 1;
        }

        public static string Report(Dictionary<ClearWhat, int> c)
        {
            var parts = new List<string>();
            void Add(ClearWhat k, string name) { if (c.TryGetValue(k, out var n) && n > 0) parts.Add($"{name} {n}"); }
            Add(ClearWhat.Creatures, L.T("существ и трупов"));
            Add(ClearWhat.Items, L.T("ресурсов"));
            Add(ClearWhat.Tombstones, L.T("надгробий"));
            Add(ClearWhat.Petrified, L.T("гаммельтроллей"));
            Add(ClearWhat.Trees, L.T("стволов"));
            return parts.Count == 0 ? L.T("убирать было нечего") : string.Join(", ", parts);
        }
    }
}
