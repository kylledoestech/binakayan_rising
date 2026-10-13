using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>A person in the Journal's Characters tab: who they were, and when the player meets them.</summary>
    public sealed class Figure
    {
        /// <summary>Stable id. Also the art folder under <c>Art/Units/</c> when the figure has a portrait.</summary>
        public readonly string Id;

        public readonly LocString Name;

        /// <summary>Birth and death years, e.g. "1862–1897". Empty for a story character.</summary>
        public readonly string Life;

        /// <summary>What they were in 1896, in a few words. Shown under the name.</summary>
        public readonly LocString Role;

        public readonly LocString Bio;

        /// <summary>
        /// The sub-quest the player meets them in. The entry opens once that quest is reached
        /// (<see cref="Meta.MetaGame.HasMet"/>).
        /// </summary>
        public readonly string MetIn;

        /// <summary>False for the narrator. The Journal says so, so he is never taken for a figure from the history books.</summary>
        public readonly bool Historical;

        public Figure(string id, LocString name, string life, LocString role, LocString bio, string metIn, bool historical)
        {
            Id = id;
            Name = name;
            Life = life ?? string.Empty;
            Role = role;
            Bio = bio;
            MetIn = metIn;
            Historical = historical;
        }
    }

    /// <summary>
    /// The Journal's Characters tab: the real people the story and the lessons name, and the
    /// narrator who tells it (panel feedback: "journal - characters (biography) + terminologies,
    /// based history").
    /// </summary>
    /// <remarks>
    /// <para>
    /// SME CHECK PENDING. Each biography keeps to what the game's lessons and glossary already
    /// say, plus birth and death years and birthplaces from standard references. Santiago
    /// Álvarez is the one person the story does not name elsewhere: he is here because the
    /// glossary has the Magdiwang holding Dalahican, and he led them there. The history adviser
    /// should read every entry before the defense.
    /// </para>
    /// <para>
    /// Listed in the order the player meets them. Evangelista and Aguinaldo command the opening
    /// roster; Bonifacio is met in the lesson the first battle unlocks; Blanco when his
    /// offensive opens Level 3; Álvarez at Dalahican.
    /// </para>
    /// </remarks>
    public static class Figures
    {
        public const string Bonifacio = "Bonifacio";
        public const string Blanco = "Blanco";
        public const string Alvarez = "Alvarez";

        private static readonly List<Figure> all = new List<Figure>
        {
            new Figure(
                UnitCatalog.Evangelista,
                new LocString("Edilberto Evangelista", "Edilberto Evangelista"),
                "1862–1897",
                new LocString("Engineer of the revolution", "Inhinyero ng himagsikan"),
                new LocString(
                    "Born in Santa Cruz, Manila, he studied civil engineering at the University of Ghent in Belgium. He came home in 1896 to join the revolution in Cavite, and designed the trenches and earthworks that held Binakayan and Dalahican that November. He was killed in battle at Zapote Bridge in February 1897.",
                    "Isinilang sa Santa Cruz, Maynila, nag-aral siya ng inhinyeriyang sibil sa Unibersidad ng Ghent sa Belhika. Umuwi siya noong 1896 upang sumapi sa himagsikan sa Kabite, at idinisenyo niya ang mga trinsera at muog na nagtanggol sa Binakayan at Dalahican nang Nobyembreng iyon. Napatay siya sa labanan sa Tulay ng Zapote noong Pebrero 1897."),
                "q01", true),
            new Figure(
                UnitCatalog.Aguinaldo,
                new LocString("Emilio Aguinaldo", "Emilio Aguinaldo"),
                "1869–1964",
                new LocString("Magdalo leader of Kawit", "Pinuno ng Magdalo sa Kawit"),
                new LocString(
                    "Born in Kawit, Cavite, he was the town's capitán municipal when the revolution began, and a leader of its Magdalo council. He commanded the defence of Binakayan in November 1896. In 1899 he became the first President of the Philippine Republic.",
                    "Isinilang sa Kawit, Kabite, siya ang capitán municipal ng bayan nang magsimula ang himagsikan, at isa sa mga pinuno ng sangguniang Magdalo nito. Pinamunuan niya ang pagtatanggol sa Binakayan noong Nobyembre 1896. Noong 1899, siya ang naging unang Pangulo ng Republika ng Pilipinas."),
                "q01", true),
            Narrator(),
            new Figure(
                Bonifacio,
                new LocString("Andrés Bonifacio", "Andrés Bonifacio"),
                "1863–1897",
                new LocString("Founder of the Katipunan", "Nagtatag ng Katipunan"),
                new LocString(
                    "Born in Tondo, Manila, he founded the Katipunan in 1892 and led it as its Supremo. The revolution he began in August 1896 spread to Cavite, where the Magdalo and Magdiwang councils took up the fight.",
                    "Isinilang sa Tondo, Maynila, itinatag niya ang Katipunan noong 1892 at pinamunuan ito bilang Supremo. Lumaganap sa Kabite ang himagsikang sinimulan niya noong Agosto 1896, at doon itinuloy ng mga sangguniang Magdalo at Magdiwang ang laban."),
                "q03", true),
            new Figure(
                Blanco,
                new LocString("Ramón Blanco", "Ramón Blanco"),
                "1833–1906",
                new LocString("Spanish Governor-General", "Kastilang Gobernador-Heneral"),
                new LocString(
                    "Governor-General of the Philippines from 1893. He launched the November 1896 offensive on Cavite that broke on the trenches of Binakayan and Dalahican. Criticised in Madrid for failing to crush the revolt, he was replaced by Camilo de Polavieja that December.",
                    "Gobernador-Heneral ng Pilipinas mula 1893. Inilunsad niya ang opensiba sa Kabite noong Nobyembre 1896 na nabigo sa mga trinsera ng Binakayan at Dalahican. Binatikos sa Madrid dahil hindi niya nasupil ang himagsikan, pinalitan siya ni Camilo de Polavieja nang Disyembreng iyon."),
                "q08", true),
            new Figure(
                Alvarez,
                new LocString("Santiago Álvarez", "Santiago Álvarez"),
                "1872–1930",
                new LocString("Magdiwang general at Dalahican", "Heneral ng Magdiwang sa Dalahican"),
                new LocString(
                    "Son of Mariano Álvarez, head of the Magdiwang council of Noveleta. Known in the Katipunan as “Kidlat ng Apoy”, he commanded the Magdiwang forces that held Dalahican against the Spanish in November 1896.",
                    "Anak ni Mariano Álvarez, pinuno ng sangguniang Magdiwang ng Noveleta. Kilala sa Katipunan bilang “Kidlat ng Apoy”, pinamunuan niya ang puwersang Magdiwang na nagtanggol sa Dalahican laban sa mga Kastila noong Nobyembre 1896."),
                "q09", true)
        };

        public static IReadOnlyList<Figure> All
        {
            get { return all; }
        }

        /// <summary>The figure with <paramref name="id"/>, or null.</summary>
        public static Figure Find(string id)
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

        /// <summary>Tadah, the Senador, from his entry in the camp's cast: met in the opening scene.</summary>
        private static Figure Narrator()
        {
            Character tadah = Characters.Find(Characters.Senador);
            return new Figure(tadah.Id, tadah.Name, string.Empty, tadah.Role, tadah.Bio, "q01", tadah.Historical);
        }
    }
}
