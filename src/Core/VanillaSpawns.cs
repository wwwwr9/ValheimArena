using System.Collections.Generic;
using UnityEngine;

namespace ValheimArena.Core
{
    /// <summary>Как игра сама выпускает существо: биомы, размер стаи, уровни, время суток, набеги, логова, алтари.</summary>
    public class VanillaSpawn
    {
        public Heightmap.Biome Biomes;
        public int GroupMin = int.MaxValue, GroupMax;
        public int LevelMin = int.MaxValue, LevelMax;
        public bool Day, Night;
        /// <summary>Приходит в набегах (RandEventSystem).</summary>
        public bool Event;
        /// <summary>Выходит из логова (SpawnArea: гнёзда, курганы).</summary>
        public bool Nest;
        /// <summary>Вызывается на алтаре (OfferingBowl).</summary>
        public bool Altar;

        public bool HasGroup => GroupMax > 0;
        public bool HasLevels => LevelMax > 0;
    }

    /// <summary>
    /// Сбор ванильных данных появления из загруженного мира: списки биомов (SpawnSystemList), набеги
    /// (RandEventSystem), логова (SpawnArea), алтари (OfferingBowl). Места локаций (CreatureSpawner) загружаются
    /// вместе с локацией — их здесь может не оказаться.
    /// </summary>
    public static class VanillaSpawns
    {
        public static Dictionary<string, VanillaSpawn> Collect()
        {
            var d = new Dictionary<string, VanillaSpawn>();
            VanillaSpawn Get(GameObject prefab)
            {
                if (!d.TryGetValue(prefab.name, out var v)) d[prefab.name] = v = new VanillaSpawn();
                return v;
            }
            void Merge(SpawnSystem.SpawnData s, bool fromEvent)
            {
                if (s == null || !s.m_prefab || !s.m_enabled || s.m_devDisabled) return;
                var v = Get(s.m_prefab);
                if (fromEvent) v.Event = true;
                else
                {
                    v.Biomes |= s.m_biome;
                    v.Day |= s.m_spawnAtDay;
                    v.Night |= s.m_spawnAtNight;
                }
                v.GroupMin = Mathf.Min(v.GroupMin, Mathf.Max(1, s.m_groupSizeMin));
                v.GroupMax = Mathf.Max(v.GroupMax, Mathf.Max(1, s.m_groupSizeMax));
                v.LevelMin = Mathf.Min(v.LevelMin, Mathf.Max(1, s.m_minLevel));
                v.LevelMax = Mathf.Max(v.LevelMax, Mathf.Max(1, s.m_maxLevel));
            }

            foreach (var list in Resources.FindObjectsOfTypeAll<SpawnSystemList>())
                foreach (var s in list.m_spawners) Merge(s, false);
            if (RandEventSystem.instance)
                foreach (var ev in RandEventSystem.instance.m_events)
                    if (ev != null && ev.m_enabled)
                        foreach (var s in ev.m_spawn) Merge(s, true);
            foreach (var area in Resources.FindObjectsOfTypeAll<SpawnArea>())
                foreach (var s in area.m_prefabs)
                {
                    if (s == null || !s.m_prefab) continue;
                    var v = Get(s.m_prefab);
                    v.Nest = true;
                    v.LevelMin = Mathf.Min(v.LevelMin, Mathf.Max(1, s.m_minLevel));
                    v.LevelMax = Mathf.Max(v.LevelMax, Mathf.Max(1, s.m_maxLevel));
                }
            foreach (var bowl in Resources.FindObjectsOfTypeAll<OfferingBowl>())
                if (bowl.m_bossPrefab) Get(bowl.m_bossPrefab).Altar = true;
            return d;
        }

        public static string BiomeNames(Heightmap.Biome b)
        {
            var parts = new List<string>();
            if ((b & Heightmap.Biome.Meadows) != 0) parts.Add(L.T("Луга"));
            if ((b & Heightmap.Biome.BlackForest) != 0) parts.Add(L.T("Чёрный лес"));
            if ((b & Heightmap.Biome.Swamp) != 0) parts.Add(L.T("Болото"));
            if ((b & Heightmap.Biome.Mountain) != 0) parts.Add(L.T("Горы"));
            if ((b & Heightmap.Biome.Plains) != 0) parts.Add(L.T("Равнины"));
            if ((b & Heightmap.Biome.Mistlands) != 0) parts.Add(L.T("Туманные земли"));
            if ((b & Heightmap.Biome.AshLands) != 0) parts.Add(L.T("Пепельные земли"));
            if ((b & Heightmap.Biome.DeepNorth) != 0) parts.Add(L.T("Глубокий север"));
            if ((b & Heightmap.Biome.Ocean) != 0) parts.Add(L.T("Океан"));
            return string.Join(", ", parts);
        }

        /// <summary>Строка «В игре: …» для редактора группы.</summary>
        public static string Describe(VanillaSpawn v)
        {
            if (v == null) return L.T("В списках появления биомов нет: появляется в особых местах или от других существ.");
            var parts = new List<string>();
            if (v.Biomes != Heightmap.Biome.None) parts.Add(BiomeNames(v.Biomes));
            if (v.Altar) parts.Add(L.T("вызов на алтаре"));
            if (v.Nest) parts.Add(L.T("из логова"));
            if (v.Event) parts.Add(L.T("в набегах"));
            if (v.HasGroup) parts.Add(v.GroupMax <= 1 ? L.T("по одному") : L.F("стая {0}–{1}", v.GroupMin, v.GroupMax));
            if (v.HasLevels) parts.Add(L.F("звёзды {0}–{1}", v.LevelMin - 1, v.LevelMax - 1));
            if (v.Day && !v.Night) parts.Add(L.T("только днём"));
            if (v.Night && !v.Day) parts.Add(L.T("только ночью"));
            return L.T("В игре: ") + string.Join("; ", parts) + ".";
        }

        /// <summary>Группа «как в игре»: размер стаи и звёзды из ванильных данных; боссы с алтаря — по одному.</summary>
        public static void ApplyTo(SpawnGroup g, VanillaSpawn v)
        {
            if (v == null) return;
            g.count = v.HasGroup ? Mathf.Clamp(v.GroupMax, 1, 200) : 1;
            if (v.HasLevels)
            {
                g.starsMin = Mathf.Clamp(v.LevelMin - 1, 0, 20);
                g.starsMax = Mathf.Clamp(v.LevelMax - 1, g.starsMin, 20);
            }
            else
            {
                g.starsMin = 0;
                g.starsMax = 0;
            }
        }
    }
}
