using System.Collections.Generic;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;

namespace BinakayanRising.Core.Content
{
    /// <summary>One weapon type. Every weapon a unit can hold adds flat Attack.</summary>
    public sealed class WeaponDef
    {
        public readonly string Id;
        public readonly int Tier;
        public readonly LocString Name;
        public readonly LocString Note;
        public readonly float AttackBonus;

        /// <summary>The weapon this one synthesizes into, or null at the top tier.</summary>
        public readonly string UpgradesTo;

        /// <summary>What synthesizing this weapon into <see cref="UpgradesTo"/> costs.</summary>
        public readonly Cost SynthesisCost;

        public WeaponDef(string id, int tier, LocString name, LocString note, float attackBonus, string upgradesTo, Cost synthesisCost)
        {
            Id = id;
            Tier = tier;
            Name = name;
            Note = note;
            AttackBonus = attackBonus;
            UpgradesTo = upgradesTo;
            SynthesisCost = synthesisCost;
        }
    }

    /// <summary>
    /// The armoury, restricted to what a Katipunero could have held in 1896 (GDD, p.36). The
    /// document's one example — "a standard bolo to a captured Mauser rifle" — is the ladder's
    /// two ends.
    /// </summary>
    public static class WeaponCatalog
    {
        public const string Bolo = "bolo";
        public const string Paltik = "paltik";
        public const string Remington = "remington";
        public const string Mauser = "mauser";

        // TODO(design): not specified in capstone document — tier list, attack bonus and
        // synthesis cost per tier (#13).
        private static readonly List<WeaponDef> all = new List<WeaponDef>
        {
            new WeaponDef(Bolo, 1,
                new LocString("Bolo", "Bolo"),
                new LocString("The farm blade every Katipunero carried.", "Ang itak-pansaka na dala ng bawat Katipunero."),
                2f, Paltik, new Cost(50, 0, 15)),
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
                9f, null, new Cost(0, 0, 0))
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
