using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;

namespace BinakayanRising.Core.Meta
{
    /// <summary>A unit going up one or more levels: what the promotion card shows.</summary>
    public sealed class LevelUp
    {
        public int UnitId;
        public string Archetype;
        public int FromLevel;
        public int ToLevel;
        public UnitStats Before;
        public UnitStats After;
    }

    /// <summary>One recruit from the Recruitment Hall.</summary>
    public sealed class PullResult
    {
        public string Archetype;
        public UnitRarity Rarity;

        /// <summary>True when the pull added a new unit to the roster.</summary>
        public bool NewUnit;

        /// <summary>The unit that was added, or the Hero that was trained by a duplicate.</summary>
        public int UnitId;

        /// <summary>XP given to an already-recruited Hero instead of a second copy.</summary>
        public int DuplicateXp;

        /// <summary>True when the pity counter forced this Hero.</summary>
        public bool FromPity;

        /// <summary>A duplicate Hero's XP may itself level the Hero up.</summary>
        public LevelUp LevelUp;
    }

    public sealed partial class MetaGame
    {
        // ------------------------------------------------------------------ roster

        public IReadOnlyList<OwnedUnit> Units
        {
            get { return Data.units; }
        }

        public OwnedUnit FindUnit(int unitId)
        {
            for (int i = 0; i < Data.units.Count; i++)
            {
                if (Data.units[i].id == unitId)
                {
                    return Data.units[i];
                }
            }

            return null;
        }

        /// <summary>An archetype's stats at <paramref name="level"/>, before any weapon.</summary>
        public UnitStats StatsAt(string archetype, int level)
        {
            UnitArchetype def = UnitCatalog.Find(archetype);
            if (def == null)
            {
                return UnitStats.Zero;
            }

            int steps = (level < 1 ? 1 : level) - 1;
            UnitStats stats = def.BaseStats;
            stats = stats.With(StatKind.MaxHP, Round1(stats.MaxHP * (1f + Rules.HpGrowthPerLevel * steps)));
            stats = stats.With(StatKind.AttackDamage, Round1(stats.AttackDamage * (1f + Rules.AttackGrowthPerLevel * steps)));
            stats = stats.With(StatKind.Defense, Round1(stats.Defense * (1f + Rules.DefenseGrowthPerLevel * steps)));
            return stats;
        }

        /// <summary>A unit's battle stats: its level plus its weapon.</summary>
        public UnitStats StatsOf(OwnedUnit unit)
        {
            UnitStats stats = StatsAt(unit.archetype, unit.level);
            WeaponDef weapon = WeaponOf(unit);
            if (weapon != null)
            {
                stats = stats.With(StatKind.AttackDamage, stats.AttackDamage + weapon.AttackBonus);
            }

            return stats;
        }

        private static float Round1(float value)
        {
            return (float)System.Math.Round(value, 1);
        }

        public int XpToNext(OwnedUnit unit)
        {
            return Rules.XpToNext(unit.level);
        }

        public bool IsMaxLevel(OwnedUnit unit)
        {
            return unit.level >= Rules.LevelCap;
        }

        /// <summary>
        /// Adds XP and resolves any level-ups, carrying the overflow. XP past the level cap is
        /// discarded.
        /// </summary>
        /// <returns>The level-up, or null when the unit stayed at its level.</returns>
        private LevelUp GrantXp(OwnedUnit unit, int xp)
        {
            if (xp <= 0 || IsMaxLevel(unit))
            {
                return null;
            }

            int from = unit.level;
            UnitStats before = StatsOf(unit);
            unit.xp += xp;

            while (!IsMaxLevel(unit) && unit.xp >= Rules.XpToNext(unit.level))
            {
                unit.xp -= Rules.XpToNext(unit.level);
                unit.level++;
            }

            if (IsMaxLevel(unit))
            {
                unit.xp = 0;
            }

            if (unit.level == from)
            {
                return null;
            }

            return new LevelUp
            {
                UnitId = unit.id,
                Archetype = unit.archetype,
                FromLevel = from,
                ToLevel = unit.level,
                Before = before,
                After = StatsOf(unit)
            };
        }

