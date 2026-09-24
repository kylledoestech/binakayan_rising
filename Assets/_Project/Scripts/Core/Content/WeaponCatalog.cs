using System.Collections.Generic;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;

namespace BinakayanRising.Core.Content
{
    /// <summary>
    /// One weapon type. Every weapon adds flat Attack; some also shift accuracy, critical chance
    /// or reach, and a few can only be used by one kind of soldier.
    /// </summary>
    public sealed class WeaponDef
    {
        public readonly string Id;
        public readonly int Tier;
        public readonly LocString Name;
        public readonly LocString Note;
        public readonly float AttackBonus;

        /// <summary>Added to the holder's hit chance, as a 0..1 fraction (-0.05 is 5% worse).</summary>
        public readonly float AccuracyBonus;

        /// <summary>Added to the holder's critical-hit chance, as a 0..1 fraction.</summary>
        public readonly float CritBonus;

        /// <summary>Added to the holder's attack reach, in grid cells.</summary>
        public readonly float RangeBonus;

        /// <summary>The only archetype that may hold this weapon, or null when anyone may.</summary>
        public readonly string RequiredArchetype;

        /// <summary>The weapon this one synthesizes into, or null when it cannot be reforged.</summary>
        public readonly string UpgradesTo;

        /// <summary>What synthesizing this weapon into <see cref="UpgradesTo"/> costs.</summary>
        public readonly Cost SynthesisCost;

        public WeaponDef(
            string id,
            int tier,
            LocString name,
            LocString note,
            float attackBonus,
            string upgradesTo,
            Cost synthesisCost,
            float accuracyBonus = 0f,
            float critBonus = 0f,
            float rangeBonus = 0f,
            string requiredArchetype = null)
        {
            Id = id;
            Tier = tier;
            Name = name;
            Note = note;
            AttackBonus = attackBonus;
            UpgradesTo = upgradesTo;
            SynthesisCost = synthesisCost;
            AccuracyBonus = accuracyBonus;
            CritBonus = critBonus;
            RangeBonus = rangeBonus;
            RequiredArchetype = requiredArchetype;
        }

        /// <summary>Whether a unit of <paramref name="archetype"/> may hold this weapon.</summary>
        public bool Allows(string archetype)
        {
            return RequiredArchetype == null || RequiredArchetype == archetype;
        }
    }

    /// <summary>
    /// The armoury, restricted to what a Katipunero could have held in 1896 (GDD, p.36). The
    /// document's one example — "a standard bolo to a captured Mauser rifle" — is the ladder's
    /// two ends.
    /// </summary>
    /// <remarks>
    /// Tier 1 is the blades and spears the revolutionaries made or already owned; each one has a
    /// small twist so the choice between them matters, and each reforges into a Paltik at the
    /// same price. Tiers 2 to 4 are the guns, home-made then captured. The Lantaka stands apart:
    /// a small cannon only the Trench Engineer can work, which cannot be reforged
    /// (DESIGN-DECISIONS #13). SME CHECK PENDING on the names and notes.
    /// </remarks>
    public static class WeaponCatalog
    {
        public const string Bolo = "bolo";
        public const string Talibong = "talibong";
        public const string Gulok = "gulok";
        public const string Sibat = "sibat";
        public const string Balaraw = "balaraw";
        public const string Paltik = "paltik";
        public const string Remington = "remington";
        public const string Mauser = "mauser";
        public const string Lantaka = "lantaka";

        // TODO(design): not specified in capstone document — tier list, attack bonus and
        // synthesis cost per tier (#13). Tier-1 twists and the Lantaka are the product owner's.
        private static readonly Cost BladeToPaltik = new Cost(50, 0, 15);

        private static readonly List<WeaponDef> all = new List<WeaponDef>
        {
            new WeaponDef(Bolo, 1,
                new LocString("Bolo", "Bolo"),
                new LocString("The farm blade most Katipuneros carried.", "Ang itak-pansaka na dala ng karamihan sa mga Katipunero."),
                2f, Paltik, BladeToPaltik),
            new WeaponDef(Talibong, 1,
                new LocString("Talibong", "Talibong"),
                new LocString("A long single-edged sword. Its keen edge finds a weak spot.", "Mahabang espadang iisa ang talim. Nahahanap ng matalas nitong talim ang mahinang bahagi."),
                2f, Paltik, BladeToPaltik, critBonus: 0.03f),
            new WeaponDef(Gulok, 1,
                new LocString("Gulok", "Gulok"),
                new LocString("A heavy work blade. Strikes hard, but swings wide.", "Mabigat na itak na pantrabaho. Malakas ang taga, pero maluwang ang wasiwas."),
                3f, Paltik, BladeToPaltik, accuracyBonus: -0.03f),
            new WeaponDef(Sibat, 1,
                new LocString("Sibat", "Sibat"),
                new LocString("An iron-headed spear on a bamboo shaft. Its reach keeps a thrust true.", "Sibat na may talim na bakal sa tagdang kawayan. Tumpak ang tusok dahil sa haba nito."),
                1f, Paltik, BladeToPaltik, accuracyBonus: 0.05f),
            new WeaponDef(Balaraw, 1,
                new LocString("Balaraw", "Balaraw"),
                new LocString("A short dagger, easy to hide. It slips past a guard.", "Maikling punyal na madaling itago. Nakalulusot ito sa sangga ng kalaban."),
                1f, Paltik, BladeToPaltik, critBonus: 0.06f),
            new WeaponDef(Paltik, 2,
                new LocString("Paltik", "Paltik"),
                new LocString("A homemade gun, forged in hidden workshops.", "Baril na gawang-kamay, pinanday sa lihim na pandayan."),
                4f, Remington, new Cost(120, 0, 30)),
            new WeaponDef(Remington, 3,
                new LocString("Remington Rifle", "Ripleng Remington"),
                new LocString("Single-shot rifle captured from Spanish garrisons.", "Ripleng isahang putok na nakuha sa mga himpilang Kastila."),
                6f, Mauser, new Cost(250, 0, 60)),
            new WeaponDef(Mauser, 4,
                new LocString("Mauser Rifle", "Ripleng Mauser"),
                new LocString("The newest Spanish rifle. A prize of war.", "Ang pinakabagong riple ng Kastila. Samsam ng digmaan."),
                9f, null, new Cost(0, 0, 0)),
            new WeaponDef(Lantaka, 3,
                new LocString("Lantaka", "Lantaka"),
                new LocString("A small bronze swivel cannon. Only an engineer can mount and aim it.", "Maliit na kanyong tanso sa paikutan. Inhinyero lamang ang makagagamit nito."),
                7f, null, new Cost(0, 0, 0), accuracyBonus: -0.05f, rangeBonus: 1f, requiredArchetype: UnitCatalog.Engineer)
        };

        public static IReadOnlyList<WeaponDef> All
        {
            get { return all; }
        }

        public static WeaponDef Find(string id)
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
    }
}
