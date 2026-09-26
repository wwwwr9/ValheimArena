using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ValheimArena.Core
{
    public enum HostilityMode { Teams = 0, Factions = 1, FreeForAll = 2 }
    public enum BoundaryShape { Circle = 0, Square = 1 }
    public enum BoundaryMode { None = 0, Wall = 1, ReturnToCenter = 2 }
    public enum CollisionMode { Normal = 0, ThroughCreatures = 1, Disabled = 2 }
    public enum Placement { TeamSide = 0, Random = 1, Center = 2, CameraAim = 3 }
    public enum FollowStyle { Orbit = 0, Chase = 1, TopDown = 2 }

    /// <summary>Поведение бойцов в бою.</summary>
    public enum ArenaBehavior
    {
        /// <summary>Всегда знает, где враги на арене, и идёт к ним.</summary>
        Hunt = 0,
        /// <summary>Стоит на месте: разворачивается и бьёт тех, кто подошёл на дистанцию атаки.</summary>
        Hold = 1,
        /// <summary>Как в игре: видит своими глазами, бродит у точки появления, боссы держатся «алтаря».</summary>
        Free = 2
    }

    /// <summary>Что делать с существами, которые не выпущены на арену, но оказались внутри границы.</summary>
    public enum IntruderPolicy { Ignore = 0, Involve = 1, Remove = 2 }

    /// <summary>Группа в составе арены: какой прототип, сколько, каких звёзд, за какую сторону.</summary>
    [Serializable]
    public class SpawnGroup
    {
        public string prefab = "";
        public int count = 1;
        public int starsMin;
        public int starsMax;
        public int team = 1;
        /// <summary>-1 — фракция прототипа.</summary>
        public int factionOverride = -1;
        /// <summary>Имя профиля из редактора существ; пусто — ванильные параметры.</summary>
        public string profile = "";
        public int placement = (int)Placement.TeamSide;
        public float spread = 4f;
        /// <summary>-1 — как у арены, иначе <see cref="ArenaBehavior"/>.</summary>
        public int behavior = -1;

        public SpawnGroup Clone() => (SpawnGroup)MemberwiseClone();
    }

    /// <summary>Все настраиваемые параметры арены. Сохраняются в settings.json, сценарии — копии этого объекта.</summary>
    [Serializable]
    public class ArenaSettings
    {
        // --- Границы
        public float radius = 30f;
        public int shape = (int)BoundaryShape.Circle;
        public int boundaryMode = (int)BoundaryMode.Wall;
        public bool showBoundary = true;
        public bool boundaryAffectsPlayer;
        /// <summary>Потолок над землёй для летающих; 0 — без потолка.</summary>
        public float heightLimit;
        /// <summary>Крейсерская высота летунов над землёй; 0 — по существу и его атакам.</summary>
        public float flyHeight;

        // --- Столкновения
        public int hostilityMode = (int)HostilityMode.Teams;
        public bool[] teamMatrix = new bool[Hostility.TeamCount * Hostility.TeamCount];
        public bool[] factionMatrix = new bool[0];
        public bool friendlyFire;
        public bool aoeHitsSameKind = true;
        /// <summary>Урон на арене не зависит от числа игроков рядом (игра меняет его в радиусе 100 м).</summary>
        public bool ignorePlayerScaling = true;
        public bool isolateOutsiders = true;
        /// <summary>0 — игрок вне команд (враг всем, если существа на него реагируют).</summary>
        public int playerTeam;

        /// <summary>Сетевая игра: гости могут начинать/останавливать бой, очищать арену и выпускать своих бойцов.</summary>
        public bool allowGuests = true;

        // --- Поведение бойцов
        public int behavior = (int)ArenaBehavior.Hunt;
        public int intruders = (int)IntruderPolicy.Involve;
        public bool noFlee = true;
        public bool noBuildingAttacks = true;
        public bool wakeSleepers = true;
        public bool dropLoot;
        public bool cleanupOrphans = true;

        // --- Что убирает «Очистить арену» (true — убрать, false — оставить)
        public bool clearCreatures = true;
        public bool clearItems;
        public bool clearTombstones;
        public bool clearPetrified;
        public bool clearTrees;
        public bool freezeBeforeStart = true;
        public bool freezeAfterFinish;
        public bool autoStart;
        public float countdown = 3f;

        // --- Персонаж
        public bool playerVisible = true;
        public int playerCollision = (int)CollisionMode.Normal;
        public bool creaturesReact = true;
        public bool playerInvulnerable;

        // --- Камера
        public float freeCamSpeed = 12f;
        public int followStyle = (int)FollowStyle.Orbit;
        public float followDistanceScale = 1f;
        public bool director;
        public float directorInterval = 8f;
        public bool autoSwitchOnDeath = true;
        public bool limitCamera = true;
        public float cameraFov = 65f;

        // --- Оверлей
        public bool overlay = true;
        public bool nameTags = true;
        public bool hpBars = true;
        public bool killFeed = true;
        public float tagDistance = 90f;

        // --- Состав
        public List<SpawnGroup> roster = new List<SpawnGroup>();

        public void EnsureValid()
        {
            int tn = Hostility.TeamCount * Hostility.TeamCount;
            if (teamMatrix == null || teamMatrix.Length != tn)
            {
                teamMatrix = new bool[tn];
                Hostility.ResetTeamMatrix(this);
            }
            int fn = Hostility.FactionCount * Hostility.FactionCount;
            if (factionMatrix == null || factionMatrix.Length != fn)
            {
                factionMatrix = new bool[fn];
                Hostility.ResetFactionMatrix(this);
            }
            if (roster == null) roster = new List<SpawnGroup>();
            roster.RemoveAll(g => g == null || string.IsNullOrEmpty(g.prefab));
            radius = Clamp(radius, 5f, 300f, 30f);
            shape = Mathf.Clamp(shape, 0, 1);
            boundaryMode = Mathf.Clamp(boundaryMode, 0, 2);
            heightLimit = Clamp(heightLimit, 0f, 60f, 0f);
            flyHeight = Clamp(flyHeight, 0f, 40f, 0f);
            hostilityMode = Mathf.Clamp(hostilityMode, 0, 2);
            playerTeam = Mathf.Clamp(playerTeam, 0, Hostility.TeamCount);
            behavior = Mathf.Clamp(behavior, 0, 2);
            intruders = Mathf.Clamp(intruders, 0, 2);
            countdown = Clamp(countdown, 0f, 10f, 3f);
            playerCollision = Mathf.Clamp(playerCollision, 0, 2);
            freeCamSpeed = Clamp(freeCamSpeed, 1f, 100f, 12f);
            followStyle = Mathf.Clamp(followStyle, 0, 2);
            followDistanceScale = Clamp(followDistanceScale, 0.3f, 4f, 1f);
            directorInterval = Clamp(directorInterval, 3f, 30f, 8f);
            cameraFov = Clamp(cameraFov, 30f, 110f, 65f);
            tagDistance = Clamp(tagDistance, 20f, 250f, 90f);
            foreach (var g in roster)
            {
                g.count = Mathf.Clamp(g.count, 1, 200);
                g.starsMin = Mathf.Clamp(g.starsMin, 0, 20);
                g.starsMax = Mathf.Clamp(g.starsMax, g.starsMin, 20);
                g.team = Mathf.Clamp(g.team, 1, Hostility.TeamCount);
                g.factionOverride = Mathf.Clamp(g.factionOverride, -1, Hostility.FactionCount - 1);
                g.placement = Mathf.Clamp(g.placement, 0, 3);
                g.spread = Clamp(g.spread, 0f, 20f, 4f);
                g.behavior = Mathf.Clamp(g.behavior, -1, 2);
                if (g.profile == null) g.profile = "";
            }
        }

        /// <summary>Clamp, который не пропускает NaN и бесконечность (Mathf.Clamp возвращает NaN как есть).</summary>
        private static float Clamp(float v, float min, float max, float fallback) =>
            float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, min, max);

        /// <summary>
        /// Личное — персонаж, камера, оверлей, уборка сирот: сценарий их не меняет, иначе загрузка чужого сценария
        /// у арены с боссом молча сняла бы, например, неуязвимость.
        /// </summary>
        public void CopyPersonalFrom(ArenaSettings s)
        {
            boundaryAffectsPlayer = s.boundaryAffectsPlayer;
            playerTeam = s.playerTeam;
            cleanupOrphans = s.cleanupOrphans;
            playerVisible = s.playerVisible;
            playerCollision = s.playerCollision;
            creaturesReact = s.creaturesReact;
            playerInvulnerable = s.playerInvulnerable;
            freeCamSpeed = s.freeCamSpeed;
            followStyle = s.followStyle;
            followDistanceScale = s.followDistanceScale;
            director = s.director;
            directorInterval = s.directorInterval;
            autoSwitchOnDeath = s.autoSwitchOnDeath;
            limitCamera = s.limitCamera;
            cameraFov = s.cameraFov;
            overlay = s.overlay;
            nameTags = s.nameTags;
            hpBars = s.hpBars;
            killFeed = s.killFeed;
            tagDistance = s.tagDistance;
        }

        public ArenaSettings Clone()
        {
            var copy = JsonIO.FromJson<ArenaSettings>(JsonIO.ToJson(this));
            copy.EnsureValid();
            return copy;
        }
    }

    public static class ArenaSettingsStore
    {
        public static ArenaSettings Current = new ArenaSettings();

        private static string SettingsPath => Path.Combine(ArenaPlugin.DataDir, "settings.json");
        public static string ScenarioDir => Path.Combine(ArenaPlugin.DataDir, "scenarios");

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    Current = JsonIO.FromJson<ArenaSettings>(File.ReadAllText(SettingsPath)) ?? new ArenaSettings();
                }
            }
            catch (Exception e)
            {
                // Испорченный файл не затирается умолчаниями при выходе — остаётся рядом копией.
                try { File.Copy(SettingsPath, SettingsPath + ".bad", true); } catch { }
                ArenaPlugin.Log.LogWarning(L.T("settings.json не прочитан, взяты умолчания (копия — settings.json.bad): ") + e.Message);
                Current = new ArenaSettings();
            }
            Current.EnsureValid();
        }

        public static void Save()
        {
            try
            {
                WriteAtomic(SettingsPath, JsonIO.ToJson(Current, true));
            }
            catch (Exception e)
            {
                ArenaPlugin.Log.LogWarning(L.T("settings.json не сохранён: ") + e.Message);
            }
        }

        /// <summary>Запись через временный файл: падение посреди записи не оставляет обрезанный JSON.</summary>
        public static void WriteAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public static List<string> ListScenarios()
        {
            var list = new List<string>();
            if (!Directory.Exists(ScenarioDir)) return list;
            foreach (var f in Directory.GetFiles(ScenarioDir, "*.json"))
                list.Add(Path.GetFileNameWithoutExtension(f));
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public static void SaveScenario(string name)
        {
            name = FileNames.Sanitize(name);
            if (name.Length == 0) return;
            Directory.CreateDirectory(ScenarioDir);
            WriteAtomic(Path.Combine(ScenarioDir, name + ".json"), JsonIO.ToJson(Current, true));
        }

        public static bool LoadScenario(string name)
        {
            var path = Path.Combine(ScenarioDir, FileNames.Sanitize(name) + ".json");
            if (!File.Exists(path)) return false;
            try
            {
                var s = JsonIO.FromJson<ArenaSettings>(File.ReadAllText(path));
                if (s == null) return false;
                s.CopyPersonalFrom(Current);
                s.EnsureValid();
                Current = s;
                return true;
            }
            catch (Exception e)
            {
                ArenaPlugin.Log.LogWarning(L.F("Сценарий {0} не прочитан: {1}", name, e.Message));
                return false;
            }
        }

        public static void DeleteScenario(string name)
        {
            var path = Path.Combine(ScenarioDir, FileNames.Sanitize(name) + ".json");
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public static class FileNames
    {
        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }
    }
}
