using System;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;

namespace BinakayanRising.Core.Content
{
    /// <summary>
    /// The Kapatiran bonds that join the campaign's newer units, built as Core
    /// <see cref="KapatiranBond"/> objects so the battle and the offline tests use the same values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core does not model rank progression. Each factory returns the modifiers of one rank, taken
    /// at face value: a higher rank <em>replaces</em> the lower one's modifiers rather than adding to
    /// them, the same reading <c>KapatiranBondAdapter</c> uses by default.
    /// </para>
    /// <para>
    /// Every bonus is a percentage of the unit's own stat, as in Capstone Table 3 and the terrain
    /// table: "+10% Evasion" on a 0.05 base gives 0.055, not 0.15.
    /// </para>
    /// </remarks>
    public static class BondCatalog
    {
        /// <summary>Bond id of the Katipunero Vanguard and Field Medic pair.</summary>
        public const string VanguardFieldMedic = "Vanguard_FieldMedic";

        /// <summary>Bond id of the Magdalo and Magdiwang infantry pair.</summary>
        public const string MagdaloMagdiwang = "Magdalo_Magdiwang";

        /// <summary>Rank label for rank B.</summary>
        public const string RankB = "B";

        /// <summary>Rank label for rank A.</summary>
        public const string RankA = "A";

        private const string VanguardFieldMedicSource = "Vanguard + Field Medic";
        private const string MagdaloMagdiwangSource = "Magdalo + Magdiwang";

        /// <summary>
        /// The Vanguard and Field Medic bond at rank A: +25% Healing Received and +5% Max HP.
        /// </summary>
        public static KapatiranBond VanguardAndMedic()
        {
            return new KapatiranBond(
                VanguardFieldMedic,
                UnitCatalog.Vanguard,
                UnitCatalog.FieldMedic,
                new List<StatModifier>
                {
                    StatModifier.Percent(StatKind.HealingReceived, 0.25f, ModifierSource.Kapatiran, VanguardFieldMedicSource),
                    StatModifier.Percent(StatKind.MaxHP, 0.05f, ModifierSource.Kapatiran, VanguardFieldMedicSource)
                },
                RankA);
        }

        /// <summary>
        /// The Magdalo and Magdiwang infantry bond. Rank B: +5% Critical Hit Chance. Rank A: +15%
        /// Critical Hit Chance and +10% Evasion.
        /// </summary>
        /// <param name="rank"><see cref="RankB"/> or <see cref="RankA"/>.</param>
        /// <exception cref="ArgumentException">Thrown for any other rank.</exception>
        public static KapatiranBond MagdaloAndMagdiwang(string rank)
        {
            List<StatModifier> modifiers;

            if (string.Equals(rank, RankA, StringComparison.Ordinal))
            {
                modifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatKind.CriticalHitChance, 0.15f, ModifierSource.Kapatiran, MagdaloMagdiwangSource),
                    StatModifier.Percent(StatKind.Evasion, 0.10f, ModifierSource.Kapatiran, MagdaloMagdiwangSource)
                };
            }
            else if (string.Equals(rank, RankB, StringComparison.Ordinal))
            {
                modifiers = new List<StatModifier>
                {
                    StatModifier.Percent(StatKind.CriticalHitChance, 0.05f, ModifierSource.Kapatiran, MagdaloMagdiwangSource)
                };
            }
            else
            {
                throw new ArgumentException("The Magdalo and Magdiwang bond has only ranks B and A.", nameof(rank));
            }

            return new KapatiranBond(
                MagdaloMagdiwang,
                UnitCatalog.MagdaloInfantry,
                UnitCatalog.MagdiwangInfantry,
                modifiers,
                rank);
        }
    }
}
