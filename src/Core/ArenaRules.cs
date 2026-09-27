using System;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimArena.Core
{
    [Serializable]
    public class ArenaRules
    {
        public float radius = 30f;
        public int shape;
        public int boundaryMode = (int)BoundaryMode.Wall;
        public float heightLimit;
        public float flyHeight;

        public int hostilityMode;
        public bool[] teamMatrix = new bool[0];
        public bool[] factionMatrix = new bool[0];
        public bool friendlyFire;
        public bool aoeHitsSameKind = true;
        public bool ignorePlayerScaling = true;
        public bool isolateOutsiders = true;

        public int behavior = (int)ArenaBehavior.Hunt;
        public int intruders = (int)IntruderPolicy.Involve;
        public bool noFlee = true;
        public bool noBuildingAttacks = true;
        public bool wakeSleepers = true;
        public bool dropLoot;
        public bool freezeBeforeStart = true;
        public bool freezeAfterFinish;

        public bool allowGuests = true;

        public static ArenaRules From(ArenaSettings s)
        {
            return new ArenaRules
            {
                radius = s.radius,
                shape = s.shape,
                boundaryMode = s.boundaryMode,
                heightLimit = s.heightLimit,
                flyHeight = s.flyHeight,
                hostilityMode = s.hostilityMode,
                teamMatrix = (bool[])s.teamMatrix.Clone(),
                factionMatrix = (bool[])s.factionMatrix.Clone(),
                friendlyFire = s.friendlyFire,
                aoeHitsSameKind = s.aoeHitsSameKind,
                ignorePlayerScaling = s.ignorePlayerScaling,
                isolateOutsiders = s.isolateOutsiders,
                behavior = s.behavior,
                intruders = s.intruders,
                noFlee = s.noFlee,
                noBuildingAttacks = s.noBuildingAttacks,
                wakeSleepers = s.wakeSleepers,
                dropLoot = s.dropLoot,
                freezeBeforeStart = s.freezeBeforeStart,
                freezeAfterFinish = s.freezeAfterFinish,
                allowGuests = s.allowGuests,
            };
        }

        public void CopyTo(ArenaSettings s)
        {
            s.radius = radius;
            s.shape = shape;
            s.boundaryMode = boundaryMode;
            s.heightLimit = heightLimit;
            s.flyHeight = flyHeight;
            s.hostilityMode = hostilityMode;
            if (teamMatrix != null && teamMatrix.Length == s.teamMatrix.Length) s.teamMatrix = (bool[])teamMatrix.Clone();
            if (factionMatrix != null && factionMatrix.Length == s.factionMatrix.Length) s.factionMatrix = (bool[])factionMatrix.Clone();
            s.friendlyFire = friendlyFire;
            s.aoeHitsSameKind = aoeHitsSameKind;
            s.ignorePlayerScaling = ignorePlayerScaling;
            s.isolateOutsiders = isolateOutsiders;
            s.behavior = behavior;
            s.intruders = intruders;
            s.noFlee = noFlee;
            s.noBuildingAttacks = noBuildingAttacks;
            s.wakeSleepers = wakeSleepers;
            s.dropLoot = dropLoot;
            s.freezeBeforeStart = freezeBeforeStart;
            s.freezeAfterFinish = freezeAfterFinish;
            s.allowGuests = allowGuests;
        }

        public bool TeamHostile(int a, int b)
        {
            int n = Hostility.TeamCount;
            if (a < 1 || b < 1 || a > n || b > n || teamMatrix == null || teamMatrix.Length != n * n) return a != b;
            return teamMatrix[(a - 1) * n + (b - 1)];
        }

        public bool FactionHostile(int a, int b)
        {
            int n = Hostility.FactionCount;
            if (a < 0 || b < 0 || a >= n || b >= n || factionMatrix == null || factionMatrix.Length != n * n)
                return Hostility.VanillaFactionHostile((Character.Faction)a, (Character.Faction)b);
            return factionMatrix[a * n + b];
        }
    }

    [Serializable]
    public class SessionState
    {
        public int protocol = ArenaNet.Protocol;
        public string id = "";
        public long hostPlayerId;
        public long hostPeer;
        public string hostName = "";
        public Vector3 center;
        public int state;
        public double countdownEnd;
        public double fightStart;
        public double fightEnd;
        public string result = "";
        public int revision;
        public ArenaRules rules = new ArenaRules();
    }

    [Serializable]
    public class UnitStat
    {
        public string key = "";
        public string label = "";
        public Color color = Color.white;
        public int team;
        public int faction;
        public int side;
        public string sideName = "";
        public Color sideColor = Color.white;
        public float dealt;
        public float taken;
        public int kills;
        public bool alive = true;
        public string killedBy = "";
        public double spawnTime;
        public double deathTime;
    }

    [Serializable]
    public class StatsSummary
    {
        public string session = "";
        public List<UnitStat> units = new List<UnitStat>();
    }

    [Serializable]
    public class KillEvent
    {
        public string session = "";
        public string victimKey = "";
        public string victim = "";
        public Color victimColor = Color.white;
        public int victimTeam;
        public int victimFaction;
        public string killerKey = "";
        public string killer = "";
        public Color killerColor = Color.gray;
    }
}
