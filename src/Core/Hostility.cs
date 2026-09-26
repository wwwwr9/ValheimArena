using System;
using UnityEngine;

namespace ValheimArena.Core
{
    /// <summary>
    /// Кто на кого нападает внутри арены. Подменяет BaseAI.IsEnemy для бойцов арены
    /// (и для локального игрока, если существа не должны на него реагировать).
    /// </summary>
    public static class Hostility
    {
        public const int TeamCount = 8;
        public static readonly int FactionCount = Enum.GetValues(typeof(Character.Faction)).Length;

        public static readonly Color[] TeamColors =
        {
            new Color(0.95f, 0.30f, 0.25f), // 1 красные
            new Color(0.30f, 0.60f, 1.00f), // 2 синие
            new Color(0.35f, 0.85f, 0.35f), // 3 зелёные
            new Color(1.00f, 0.80f, 0.20f), // 4 жёлтые
            new Color(0.75f, 0.40f, 0.95f), // 5 фиолетовые
            new Color(0.25f, 0.90f, 0.90f), // 6 бирюзовые
            new Color(1.00f, 0.55f, 0.15f), // 7 оранжевые
            new Color(0.85f, 0.85f, 0.85f), // 8 белые
        };

        public static string[] TeamNames => new[]
        {
            L.T("Красные"), L.T("Синие"), L.T("Зелёные"), L.T("Жёлтые"), L.T("Фиолетовые"), L.T("Бирюзовые"), L.T("Оранжевые"), L.T("Белые")
        };

        public static Color TeamColor(int team) => team >= 1 && team <= TeamCount ? TeamColors[team - 1] : new Color(0.62f, 0.6f, 0.56f);
        public static string TeamName(int team) => team >= 1 && team <= TeamCount ? TeamNames[team - 1] : team == 0 ? L.T("Посторонние") : "—";

        public static string FactionName(int f)
        {
            switch ((Character.Faction)f)
            {
                case Character.Faction.Players: return L.T("Игроки");
                case Character.Faction.AnimalsVeg: return L.T("Животные");
                case Character.Faction.ForestMonsters: return L.T("Лесные");
                case Character.Faction.Undead: return L.T("Нежить");
                case Character.Faction.Demon: return L.T("Демоны");
                case Character.Faction.MountainMonsters: return L.T("Горные");
                case Character.Faction.SeaMonsters: return L.T("Морские");
                case Character.Faction.PlainsMonsters: return L.T("Равнинные");
                case Character.Faction.Boss: return L.T("Боссы");
                case Character.Faction.MistlandsMonsters: return L.T("Туманные");
                case Character.Faction.Dverger: return L.T("Двергры");
                case Character.Faction.PlayerSpawned: return L.T("Призванные");
                case Character.Faction.TrainingDummy: return L.T("Манекены");
                case Character.Faction.DeepNorth: return L.T("Глубокий север");
                default: return ((Character.Faction)f).ToString();
            }
        }

        public static string ModeName(HostilityMode m)
        {
            switch (m)
            {
                case HostilityMode.Teams: return L.T("Команды");
                case HostilityMode.Factions: return L.T("Фракции");
                default: return L.T("Все против всех");
            }
        }

