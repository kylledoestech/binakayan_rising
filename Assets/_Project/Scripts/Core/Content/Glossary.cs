using System;
using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>One word of the historical glossary and what it means.</summary>
    public sealed class GlossaryTerm
    {
        public readonly string Id;

        /// <summary>The word as it is listed. Usually the same in both languages.</summary>
        public readonly LocString Term;

        public readonly LocString Definition;

        public GlossaryTerm(string id, LocString term, LocString definition)
        {
            Id = id;
            Term = term;
            Definition = definition;
        }
    }

    /// <summary>
    /// The Library's historical glossary: the people, places, words and things the story and the
    /// lessons name (Proposal Appendix F, "a historical glossary tab"; DESIGN-DECISIONS #23).
    /// </summary>
    /// <remarks>
    /// <para>
    /// SME CHECK PENDING. Every definition is kept to what the capstone document and the
    /// Library's lessons already say, or to widely published facts, and none adds a claim the
    /// story depends on. The history adviser should still read the list before the defense.
    /// </para>
    /// <para>
    /// Authored in no particular order; <see cref="Sorted"/> gives the order a reader sees, which
    /// differs by language because the Filipino headwords differ.
    /// </para>
    /// </remarks>
    public static class Glossary
    {
        private static readonly List<GlossaryTerm> all = new List<GlossaryTerm>
        {
            Term("katipunan", "Katipunan", "Katipunan",
                "The secret revolutionary society founded in Manila in 1892. Its full name means the Highest and Most Honorable Society of the Children of the Nation, shortened to KKK.",
                "Ang lihim na samahang rebolusyonaryo na itinatag sa Maynila noong 1892. Ang buong pangalan nito ay Kataas-taasang, Kagalang-galangang Katipunan ng mga Anak ng Bayan, o KKK."),
            Term("katipunero", "Katipunero", "Katipunero",
                "A member of the Katipunan. Members swore an oath and treated one another as brothers.",
                "Kasapi ng Katipunan. Nanumpa ang mga kasapi at itinuring ang isa't isa bilang magkapatid."),
            Term("bonifacio", "Andrés Bonifacio", "Andrés Bonifacio",
                "Founder of the Katipunan in 1892 and its Supremo, the society's highest leader.",
                "Nagtatag ng Katipunan noong 1892 at naging Supremo nito, ang pinakamataas na pinuno ng samahan."),
            Term("supremo", "Supremo", "Supremo",
                "The title of the head of the Katipunan's supreme council, held by Andrés Bonifacio.",
                "Ang titulo ng pinuno ng kataas-taasang sanggunian ng Katipunan, na hinawakan ni Andrés Bonifacio."),
            Term("aguinaldo", "Emilio Aguinaldo", "Emilio Aguinaldo",
                "A leader of the Magdalo council from Kawit, Cavite, who commanded revolutionary forces in Cavite in 1896.",
                "Isang pinuno ng sangguniang Magdalo mula Kawit, Kabite, na namuno sa mga puwersang rebolusyonaryo sa Kabite noong 1896."),
            Term("evangelista", "Edilberto Evangelista", "Edilberto Evangelista",
                "A Filipino civil engineer who studied at the University of Ghent in Belgium. He came home to join the revolution and designed the trenches and earthworks at Binakayan-Dalahican. He was killed in battle at Zapote Bridge in February 1897.",
                "Isang Pilipinong inhinyerong sibil na nag-aral sa Unibersidad ng Ghent sa Belhika. Umuwi siya upang sumapi sa himagsikan at idinisenyo ang mga trinsera at muog sa Binakayan-Dalahican. Napatay siya sa labanan sa Tulay ng Zapote noong Pebrero 1897."),
            Term("blanco", "Ramón Blanco", "Ramón Blanco",
                "The Spanish Governor-General of the Philippines who launched the November 1896 offensive on Cavite.",
                "Ang Kastilang Gobernador-Heneral ng Pilipinas na naglunsad ng opensiba sa Kabite noong Nobyembre 1896."),
            Term("magdalo", "Magdalo", "Magdalo",
                "The Katipunan council based in Kawit, Cavite. Its leaders included Emilio Aguinaldo. Magdalo forces held Binakayan.",
                "Ang sanggunian ng Katipunan na nakabase sa Kawit, Kabite. Kabilang sa mga pinuno nito si Emilio Aguinaldo. Ang puwersang Magdalo ang nagtanggol sa Binakayan."),
            Term("magdiwang", "Magdiwang", "Magdiwang",
                "The Katipunan council based in Noveleta, Cavite. Magdiwang forces held Dalahican.",
                "Ang sanggunian ng Katipunan na nakabase sa Noveleta, Kabite. Ang puwersang Magdiwang ang nagtanggol sa Dalahican."),
            Term("cavite", "Cavite", "Kabite",
                "The province south of Manila on Manila Bay. In 1896 it became the stronghold of the revolution.",
                "Ang lalawigan sa timog ng Maynila sa baybayin ng Look ng Maynila. Noong 1896, naging muog ito ng himagsikan."),
            Term("kawit", "Kawit", "Kawit",
                "A town in Cavite, home of the Magdalo council. Binakayan is part of Kawit.",
                "Isang bayan sa Kabite, tahanan ng sangguniang Magdalo. Bahagi ng Kawit ang Binakayan."),
            Term("noveleta", "Noveleta", "Noveleta",
                "A town in Cavite, home of the Magdiwang council. Dalahican is part of Noveleta.",
                "Isang bayan sa Kabite, tahanan ng sangguniang Magdiwang. Bahagi ng Noveleta ang Dalahican."),
            Term("binakayan", "Binakayan", "Binakayan",
                "A place in Kawit, Cavite, by the shore of Manila Bay. Spanish troops attacked the trenches here from November 9, 1896.",
                "Isang pook sa Kawit, Kabite, sa baybayin ng Look ng Maynila. Sinalakay ng tropang Kastila ang mga trinsera rito simula Nobyembre 9, 1896."),
            Term("dalahican", "Dalahican", "Dalahican",
                "A coastal place in Noveleta, Cavite. Spanish troops attacked it across the shallows and the shore in November 1896.",
                "Isang pook sa baybayin ng Noveleta, Kabite. Sinalakay ito ng tropang Kastila sa mababaw na tubig at baybayin noong Nobyembre 1896."),
            Term("manilabay", "Manila Bay", "Look ng Maynila",
                "The wide bay west of Manila and north of Cavite. Spanish warships in the bay supported the 1896 offensive.",
                "Ang malawak na look sa kanluran ng Maynila at hilaga ng Kabite. Sinuportahan ng mga barkong pandigmang Kastila sa look ang opensiba noong 1896."),
            Term("ghent", "Ghent", "Ghent",
                "A city in Belgium whose university trained engineers. Edilberto Evangelista studied there.",
                "Isang lungsod sa Belhika na may pamantasang nagsasanay ng mga inhinyero. Doon nag-aral si Edilberto Evangelista."),
            Term("trench", "Trench (trinchera)", "Trinsera (trinchera)",
                "A long ditch dug for cover, with the earth thrown up in front as a bank. Soldiers in a trench are hard to hit. Trinchera is the Spanish word.",
                "Mahabang hukay na pinagkukublihan, na ang lupang hinukay ay itinambak sa harap bilang pilapil. Mahirap tamaan ang sundalong nasa trinsera. Trinchera ang salitang Kastila."),
            Term("muog", "Muog", "Muog",
                "A fortification or earthwork: a wall or bank of earth, stone or bamboo built for defense.",
                "Isang tanggulan: pader o pilapil ng lupa, bato o kawayan na itinayo para sa pagtatanggol."),
            Term("bolo", "Bolo", "Itak (bolo)",
                "A long single-edged knife, a farm tool used to clear brush. Many Katipuneros fought with it when they had no rifle.",
                "Mahabang patalim na iisa ang talim, kasangkapan sa bukid na pantabas ng damo. Marami sa mga Katipunero ang lumaban gamit ito nang wala silang riple."),
            Term("paltik", "Paltik", "Paltik",
                "A homemade gun, made by local smiths from whatever metal they could find.",
                "Baril na gawang-bahay, ginawa ng mga lokal na panday mula sa anumang bakal na kanilang makuha."),
            Term("remington", "Remington", "Remington",
                "An older single-shot rifle carried by Spanish troops. Revolutionaries used the ones they captured.",
                "Isang lumang riple na iisang bala bawat karga, dala ng mga tropang Kastila. Ginamit ng mga rebolusyonaryo ang mga nasamsam nila."),
            Term("mauser", "Mauser", "Mauser",
                "A modern Spanish army rifle of the 1890s that loaded five rounds at a time.",
                "Isang makabagong riple ng hukbong Kastila noong dekada 1890 na nagkakarga ng limang bala nang sabay."),
            Term("talibong", "Talibong", "Talibong",
                "A long sword with a single edge, from the Visayas. Blades like it were among the arms of the revolution.",
                "Mahabang espada na iisa ang talim, mula sa Kabisayaan. Kabilang ang ganitong mga talim sa mga sandata ng himagsikan."),
            Term("gulok", "Gulok", "Gulok",
                "A heavy work blade for cutting wood and clearing brush. Like the bolo, it was a farm tool before it was a weapon.",
                "Mabigat na itak na pantrabaho, pamutol ng kahoy at panghawan ng damo. Gaya ng bolo, kasangkapan ito sa bukid bago naging sandata."),
            Term("sibat", "Sibat", "Sibat",
                "Tagalog for spear: a pointed head, often of iron, on a long shaft of wood or bamboo.",
                "Mahabang tagdan na yari sa kahoy o kawayan na may matulis na dulo, kadalasang bakal."),
            Term("balaraw", "Balaraw", "Balaraw",
                "A dagger with two edges, short enough to carry hidden.",
                "Punyal na magkabila ang talim, sapat ang ikli upang maitago."),
            Term("lantaka", "Lantaka", "Lantaka",
                "A small bronze cannon on a swivel, long used in the islands on boats and forts. Revolutionaries used them too.",
                "Maliit na kanyong tanso na nakakabit sa paikutan, matagal nang gamit sa kapuluan sa mga bangka at kuta. Ginamit din ito ng mga rebolusyonaryo."),
            Term("reales", "Reales", "Reales",
                "Spanish silver coins (one real, two reales). In the game, Reales pay for recruits and drills.",
                "Mga baryang pilak ng Kastila (isang real, dalawang reales). Sa laro, ipinambabayad ang Reales sa pangangalap at pagsasanay."),
            Term("guardiacivil", "Guardia Civil", "Guardia Civil",
                "The Spanish colonial police force in the Philippines, which kept watch on the towns and hunted the Katipunan.",
                "Ang puwersang pulis ng pamahalaang kolonyal ng Kastila sa Pilipinas, na nagbabantay sa mga bayan at tumutugis sa Katipunan."),
            Term("cazadores", "Cazadores", "Cazadores",
                "Spanish for \"hunters\": light infantry battalions of the Spanish army, sent to the Philippines to fight the revolution.",
                "Salitang Kastila para sa \"mangangaso\": mga batalyon ng magaan na impanterya ng hukbong Kastila, na ipinadala sa Pilipinas upang labanan ang himagsikan."),
            Term("kapatiran", "Kapatiran", "Kapatiran",
                "Tagalog for brotherhood: the bond the Katipuneros swore to keep with one another.",
                "Salitang Tagalog para sa pagkakapatiran: ang buklod na sinumpaang panatilihin ng mga Katipunero sa isa't isa."),
            Term("himagsikan", "Himagsikan", "Himagsikan",
                "Tagalog for revolution. The Philippine Revolution against Spain began in August 1896.",
                "Salitang Tagalog para sa rebolusyon. Nagsimula ang Himagsikang Pilipino laban sa Espanya noong Agosto 1896."),
            Term("bayan", "Bayan", "Bayan",
                "Tagalog for town, people and nation at once. The Katipuneros called themselves the children of the Bayan.",
                "Salitang Tagalog na nangangahulugang bayan, taumbayan at bansa. Tinawag ng mga Katipunero ang kanilang sarili na mga anak ng Bayan."),
            Term("salakot", "Salakot", "Salakot",
                "A wide conical hat of woven bamboo or palm, worn against sun and rain.",
                "Malapad at hugis-apang na sombrerong yari sa hinabing kawayan o palma, pananggalang sa araw at ulan."),
            Term("sanggunian", "Sanggunian", "Sanggunian",
                "Tagalog for council. The Katipunan in each province was run by councils such as the Magdalo and the Magdiwang.",
                "Salitang Tagalog para sa konseho. Pinamahalaan ang Katipunan sa bawat lalawigan ng mga sanggunian gaya ng Magdalo at Magdiwang.")
        };

        public static IReadOnlyList<GlossaryTerm> All
        {
            get { return all; }
        }

        /// <summary>Every term, in alphabetical order of its headword in <paramref name="language"/>.</summary>
        public static List<GlossaryTerm> Sorted(Language language)
        {
            var sorted = new List<GlossaryTerm>(all);
            sorted.Sort((a, b) => string.Compare(SortKey(a.Term.Get(language)), SortKey(b.Term.Get(language)), StringComparison.Ordinal));
            return sorted;
        }

        /// <summary>The term with <paramref name="id"/>, or null.</summary>
        public static GlossaryTerm Find(string id)
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

        /// <summary>
        /// Folds case and the accented letters the glossary uses, so "Ramón" sorts with the Rs and
        /// "Andrés" with the As rather than after Z.
        /// </summary>
        public static string SortKey(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var chars = text.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                switch (chars[i])
                {
                    case 'á': case 'à': chars[i] = 'a'; break;
                    case 'é': case 'è': chars[i] = 'e'; break;
                    case 'í': chars[i] = 'i'; break;
                    case 'ó': chars[i] = 'o'; break;
                    case 'ú': case 'ü': chars[i] = 'u'; break;
                    case 'ñ': chars[i] = 'n'; break;
                }
            }

            return new string(chars);
        }

        private static GlossaryTerm Term(string id, string termEn, string termFil, string definitionEn, string definitionFil)
        {
            return new GlossaryTerm(id, new LocString(termEn, termFil), new LocString(definitionEn, definitionFil));
        }
    }
}
