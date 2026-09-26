using System;
using System.Collections.Generic;

namespace ValheimArena.Profiles
{
    /// <summary>Набор урона по типам (зеркало HitData.DamageTypes, пригодное для JsonUtility).</summary>
    [Serializable]
    public class DamageSet
    {
        public float damage, blunt, slash, pierce, chop, pickaxe, fire, frost, lightning, poison, spirit, nonPlayer;

        public static string[] Labels => new[]
        {
            L.T("Чистый (мимо сопротивлений)"), L.T("Дробящий"), L.T("Режущий"), L.T("Колющий"), L.T("Рубка (дерево)"), L.T("Кирка (камень)"),
            L.T("Огонь"), L.T("Мороз"), L.T("Молния"), L.T("Яд"), L.T("Дух"), L.T("Особый (NonPlayer)")
        };

        public static DamageSet From(HitData.DamageTypes d) => new DamageSet
        {
            damage = d.m_damage, blunt = d.m_blunt, slash = d.m_slash, pierce = d.m_pierce, chop = d.m_chop,
            pickaxe = d.m_pickaxe, fire = d.m_fire, frost = d.m_frost, lightning = d.m_lightning,
            poison = d.m_poison, spirit = d.m_spirit, nonPlayer = d.m_nonPlayer
        };

        public HitData.DamageTypes ToDamageTypes()
        {
            return new HitData.DamageTypes
            {
                m_damage = damage, m_blunt = blunt, m_slash = slash, m_pierce = pierce, m_chop = chop,
                m_pickaxe = pickaxe, m_fire = fire, m_frost = frost, m_lightning = lightning,
                m_poison = poison, m_spirit = spirit, m_nonPlayer = nonPlayer
            };
        }

        public float Get(int i)
        {
            switch (i)
            {
                case 0: return damage; case 1: return blunt; case 2: return slash; case 3: return pierce;
                case 4: return chop; case 5: return pickaxe; case 6: return fire; case 7: return frost;
                case 8: return lightning; case 9: return poison; case 10: return spirit; default: return nonPlayer;
            }
        }

        public void Set(int i, float v)
        {
            switch (i)
            {
                case 0: damage = v; break; case 1: blunt = v; break; case 2: slash = v; break; case 3: pierce = v; break;
                case 4: chop = v; break; case 5: pickaxe = v; break; case 6: fire = v; break; case 7: frost = v; break;
                case 8: lightning = v; break; case 9: poison = v; break; case 10: spirit = v; break; default: nonPlayer = v; break;
            }
        }

        public float Total()
        {
            float t = 0f;
            for (int i = 0; i < Labels.Length; i++) t += Get(i);
            return t;
        }

        public DamageSet Clone() => (DamageSet)MemberwiseClone();
    }

    /// <summary>Сопротивления (значения enum HitData.DamageModifier).</summary>
    [Serializable]
    public class ResistSet
    {
        public int blunt, slash, pierce, chop, pickaxe, fire, frost, lightning, poison, spirit, nonPlayer;

        public static string[] Labels => new[]
        {
            L.T("Дробящий"), L.T("Режущий"), L.T("Колющий"), L.T("Рубка"), L.T("Кирка"), L.T("Огонь"), L.T("Мороз"), L.T("Молния"), L.T("Яд"), L.T("Дух"), L.T("Особый (NonPlayer)")
        };

        public static ResistSet From(HitData.DamageModifiers m) => new ResistSet
        {
            blunt = (int)m.m_blunt, slash = (int)m.m_slash, pierce = (int)m.m_pierce, chop = (int)m.m_chop,
            pickaxe = (int)m.m_pickaxe, fire = (int)m.m_fire, frost = (int)m.m_frost, lightning = (int)m.m_lightning,
            poison = (int)m.m_poison, spirit = (int)m.m_spirit, nonPlayer = (int)m.m_nonPlayer
        };

        public HitData.DamageModifiers ToModifiers()
        {
            return new HitData.DamageModifiers
            {
                m_blunt = (HitData.DamageModifier)blunt, m_slash = (HitData.DamageModifier)slash,
                m_pierce = (HitData.DamageModifier)pierce, m_chop = (HitData.DamageModifier)chop,
                m_pickaxe = (HitData.DamageModifier)pickaxe, m_fire = (HitData.DamageModifier)fire,
                m_frost = (HitData.DamageModifier)frost, m_lightning = (HitData.DamageModifier)lightning,
                m_poison = (HitData.DamageModifier)poison, m_spirit = (HitData.DamageModifier)spirit,
                m_nonPlayer = (HitData.DamageModifier)nonPlayer
            };
        }