        /// <summary>
        /// Копия ванильной таблицы BaseAI.IsEnemy (ветка без приручения и агрессии), чтобы матрица фракций
        /// по умолчанию совпадала с игрой. Направленная: a — тот, кто ищет цель.
        /// </summary>
        public static bool VanillaFactionHostile(Character.Faction a, Character.Faction b)
        {
            if (a == b) return false;
            switch (a)
            {
                case Character.Faction.AnimalsVeg:
                case Character.Faction.PlayerSpawned:
                    return true;
                case Character.Faction.Players:
                    return b != Character.Faction.Dverger;
                case Character.Faction.ForestMonsters:
                    return b != Character.Faction.AnimalsVeg && b != Character.Faction.Boss;
                case Character.Faction.Undead:
                    return b != Character.Faction.Demon && b != Character.Faction.Boss;
                case Character.Faction.Demon:
                    return b != Character.Faction.Undead && b != Character.Faction.Boss;
                case Character.Faction.MountainMonsters:
                case Character.Faction.SeaMonsters:
                case Character.Faction.PlainsMonsters:
                    return b != Character.Faction.Boss;
                case Character.Faction.MistlandsMonsters:
                case Character.Faction.DeepNorth:
                    return b != Character.Faction.AnimalsVeg && b != Character.Faction.Boss;
                case Character.Faction.Dverger:
                    return b != Character.Faction.AnimalsVeg && b != Character.Faction.Boss && b != Character.Faction.Players;
                case Character.Faction.Boss:
                    return b == Character.Faction.Players || b == Character.Faction.PlayerSpawned;
                case Character.Faction.TrainingDummy:
                    return b == Character.Faction.Players;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Полная копия ванильного BaseAI.IsEnemy (сам метод перехвачен патчем): общая группа (m_group) — не враги;
        /// приручённые и разозлённые — по правилам игры; дальше — таблица фракций.
        /// </summary>
        public static bool VanillaIsEnemy(Character a, Character b)
        {
            if (a == b || !a || !b) return false;
            string group = a.GetGroup();
            if (group.Length > 0 && group == b.GetGroup()) return false;
            var fa = a.GetFaction();
            var fb = b.GetFaction();
            bool tamedA = a.IsTamed(), tamedB = b.IsTamed();
            bool angryA = a.GetBaseAI() && a.GetBaseAI().IsAggravated();
            bool angryB = b.GetBaseAI() && b.GetBaseAI().IsAggravated();
            if (tamedA || tamedB)
            {
                bool friends = (tamedA && tamedB) || (tamedA && fb == Character.Faction.Players) || (tamedB && fa == Character.Faction.Players) ||
                               (tamedA && fb == Character.Faction.Dverger && !angryB) || (tamedB && fa == Character.Faction.Dverger && !angryA);
                return !friends;
            }
            if ((angryA && fb == Character.Faction.Players) || (angryB && fa == Character.Faction.Players)) return true;
            return VanillaFactionHostile(fa, fb);
        }

        public static void ResetTeamMatrix(ArenaSettings s)
        {
            for (int i = 0; i < TeamCount; i++)
                for (int j = 0; j < TeamCount; j++)
                    s.teamMatrix[i * TeamCount + j] = i != j;
        }

        public static void ResetFactionMatrix(ArenaSettings s)
        {
            for (int i = 0; i < FactionCount; i++)
                for (int j = 0; j < FactionCount; j++)
                    s.factionMatrix[i * FactionCount + j] = VanillaFactionHostile((Character.Faction)i, (Character.Faction)j);
        }

        /// <summary>Боссы нападают на всех рядовых (и, по желанию, рядовые отвечают).</summary>
        public static void BossesAttackEveryone(ArenaSettings s, bool reciprocal)
        {
            int boss = (int)Character.Faction.Boss;
            for (int j = 0; j < FactionCount; j++)
            {
                if (j == boss) continue;
                s.factionMatrix[boss * FactionCount + j] = true;
                if (reciprocal) s.factionMatrix[j * FactionCount + boss] = true;
            }
        }

        public static void AllFactionsHostile(ArenaSettings s, bool includeSame)
        {
            for (int i = 0; i < FactionCount; i++)
                for (int j = 0; j < FactionCount; j++)
                    s.factionMatrix[i * FactionCount + j] = includeSame || i != j;
        }

        public static bool TeamHostile(ArenaSettings s, int a, int b)
        {
            if (a < 1 || b < 1 || a > TeamCount || b > TeamCount) return a != b;
            return s.teamMatrix[(a - 1) * TeamCount + (b - 1)];
        }

        public static void SetTeamHostile(ArenaSettings s, int a, int b, bool value)
        {
            s.teamMatrix[(a - 1) * TeamCount + (b - 1)] = value;
            s.teamMatrix[(b - 1) * TeamCount + (a - 1)] = value;
        }

        public static bool FactionHostile(ArenaSettings s, int a, int b)
        {
            if (a < 0 || b < 0 || a >= FactionCount || b >= FactionCount) return false;
            return s.factionMatrix[a * FactionCount + b];
        }

        /// <summary>
        /// Враждебен ли боец a к бойцу b (направленно: a ищет цель). Бойцы одной арены; правила — этой арены,
        /// у всех клиентов одинаковые (их рассылает хозяин).
        /// </summary>
        public static bool UnitsHostile(ArenaUnit a, ArenaUnit b)
        {
            if (a == b) return false;
            var r = a.Rules;
            if (r == null) return a.Team != b.Team;
            switch ((HostilityMode)r.hostilityMode)
            {
                case HostilityMode.Teams:
                    if (a.Team == 0 && b.Team == 0)
                        return VanillaIsEnemy(a.Character, b.Character);
                    return a.Team != b.Team && r.TeamHostile(a.Team, b.Team);
                case HostilityMode.Factions:
                    return r.FactionHostile((int)a.Character.GetFaction(), (int)b.Character.GetFaction());
                default:
                    return true;
            }
        }

        /// <summary>
        /// Боец и игрок (любой, не только свой): учитываются его настройки из ZDO персонажа — реагируют ли на него
        /// существа и в какой он команде. Игрок за границей изолированной арены — посторонний.
        /// </summary>
        public static bool UnitVsPlayer(ArenaUnit u, Player p, bool unitIsSeeker)
        {
            if (PlayerPresence.Untouchable(p)) return false;
            var session = u.Session;
            var r = session?.Rules;
            if (r == null) return true;
            if (r.isolateOutsiders && !session.IsInside(p.transform.position, 3f)) return false;
            switch ((HostilityMode)r.hostilityMode)
            {
                case HostilityMode.Teams:
                    int team = PlayerPresence.TeamOf(p);
                    if (team == 0) return true;
                    return u.Team != team && r.TeamHostile(u.Team, team);
                case HostilityMode.Factions:
                    int f = (int)u.Character.GetFaction();
                    int pf = (int)Character.Faction.Players;
                    return unitIsSeeker ? r.FactionHostile(f, pf) : r.FactionHostile(pf, f);
                default:
                    return true;
            }
        }

        /// <summary>Стороны от этого номера — «сам за себя» (режим «Все против всех»).</summary>
        public const int SoloSides = 100000;

        /// <summary>Сторона бойца для подсчёта победителя.</summary>
        public static int SideOf(ArenaUnit u)
        {
            var mode = (HostilityMode)(u.Rules?.hostilityMode ?? 0);
            return SideOf(mode, u.Team, (int)u.Character.GetFaction(), u.Key);
        }

        /// <summary>
        /// Сторона по данным бойца. «Сам за себя» — по полному ключу ZDO (игрок + номер объекта): номера объектов
        /// у каждого игрока свои, и одинаковый номер у бойцов разных игроков слил бы их в одну сторону.
        /// </summary>
        public static int SideOf(HostilityMode mode, int team, int faction, string key)
        {
            switch (mode)
            {
                case HostilityMode.Teams: return team;
                case HostilityMode.Factions: return 1000 + faction;
                default: return SoloSides + ((key ?? "").GetStableHashCode() & 0x3FFFFFFF);
            }
        }

        public static string SideName(int side, string soloLabel)
        {
            if (side == 0) return L.T("Посторонние");
            if (side < 1000) return L.F("Команда «{0}»", TeamName(side));
            if (side < SoloSides) return L.F("Фракция «{0}»", FactionName(side - 1000));
            return string.IsNullOrEmpty(soloLabel) ? L.T("Одиночка") : soloLabel;
        }

        public static Color SideColor(int side, int team)
        {
            if (side < 1000) return TeamColor(side);
            if (side < SoloSides) return FactionColor(side - 1000);
            return TeamColor(team);
        }

        public static Color FactionColor(int f) => Color.HSVToRGB((f * 0.137f) % 1f, 0.55f, 1f);

        /// <summary>Цвет бойца в оверлее и ленте: цвет команды или фракции — по режиму его арены.</summary>
        public static Color UnitColor(ArenaUnit u)
        {
            if ((HostilityMode)(u.Rules?.hostilityMode ?? 0) == HostilityMode.Factions)
                return FactionColor((int)u.Character.GetFaction());
            return TeamColor(u.Team);
        }
    }
}
