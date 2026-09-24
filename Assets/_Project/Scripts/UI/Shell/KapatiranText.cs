using System.Collections.Generic;
using System.Text;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using UnityEngine;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// How the Kapatiran bonds are phrased on screen (#19): a pair's name, a rank's bonus in the
    /// short stat names, and one soldier's bond line in the Training Grounds.
    /// </summary>
    public static class KapatiranText
    {
        /// <summary>"Evangelista + Aguinaldo", in the player's language.</summary>
        public static string PairName(BondPair pair)
        {
            return UnitName(pair.ArchetypeA) + " + " + UnitName(pair.ArchetypeB);
        }

        /// <summary>A unit archetype's name, or the id when it is unknown.</summary>
        public static string UnitName(string archetypeId)
        {
            UnitArchetype archetype = UnitCatalog.Find(archetypeId);
            return archetype != null ? archetype.Name.Get() : archetypeId;
        }

        /// <summary>
        /// The bonus <paramref name="rank"/> gives the pair, e.g. "ATK +15%, DEF +10%", or null when
        /// that rank gives none (no rank, and rank C).
        /// </summary>
        public static string Bonus(string bondId, BondRank rank)
        {
            KapatiranBond bond = BondCatalog.Create(bondId, rank);
            if (bond == null)
            {
                return null;
            }

            var effects = new List<string>();
            for (int m = 0; m < bond.Modifiers.Count; m++)
            {
                effects.Add(ModifierText(bond.Modifiers[m]));
            }

            return string.Join(", ", effects);
        }

        /// <summary>
        /// "Bond: partner · rank B · ACC +10%" for every pair <paramref name="archetypeId"/> is in,
        /// at the rank <paramref name="game"/> has earned, or null when it is in none.
        /// </summary>
        public static string ForUnit(MetaGame game, string archetypeId)
        {
            BondPair pair = BondCatalog.PairOf(archetypeId);
            if (pair == null)
            {
                return null;
            }

            BondRank rank = game != null ? game.BondRankOf(pair.Id) : BondRank.None;
            if (rank == BondRank.None)
            {
                return Loc.Format(TextKey.TrnBondUnranked, UnitName(pair.PartnerOf(archetypeId)));
            }

            string bonus = Bonus(pair.Id, rank) ?? Loc.Get(TextKey.BondNoBonusShort);
            return Loc.Format(TextKey.TrnBondRanked, UnitName(pair.PartnerOf(archetypeId)), BondCatalog.Label(rank), bonus);
        }

        /// <summary>"ATK +15%" or "RNG +1".</summary>
        public static string ModifierText(StatModifier modifier)
        {
            var text = new StringBuilder(Loc.Get(StatShort(modifier.Stat)));
            if (modifier.HasPercent)
            {
                text.Append(' ').Append(modifier.PercentDelta >= 0f ? "+" : string.Empty)
                    .Append(Mathf.RoundToInt(modifier.PercentDelta * 100f)).Append('%');
            }

            if (modifier.HasFlat)
            {
                text.Append(' ').Append(modifier.FlatDelta >= 0f ? "+" : string.Empty)
                    .Append(PromotionCard.StatNumber(modifier.FlatDelta));
            }

            return text.ToString();
        }

        /// <summary>The short on-card name of a stat.</summary>
        public static TextKey StatShort(StatKind stat)
        {
            switch (stat)
            {
                case StatKind.MaxHP: return TextKey.TrnStatHp;
                case StatKind.AttackDamage: return TextKey.TrnStatAtk;
                case StatKind.Defense: return TextKey.TrnStatDef;
                case StatKind.Evasion: return TextKey.TrnStatEva;
                case StatKind.RangedAccuracy: return TextKey.TrnStatAcc;
                case StatKind.AttackRange: return TextKey.TrnStatRng;
                case StatKind.CriticalHitChance: return TextKey.TrnStatCrit;
                case StatKind.MovementSpeed: return TextKey.TrnStatMove;
                default: return TextKey.TrnStatHeal;
            }
        }
    }
}