        public int Get(int i)
        {
            switch (i)
            {
                case 0: return blunt; case 1: return slash; case 2: return pierce; case 3: return chop; case 4: return pickaxe;
                case 5: return fire; case 6: return frost; case 7: return lightning; case 8: return poison; case 9: return spirit;
                default: return nonPlayer;
            }
        }

        public void Set(int i, int v)
        {
            switch (i)
            {
                case 0: blunt = v; break; case 1: slash = v; break; case 2: pierce = v; break; case 3: chop = v; break;
                case 4: pickaxe = v; break; case 5: fire = v; break; case 6: frost = v; break; case 7: lightning = v; break;
                case 8: poison = v; break; case 9: spirit = v; break; default: nonPlayer = v; break;
            }
        }

        public ResistSet Clone() => (ResistSet)MemberwiseClone();

        /// <summary>Порядок от «уязвим» к «неуязвим» для кнопок −/+.</summary>
        public static readonly HitData.DamageModifier[] Ladder =
        {
            HitData.DamageModifier.VeryWeak, HitData.DamageModifier.Weak, HitData.DamageModifier.SlightlyWeak,
            HitData.DamageModifier.Normal, HitData.DamageModifier.SlightlyResistant, HitData.DamageModifier.Resistant,
            HitData.DamageModifier.VeryResistant, HitData.DamageModifier.Immune, HitData.DamageModifier.Ignore
        };

        public static string ModifierName(int m)
        {
            switch ((HitData.DamageModifier)m)
            {
                case HitData.DamageModifier.Normal: return L.T("Обычный ×1");
                case HitData.DamageModifier.Resistant: return L.T("Стойкий ×0.5");
                case HitData.DamageModifier.Weak: return L.T("Уязвим ×1.5");
                case HitData.DamageModifier.Immune: return L.T("Иммунитет ×0");
                case HitData.DamageModifier.Ignore: return L.T("Игнор ×0");
                case HitData.DamageModifier.VeryResistant: return L.T("Оч. стойкий ×0.25");
                case HitData.DamageModifier.VeryWeak: return L.T("Оч. уязвим ×2");
                case HitData.DamageModifier.SlightlyResistant: return L.T("Слегка стойкий ×0.75");
                case HitData.DamageModifier.SlightlyWeak: return L.T("Слегка уязвим ×1.25");
                default: return m.ToString();
            }
        }
    }

    /// <summary>
    /// Эффект с собственным уроном (AoE или снаряд, чей урон задан в самом префабе, а не в атаке):
    /// удары по площади боссов, взрывы, лужи яда и т.п.
    /// </summary>
    [Serializable]
    public class EffectOverride
    {
        public string prefab = "";
        public string kind = "";
        public DamageSet damage = new DamageSet();
        public float attackForce;
        public bool modified;

        public EffectOverride Clone()
        {
            var c = (EffectOverride)MemberwiseClone();
            c.damage = damage.Clone();
            return c;
        }
    }

    /// <summary>
    /// Слабое место (WeakSpot): попадание в его коллайдер игра считает по его сопротивлениям вместо основных.
    /// </summary>
    [Serializable]
    public class WeakSpotOverride
    {
        public string name = "";
        public ResistSet resists = new ResistSet();

        public WeakSpotOverride Clone()
        {
            var c = (WeakSpotOverride)MemberwiseClone();
            c.resists = resists.Clone();
            return c;
        }
    }

    /// <summary>Одна атака существа (у гуманоидов Valheim атака — это «оружие»-предмет в инвентаре).</summary>
    [Serializable]
    public class AttackOverride
    {
        /// <summary>Имя префаба предмета-атаки — ключ сопоставления.</summary>
        public string item = "";
        /// <summary>m_shared.m_name — запасной ключ.</summary>
        public string sharedName = "";
        public string displayName = "";
        public string source = "";
        public bool enabled = true;

        public DamageSet damage = new DamageSet();
        public DamageSet damagePerLevel = new DamageSet();
        public float attackForce;
        public float backstabBonus;
        public bool blockable;
        public bool dodgeable;

