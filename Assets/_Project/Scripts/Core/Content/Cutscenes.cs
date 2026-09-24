using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>What a cutscene slide's picture shows, when no illustration was drawn for it.</summary>
    public enum SceneMood
    {
        /// <summary>A European city under a grey sky: Ghent.</summary>
        City = 0,

        /// <summary>The encampment by day: tents and palms.</summary>
        Camp = 1,

        /// <summary>Manila Bay: sea, a distant ship.</summary>
        Sea = 2,

        /// <summary>The trench line at dusk.</summary>
        Trench = 3,

        /// <summary>Smoke and fire over the field.</summary>
        Battle = 4,

        /// <summary>First light over the rice fields.</summary>
        Dawn = 5,

        /// <summary>Night, lamplight.</summary>
        Night = 6
    }

    /// <summary>One picture and one line of narration.</summary>
    public sealed class CutsceneSlide
    {
        /// <summary>
        /// The illustration's name under <c>Resources/Cutscenes/</c>. When there is none, the
        /// player paints <see cref="Mood"/> instead.
        /// </summary>
        public readonly string Image;

        public readonly SceneMood Mood;

        /// <summary>Where and when, over the picture.</summary>
        public readonly LocString Caption;

        /// <summary>What the narrator says.</summary>
        public readonly LocString Line;

        public CutsceneSlide(string image, SceneMood mood, LocString caption, LocString line)
        {
            Image = image;
            Mood = mood;
            Caption = caption;
            Line = line;
        }
    }

    /// <summary>A short story scene: a few slides told by one narrator.</summary>
    public sealed class Cutscene
    {
        public readonly string Id;

        /// <summary>The <see cref="Characters"/> id of who narrates.</summary>
        public readonly string Narrator;

        public readonly CutsceneSlide[] Slides;

        public Cutscene(string id, string narrator, params CutsceneSlide[] slides)
        {
            Id = id;
            Narrator = narrator;
            Slides = slides;
        }
    }

    /// <summary>
    /// The campaign's story scenes, one before each sub-quest and one after the last battle, all
    /// told by Tadah, the Senador (panel feedback: "scenarios (cut scenes)").
    /// </summary>
    /// <remarks>
    /// <para>
    /// The proposal's four acts (Appendix F, "The Lore: The Architect's Ascent") open q01, q02,
    /// q03 and q08; the aftermath closes q10. A hub-task quest's opening scene is played by the
    /// encampment the first time the quest is current; a battle's plays when it is launched.
    /// </para>
    /// <para>
    /// SME CHECK PENDING (#34, #40). Acts 2 and 3 dramatize, as the proposal does: the veterans'
    /// doubt and the open-field skirmish are story, not a claim about one recorded event.
    /// </para>
    /// <para>
    /// The history follows the capstone document's account of the Battle of
    /// Binakayan-Dalahican, 9 to 11 November 1896. The group should check every line against
    /// its sources before the defense; nothing here is meant to add a claim the document does
    /// not make.
    /// </para>
    /// </remarks>
    public static class Cutscenes
    {
        /// <summary>Act 1, The Scholar of Ghent: opens the campaign (q01).</summary>
        public const string Act1 = "act1_scholar";

        /// <summary>Act 2, The Catalyst of Defeat: opens the first battle (q02).</summary>
        public const string Act2 = "act2_catalyst";

        /// <summary>Act 3, Forging the Brotherhood: opens the Vanguard's Rally (q03).</summary>
        public const string Act3 = "act3_brotherhood";

        /// <summary>Act 4, The Masterpiece of Binakayan-Dalahican: opens Level 3 (q08).</summary>
        public const string Act4 = "act4_masterpiece";

        /// <summary>The visual-novel aftermath of November 11, 1896, after the final battle (q10).</summary>
        public const string Aftermath = "c11_aftermath";

        private static readonly List<Cutscene> all = new List<Cutscene>
        {
            // ---------------------------------------------------------- Act 1 (#34)
            new Cutscene(Act1, Characters.Senador,
                Slide("act1_1", SceneMood.City,
                    "Act 1 - The Scholar of Ghent", "Unang Yugto - Ang Iskolar ng Ghent",
                    "Ghent, Belgium, 1896. Edilberto Evangelista studies civil engineering at the university. A comfortable future waits for him in Europe.",
                    "Ghent, Belhika, 1896. Nag-aaral si Edilberto Evangelista ng inhinyeriyang sibil sa pamantasan. Isang maginhawang kinabukasan ang naghihintay sa kanya sa Europa."),
                Slide("act1_2", SceneMood.Night,
                    "Letters from home", "Mga liham mula sa tahanan",
                    "Letters from the Philippines reach him. The Katipunan has risen against Spain.",
                    "Dumating sa kanya ang mga liham mula sa Pilipinas. Bumangon na ang Katipunan laban sa Espanya."),
                Slide("act1_3", SceneMood.Night,
                    "A choice", "Isang pasya",
                    "He can stay and prosper, or go home to a war. He packs his drawings and his books.",
                    "Maaari siyang manatili at umunlad, o umuwi sa digmaan. Inimpake niya ang kanyang mga guhit at aklat."),
                Slide("act1_4", SceneMood.Sea,
                    "The voyage home", "Ang paglalayag pauwi",
                    "He sails for Manila. An engineer knows how to shape earth and build walls that stand - skills a revolution will need.",
                    "Naglayag siya patungong Maynila. Alam ng inhinyero kung paano hubugin ang lupa at magtayo ng pader na matibay - mga kasanayang kakailanganin ng himagsikan."),
                Slide("act1_5", SceneMood.Camp,
                    "Kawit, Cavite", "Kawit, Kabite",
                    "He comes home to the camp at Kawit. Here, you are that engineer. Your aide Tomas is waiting.",
                    "Umuwi siya sa kampo sa Kawit. Dito, ikaw ang inhinyerong iyon. Naghihintay ang iyong katuwang na si Tomas.")),

            // ---------------------------------------------------------- Act 2 (#34)
            // A dramatization: the skirmish stands for the early open-field losses the lessons
            // describe, not for one named battle.
            new Cutscene(Act2, Characters.Senador,
                Slide("act2_1", SceneMood.Camp,
                    "Act 2 - The Catalyst of Defeat", "Ikalawang Yugto - Ang Pagkatalong Nagmulat",
                    "The veteran commanders are unsure of the young engineer. Wars, they say, are won with courage and the bolo.",
                    "Hindi pa panatag ang mga beteranong pinuno sa batang inhinyero. Ayon sa kanila, napagtatagumpayan ang digmaan sa tapang at itak."),
                Slide("act2_2", SceneMood.Battle,
                    "An open field", "Isang bukas na parang",
                    "He watches brave fighters charge across open ground. Spanish rifles and artillery cut them down.",
                    "Pinanood niya ang matatapang na mandirigmang sumugod sa bukas na lupa. Pinabagsak sila ng riple at kanyon ng Kastila."),
                Slide("act2_3", SceneMood.Trench,
                    "A hard lesson", "Isang mapait na aral",
                    "Courage without cover is not enough. Soldiers need earth between them and the guns.",
                    "Hindi sapat ang tapang kung walang kublihan. Kailangan ng sundalo ng lupang nakapagitan sa kanila at sa mga baril."),
                Slide("act2_4", SceneMood.Trench,
                    "Kawit - the trench line", "Kawit - ang hanay ng trinsera",
                    "Now a Spanish patrol is on the road. Scouts report soldiers probing the trenches.",
                    "Ngayon ay may patrolyang Kastila sa daan. Iniulat ng mga tanod na may mga sundalong sumusubok sa trinsera."),
                Slide("act2_5", SceneMood.Battle,
                    "Your first command", "Ang iyong unang utos",
                    "Place your soldiers well. Once the assault begins, they fight on their own - the planning is yours.",
                    "Ipuwesto nang mabuti ang iyong mga sundalo. Kapag nagsimula na ang salakay, sila na ang lalaban - sa iyo ang pagpaplano.")),

            // ---------------------------------------------------------- Act 3 (#34)
            new Cutscene(Act3, Characters.Senador,
                Slide("act3_1", SceneMood.Camp,
                    "Act 3 - Forging the Brotherhood", "Ikatlong Yugto - Ang Pagbuo ng Kapatiran",
                    "Evangelista builds a camp of his own. Word of the skirmish spreads, and farmers, teachers and medics walk in, ready to serve.",
                    "Nagtayo si Evangelista ng sariling kampo. Kumalat ang balita ng sagupaan, at naglakad papasok ang mga magsasaka, guro at mediko, handang maglingkod."),
                Slide("act3_2", SceneMood.Camp,
                    "Kapatiran", "Kapatiran",
                    "Courage is not enough. Recruit them, drill them and arm them, until they trust one another like brothers.",
                    "Hindi sapat ang tapang. Kalapin sila, sanayin at armasan, hanggang magtiwala sila sa isa't isa na parang magkapatid."),
                Slide("act3_3", SceneMood.Night,
                    "Command", "Pamumuno",
                    "The revolution's leaders see what he can do, and give him the work of fortifying the Cavite line.",
                    "Nakita ng mga pinuno ng himagsikan ang kanyang kakayahan, at ipinagkatiwala sa kanya ang pagpapatibay ng hanay sa Kabite."),
                Slide("act3_4", SceneMood.Trench,
                    "Earth and bamboo", "Lupa at kawayan",
                    "He begins to plan trenches, earthworks and bamboo defenses across the approaches to Binakayan and Dalahican.",
                    "Sinimulan niyang planuhin ang mga trinsera, muog at depensang kawayan sa mga daanan patungong Binakayan at Dalahican.")),

            new Cutscene("c04_scavenge", Characters.Senador,
                Slide("c04_1", SceneMood.Dawn,
                    "Noveleta", "Noveleta",
                    "An army marches on its stomach. The rice fields of Cavite and old iron from every town will keep this one alive.",
                    "Lumalakad ang hukbo sa sikmura. Ang palayan ng Kabite at lumang bakal mula sa bawat bayan ang bubuhay sa hukbong ito.")),

            new Cutscene("c05_armada", Characters.Senador,
                Slide("c05_1", SceneMood.Sea,
                    "Bacoor Bay", "Look ng Bacoor",
                    "Spanish boats are seen off the shore. A landing party means to test the rebels' coast.",
                    "May mga bangkang Kastila sa laot. Balak ng isang pangkat na dumaong at subukin ang baybayin ng mga rebelde."),
                Slide("c05_2", SceneMood.Trench,
                    "Kapatiran", "Kapatiran",
                    "Soldiers who trust each other fight better side by side. Keep bonded pairs together.",
                    "Mas mahusay lumaban nang magkatabi ang mga sundalong nagtitiwala sa isa't isa. Pagsamahin ang magkabuklod.")),

            new Cutscene("c06_earthworks", Characters.Senador,
                Slide("c06_1", SceneMood.Trench,
                    "Binakayan", "Binakayan",
                    "The engineer's plan takes shape: trenches and earthworks along the approach to Binakayan.",
                    "Nabubuo ang plano ng inhinyero: mga trinsera at muog sa daanan patungong Binakayan."),
                Slide("c06_2", SceneMood.Battle,
                    "Hold the line", "Manindigan",
                    "The works are half dug when the enemy comes. The diggers need time - hold until they finish.",
                    "Kalahati pa lamang ang nahuhukay nang dumating ang kaaway. Kailangan ng oras ng mga manghuhukay - manindigan hanggang matapos sila.")),

            new Cutscene("c07_sabotage", Characters.Senador,
                Slide("c07_1", SceneMood.Night,
                    "Cavite Nuevo", "Cavite Nuevo",
                    "A guard post watches the road. Only a few can slip past unseen. Choose them with care.",
                    "Isang bantayan ang nagmamasid sa daan. Iilan lamang ang makalulusot nang hindi nakikita. Piliin silang mabuti.")),

            // ---------------------------------------------------------- Act 4 (#34)
            new Cutscene(Act4, Characters.Senador,
                Slide("act4_1", SceneMood.Sea,
                    "Act 4 - The Masterpiece of Binakayan-Dalahican", "Ikaapat na Yugto - Ang Obra Maestra ng Binakayan-Dalahican",
                    "November 9, 1896. Governor-General Ramón Blanco launches his offensive on Cavite: soldiers on land, warships in the bay.",
                    "Nobyembre 9, 1896. Inilunsad ni Gobernador-Heneral Ramón Blanco ang kanyang opensiba sa Kabite: mga sundalo sa lupa, mga barkong pandigma sa look."),
                Slide("act4_2", SceneMood.Trench,
                    "Two fronts", "Dalawang harapan",
                    "The Spanish attack at Binakayan and at Dalahican at once. In their way stand Evangelista's trenches.",
                    "Sabay na sumalakay ang Kastila sa Binakayan at sa Dalahican. Nakaharang sa kanila ang mga trinsera ni Evangelista."),
                Slide("act4_3", SceneMood.Battle,
                    "The grand design", "Ang dakilang plano",
                    "The trenches you built are about to be tested. Place every soldier where the earthworks protect them best.",
                    "Masusubok na ang mga trinserang itinayo mo. Ipuwesto ang bawat sundalo kung saan sila pinakamahusay na maipagtatanggol ng mga muog.")),

            new Cutscene("c09_attrition", Characters.Senador,
                Slide("c09_1", SceneMood.Sea,
                    "November 10, 1896", "Nobyembre 10, 1896",
                    "The fighting spreads to the shore at Dalahican. The enemy wades through the shallows toward the line.",
                    "Lumaganap ang labanan sa baybayin ng Dalahican. Lumulusong ang kaaway sa mababaw na tubig patungo sa hanay.")),

            new Cutscene("c10_dawn", Characters.Senador,
                Slide("c10_1", SceneMood.Dawn,
                    "November 11, 1896", "Nobyembre 11, 1896",
                    "The third day. Both sides are tired, and the largest assault is coming.",
                    "Ikatlong araw. Pagod na ang magkabilang panig, at paparating ang pinakamalaking salakay."),
                Slide("c10_2", SceneMood.Trench,
                    "The decisive dawn", "Ang mapagpasyang bukang-liwayway",
                    "Everything built, harvested, trained and learned comes down to this morning.",
                    "Ang lahat ng itinayo, inani, sinanay at natutunan ay nakasalalay sa umagang ito.")),

            // ---------------------------------------------------------- Aftermath (#40)
            new Cutscene(Aftermath, Characters.Senador,
                Slide("c11_1", SceneMood.Dawn,
                    "November 11, 1896", "Nobyembre 11, 1896",
                    "On the third day the last Spanish assault breaks against the trenches. The columns fall back.",
                    "Sa ikatlong araw, nabasag sa mga trinsera ang huling salakay ng Kastila. Umurong ang mga hanay nila."),
                Slide("c11_2", SceneMood.Battle,
                    "Binakayan-Dalahican", "Binakayan-Dalahican",
                    "The Spanish offensive on Cavite has failed. The Magdalo held Binakayan; the Magdiwang held Dalahican.",
                    "Nabigo ang opensiba ng Kastila sa Kabite. Naipagtanggol ng Magdalo ang Binakayan; naipagtanggol ng Magdiwang ang Dalahican."),
                Slide("c11_3", SceneMood.Trench,
                    "Evangelista's works", "Ang mga gawa ni Evangelista",
                    "The trenches did their work. Behind walls of earth, soldiers with few rifles held against a modern army.",
                    "Nagawa ng mga trinsera ang kanilang tungkulin. Sa likod ng pader na lupa, napigilan ng mga sundalong kakaunti ang riple ang isang makabagong hukbo."),
                Slide("c11_4", SceneMood.Camp,
                    "The news spreads", "Kumalat ang balita",
                    "It is one of the first great victories of the revolution. The news gives hope far beyond Cavite.",
                    "Isa ito sa mga unang dakilang tagumpay ng himagsikan. Nagbigay ng pag-asa ang balita hanggang sa labas ng Kabite."),
                Slide("c11_5", SceneMood.Night,
                    "What came after", "Ang sumunod",
                    "Within weeks, Blanco was replaced as Governor-General. The war went on, and Evangelista himself fell at Zapote Bridge in February 1897.",
                    "Makalipas ang ilang linggo, pinalitan si Blanco bilang Gobernador-Heneral. Nagpatuloy ang digmaan, at nasawi si Evangelista sa Tulay ng Zapote noong Pebrero 1897."),
                Slide("c11_6", SceneMood.Camp,
                    "Remember", "Alalahanin",
                    "Victories are won by planning, by people who feed and arm an army, and by soldiers who trust each other. Remember them.",
                    "Napagtatagumpayan ang digmaan sa pagpaplano, sa mga taong nagpapakain at nag-aarmas sa hukbo, at sa mga sundalong nagtitiwala sa isa't isa. Alalahanin sila."))
        };

        public static IReadOnlyList<Cutscene> All
        {
            get { return all; }
        }

        /// <summary>The cutscene with <paramref name="id"/>, or null.</summary>
        public static Cutscene Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Id == id)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static CutsceneSlide Slide(string image, SceneMood mood, string captionEn, string captionFil, string lineEn, string lineFil)
        {
            return new CutsceneSlide(image, mood, new LocString(captionEn, captionFil), new LocString(lineEn, lineFil));
        }
    }
}
