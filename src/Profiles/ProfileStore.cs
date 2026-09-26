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
        private static readonly Dictionary<string, CreatureProfile> Cache = new Dictionary<string, CreatureProfile>();

        public static string Root => Path.Combine(ArenaPlugin.DataDir, "profiles");

        private static string Dir(string prefab) => Path.Combine(Root, FileNames.Sanitize(prefab));
        private static string FilePath(string prefab, string name) => Path.Combine(Dir(prefab), FileNames.Sanitize(name) + ".json");
        private static string Key(string prefab, string name) => prefab + "/" + name;

        public static List<string> List(string prefab)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(prefab)) return list;
            var dir = Dir(prefab);
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.GetFiles(dir, "*.json"))
                list.Add(Path.GetFileNameWithoutExtension(f));
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>Префабы, для которых есть хотя бы один профиль.</summary>
        public static List<string> PrefabsWithProfiles()
        {
            var list = new List<string>();
            if (!Directory.Exists(Root)) return list;
            foreach (var d in Directory.GetDirectories(Root))
                if (Directory.GetFiles(d, "*.json").Length > 0)
                    list.Add(Path.GetFileName(d));
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
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
                var p = JsonUtility.FromJson<CreatureProfile>(File.ReadAllText(path));
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
            File.WriteAllText(FilePath(p.prefab, p.name), JsonUtility.ToJson(p, true));
            Cache[Key(p.prefab, p.name)] = p.Clone();
        }

        public static void Delete(string prefab, string name)
        {
            var path = FilePath(prefab, name);
            if (File.Exists(path)) File.Delete(path);
            Cache.Remove(Key(prefab, name));
        }

        public static string ToJson(CreatureProfile p) => p == null ? "" : JsonUtility.ToJson(p);

        public static CreatureProfile FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<CreatureProfile>(json); }
            catch { return null; }
        }
    }
}
