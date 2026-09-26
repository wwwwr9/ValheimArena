using System.Collections.Generic;
using UnityEngine;

namespace ValheimArena.Core
{
    public enum ArenaState { Inactive, Preparing, Countdown, Fighting, Paused, Finished }

    public struct FeedEntry
    {
        public float Time;
        public string Killer;
        public Color KillerColor;
        public string Victim;
        public Color VictimColor;
    }

    public class SideSummary
    {
        public int Side;
        public string Name;
        public Color Color;
        public int Spawned;
        public int Alive;
        public int Kills;
        public float Damage;
    }

    /// <summary>
    /// Одна арена в мире. Своя (IsLocal) — её ведёт этот клиент; чужая — копия состояния, присланного хозяином.
    /// Геометрия, правила и статистика — здесь; сетевой обмен — в <see cref="ArenaNet"/>, общая логика — в <see cref="Arena"/>.
    /// </summary>
    public class ArenaSession
    {
        /// <summary>Сколько секунд без вестей от хозяина арена считается живой.</summary>
        public const float StaleSeconds = 15f;

        public string Id = "";
        public bool IsLocal;
        public long HostPlayerId;
        public long HostPeer;
        public string HostName = "";
        public Vector3 Center;
        public ArenaState State = ArenaState.Preparing;
        public double CountdownEnd;
        public double FightStart;
        public double FightEnd;
        public string Result = "";
        public int Revision;
        public ArenaRules Rules = new ArenaRules();
        public float LastHeard;

        public readonly Dictionary<string, UnitStat> Stats = new Dictionary<string, UnitStat>();
        public readonly List<FeedEntry> Feed = new List<FeedEntry>();
        public readonly Dictionary<int, SideSummary> Sides = new Dictionary<int, SideSummary>();
        public bool StatsDirty;

        public bool Live => IsLocal || Time.unscaledTime - LastHeard < StaleSeconds;

        public static double Now => ZNet.instance ? ZNet.instance.GetTimeSeconds() : Time.time;

        public float CountdownLeft => Mathf.Max(0f, (float)(CountdownEnd - Now));

        public float FightSeconds
        {
            get
            {
                if (State == ArenaState.Finished) return (float)(FightEnd - FightStart);
                if (State == ArenaState.Fighting || State == ArenaState.Paused) return (float)(Now - FightStart);
                return 0f;
            }
        }

        public string StateName
        {
            get
            {
                switch (State)
                {
                    case ArenaState.Inactive: return L.T("не активна");
                    case ArenaState.Preparing: return L.T("подготовка");
                    case ArenaState.Countdown: return L.T("отсчёт");
                    case ArenaState.Fighting: return L.T("бой");
                    case ArenaState.Paused: return L.T("пауза");
                    default: return L.T("итоги");
                }
            }
        }

        // ------------------------------------------------------------------ сеть

        public SessionState ToState()
        {
            return new SessionState
            {
                id = Id,
                hostPlayerId = HostPlayerId,
                hostPeer = HostPeer,
                hostName = HostName,
                center = Center,
                state = (int)State,
                countdownEnd = CountdownEnd,
                fightStart = FightStart,
                fightEnd = FightEnd,
                result = Result ?? "",
                revision = Revision,
                rules = Rules
            };
        }

        /// <summary>Принять состояние от хозяина. Возвращает прежнее состояние боя.</summary>
        public ArenaState Apply(SessionState s)
        {
            var old = State;
            Id = s.id;
            HostPlayerId = s.hostPlayerId;
            HostPeer = s.hostPeer;
            HostName = s.hostName ?? "";
            Center = s.center;
            State = (ArenaState)s.state;
            CountdownEnd = s.countdownEnd;
            FightStart = s.fightStart;
            FightEnd = s.fightEnd;
            Result = s.result ?? "";
            Revision = s.revision;
            if (s.rules != null) Rules = s.rules;
            LastHeard = Time.unscaledTime;
            return old;
        }

