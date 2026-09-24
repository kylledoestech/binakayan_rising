using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>Recruiting tier. Also sets how the unit is framed in the codex.</summary>
    public enum UnitRarity
    {
        Common = 0,
        Rare = 1,
        Hero = 2
    }

    /// <summary>What a unit does on the board, in one word. Shown next to its name everywhere.</summary>
    public enum UnitRole
    {
        Leader,
        Frontline,
        Ranged,
        Defender,
        Support,
        Infantry,

        /// <summary>Spanish guns: long reach, slow, a blast that catches neighbours. DESIGN-DECISIONS #19.</summary>
        Artillery,

        /// <summary>Spanish light infantry: fast, hard to hit, shoots from two tiles.</summary>
        Skirmisher,

        /// <summary>Spanish officer: sturdier, and lifts the attack of the Spanish around him.</summary>
        Officer,

        /// <summary>Spanish naval infantry: at home in the coastal shallows.</summary>
        Marine
    }

    /// <summary>One kind of unit: who it is, what it does, and its level-1 stats.</summary>
    public sealed class UnitArchetype
    {
        /// <summary>Stable id. Also the art folder under <c>Art/Units/</c>.</summary>
        public readonly string Id;

        /// <summary>Three-letter board tag.</summary>
        public readonly string ShortName;

        public readonly LocString Name;
        public readonly UnitRole Role;
        public readonly UnitRarity Rarity;

        /// <summary>
        /// A named historical figure. Recruiting one again trains the one you have instead of
        /// adding a second Evangelista to the trench.
        /// </summary>
        public readonly bool Unique;

        public readonly UnitStats BaseStats;

        /// <summary>One line for instructions: what the unit is for.</summary>
        public readonly LocString Summary;

        /// <summary>A short historical note for the Characters codex.</summary>
        public readonly LocString Bio;

        public readonly Team Team;

        /// <summary>Health restored to a wounded ally in place of an attack; zero for everyone but healers.</summary>
        public readonly float HealPower;

        /// <summary>Splash, reload, aura or home terrain. <see cref="UnitAbilities.None"/> for most units.</summary>
        public readonly UnitAbilities Abilities;

        /// <summary>
        /// The art folder the unit's body and portrait come from. Its own id unless it borrows
        /// another's figure until its own is rendered.
        /// </summary>
        public readonly string ArtId;

        /// <summary>
        /// Colour multiplied over borrowed art so the borrower reads as a different unit, as
        /// 0xRRGGBBAA. White (0xFFFFFFFF) for units with their own art.
        /// </summary>
        public readonly uint ArtTint;

        public UnitArchetype(
            string id, string shortName, LocString name, UnitRole role, UnitRarity rarity, bool unique,
            UnitStats baseStats, LocString summary, LocString bio, Team team = Team.Katipunan, float healPower = 0f,
            UnitAbilities abilities = null, string artId = null, uint artTint = 0xFFFFFFFFu)
        {
            HealPower = healPower;
            Abilities = abilities ?? UnitAbilities.None;
            ArtId = artId ?? id;
            ArtTint = artTint;
            Id = id;
            ShortName = shortName;
            Name = name;
            Role = role;
            Rarity = rarity;
            Unique = unique;
            BaseStats = baseStats;
            Summary = summary;
            Bio = bio;
            Team = team;
        }
    }

    /// <summary>
    /// Every unit in the game. The Katipunan roster is Table 3's eight named units; the Spanish
    /// side, which the proposal never enumerates, is five archetypes: the regular, the artillery
    /// the story names, Cazadores skirmishers, an officer, and naval infantry for the landings
    /// (DESIGN-DECISIONS #19).
    /// </summary>
    /// <remarks>
    /// Stats are the vertical slice's placeholders (DESIGN-DECISIONS #2), with the three newly
    /// added units expressed as small deviations from them. Historical notes are drafts for the
    /// adviser to check.
    /// </remarks>
    public static class UnitCatalog
    {
        public const string Evangelista = "Evangelista";
        public const string Aguinaldo = "Aguinaldo";
        public const string Vanguard = "Vanguard";
        public const string FieldMedic = "FieldMedic";
        public const string Marksman = "Marksman";
        public const string Engineer = "Engineer";
        public const string MagdaloInfantry = "MagdaloInfantry";
        public const string MagdiwangInfantry = "MagdiwangInfantry";
        public const string SpanishRegular = "SpanishRegular";
        public const string SpanishArtillery = "SpanishArtillery";
        public const string SpanishCazador = "SpanishCazador";
        public const string SpanishOfficer = "SpanishOfficer";
        public const string SpanishMarine = "SpanishMarine";

        /// <summary>Share of an artillery hit that lands on each enemy next to its target.</summary>
        public const float ArtillerySplash = 0.5f;

        /// <summary>Turns an artillery crew spends reloading after each shot.</summary>
        public const int ArtilleryReload = 1;

        /// <summary>Reach of an officer's aura, in tiles.</summary>
        public const int OfficerAuraRadius = 2;

        /// <summary>Attack an officer's aura adds to each Spanish unit inside it.</summary>
        public const float OfficerAuraAttack = 0.10f;

        /// <summary>Attack and Defense a marine gains while standing in the coastal shallows.</summary>
        public const float MarineShallowsBonus = 0.15f;

        private static readonly List<UnitArchetype> all = new List<UnitArchetype>
        {
            // TODO(design): not specified in capstone document — every stat block below.
            new UnitArchetype(
                Evangelista, "EVA",
                new LocString("Gen. Edilberto Evangelista", "Hen. Edilberto Evangelista"),
                UnitRole.Leader, UnitRarity.Hero, true,
                new UnitStats(140f, 16f, 8f, 0.05f, 0.90f, 1f, 0.10f, 0f),
                new LocString("Leader. Sturdy and steady; bonds with Aguinaldo.",
                    "Pinuno. Matibay at matatag; may buklod kay Aguinaldo."),
                new LocString(
                    "A civil engineer trained at the University of Ghent in Belgium. He came home in 1896 to join the revolution and designed the trench works that held Cavite in November.",
                    "Isang inhinyerong sibil na nag-aral sa Unibersidad ng Ghent sa Belhika. Umuwi siya noong 1896 upang sumapi sa himagsikan at idinisenyo ang mga trinserang nagtanggol sa Kabite noong Nobyembre.")),
            new UnitArchetype(
                Aguinaldo, "AGU",
                new LocString("Emilio Aguinaldo", "Emilio Aguinaldo"),
                UnitRole.Leader, UnitRarity.Hero, true,
                new UnitStats(130f, 15f, 7f, 0.05f, 0.90f, 1f, 0.15f, 0f),
                new LocString("Leader. Hits hard, lands critical blows; bonds with Evangelista.",
                    "Pinuno. Malakas tumama at madalas mag-kritikal; may buklod kay Evangelista."),
                new LocString(
                    "Leader of the Magdalo council of Kawit. He commanded the defence of Binakayan and later became the first President of the Philippine Republic.",
                    "Pinuno ng sangguniang Magdalo ng Kawit. Pinamunuan niya ang pagtatanggol sa Binakayan at kalaunan ay naging unang Pangulo ng Republika ng Pilipinas.")),
            new UnitArchetype(
                Vanguard, "VAN",
                new LocString("Katipunero Vanguard", "Katipunerong Taliba"),
                UnitRole.Frontline, UnitRarity.Common, false,
                new UnitStats(150f, 15f, 10f, 0.05f, 0.85f, 1f, 0.10f, 0f),
                new LocString("Frontline. The most health; holds the first line.",
                    "Unahan. Pinakamaraming buhay; humahawak sa unang hanay."),
                new LocString(
                    "The bolo-armed fighters of the Katipunan who stood in the front of every charge and every defence.",
                    "Ang mga Katipunerong may bolo na nasa unahan ng bawat pagsalakay at pagtatanggol.")),
            new UnitArchetype(
                FieldMedic, "MED",
                new LocString("Field Medic", "Mediko sa Larangan"),
                UnitRole.Support, UnitRarity.Rare, false,
                new UnitStats(100f, 8f, 6f, 0.08f, 0.85f, 2f, 0.05f, 0f),
                new LocString("Support. Heals the most wounded ally within 2 tiles; bonds with the Vanguard.",
                    "Suporta. Pinagagaling ang pinakasugatang kakampi sa loob ng 2 tile; may buklod sa Taliba."),
                new LocString(
                    "Volunteers who carried and tended the wounded behind the lines, with herbs, cloth and whatever the town could spare.",
                    "Mga boluntaryong nagdala at nag-alaga sa mga sugatan sa likod ng hanay, gamit ang halamang-gamot, tela at anumang maibibigay ng bayan."),
                // TODO(design): not specified in capstone document — how much a Field Medic heals.
                healPower: 14f),
            new UnitArchetype(
                Marksman, "MRK",
                new LocString("Caviteño Marksman", "Manunudlang Kabitenyo"),
                UnitRole.Ranged, UnitRarity.Rare, false,
                new UnitStats(90f, 14f, 4f, 0.05f, 0.75f, 2f, 0.15f, 0f),
                new LocString("Ranged. Hits from 2 tiles away, but fragile.",
                    "Malayuan. Tumatama mula 2 tile ang layo, pero marupok."),
                new LocString(
                    "Rifles were scarce and precious. The few who carried them were the best shots in Cavite, often with guns taken from the enemy.",
                    "Bihira at mahalaga ang riple. Ang iilang may hawak nito ang pinakamahusay tumudla sa Kabite, kadalasan gamit ang baril na nakuha sa kaaway.")),
            new UnitArchetype(
                Engineer, "ENG",
                new LocString("Trench Engineer", "Inhinyero ng Trinsera"),
                UnitRole.Defender, UnitRarity.Rare, false,
                new UnitStats(110f, 10f, 12f, 0.05f, 0.85f, 1f, 0.05f, 0f),
                new LocString("Defender. The toughest armour; gives the Marksman +1 range.",
                    "Tagapagtanggol. Pinakamatibay; nagbibigay ng +1 abot sa Manunudla."),
                new LocString(
                    "Farmers turned diggers who built Evangelista's network of trenches and bamboo barricades along the Cavite shore.",
                    "Mga magsasakang naging manghuhukay na nagtayo ng mga trinsera at barikadang kawayan ni Evangelista sa baybayin ng Kabite.")),
            new UnitArchetype(
                MagdaloInfantry, "MGD",
                new LocString("Magdalo Infantry", "Impanteriya ng Magdalo"),
                UnitRole.Infantry, UnitRarity.Common, false,
                new UnitStats(110f, 12f, 6f, 0.05f, 0.80f, 1f, 0.08f, 0f),
                new LocString("Infantry. Reliable all-rounder; bonds with the Magdiwang.",
                    "Impanteriya. Maaasahan sa lahat; may buklod sa Magdiwang."),
                new LocString(
                    "Soldiers of the Magdalo council, based in Kawit. They held the Binakayan line.",
                    "Mga kawal ng sangguniang Magdalo na nakabase sa Kawit. Sila ang humawak sa hanay ng Binakayan.")),
            new UnitArchetype(
                MagdiwangInfantry, "MGW",
                new LocString("Magdiwang Infantry", "Impanteriya ng Magdiwang"),
                UnitRole.Infantry, UnitRarity.Common, false,
                new UnitStats(105f, 13f, 5f, 0.06f, 0.80f, 1f, 0.10f, 0f),
                new LocString("Infantry. Slightly more attack; bonds with the Magdalo.",
                    "Impanteriya. Bahagyang mas malakas; may buklod sa Magdalo."),
                new LocString(
                    "Soldiers of the Magdiwang council, based in Noveleta. They held the Dalahican shore.",
                    "Mga kawal ng sangguniang Magdiwang na nakabase sa Noveleta. Sila ang humawak sa baybayin ng Dalahican.")),
            new UnitArchetype(
                SpanishRegular, "REG",
                new LocString("Spanish Regular", "Regular na Kastila"),
                UnitRole.Infantry, UnitRarity.Common, false,
                new UnitStats(100f, 14f, 5f, 0.05f, 0.85f, 1f, 0.10f, 1f),
                new LocString("Enemy infantry. Advances across open ground and the shallows.",
                    "Kaaway na impanteriya. Sumusulong sa bukas na lupa at mababaw na tubig."),
                new LocString(
                    "Colonial infantry sent by Governor-General Ramon Blanco to retake Cavite in November 1896.",
                    "Impanteriyang kolonyal na ipinadala ni Gobernador-Heneral Ramon Blanco upang bawiin ang Kabite noong Nobyembre 1896."),
                Team.Spanish),

            // DESIGN-DECISIONS #19. Each is a small deviation from the regular above. They borrow
            // the regular's figure, tinted, until Tools/sprites/units.py renders their own.
            new UnitArchetype(
                SpanishArtillery, "ART",
                new LocString("Spanish Artillery", "Artilyeryang Kastila"),
                UnitRole.Artillery, UnitRarity.Common, false,
                new UnitStats(70f, 24f, 2f, 0f, 0.70f, 4f, 0.05f, 0.5f),
                new LocString("Enemy gun. Fires from 4 tiles every other turn; the blast also hits the units beside its target.",
                    "Kanyon ng kaaway. Nagpapaputok mula 4 tile tuwing ikalawang yugto; tinatamaan din ng pagsabog ang katabi ng tinudla."),
                new LocString(
                    "Field guns and mountain howitzers shelled the Katipunan trenches before every Spanish assault on Cavite.",
                    "Binomba ng mga kanyon at obus ang mga trinsera ng Katipunan bago ang bawat salakay ng Kastila sa Kabite."),
                Team.Spanish,
                abilities: UnitAbilities.Artillery(ArtillerySplash, ArtilleryReload),
                artId: SpanishRegular, artTint: 0x8A8A8AFFu),
            new UnitArchetype(
                SpanishCazador, "CAZ",
                new LocString("Spanish Cazador", "Kasador na Kastila"),
                UnitRole.Skirmisher, UnitRarity.Common, false,
                new UnitStats(80f, 12f, 3f, 0.20f, 0.80f, 2f, 0.10f, 2f),
                new LocString("Enemy skirmisher. Moves 2 tiles a turn, dodges often, and shoots from 2 tiles.",
                    "Kaaway na eskirmisador. Gumagalaw nang 2 tile bawat yugto, madalas umiwas, at bumabaril mula 2 tile."),
                new LocString(
                    "Cazadores were Spain's light rifle battalions, trained to fight in open order ahead of the main column.",
                    "Ang mga Kasador ang magagaang batalyon ng riple ng Espanya, sinanay na lumaban nang nakakalat sa unahan ng pangunahing hanay."),
                Team.Spanish,
                artId: SpanishRegular, artTint: 0x9FD08CFFu),
            new UnitArchetype(
                SpanishOfficer, "OFF",
                new LocString("Spanish Officer", "Opisyal na Kastila"),
                UnitRole.Officer, UnitRarity.Common, false,
                new UnitStats(130f, 13f, 8f, 0.05f, 0.85f, 1f, 0.10f, 1f),
                new LocString("Enemy commander. Tougher than a regular; Spanish units within 2 tiles of him gain +10% attack.",
                    "Pinuno ng kaaway. Mas matibay kaysa sa regular; may +10% atake ang mga Kastilang nasa loob ng 2 tile mula sa kaniya."),
                new LocString(
                    "Peninsular officers led the columns Blanco sent against Binakayan and Dalahican, often from the front.",
                    "Pinamunuan ng mga opisyal na peninsular ang mga hanay na ipinadala ni Blanco laban sa Binakayan at Dalahican, kadalasan mula sa unahan."),
                Team.Spanish,
                abilities: UnitAbilities.Officer(OfficerAuraRadius, new[]
                {
                    StatModifier.Percent(StatKind.AttackDamage, OfficerAuraAttack, ModifierSource.Ability, "OfficerAura")
                }),
                artId: SpanishRegular, artTint: 0xF0C75AFFu),
            new UnitArchetype(
                SpanishMarine, "MAR",
                new LocString("Spanish Marine", "Marinong Kastila"),
                UnitRole.Marine, UnitRarity.Common, false,
                new UnitStats(105f, 14f, 6f, 0.05f, 0.85f, 1f, 0.10f, 1f),
                new LocString("Enemy naval infantry. The shallows do not slow it; there it gains +15% attack and defense.",
                    "Kaaway na impanteriya ng hukbong-dagat. Hindi ito binabagalan ng mababaw na tubig; doon ito may +15% atake at depensa."),
                new LocString(
                    "Infanteria de Marina landed from the ships of the Spanish squadron to strike the Cavite shore from the bay.",
                    "Dumaong ang Infanteria de Marina mula sa mga barko ng eskwadrang Kastila upang salakayin ang baybayin ng Kabite mula sa look."),
                Team.Spanish,
                abilities: UnitAbilities.HomeTerrain(TerrainType.CoastalShallows, new[]
                {
                    StatModifier.Percent(StatKind.AttackDamage, MarineShallowsBonus, ModifierSource.Ability, "MarineFooting"),
                    StatModifier.Percent(StatKind.Defense, MarineShallowsBonus, ModifierSource.Ability, "MarineFooting")
                }),
                artId: SpanishRegular, artTint: 0x7FA8E0FFu)
        };

        /// <summary>Every archetype, Katipunan first.</summary>
        public static IReadOnlyList<UnitArchetype> All
        {
            get { return all; }
        }

        /// <summary>The archetype with <paramref name="id"/>, or null.</summary>
        public static UnitArchetype Find(string id)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == id)
                {
                    return all[i];
                }
            }

            return null;
        }

        /// <summary>True for a Spanish archetype.</summary>
        public static bool IsSpanish(string id)
        {
            UnitArchetype archetype = Find(id);
            return archetype != null && archetype.Team == Team.Spanish;
        }

        /// <summary>Katipunan archetypes of <paramref name="rarity"/>, in catalog order.</summary>
        public static List<UnitArchetype> Recruitable(UnitRarity rarity)
        {
            var pool = new List<UnitArchetype>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Team == Team.Katipunan && all[i].Rarity == rarity)
                {
                    pool.Add(all[i]);
                }
            }

            return pool;
        }

        /// <summary>The role's display label.</summary>
        public static LocString RoleLabel(UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Leader: return new LocString("Leader", "Pinuno");
                case UnitRole.Frontline: return new LocString("Frontline", "Unahan");
                case UnitRole.Ranged: return new LocString("Ranged", "Malayuan");
                case UnitRole.Defender: return new LocString("Defender", "Tagapagtanggol");
                case UnitRole.Support: return new LocString("Support", "Suporta");
                case UnitRole.Artillery: return new LocString("Artillery", "Artilerya");
                case UnitRole.Skirmisher: return new LocString("Skirmisher", "Eskirmisador");
                case UnitRole.Officer: return new LocString("Officer", "Opisyal");
                case UnitRole.Marine: return new LocString("Marine", "Marino");
                default: return new LocString("Infantry", "Impanteriya");
            }
        }

        /// <summary>The rarity's display label.</summary>
        public static LocString RarityLabel(UnitRarity rarity)
        {
            switch (rarity)
            {
                case UnitRarity.Hero: return new LocString("Hero", "Bayani");
                case UnitRarity.Rare: return new LocString("Rare", "Bihira");
                default: return new LocString("Common", "Karaniwan");
            }
        }
    }
}