        /// <summary>Gives <paramref name="xp"/> to each listed unit.</summary>
        /// <returns>Every level-up that resulted, in the order given.</returns>
        public List<LevelUp> AwardXp(IList<int> unitIds, int xp)
        {
            var ups = new List<LevelUp>();
            for (int i = 0; i < unitIds.Count; i++)
            {
                OwnedUnit unit = FindUnit(unitIds[i]);
                if (unit == null)
                {
                    continue;
                }

                LevelUp up = GrantXp(unit, xp);
                if (up != null)
                {
                    ups.Add(up);
                }
            }

            RaiseChanged();
            return ups;
        }

        /// <summary>Whether <paramref name="unit"/> can be drilled right now.</summary>
        public bool CanDrill(OwnedUnit unit)
        {
            return unit != null && !IsMaxLevel(unit) && CanAfford(Rules.DrillCost);
        }

        /// <summary>One paid drill at the Training Grounds.</summary>
        /// <param name="levelUp">The resulting level-up, or null if the unit only gained XP.</param>
        /// <returns>False when the drill was refused (unknown unit, max level, or unaffordable).</returns>
        public bool TryDrill(int unitId, out LevelUp levelUp)
        {
            levelUp = null;
            OwnedUnit unit = FindUnit(unitId);
            if (!CanDrill(unit))
            {
                return false;
            }

            Data.reales -= Rules.DrillCost.Reales;
            Data.rations -= Rules.DrillCost.Rations;
            Data.scrap -= Rules.DrillCost.Scrap;
            levelUp = GrantXp(unit, Rules.DrillXp);
            MarkTaskSilently(Campaign.TaskTrain);
            RaiseChanged();
            return true;
        }

        // ------------------------------------------------------------------ armoury

        public IReadOnlyList<OwnedWeapon> Weapons
        {
            get { return Data.weapons; }
        }

        public OwnedWeapon FindWeapon(int weaponId)
        {
            for (int i = 0; i < Data.weapons.Count; i++)
            {
                if (Data.weapons[i].id == weaponId)
                {
                    return Data.weapons[i];
                }
            }

            return null;
        }

        public WeaponDef WeaponOf(OwnedUnit unit)
        {
            if (unit == null || unit.weaponId == 0)
            {
                return null;
            }

            OwnedWeapon owned = FindWeapon(unit.weaponId);
            return owned == null ? null : WeaponCatalog.Find(owned.weapon);
        }

