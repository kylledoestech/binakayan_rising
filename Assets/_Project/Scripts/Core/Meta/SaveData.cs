using System;
using System.Collections.Generic;
using BinakayanRising.Core.Content;

namespace BinakayanRising.Core.Meta
{
    /// <summary>
    /// Everything that persists between sessions: the save file, as data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shaped for Unity's <c>JsonUtility</c>: public fields, <see cref="List{T}"/> rather than
    /// dictionaries, no properties, no polymorphism. The file is written whole and read whole
    /// (DESIGN-DECISIONS #22), so there is no partial-update path to get wrong.
    /// </para>
    /// <para>
    /// Rules never live here. <see cref="MetaGame"/> owns every change to this object; the only
    /// thing this class does on its own is repair a file that was edited or truncated into an
    /// impossible state, see <see cref="Repair"/>.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Bumped whenever a field changes meaning. Older files are migrated on load.</summary>
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;

        /// <summary>UTC ticks when this campaign was started.</summary>
        public long createdUtcTicks;

        /// <summary>UTC ticks of the last write.</summary>
        public long savedUtcTicks;

        /// <summary>Total seconds of play across every session.</summary>
        public double playSeconds;

        public int reales;
        public int rations;
        public int scrap;

        public FacilityState farm = new FacilityState();
        public FacilityState mine = new FacilityState();

        public List<OwnedUnit> units = new List<OwnedUnit>();
        public List<OwnedWeapon> weapons = new List<OwnedWeapon>();
        public int nextUnitId = 1;
        public int nextWeaponId = 1;

        /// <summary>Ids of cleared campaign sub-quests.</summary>
        public List<string> clearedQuests = new List<string>();

        /// <summary>
        /// One-way progress markers: cutscenes seen, facilities visited, hub-task steps done,
        /// aide explanations given. A flag is only ever added, never removed.
        /// </summary>
        public List<string> flags = new List<string>();

        /// <summary>Ids of unlocked lessons in the Learning panel.</summary>
        public List<string> lessons = new List<string>();

        public List<AssessmentRecord> assessments = new List<AssessmentRecord>();

        /// <summary>
        /// The highest player rank the player has been <i>shown</i>. When the earned rank is above
        /// this, a promotion card is owed.
        /// </summary>
        public int rankShown;

        /// <summary>Recruiting draws from a stream seeded here, so reloading cannot re-roll a pull.</summary>
        public int gachaSeed;
        public int gachaPulls;

        /// <summary>Pulls since the last Hero.</summary>
        public int pity;

        public int quizAnswered;
        public int quizCorrect;

        /// <summary>Quiz questions already asked in battle, so the bank does not repeat itself.</summary>
        public List<string> askedQuestions = new List<string>();

        /// <summary>
        /// Kapatiran support earned per bonded pair (#19). Version 3 added it; a pair with no
        /// record has earned nothing. The rank is derived from the points, never stored.
        /// </summary>
        public List<BondRecord> bonds = new List<BondRecord>();

        public bool HasFlag(string flag)
        {
            return flags.Contains(flag);
        }

        private static bool MayHold(OwnedUnit unit, string weapon)
        {
            WeaponDef def = WeaponCatalog.Find(weapon);
            return def == null || def.Allows(unit.archetype);
        }

