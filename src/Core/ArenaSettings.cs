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

    public enum ArenaBehavior
    {
        Hunt = 0,
        Hold = 1,
        Free = 2
    }

    public enum IntruderPolicy { Ignore = 0, Involve = 1, Remove = 2 }

    [Serializable]
    public class SpawnGroup
    {
        public string prefab = "";
        public int count = 1;
        public int starsMin;
        public int starsMax;
        public int team = 1;
        public int factionOverride = -1;
        public string profile = "";
        public int placement = (int)Placement.TeamSide;
        public float spread = 4f;
        public int behavior = -1;

        public SpawnGroup Clone() => (SpawnGroup)MemberwiseClone();
    }

    [Serializable]
    public class ArenaSettings
    {
        public float radius = 30f;
        public int shape = (int)BoundaryShape.Circle;
        public int boundaryMode = (int)BoundaryMode.Wall;
        public bool showBoundary = true;
        public bool boundaryAffectsPlayer;
        public float heightLimit;
        public float flyHeight;

        public int hostilityMode = (int)HostilityMode.Teams;
        public bool[] teamMatrix = new bool[Hostility.TeamCount * Hostility.TeamCount];
        public bool[] factionMatrix = new bool[0];
        public bool friendlyFire;
        public bool aoeHitsSameKind = true;
        public bool ignorePlayerScaling = true;
        public bool isolateOutsiders = true;
        public int playerTeam;

        public bool allowGuests = true;

        public int behavior = (int)ArenaBehavior.Hunt;
        public int intruders = (int)IntruderPolicy.Involve;
        public bool noFlee = true;
        public bool noBuildingAttacks = true;
        public bool wakeSleepers = true;
        public bool dropLoot;
        public bool cleanupOrphans = true;

        public bool clearCreatures = true;
        public bool clearItems;
        public bool clearTombstones;
        public bool clearPetrified;
        public bool clearTrees;
        public bool freezeBeforeStart = true;
        public bool freezeAfterFinish;
        public bool autoStart;
        public float countdown = 3f;

        public bool playerVisible = true;
        public int playerCollision = (int)CollisionMode.Normal;
        public bool creaturesReact = true;
        public bool playerInvulnerable;

        public float freeCamSpeed = 12f;
        public int followStyle = (int)FollowStyle.Orbit;
        public float followDistanceScale = 1f;
        public bool director;
        public float directorInterval = 8f;
        public bool autoSwitchOnDeath = true;
        public bool limitCamera = true;
        public float cameraFov = 65f;

        public bool overlay = true;
        public bool nameTags = true;
        public bool hpBars = true;
        public bool killFeed = true;
        public float tagDistance = 90f;

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
            radius = Mathf.Clamp(radius, 5f, 300f);
            playerTeam = Mathf.Clamp(playerTeam, 0, Hostility.TeamCount);
            behavior = Mathf.Clamp(behavior, 0, 2);
            intruders = Mathf.Clamp(intruders, 0, 2);
            foreach (var g in roster)
            {
                g.count = Mathf.Clamp(g.count, 1, 200);
                g.starsMin = Mathf.Clamp(g.starsMin, 0, 20);
                g.starsMax = Mathf.Clamp(g.starsMax, g.starsMin, 20);
                g.team = Mathf.Clamp(g.team, 1, Hostility.TeamCount);
                g.behavior = Mathf.Clamp(g.behavior, -1, 2);
                if (g.profile == null) g.profile = "";
            }
        }

        public ArenaSettings Clone()
        {
            var json = JsonUtility.ToJson(this);
            var copy = JsonUtility.FromJson<ArenaSettings>(json);
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
                    Current = JsonUtility.FromJson<ArenaSettings>(File.ReadAllText(SettingsPath)) ?? new ArenaSettings();
                }
            }
            catch (Exception e)
            {
                ArenaPlugin.Log.LogWarning(L.T("settings.json не прочитан, взяты умолчания: ") + e.Message);
                Current = new ArenaSettings();
            }
            Current.EnsureValid();
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(SettingsPath, JsonUtility.ToJson(Current, true));
            }
            catch (Exception e)
            {
                ArenaPlugin.Log.LogWarning(L.T("settings.json не сохранён: ") + e.Message);
            }
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
            File.WriteAllText(Path.Combine(ScenarioDir, name + ".json"), JsonUtility.ToJson(Current, true));
        }

        public static bool LoadScenario(string name)
        {
            var path = Path.Combine(ScenarioDir, FileNames.Sanitize(name) + ".json");
            if (!File.Exists(path)) return false;
            try
            {
                var s = JsonUtility.FromJson<ArenaSettings>(File.ReadAllText(path));
                if (s == null) return false;
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
