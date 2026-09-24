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
    /// The history follows the capstone document's account of the Battle of
    /// Binakayan-Dalahican, 9 to 11 November 1896. The group should check every line against
    /// its sources before the defense; nothing here is meant to add a claim the document does
    /// not make.
    /// </remarks>
    public static class Cutscenes
    {
        private static readonly List<Cutscene> all = new List<Cutscene>
        {
            new Cutscene("c01_ghent", Characters.Senador,
                Slide("c01_1", SceneMood.City,
                    "Ghent, Belgium - 1896", "Ghent, Belhika - 1896",
                    "Far from home, a young Filipino studies to become an engineer. He learns how walls stand, how water moves, how earth is shaped.",
                    "Malayo sa tahanan, isang binatang Pilipino ang nag-aaral upang maging inhinyero. Natutunan niya kung paano tumatayo ang pader, gumagalaw ang tubig, at hinuhubog ang lupa."),
                Slide("c01_2", SceneMood.Night,
                    "A letter from Cavite", "Isang liham mula sa Kabite",
                    "Then news arrives: the Katipunan has risen. His country needs builders now, not only soldiers.",
                    "Pagkatapos ay dumating ang balita: bumangon na ang Katipunan. Kailangan ngayon ng bayan ang mga tagapagtayo, hindi lamang mga sundalo."),
                Slide("c01_3", SceneMood.Camp,
                    "Kawit, Cavite", "Kawit, Kabite",
                    "He comes home to the camp at Kawit. Here, you are that engineer. Your aide Tomas is waiting.",
                    "Umuwi siya sa kampo sa Kawit. Dito, ikaw ang inhinyerong iyon. Naghihintay ang iyong katuwang na si Tomas.")),

            new Cutscene("c02_reality", Characters.Senador,
                Slide("c02_1", SceneMood.Trench,
                    "Kawit - the trench line", "Kawit - ang hanay ng trinsera",
                    "Books are one thing. A Spanish patrol on the road is another. Scouts report soldiers probing the trenches.",
                    "Iba ang aklat. Iba ang patrolyang Kastila sa daan. Iniulat ng mga tanod na may mga sundalong sumusubok sa trinsera."),
                Slide("c02_2", SceneMood.Battle,
                    "Your first command", "Ang iyong unang utos",
                    "Place your soldiers well. Once the assault begins, they fight on their own - the planning is yours.",
                    "Ipuwesto nang mabuti ang iyong mga sundalo. Kapag nagsimula na ang salakay, sila na ang lalaban - sa iyo ang pagpaplano.")),

            new Cutscene("c03_rally", Characters.Senador,
                Slide("c03_1", SceneMood.Camp,
                    "The Vanguard's Rally", "Ang Pagtitipon ng Taliba",
                    "Word of the skirmish spreads. Farmers, teachers and medics walk into camp, ready to serve.",
                    "Kumalat ang balita ng sagupaan. Naglakad papasok sa kampo ang mga magsasaka, guro at mediko, handang maglingkod."),
                Slide("c03_2", SceneMood.Camp,
                    "Kawit encampment", "Kampo sa Kawit",
                    "Courage is not enough. Recruit them, drill them, and put a weapon in their hands.",
                    "Hindi sapat ang tapang. Kalapin sila, sanayin, at bigyan ng sandata.")),

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

            new Cutscene("c08_firstwave", Characters.Senador,
                Slide("c08_1", SceneMood.Sea,
                    "November 9, 1896", "Nobyembre 9, 1896",
                    "Governor-General Ramon Blanco launches his offensive on Cavite. Spanish columns move on Binakayan.",
                    "Sinimulan ni Gobernador-Heneral Ramon Blanco ang kanyang opensiba sa Kabite. Sumusulong sa Binakayan ang mga hanay ng Kastila."),
                Slide("c08_2", SceneMood.Battle,
                    "Binakayan", "Binakayan",
                    "The trenches you built are about to be tested. Every soldier you trained stands in them.",
                    "Masusubok na ang mga trinserang itinayo mo. Nakatayo roon ang bawat sundalong sinanay mo.")),

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

            new Cutscene("c11_aftermath", Characters.Senador,
                Slide("c11_1", SceneMood.Dawn,
                    "Binakayan-Dalahican", "Binakayan-Dalahican",
                    "The Spanish offensive is thrown back. It is one of the first great victories of the revolution.",
                    "Naitaboy ang opensiba ng Kastila. Isa ito sa mga unang dakilang tagumpay ng himagsikan."),
                Slide("c11_2", SceneMood.Camp,
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