        /// <summary>
        /// Brings a loaded file back into a state every system can trust: no null lists, no
        /// negative purse, no duplicate or dangling ids, no level outside the cap.
        /// </summary>
        /// <returns>True when anything had to be changed.</returns>
        public bool Repair(MetaRules rules)
        {
            bool changed = false;

            if (farm == null) { farm = new FacilityState(); changed = true; }
            if (mine == null) { mine = new FacilityState(); changed = true; }
            if (units == null) { units = new List<OwnedUnit>(); changed = true; }
            if (weapons == null) { weapons = new List<OwnedWeapon>(); changed = true; }
            if (clearedQuests == null) { clearedQuests = new List<string>(); changed = true; }
            if (flags == null) { flags = new List<string>(); changed = true; }
            if (lessons == null) { lessons = new List<string>(); changed = true; }
            if (assessments == null) { assessments = new List<AssessmentRecord>(); changed = true; }
            if (askedQuestions == null) { askedQuestions = new List<string>(); changed = true; }
            if (bonds == null) { bonds = new List<BondRecord>(); changed = true; }

            if (reales < 0) { reales = 0; changed = true; }
            if (rations < 0) { rations = 0; changed = true; }
            if (scrap < 0) { scrap = 0; changed = true; }
            if (pity < 0) { pity = 0; changed = true; }
            if (playSeconds < 0 || double.IsNaN(playSeconds)) { playSeconds = 0; changed = true; }

            // Forward passes, so when an id appears twice the first entry — the original — wins.
            var seenUnits = new HashSet<int>();
            var keptUnits = new List<OwnedUnit>(units.Count);
            for (int i = 0; i < units.Count; i++)
            {
                OwnedUnit unit = units[i];
                if (unit == null || string.IsNullOrEmpty(unit.archetype) || unit.id <= 0 || !seenUnits.Add(unit.id))
                {
                    changed = true;
                    continue;
                }

                int level = unit.level < 1 ? 1 : (unit.level > rules.LevelCap ? rules.LevelCap : unit.level);
                if (level != unit.level) { unit.level = level; changed = true; }
                if (unit.xp < 0) { unit.xp = 0; changed = true; }
                keptUnits.Add(unit);
            }

            units = keptUnits;

            var seenWeapons = new HashSet<int>();
            var keptWeapons = new List<OwnedWeapon>(weapons.Count);
            for (int i = 0; i < weapons.Count; i++)
            {
                OwnedWeapon weapon = weapons[i];
                if (weapon == null || string.IsNullOrEmpty(weapon.weapon) || weapon.id <= 0 || !seenWeapons.Add(weapon.id))
                {
                    changed = true;
                    continue;
                }

                keptWeapons.Add(weapon);
            }

            weapons = keptWeapons;

            // A unit holding a weapon that no longer exists, two units holding the same one, or a
            // unit holding a weapon reserved to another archetype (the Engineer's Lantaka).
            var kinds = new Dictionary<int, string>();
            for (int i = 0; i < weapons.Count; i++)
            {
                kinds[weapons[i].id] = weapons[i].weapon;
            }

            var held = new HashSet<int>();
            for (int i = 0; i < units.Count; i++)
            {
                int weaponId = units[i].weaponId;
                if (weaponId != 0 && (!seenWeapons.Contains(weaponId) || !held.Add(weaponId) || !MayHold(units[i], kinds[weaponId])))
                {
                    units[i].weaponId = 0;
                    changed = true;
                }
            }

            int maxUnit = 0;
            foreach (int id in seenUnits) { if (id > maxUnit) maxUnit = id; }
            if (nextUnitId <= maxUnit) { nextUnitId = maxUnit + 1; changed = true; }

            int maxWeapon = 0;
            foreach (int id in seenWeapons) { if (id > maxWeapon) maxWeapon = id; }
            if (nextWeaponId <= maxWeapon) { nextWeaponId = maxWeapon + 1; changed = true; }

            // Version 2 replaced the four-rank ladder with eight; carry the shown rank across so an
            // old save is not owed a promotion it already saw.
            if (version < 2)
            {
                rankShown = PlayerRanks.FromLevelLadder(rankShown);
                changed = true;
            }

            // Version 3 added Kapatiran support. Older saves start every pair at no rank: nothing
            // was ever earned, and the rank-A bonds the old build handed out were not progress.
            // One record per known pair, never negative.
            var seenBonds = new HashSet<string>();
            var keptBonds = new List<BondRecord>(bonds.Count);
            for (int i = 0; i < bonds.Count; i++)
            {
                BondRecord record = bonds[i];
                if (record == null || BondCatalog.Find(record.bond) == null || !seenBonds.Add(record.bond))
                {
                    changed = true;
                    continue;
                }

                if (record.support < 0) { record.support = 0; changed = true; }
                keptBonds.Add(record);
            }

            bonds = keptBonds;

            if (version < CurrentVersion) { version = CurrentVersion; changed = true; }

            return changed;
        }
    }

    /// <summary>A Farm or Mine: when it was last emptied.</summary>
    [Serializable]
    public sealed class FacilityState
    {
        /// <summary>
        /// UTC ticks from which production is counted. Harvesting moves this forward by exactly
        /// the time the harvested goods took to make, so partial progress is never thrown away.
        /// </summary>
        public long sinceUtcTicks;
    }

    /// <summary>One recruited unit.</summary>
    [Serializable]
    public sealed class OwnedUnit
    {
        public int id;
        public string archetype;
        public int level = 1;

        /// <summary>XP towards the next level. Resets on level-up; the overflow carries.</summary>
        public int xp;

        /// <summary>Id of the equipped <see cref="OwnedWeapon"/>, or 0 for bare hands.</summary>
        public int weaponId;
    }

    /// <summary>One weapon in the armoury.</summary>
    [Serializable]
    public sealed class OwnedWeapon
    {
        public int id;

        /// <summary>Weapon type id from the weapon catalog.</summary>
        public string weapon;
    }

    /// <summary>Kapatiran support earned by one bonded pair.</summary>
    [Serializable]
    public sealed class BondRecord
    {
        /// <summary>A <see cref="BondCatalog"/> pair id.</summary>
        public string bond;

        /// <summary>Battles fought side by side, in support points.</summary>
        public int support;
    }

    /// <summary>The player's record on one level's assessment.</summary>
    [Serializable]
    public sealed class AssessmentRecord
    {
        public int level;
        public int best;
        public int total;
        public int attempts;
        public bool passed;
    }
}
