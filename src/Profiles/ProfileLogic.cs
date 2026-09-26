using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ValheimArena.Core;

namespace ValheimArena.Profiles
{
    /// <summary>Снятие параметров с прототипа и применение профиля к живому бойцу.</summary>
    public static class ProfileLogic
    {
        private static readonly MethodInfo MemberwiseCloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        // ------------------------------------------------------------------ снятие с прототипа

        public static CreatureProfile Capture(GameObject prefab)
        {
            var ch = prefab ? prefab.GetComponent<Character>() : null;
            if (!ch) return null;
            var ai = prefab.GetComponent<BaseAI>();
            var mai = ai as MonsterAI;

            var p = new CreatureProfile
            {
                prefab = prefab.name,
                health = ch.m_health,
                scale = 1f,
                resists = ResistSet.From(ch.m_damageModifiers),
                staggerDamageFactor = ch.m_staggerDamageFactor,
                staggerWhenBlocked = ch.m_staggerWhenBlocked,
                regenAllHPTime = ch.m_regenAllHPTime,
                walkSpeed = ch.m_walkSpeed,
                speed = ch.m_speed,
                runSpeed = ch.m_runSpeed,
                turnSpeed = ch.m_turnSpeed,
                runTurnSpeed = ch.m_runTurnSpeed,
                acceleration = ch.m_acceleration,
                flying = ch.m_flying,
                flySlowSpeed = ch.m_flySlowSpeed,
                flyFastSpeed = ch.m_flyFastSpeed,
                flyTurnSpeed = ch.m_flyTurnSpeed,
                swimSpeed = ch.m_swimSpeed,
                swimTurnSpeed = ch.m_swimTurnSpeed,
                jumpForce = ch.m_jumpForce,
            };
            if (ch.m_weakSpots != null)
            {
                var keys = WeakSpotKeys(ch.m_weakSpots);
                int i = 0;
                foreach (var ws in ch.m_weakSpots)
                {
                    string key = keys[i++];
                    if (ws && p.weakSpots.Find(x => x.name == key) == null)
                        p.weakSpots.Add(new WeakSpotOverride { name = key, resists = ResistSet.From(ws.m_damageModifiers) });
                }
            }
            if (ai)
            {
                p.viewRange = ai.m_viewRange;
                p.viewAngle = ai.m_viewAngle;
                p.hearRange = ai.m_hearRange;
                p.mistVision = ai.m_mistVision;
            }
            if (mai)
            {
                p.hasMonsterAI = true;
                p.alertRange = mai.m_alertRange;
                p.minAttackInterval = mai.m_minAttackInterval;
                p.circleTargetInterval = mai.m_circleTargetInterval;
                p.fleeIfLowHealth = mai.m_fleeIfLowHealth;
            }

            if (ch is Humanoid h)
            {
                foreach (var entry in EnumerateItemPrefabs(h))
                {
                    var go = entry.Key;
                    var drop = go.GetComponent<ItemDrop>();
                    if (!drop || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                    if (!IsAttackItem(drop.m_itemData)) continue;
                    if (p.FindAttack(go.name, null) != null) continue;
                    var a = CaptureAttack(go, drop.m_itemData.m_shared, entry.Value);
                    p.attacks.Add(a);
                    foreach (var fx in ScanEffects(drop.m_itemData.m_shared))
                    {
                        if (!a.effects.Contains(fx.prefab)) a.effects.Add(fx.prefab);
                        if (p.FindEffect(fx.prefab) == null) p.effects.Add(fx);
                    }
                }
            }
            return p;
        }

        /// <summary>
        /// Ключи слабых мест по порядку: имя объекта, а у повторившегося имени — с номером («Имя #2»), чтобы
        /// одноимённые места с разными сопротивлениями не сливались в одно.
        /// </summary>
        public static List<string> WeakSpotKeys(IEnumerable<WeakSpot> spots)
        {
            var keys = new List<string>();
            var seen = new Dictionary<string, int>();
            foreach (var ws in spots)
            {
                string name = ws ? ws.gameObject.name : "";
                seen.TryGetValue(name, out int n);
                seen[name] = ++n;
                keys.Add(n == 1 ? name : name + " #" + n);
            }
            return keys;
        }

        public static bool IsAttackItem(ItemDrop.ItemData item)
        {
            var s = item.m_shared;
            return s.m_attack != null && item.IsWeapon();
        }

        /// <summary>Все предметы, которые гуманоид может получить при появлении, с подписью источника.</summary>
        public static IEnumerable<KeyValuePair<GameObject, string>> EnumerateItemPrefabs(Humanoid h)
        {
            if (h.m_defaultItems != null)
                foreach (var go in h.m_defaultItems)
                    if (go) yield return new KeyValuePair<GameObject, string>(go, "всегда");
            if (h.m_randomWeapon != null)
                foreach (var go in h.m_randomWeapon)
                    if (go) yield return new KeyValuePair<GameObject, string>(go, "случайное оружие");
            if (h.m_randomShield != null)
                foreach (var go in h.m_randomShield)
                    if (go) yield return new KeyValuePair<GameObject, string>(go, "случайный щит");
            if (h.m_randomSets != null)
                foreach (var set in h.m_randomSets)
                    if (set?.m_items != null)
                        foreach (var go in set.m_items)
                            if (go) yield return new KeyValuePair<GameObject, string>(go, "набор «" + set.m_name + "»");
            if (h.m_randomItems != null)
                foreach (var ri in h.m_randomItems)
                    if (ri != null && ri.m_prefab) yield return new KeyValuePair<GameObject, string>(ri.m_prefab, "случайный предмет");
        }

        private static AttackOverride CaptureAttack(GameObject go, ItemDrop.ItemData.SharedData s, string source)
        {
            var atk = s.m_attack;
            var a = new AttackOverride
            {
                item = go.name,
                sharedName = s.m_name,
                displayName = Loc(s.m_name),
                source = source,
                enabled = true,
                damage = DamageSet.From(s.m_damages),
                damagePerLevel = DamageSet.From(s.m_damagesPerLevel),
                attackForce = s.m_attackForce,
                backstabBonus = s.m_backstabBonus,
                blockable = s.m_blockable,
                dodgeable = s.m_dodgeable,
                aiAttackRange = s.m_aiAttackRange,
                aiAttackRangeMin = s.m_aiAttackRangeMin,
                aiAttackInterval = s.m_aiAttackInterval,
                aiAttackMaxAngle = s.m_aiAttackMaxAngle,
                aiPrioritized = s.m_aiPrioritized,
                aiMaxHealthPercentage = s.m_aiMaxHealthPercentage,
                aiMinHealthPercentage = s.m_aiMinHealthPercentage,
                captured = 2,
                statusEffect = s.m_attackStatusEffect ? s.m_attackStatusEffect.name : "",
                statusEffectChance = s.m_attackStatusEffectChance,
                aiTargetType = (int)s.m_aiTargetType,
                aiInDungeonOnly = s.m_aiInDungeonOnly,
                aiInMistOnly = s.m_aiInMistOnly,
                aiWhenFlying = s.m_aiWhenFlying,
                aiWhenWalking = s.m_aiWhenWalking,
                aiWhenSwiming = s.m_aiWhenSwiming,
            };
            if (atk != null)
            {
                a.damageMultiplier = atk.m_damageMultiplier;
                a.forceMultiplier = atk.m_forceMultiplier;
                a.staggerMultiplier = atk.m_staggerMultiplier;
                a.attackRange = atk.m_attackRange;
                a.attackAngle = atk.m_attackAngle;
                a.attackType = (int)atk.m_attackType;
                a.attackRayWidth = atk.m_attackRayWidth;
                a.hasProjectile = atk.m_attackProjectile;
                a.projectileVel = atk.m_projectileVel;
                a.projectileVelMin = atk.m_projectileVelMin;
                a.projectiles = atk.m_projectiles;
                a.projectileBursts = atk.m_projectileBursts;
                a.animation = atk.m_attackAnimation;
            }
            return a;
        }

        /// <summary>
        /// Эффекты с собственным уроном, которые атака может породить (AoE-удары, взрывы, лужи, осколки).
        /// Глубина — два уровня (атака → снаряд → то, что он порождает при попадании).
        /// </summary>
        public static List<EffectOverride> ScanEffects(ItemDrop.ItemData.SharedData s)
        {
            var result = new List<EffectOverride>();
            var seen = new HashSet<GameObject>();
            var atk = s.m_attack;
            if (atk != null)
            {
                Visit(atk.m_attackProjectile, true, false, 0, result, seen);
                Visit(atk.m_spawnOnTrigger, false, false, 0, result, seen);
                Visit(atk.m_spawnOnHit, false, false, 0, result, seen);
            }
            Visit(s.m_spawnOnHit, false, false, 0, result, seen);
            Visit(s.m_spawnOnHitTerrain, false, false, 0, result, seen);
            return result;
        }

        /// <param name="inheritsHit">Порождён снарядом, который передаёт ему урон атаки (m_projectilesInheritHitData).</param>
        private static void Visit(GameObject go, bool isDirectProjectile, bool inheritsHit, int depth, List<EffectOverride> result, HashSet<GameObject> seen)
        {
            if (!go || depth > 3 || !seen.Add(go)) return;
            var aoe = go.GetComponentInChildren<Aoe>(true);
            if (aoe)
            {
                // С уроном атаки (m_useAttackSettings и переданный hitData) собственный урон AoE не используется.
                bool usesAttackHit = aoe.m_useAttackSettings && (isDirectProjectile || inheritsHit);
                if (!usesAttackHit || aoe.m_damage.GetTotalDamage() > 0f)
                {
                    result.Add(new EffectOverride
                    {
                        prefab = go.name,
                        kind = "AoE",
                        damage = DamageSet.From(aoe.m_damage),
                        attackForce = aoe.m_attackForce
                    });
                }
                Visit(aoe.m_spawnOnHitTerrain, false, false, depth + 1, result, seen);
                Visit(aoe.m_chainObj, false, false, depth + 1, result, seen);
            }
            var proj = go.GetComponentInChildren<Projectile>(true);
            if (proj)
            {
                // m_onlySpawnedProjectilesDealDamage: игра обнуляет урон самого снаряда — бьют только порождённые.
                if (!isDirectProjectile && !proj.m_onlySpawnedProjectilesDealDamage && proj.m_damage.GetTotalDamage() > 0f)
                {
                    result.Add(new EffectOverride
                    {
                        prefab = go.name,
                        kind = "снаряд",
                        damage = DamageSet.From(proj.m_damage),
                        attackForce = proj.m_attackForce
                    });
                }
                bool pass = proj.m_projectilesInheritHitData;
                Visit(proj.m_spawnOnHit, false, pass, depth + 1, result, seen);
                if (proj.m_randomSpawnOnHit != null)
                    foreach (var r in proj.m_randomSpawnOnHit)
                        Visit(r, false, pass, depth + 1, result, seen);
            }
            // Способность призыва (боссы, посохи) создаёт свой AoE без урона атаки.
            var ability = go.GetComponentInChildren<SpawnAbility>(true);
            if (ability) Visit(ability.m_aoePrefab, false, false, depth + 1, result, seen);
        }

        public static string Loc(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            try { return Localization.instance != null ? Localization.instance.Localize(token) : token; }
            catch { return token; }
        }

        // ------------------------------------------------------------------ применение к бойцу

        /// <summary>Параметры самого существа (без атак). Здоровье пересчитывается с сохранением доли.</summary>
        public static void ApplyBase(ArenaUnit unit, CreatureProfile p, bool keepHealthFraction)
        {
            var ch = unit.Character;
            if (!ch || p == null) return;

            if (p.health > 0f) ch.m_health = p.health;
            unit.transform.localScale = unit.BaseScale * Mathf.Max(0.05f, p.scale);
            if (p.resists != null) ch.m_damageModifiers = p.resists.ToModifiers();
            // Слабые места — компоненты экземпляра: правка касается только этого бойца.
            if (ch.m_weakSpots != null && p.weakSpots != null && p.weakSpots.Count > 0)
            {
                var keys = WeakSpotKeys(ch.m_weakSpots);
                int i = 0;
                foreach (var ws in ch.m_weakSpots)
                {
                    string key = keys[i++];
                    if (!ws) continue;
                    // Профили, снятые до номеров у одноимённых мест, правят все места с этим именем.
                    var o = p.weakSpots.Find(x => x.name == key) ?? p.weakSpots.Find(x => x.name == ws.gameObject.name);
                    if (o != null) ws.m_damageModifiers = o.resists.ToModifiers();
                }
            }
            ch.m_staggerDamageFactor = p.staggerDamageFactor;
            ch.m_staggerWhenBlocked = p.staggerWhenBlocked;
            if (p.regenAllHPTime > 0f) ch.m_regenAllHPTime = p.regenAllHPTime;

            ch.m_walkSpeed = p.walkSpeed;
            ch.m_speed = p.speed;
            ch.m_runSpeed = p.runSpeed;
            ch.m_turnSpeed = p.turnSpeed;
            ch.m_runTurnSpeed = p.runTurnSpeed;
            ch.m_acceleration = p.acceleration;
            ch.m_flySlowSpeed = p.flySlowSpeed;
            ch.m_flyFastSpeed = p.flyFastSpeed;
            ch.m_jumpForce = p.jumpForce;
            if (p.version >= 2)
            {
                if (p.flyTurnSpeed > 0f) ch.m_flyTurnSpeed = p.flyTurnSpeed;
                if (p.swimSpeed > 0f) ch.m_swimSpeed = p.swimSpeed;
                if (p.swimTurnSpeed > 0f) ch.m_swimTurnSpeed = p.swimTurnSpeed;
            }

            var ai = unit.AI;
            if (ai)
            {
                ai.m_viewRange = p.viewRange;
                unit.BaseViewRange = p.viewRange;
                ai.m_viewAngle = p.viewAngle;
                ai.m_hearRange = p.hearRange;
                ai.m_mistVision = p.mistVision;
            }
            var mai = unit.Monster;
            if (mai && p.hasMonsterAI)
            {
                mai.m_alertRange = p.alertRange;
                mai.m_minAttackInterval = p.minAttackInterval;
                mai.m_circleTargetInterval = p.circleTargetInterval;
                mai.m_fleeIfLowHealth = p.fleeIfLowHealth;
            }

            unit.DamageDealtMultiplier = p.damageDealtMultiplier;
            unit.DamageTakenMultiplier = p.damageTakenMultiplier;
            unit.Effects.Clear();
            foreach (var e in p.effects)
                if (e.modified) unit.Effects[e.prefab] = e;
            unit.DisabledAttacks.Clear();
            foreach (var a in p.attacks)
                if (!a.enabled) unit.DisabledAttacks.Add(a.item);

            if (keepHealthFraction) RecalculateHealth(unit);
        }

        /// <summary>
        /// Максимум HP по профилю и уровню (как Character.SetupMaxHealth) с сохранением доли. Свой максимум
        /// пишется в ZDO: при повторном создании существа у владельца Character.Awake заново считает максимум по
        /// ванильному m_health (если HP полное), и доля «HP / максимум» у бойца со слабым профилем оказалась бы
        /// меньше единицы — он терял бы здоровье. Долю берём и по своему максимуму, и по игровому — большую.
        /// </summary>
        public static void RecalculateHealth(ArenaUnit unit)
        {
            var ch = unit.Character;
            var nview = unit.NView;
            if (!ch || !nview || !nview.IsValid() || !nview.IsOwner()) return;
            var zdo = nview.GetZDO();
            float frac = Mathf.Clamp01(ch.GetHealthPercentage());
            float stored = zdo.GetFloat(ArenaUnit.ZMaxHealth, 0f);
            if (stored > 0f) frac = Mathf.Max(frac, Mathf.Clamp01(ch.GetHealth() / stored));
            float max = ch.GetMaxHealthBase() * ch.GetLevel();
            ch.SetMaxHealth(max);
            ch.SetHealth(Mathf.Max(1f, max * frac));
            zdo.Set(ArenaUnit.ZMaxHealth, max);
        }

        /// <summary>Атаки: собственная копия SharedData на каждый предмет бойца, затем запись значений профиля.</summary>
        public static void ApplyItems(ArenaUnit unit, CreatureProfile p)
        {
            if (!(unit.Character is Humanoid h)) return;
            var inv = h.GetInventory();
            if (inv == null) return;
            foreach (var item in inv.GetAllItems())
            {
                if (item?.m_shared == null) continue;
                var key = ItemKey(item);
                var a = p?.FindAttack(key, item.m_shared.m_name);
                if (a == null)
                {
                    RestoreItem(unit, item);
                    continue;
                }
                EnsureOwnShared(unit, item);
                var s = item.m_shared;
                s.m_damages = a.damage.ToDamageTypes();
                s.m_damagesPerLevel = a.damagePerLevel.ToDamageTypes();
                s.m_attackForce = a.attackForce;
                s.m_backstabBonus = a.backstabBonus;
                s.m_blockable = a.blockable;
                s.m_dodgeable = a.dodgeable;
                s.m_aiAttackRange = a.aiAttackRange;
                s.m_aiAttackRangeMin = a.aiAttackRangeMin;
                s.m_aiAttackInterval = a.aiAttackInterval;
                s.m_aiAttackMaxAngle = Mathf.Max(0.1f, a.aiAttackMaxAngle);
                s.m_aiPrioritized = a.aiPrioritized;
                s.m_aiMaxHealthPercentage = a.aiMaxHealthPercentage;
                s.m_aiMinHealthPercentage = a.aiMinHealthPercentage;
                if (a.captured >= 2)
                {
                    if (s.m_attackStatusEffect) s.m_attackStatusEffectChance = Mathf.Clamp01(a.statusEffectChance);
                    s.m_aiInDungeonOnly = a.aiInDungeonOnly;
                    s.m_aiInMistOnly = a.aiInMistOnly;
                }
                var atk = s.m_attack;
                if (atk != null)
                {
                    atk.m_damageMultiplier = a.damageMultiplier;
                    atk.m_forceMultiplier = a.forceMultiplier;
                    atk.m_staggerMultiplier = a.staggerMultiplier;
                    atk.m_attackRange = a.attackRange;
                    atk.m_attackAngle = a.attackAngle;
                    if (a.captured >= 2 && a.attackRayWidth > 0f) atk.m_attackRayWidth = a.attackRayWidth;
                    if (atk.m_attackProjectile)
                    {
                        atk.m_projectileVel = a.projectileVel;
                        atk.m_projectileVelMin = a.projectileVelMin;
                        atk.m_projectiles = Mathf.Max(1, a.projectiles);
                        atk.m_projectileBursts = Mathf.Max(1, a.projectileBursts);
                    }
                }
            }
        }

        /// <summary>Вернуть всем предметам бойца общие ванильные SharedData.</summary>
        public static void RestoreAllItems(ArenaUnit unit)
        {
            foreach (var kv in new List<KeyValuePair<ItemDrop.ItemData, ItemDrop.ItemData.SharedData>>(unit.OriginalShared))
                kv.Key.m_shared = kv.Value;
            unit.OriginalShared.Clear();
        }

        private static void RestoreItem(ArenaUnit unit, ItemDrop.ItemData item)
        {
            if (unit.OriginalShared.TryGetValue(item, out var orig))
            {
                item.m_shared = orig;
                unit.OriginalShared.Remove(item);
            }
        }

        public static string ItemKey(ItemDrop.ItemData item)
        {
            if (item.m_dropPrefab) return item.m_dropPrefab.name;
            return "";
        }

        private static void EnsureOwnShared(ArenaUnit unit, ItemDrop.ItemData item)
        {
            if (unit.OriginalShared.ContainsKey(item)) return;
            var orig = item.m_shared;
            var copy = (ItemDrop.ItemData.SharedData)MemberwiseCloneMethod.Invoke(orig, null);
            if (orig.m_attack != null) copy.m_attack = orig.m_attack.Clone();
            if (orig.m_secondaryAttack != null) copy.m_secondaryAttack = orig.m_secondaryAttack.Clone();
            item.m_shared = copy;
            unit.OriginalShared[item] = orig;
        }
    }
}
