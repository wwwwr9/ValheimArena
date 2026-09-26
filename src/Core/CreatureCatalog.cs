using System;
using System.Collections.Generic;
using UnityEngine;
using ValheimArena.Profiles;

namespace ValheimArena.Core
{
    public class CreatureEntry
    {
        public string Prefab;
        public string Display;
        /// <summary>Босс по признаку игры (Character.IsBoss → m_boss).</summary>
        public bool Boss;
        /// <summary>Летает всегда (m_flying): появляется в воздухе.</summary>
        public bool Flying;
        /// <summary>Умеет летать: всегда или взлетает и садится сам (MonsterAI.m_randomFly).</summary>
        public bool CanFly;
        /// <summary>Не выходит на сушу (MonsterAI.m_avoidLand): появляется на воде.</summary>
        public bool Swimmer;
        public Character.Faction Faction;
        /// <summary>Как игра выпускает его сама (null — нет в списках появления).</summary>
        public VanillaSpawn Spawn;
        public float Health;
        public GameObject GameObject;
        public string SearchKey;
    }

    /// <summary>Все существа игры (и модов), которых можно выпустить на арену: префабы ZNetScene с Character и ИИ.</summary>
    public static class CreatureCatalog
    {
        private static readonly List<CreatureEntry> Entries = new List<CreatureEntry>();
        private static readonly Dictionary<string, CreatureEntry> ByPrefab = new Dictionary<string, CreatureEntry>();
        private static ZNetScene _builtFor;
        private static string _builtLanguage;

        public static IReadOnlyList<CreatureEntry> All
        {
            get { EnsureBuilt(); return Entries; }
        }

        public static CreatureEntry Get(string prefab)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(prefab)) return null;
            ByPrefab.TryGetValue(prefab, out var e);
            return e;
        }

        public static string DisplayName(string prefab)
        {
            var e = Get(prefab);
            return e != null ? e.Display : prefab;
        }

        public static void Invalidate() => _builtFor = null;

        private static void EnsureBuilt()
        {
            var scene = ZNetScene.instance;
            string lang = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "";
            if (scene == null)
            {
                if (_builtFor != null) { Entries.Clear(); ByPrefab.Clear(); _builtFor = null; }
                return;
            }
            if (_builtFor == scene && _builtLanguage == lang) return;
            _builtFor = scene;
            _builtLanguage = lang;
            Entries.Clear();
            ByPrefab.Clear();
            var vanilla = VanillaSpawns.Collect();
            foreach (var go in scene.m_prefabs)
            {
                if (!go) continue;
                var ch = go.GetComponent<Character>();
                if (!ch || ch is Player) continue;
                var ai = go.GetComponent<BaseAI>();
                if (!ai) continue;
                if (!go.GetComponent<ZNetView>()) continue;
                if (ByPrefab.ContainsKey(go.name)) continue;
                var display = ProfileLogic.Loc(ch.m_name);
                if (string.IsNullOrEmpty(display) || display.StartsWith("$")) display = go.name;
                var monster = ai as MonsterAI;
                var e = new CreatureEntry
                {
                    Prefab = go.name,
                    Display = display,
                    Boss = ch.m_boss,
                    Flying = ch.m_flying,
                    CanFly = ch.m_flying || (monster && monster.m_randomFly),
                    Swimmer = monster && monster.m_avoidLand,
                    Faction = ch.m_faction,
                    Spawn = vanilla.TryGetValue(go.name, out var vs) ? vs : null,
                    Health = ch.m_health,
                    GameObject = go,
                };
                e.SearchKey = (e.Display + " " + e.Prefab).ToLowerInvariant();
                Entries.Add(e);
                ByPrefab[e.Prefab] = e;
            }
            Entries.Sort((a, b) => string.Compare(a.Display, b.Display, StringComparison.CurrentCultureIgnoreCase));
            Factions.Clear();
            foreach (var e in Entries)
                if (!Factions.Contains(e.Faction)) Factions.Add(e.Faction);
            Factions.Sort();
        }

        /// <summary>Фракции, которые есть в каталоге, по порядку перечисления игры.</summary>
        public static IReadOnlyList<Character.Faction> PresentFactions
        {
            get { EnsureBuilt(); return Factions; }
        }

        private static readonly List<Character.Faction> Factions = new List<Character.Faction>();
    }
}
