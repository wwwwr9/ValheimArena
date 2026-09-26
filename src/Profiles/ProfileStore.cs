using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.Profiles
{
    /// <summary>Профили существ на диске: BepInEx\config\ValheimArena\profiles\&lt;префаб&gt;\&lt;имя&gt;.json</summary>
    public static class ProfileStore
    {
        // Ключ — как имя файла: регистр в именах файлов Windows не различает, «Troll/Fast» и «troll/fast» — один профиль.
        private static readonly Dictionary<string, CreatureProfile> Cache = new Dictionary<string, CreatureProfile>(StringComparer.OrdinalIgnoreCase);

        // Списки файлов — для окон, которые рисуются несколько раз за кадр: с диска не чаще раза в 2 с
        // и не посреди кадра (раскладка и отрисовка одного кадра видят один и тот же список).
        private static readonly Dictionary<string, List<string>> ListCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static List<string> _withProfiles;
        private static readonly ListTimer Lists = new ListTimer();

        public static string Root => Path.Combine(ArenaPlugin.DataDir, "profiles");

        private static string Dir(string prefab) => Path.Combine(Root, FileNames.Sanitize(prefab));
        private static string FilePath(string prefab, string name) => Path.Combine(Dir(prefab), FileNames.Sanitize(name) + ".json");
        private static string Key(string prefab, string name) => FileNames.Sanitize(prefab) + "/" + FileNames.Sanitize(name);

        /// <summary>Имена профилей существа (список только для чтения).</summary>
        public static List<string> List(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return new List<string>();
            if (Lists.Expired()) { ListCache.Clear(); _withProfiles = null; }
            if (ListCache.TryGetValue(prefab, out var cached)) return cached;
            var list = new List<string>();
            var dir = Dir(prefab);
            if (Directory.Exists(dir))
                foreach (var f in Directory.GetFiles(dir, "*.json"))
                    list.Add(Path.GetFileNameWithoutExtension(f));
            list.Sort(StringComparer.OrdinalIgnoreCase);
            ListCache[prefab] = list;
            return list;
        }

        /// <summary>Префабы, для которых есть хотя бы один профиль (список только для чтения).</summary>
        public static List<string> PrefabsWithProfiles()
        {
            if (Lists.Expired()) { ListCache.Clear(); _withProfiles = null; }
            if (_withProfiles != null) return _withProfiles;
            var list = new List<string>();
            if (Directory.Exists(Root))
                foreach (var d in Directory.GetDirectories(Root))
                    if (Directory.GetFiles(d, "*.json").Length > 0)
                        list.Add(Path.GetFileName(d));
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return _withProfiles = list;
        }

        private static void InvalidateLists()
        {
            ListCache.Clear();
            _withProfiles = null;
        }

        public static CreatureProfile Load(string prefab, string name)
        {
            if (string.IsNullOrEmpty(prefab) || string.IsNullOrEmpty(name)) return null;
            var key = Key(prefab, name);
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var path = FilePath(prefab, name);
            if (!File.Exists(path)) return null;
            try
            {
                var p = Normalize(JsonIO.FromJson<CreatureProfile>(File.ReadAllText(path)), prefab);
                if (p == null) return null;
                p.prefab = prefab;
                p.name = name;
                Cache[key] = p;
                return p;
            }
            catch (Exception e)
            {
                ArenaPlugin.Log.LogWarning(L.F("Профиль {0} не прочитан: {1}", key, e.Message));
                return null;
            }
        }

        public static void Save(CreatureProfile p)
        {
            if (p == null || string.IsNullOrEmpty(p.prefab) || string.IsNullOrEmpty(p.name)) return;
            p.name = FileNames.Sanitize(p.name);
            Directory.CreateDirectory(Dir(p.prefab));
            File.WriteAllText(FilePath(p.prefab, p.name), JsonIO.ToJson(p, true));
            Cache[Key(p.prefab, p.name)] = p.Clone();
            InvalidateLists();
        }

        public static void Delete(string prefab, string name)
        {
            var path = FilePath(prefab, name);
            if (File.Exists(path)) File.Delete(path);
            Cache.Remove(Key(prefab, name));
            InvalidateLists();
        }

        public static string ToJson(CreatureProfile p) => p == null ? "" : JsonIO.ToJson(p);

        public static CreatureProfile FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var p = JsonIO.FromJson<CreatureProfile>(json);
                return Normalize(p, p?.prefab);
            }
            catch { return null; }
        }

        /// <summary>
        /// Чего нет в тексте — как в игре. Профили, записанные до перехода на Newtonsoft.Json (JsonUtility терял
        /// вложенные классы), приходят без сопротивлений и атак: «все обычные» сняли бы с существа ванильные
        /// иммунитеты, поэтому сопротивления берутся с прототипа, а списки — пустые (атаки остаются ванильными).
        /// </summary>
        private static CreatureProfile Normalize(CreatureProfile p, string prefab)
        {
            if (p == null) return null;
            if (p.resists == null)
            {
                var go = ZNetScene.instance && !string.IsNullOrEmpty(prefab) ? ZNetScene.instance.GetPrefab(prefab) : null;
                var ch = go ? go.GetComponent<Character>() : null;
                p.resists = ch ? ResistSet.From(ch.m_damageModifiers) : new ResistSet();
            }
            if (p.attacks == null) p.attacks = new List<AttackOverride>();
            if (p.effects == null) p.effects = new List<EffectOverride>();
            if (p.weakSpots == null) p.weakSpots = new List<WeakSpotOverride>();
            foreach (var a in p.attacks)
            {
                if (a.damage == null) a.damage = new DamageSet();
                if (a.damagePerLevel == null) a.damagePerLevel = new DamageSet();
                if (a.effects == null) a.effects = new List<string>();
            }
            foreach (var e in p.effects)
                if (e.damage == null) e.damage = new DamageSet();
            p.weakSpots.RemoveAll(w => w == null || w.resists == null);
            return p;
        }
    }
}