        /// <summary>The unit holding <paramref name="weaponId"/>, or null when it is on the rack.</summary>
        public OwnedUnit HolderOf(int weaponId)
        {
            for (int i = 0; i < Data.units.Count; i++)
            {
                if (Data.units[i].weaponId == weaponId)
                {
                    return Data.units[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Puts <paramref name="weaponId"/> in <paramref name="unitId"/>'s hands, taking it from
        /// whoever held it. Pass 0 to disarm the unit.
        /// </summary>
        public bool TryEquip(int unitId, int weaponId)
        {
            OwnedUnit unit = FindUnit(unitId);
            if (unit == null || (weaponId != 0 && FindWeapon(weaponId) == null))
            {
                return false;
            }

            if (weaponId != 0)
            {
                OwnedUnit holder = HolderOf(weaponId);
                if (holder != null)
                {
                    holder.weaponId = 0;
                }

                MarkTaskSilently(Campaign.TaskEquip);
            }

            unit.weaponId = weaponId;
            RaiseChanged();
            return true;
        }

        public bool CanSynthesize(OwnedWeapon weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            WeaponDef def = WeaponCatalog.Find(weapon.weapon);
            return def != null && def.UpgradesTo != null && CanAfford(def.SynthesisCost);
        }

        /// <summary>
        /// Reforges a weapon into the next tier in place: same id, so whoever holds it keeps
        /// holding it.
        /// </summary>
        public bool TrySynthesize(int weaponId)
        {
            OwnedWeapon weapon = FindWeapon(weaponId);
            if (!CanSynthesize(weapon))
            {
                return false;
            }

            WeaponDef def = WeaponCatalog.Find(weapon.weapon);
            Data.reales -= def.SynthesisCost.Reales;
            Data.rations -= def.SynthesisCost.Rations;
            Data.scrap -= def.SynthesisCost.Scrap;
            weapon.weapon = def.UpgradesTo;
            RaiseChanged();
            return true;
        }

        private OwnedWeapon AddWeaponSilently(string weapon)
        {
            var owned = new OwnedWeapon { id = Data.nextWeaponId++, weapon = weapon };
            Data.weapons.Add(owned);
            return owned;
        }

        // ------------------------------------------------------------------ recruiting

        public int PullCost(int count)
        {
            return count >= Rules.MultiPullCount ? Rules.MultiPullCost : Rules.SinglePullCost * count;
        }

        /// <summary>Pulls left until a Hero is guaranteed (1 means the next pull is one).</summary>
        public int PullsToPity
        {
            get
            {
                int left = Rules.PityThreshold - Data.pity;
                return left < 1 ? 1 : left;
            }
        }

        /// <summary>The published chance of each rarity, 0..1, Common, Rare, Hero.</summary>
        public float RarityChance(UnitRarity rarity)
        {
            int total = 0;
            for (int i = 0; i < Rules.RarityWeights.Length; i++)
            {
                total += Rules.RarityWeights[i];
            }

            int index = (int)rarity;
            return total <= 0 || index >= Rules.RarityWeights.Length ? 0f : Rules.RarityWeights[index] / (float)total;
        }

        /// <summary>
        /// Recruits <paramref name="count"/> units for Reales. Each pull draws from its own stream,
        /// seeded by the campaign seed and the pull's number, so saving and reloading cannot
        /// change what a pull gives.
        /// </summary>
        /// <returns>The recruits, or null when the purse cannot pay.</returns>
        public List<PullResult> TryPull(int count)
        {
            if (count < 1)
            {
                return null;
            }

            int cost = PullCost(count);
            if (Data.reales < cost)
            {
                return null;
            }

            Data.reales -= cost;
            var results = new List<PullResult>();

            for (int i = 0; i < count; i++)
            {
                results.Add(PullOnce());
            }

            MarkTaskSilently(Campaign.TaskRecruit);
            RaiseChanged();
            return results;
        }

        private PullResult PullOnce()
        {
            var random = new DeterministicRandom(unchecked(Data.gachaSeed * 7919 + Data.gachaPulls * 104729 + 1));
            Data.gachaPulls++;

            bool pity = Data.pity + 1 >= Rules.PityThreshold;
            UnitRarity rarity = pity ? UnitRarity.Hero : RollRarity(random);

            List<UnitArchetype> pool = UnitCatalog.Recruitable(rarity);
            if (pool.Count == 0)
            {
                pool = UnitCatalog.Recruitable(UnitRarity.Common);
            }

            UnitArchetype pick = pool[random.NextInt(0, pool.Count)];
            Data.pity = rarity == UnitRarity.Hero ? 0 : Data.pity + 1;

            var result = new PullResult { Archetype = pick.Id, Rarity = rarity, FromPity = pity && rarity == UnitRarity.Hero };

            OwnedUnit existing = pick.Unique ? FirstOf(pick.Id) : null;
            if (existing != null)
            {
                result.UnitId = existing.id;
                result.DuplicateXp = Rules.DuplicateHeroXp;
                result.LevelUp = GrantXp(existing, Rules.DuplicateHeroXp);
                return result;
            }

            var unit = new OwnedUnit { id = Data.nextUnitId++, archetype = pick.Id, level = 1 };
            Data.units.Add(unit);
            result.NewUnit = true;
            result.UnitId = unit.id;
            return result;
        }

        private UnitRarity RollRarity(DeterministicRandom random)
        {
            int total = 0;
            for (int i = 0; i < Rules.RarityWeights.Length; i++)
            {
                total += Rules.RarityWeights[i] < 0 ? 0 : Rules.RarityWeights[i];
            }

            if (total <= 0)
            {
                return UnitRarity.Common;
            }

            int roll = random.NextInt(0, total);
            for (int i = 0; i < Rules.RarityWeights.Length; i++)
            {
                int weight = Rules.RarityWeights[i] < 0 ? 0 : Rules.RarityWeights[i];
                if (roll < weight)
                {
                    return (UnitRarity)i;
                }

                roll -= weight;
            }

            return UnitRarity.Common;
        }

        private OwnedUnit FirstOf(string archetype)
        {
            for (int i = 0; i < Data.units.Count; i++)
            {
                if (Data.units[i].archetype == archetype)
                {
                    return Data.units[i];
                }
            }

            return null;
        }
    }
}
