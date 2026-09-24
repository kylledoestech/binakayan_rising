using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>
    /// A person who speaks in the game but does not fight: the aide and the encampment keepers.
    /// </summary>
    /// <remarks>
    /// Every line of dialogue is labelled with the speaker's portrait, name and role, which is
    /// the panel's "instructions labeling of characters": a player always knows who is telling
    /// them what, and why that person would know.
    /// </remarks>
    public sealed class Character
    {
        /// <summary>Stable id. Also the art folder under <c>Art/Units/</c>.</summary>
        public readonly string Id;

        public readonly LocString Name;

        /// <summary>What the character does in the camp, in a few words. Shown under the name.</summary>
        public readonly LocString Role;

        /// <summary>A short note for the Characters codex.</summary>
        public readonly LocString Bio;

        /// <summary>
        /// False for invented people. The codex says so, so a composite keeper is never taken for
        /// a figure from the history books.
        /// </summary>
        public readonly bool Historical;

        public Character(string id, LocString name, LocString role, LocString bio, bool historical)
        {
            Id = id;
            Name = name;
            Role = role;
            Bio = bio;
            Historical = historical;
        }
    }

    /// <summary>The camp's speaking cast, besides the units themselves.</summary>
    public static class Characters
    {
        public const string Tomas = "Tomas";
        public const string Farmer = "Farmer";
        public const string Miner = "Miner";
        public const string Trader = "Trader";
        public const string Sergeant = "DrillSergeant";

        /// <summary>Tadah, the Senador: narrates the cutscenes and announces each rank.</summary>
        public const string Senador = "Senador";

        private static readonly List<Character> all = new List<Character>
        {
            new Character(
                Tomas,
                new LocString("Tomas", "Tomas"),
                new LocString("Your aide-de-camp", "Iyong katuwang"),
                new LocString(
                    "A schoolteacher's son from Kawit who keeps the camp's ledgers and the general's schedule. He knows every keeper by name.",
                    "Anak ng isang guro mula sa Kawit na nag-iingat ng talaan ng kampo at ng iskedyul ng heneral. Kilala niya ang bawat tagapangasiwa."),
                false),
            new Character(
                Farmer,
                new LocString("Aling Ines", "Aling Ines"),
                new LocString("Keeper of the Farm", "Tagapangasiwa ng Bukid"),
                new LocString(
                    "A farmer from the rice fields of Binakayan. Towns across Cavite fed the revolution from harvests like hers.",
                    "Magsasaka mula sa palayan ng Binakayan. Pinakain ng mga bayan sa buong Kabite ang himagsikan mula sa aning tulad ng sa kanya."),
                false),
            new Character(
                Miner,
                new LocString("Mang Andoy", "Mang Andoy"),
                new LocString("Foreman of the Mine", "Kapatas ng Minahan"),
                new LocString(
                    "A blacksmith turned scavenger. The revolutionaries had few factories, so old iron, nails and broken tools were reforged into arms.",
                    "Isang panday na naging tagahanap ng bakal. Kaunti ang pabrika ng mga rebolusyonaryo, kaya ang lumang bakal, pako at sirang kasangkapan ay pinanday bilang sandata."),
                false),
            new Character(
                Trader,
                new LocString("Ka Tasyo", "Ka Tasyo"),
                new LocString("Trader at the Exchange", "Mangangalakal ng Palitan"),
                new LocString(
                    "A market trader from Cavite Viejo who turns the camp's surplus into coin. Spanish reales and Mexican pesos both passed through markets like his.",
                    "Isang mangangalakal mula sa Cavite Viejo na ginagawang salapi ang sobrang ani ng kampo. Parehong dumaan sa mga pamilihang tulad ng sa kanya ang reales ng Espanya at pesong Mehikano."),
                false),
            new Character(
                Sergeant,
                new LocString("Sarhento Dimas", "Sarhento Dimas"),
                new LocString("Drill sergeant", "Sarhento ng pagsasanay"),
                new LocString(
                    "A former native soldier of the Spanish army who deserted to the Katipunan. Men like him taught farmers to load, aim and hold a line.",
                    "Dating katutubong sundalo ng hukbong Kastila na tumiwalag at sumapi sa Katipunan. Ang mga tulad niya ang nagturo sa mga magsasaka na magkarga, tumutok at manindigan sa hanay."),
                false),
            new Character(
                Senador,
                new LocString("Tadah", "Tadah"),
                new LocString("Senador · Narrator", "Senador · Tagapagsalaysay"),
                new LocString(
                    "An elder of Cavite who lived through the revolution and now tells its story. He speaks between battles and marks each rank the player earns.",
                    "Isang matanda ng Kabite na nabuhay sa panahon ng himagsikan at ngayo'y nagsasalaysay nito. Nagsasalita siya sa pagitan ng mga labanan at ipinapahayag ang bawat ranggong makakamit."),
                false)
        };

        public static IReadOnlyList<Character> All
        {
            get { return all; }
        }

        /// <summary>The character with <paramref name="id"/>, or null.</summary>
        public static Character Find(string id)
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