        public float aiAttackRange;
        public float aiAttackRangeMin;
        public float aiAttackInterval;
        public float aiAttackMaxAngle;
        public bool aiPrioritized;
        public float aiMaxHealthPercentage = 1f;
        public float aiMinHealthPercentage;

        public float damageMultiplier = 1f;
        public float forceMultiplier = 1f;
        public float staggerMultiplier = 1f;
        public float attackRange;
        public float attackAngle;
        public float projectileVel;
        public float projectileVelMin;
        public int projectiles = 1;
        public int projectileBursts = 1;
        public bool hasProjectile;

        public string animation = "";
        public List<string> effects = new List<string>();

        /// <summary>2 — сняты и поля ниже; у профилей старых версий их нет (нули), и они не применяются.</summary>
        public int captured;
        /// <summary>Attack.AttackType: удар (горизонтальный/вертикальный), снаряд, площадь…</summary>
        public int attackType;
        /// <summary>У удара по площади — радиус площади (m_attackRayWidth); у ближнего — ширина луча.</summary>
        public float attackRayWidth;
        public string statusEffect = "";
        public float statusEffectChance = 1f;
        /// <summary>ItemData.AiTarget: враг / раненый союзник (лечение) / союзник (поддержка).</summary>
        public int aiTargetType;
        public bool aiInDungeonOnly;
        public bool aiInMistOnly;
        public bool aiWhenFlying = true;
        public bool aiWhenWalking = true;
        public bool aiWhenSwiming = true;

        public AttackOverride Clone()
        {
            var c = (AttackOverride)MemberwiseClone();
            c.damage = damage.Clone();
            c.damagePerLevel = damagePerLevel.Clone();
            c.effects = new List<string>(effects);
            return c;
        }
    }

    /// <summary>Профиль существа из редактора: полный снимок параметров, применяемый к бойцам арены.</summary>
    [Serializable]
    public class CreatureProfile
    {
        /// <summary>2 — сняты поля полёта/плавания ниже; у профилей версии 1 их нет, они не применяются.</summary>
        public int version = 2;
        public string prefab = "";
        public string name = "";

        // Живучесть
        public float health;
        public float scale = 1f;
        public float damageDealtMultiplier = 1f;
        public float damageTakenMultiplier = 1f;
        /// <summary>Без инициализатора: нет в тексте — берутся с прототипа (ProfileStore.Normalize), а не «все обычные».</summary>
        public ResistSet resists;
        public float staggerDamageFactor;
        public bool staggerWhenBlocked = true;
        public float regenAllHPTime;

        // Движение
        public float walkSpeed;
        public float speed;
        public float runSpeed;
        public float turnSpeed;
        public float runTurnSpeed;
        public float acceleration;
        public bool flying;
        public float flySlowSpeed;
        public float flyFastSpeed;
        public float flyTurnSpeed;
        public float swimSpeed;
        public float swimTurnSpeed;
        public float jumpForce;

        // Поведение
        public float viewRange;
        public float viewAngle;
        public float hearRange;
        public bool mistVision;
        public float alertRange;
        public float minAttackInterval;
        public float circleTargetInterval;
        public float fleeIfLowHealth;
        public bool hasMonsterAI;

        public List<AttackOverride> attacks = new List<AttackOverride>();
        public List<EffectOverride> effects = new List<EffectOverride>();
        public List<WeakSpotOverride> weakSpots = new List<WeakSpotOverride>();

        public AttackOverride FindAttack(string itemPrefab, string sharedName)
        {
            if (!string.IsNullOrEmpty(itemPrefab))
                foreach (var a in attacks)
                    if (a.item == itemPrefab) return a;
            if (!string.IsNullOrEmpty(sharedName))
                foreach (var a in attacks)
                    if (a.sharedName == sharedName) return a;
            return null;
        }

        public EffectOverride FindEffect(string prefab)
        {
            foreach (var e in effects)
                if (e.prefab == prefab) return e;
            return null;
        }

        public CreatureProfile Clone()
        {
            var c = (CreatureProfile)MemberwiseClone();
            c.resists = resists?.Clone();
            c.attacks = new List<AttackOverride>();
            foreach (var a in attacks) c.attacks.Add(a.Clone());
            c.effects = new List<EffectOverride>();
            foreach (var e in effects) c.effects.Add(e.Clone());
            c.weakSpots = new List<WeakSpotOverride>();
            if (weakSpots != null) foreach (var w in weakSpots) c.weakSpots.Add(w.Clone());
            return c;
        }
    }
}