        // ------------------------------------------------------------------ геометрия

        public bool IsInside(Vector3 p, float margin = 0f)
        {
            Vector3 d = p - Center;
            float r = Rules.radius + margin;
            if ((BoundaryShape)Rules.shape == BoundaryShape.Square)
                return Mathf.Abs(d.x) <= r && Mathf.Abs(d.z) <= r;
            d.y = 0f;
            return d.magnitude <= r;
        }

        public Vector3 ClampInside(Vector3 p, float factor)
        {
            Vector3 d = p - Center;
            d.y = 0f;
            float r = Rules.radius * factor;
            if ((BoundaryShape)Rules.shape == BoundaryShape.Square)
            {
                d.x = Mathf.Clamp(d.x, -r, r);
                d.z = Mathf.Clamp(d.z, -r, r);
            }
            else if (d.magnitude > r)
            {
                d = d.normalized * r;
            }
            return new Vector3(Center.x + d.x, p.y, Center.z + d.z);
        }

        public Vector3 RandomPointInside(float factor)
        {
            float r = Rules.radius * factor;
            if ((BoundaryShape)Rules.shape == BoundaryShape.Square)
                return Center + new Vector3(Random.Range(-r, r), 0f, Random.Range(-r, r));
            var c = Random.insideUnitCircle * r;
            return Center + new Vector3(c.x, 0f, c.y);
        }

        /// <summary>Точка периметра по параметру t∈[0,1).</summary>
        public Vector3 PerimeterPoint(float t)
        {
            Vector3 c = Center;
            float r = Rules.radius;
            if ((BoundaryShape)Rules.shape == BoundaryShape.Square)
            {
                float per = t * 4f;
                int side = Mathf.FloorToInt(per) % 4;
                float f = per - Mathf.Floor(per);
                switch (side)
                {
                    case 0: return c + new Vector3(-r + 2f * r * f, 0f, -r);
                    case 1: return c + new Vector3(r, 0f, -r + 2f * r * f);
                    case 2: return c + new Vector3(r - 2f * r * f, 0f, r);
                    default: return c + new Vector3(-r, 0f, r - 2f * r * f);
                }
            }
            float a = t * Mathf.PI * 2f;
            return c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        // ------------------------------------------------------------------ статистика

        public UnitStat Stat(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!Stats.TryGetValue(key, out var s))
            {
                s = new UnitStat { key = key, spawnTime = Now };
                Stats[key] = s;
                StatsDirty = true;
            }
            return s;
        }

        public void ReplaceStats(List<UnitStat> units)
        {
            Stats.Clear();
            if (units != null)
                foreach (var u in units)
                    if (u != null && !string.IsNullOrEmpty(u.key)) Stats[u.key] = u;
            RebuildSides();
        }

        public void ClearStats()
        {
            Stats.Clear();
            Feed.Clear();
            Sides.Clear();
            Result = "";
            StatsDirty = true;
        }

        /// <summary>Сводка по сторонам из строк статистики (одинаково у хозяина и гостей).</summary>
        public void RebuildSides()
        {
            Sides.Clear();
            foreach (var st in Stats.Values)
            {
                if (string.IsNullOrEmpty(st.label)) continue;
                if (!Sides.TryGetValue(st.side, out var s))
                {
                    s = new SideSummary { Side = st.side, Name = st.sideName, Color = st.sideColor };
                    Sides[st.side] = s;
                }
                s.Spawned++;
                if (st.alive) s.Alive++;
                s.Kills += st.kills;
                s.Damage += st.dealt;
            }
        }

        public void AddFeed(KillEvent e)
        {
            Feed.Add(new FeedEntry
            {
                Time = Time.unscaledTime,
                Killer = e.killer,
                KillerColor = e.killerColor,
                Victim = e.victim,
                VictimColor = e.victimColor
            });
            if (Feed.Count > 30) Feed.RemoveAt(0);
        }
    }
}
