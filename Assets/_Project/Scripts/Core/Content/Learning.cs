using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>A lesson in the Library, unlocked by clearing a sub-quest.</summary>
    public sealed class Lesson
    {
        public readonly string Id;

        /// <summary>The campaign level whose test covers this lesson.</summary>
        public readonly int Level;

        public readonly LocString Title;
        public readonly LocString Body;

        public Lesson(string id, int level, LocString title, LocString body)
        {
            Id = id;
            Level = level;
            Title = title;
            Body = body;
        }
    }

    /// <summary>A multiple-choice question, asked mid-battle and in the Library's level tests.</summary>
    public sealed class Question
    {
        public readonly string Id;
        public readonly int Level;
        public readonly LocString Text;

        /// <summary>Always four choices.</summary>
        public readonly LocString[] Choices;

        /// <summary>Index into <see cref="Choices"/> of the right answer.</summary>
        public readonly int Answer;

        /// <summary>Shown after the player answers, right or wrong.</summary>
        public readonly LocString Explanation;

        public Question(string id, int level, LocString text, LocString[] choices, int answer, LocString explanation)
        {
            Id = id;
            Level = level;
            Text = text;
            Choices = choices;
            Answer = answer;
            Explanation = explanation;
        }
    }

    /// <summary>
    /// The learning content: ten Library lessons, one per sub-quest, and ten questions per
    /// campaign level (panel feedback: "learning panel / assessment").
    /// </summary>
    /// <remarks>
    /// <para>
    /// The facts come from the capstone document (its lore, Tables 2 to 4, and its sources:
    /// Agoncillo 1990, Alvarez 1992). Four questions are Table 4's own. <b>The group must
    /// fact-check every lesson and question against the sources before the defense.</b>
    /// </para>
    /// <para>
    /// Some questions teach the game's rules (trenches, Kapatiran, rations) rather than history:
    /// the rules model the history, and knowing them is how a player wins.
    /// </para>
    /// </remarks>
    public static class Learning
    {
        public const int ChoiceCount = 4;

        private static readonly List<Lesson> lessons = new List<Lesson>
        {
            new Lesson("l01", 1,
                new LocString("The Scholar of Ghent", "Ang Iskolar ng Ghent"),
                new LocString(
                    "Edilberto Evangelista studied civil engineering at the University of Ghent in Belgium. When news of the Katipunan's revolution reached him in 1896, he chose to come home and serve.\n\nAn engineer knows how to shape earth, move water and build walls that stand. In the revolution, those skills would matter as much as any rifle.",
                    "Nag-aral si Edilberto Evangelista ng inhinyeriyang sibil sa Unibersidad ng Ghent sa Belhika. Nang dumating sa kanya ang balita ng himagsikan ng Katipunan noong 1896, pinili niyang umuwi at maglingkod.\n\nAlam ng inhinyero kung paano hubugin ang lupa, padaluyin ang tubig at magtayo ng pader na matibay. Sa himagsikan, kasinghalaga ng anumang riple ang mga kasanayang ito.")),
            new Lesson("l02", 1,
                new LocString("Courage Is Not Enough", "Hindi Sapat ang Tapang"),
                new LocString(
                    "The Katipunan was founded in 1892 by Andrés Bonifacio. Its revolution began in August 1896.\n\nEarly fighters were brave but often poorly armed, charging across open ground into Spanish rifles and artillery. Planning where to stand, and what to stand behind, saves lives.",
                    "Itinatag ni Andrés Bonifacio ang Katipunan noong 1892. Nagsimula ang himagsikan nito noong Agosto 1896.\n\nMatatapang ang mga unang mandirigma ngunit kulang sa armas, at madalas sumugod sa bukas na lupa laban sa riple at kanyon ng Kastila. Nakapagliligtas ng buhay ang pagpaplano kung saan tatayo at ano ang pagtataguan.")),
            new Lesson("l03", 1,
                new LocString("The People's Army", "Ang Hukbo ng Bayan"),
                new LocString(
                    "The revolutionary army was made of ordinary people: farmers, teachers, craftsmen and medics. Few were trained soldiers.\n\nIn Cavite, two councils of the Katipunan led the fight: the Magdalo, based in Kawit, and the Magdiwang, based in Noveleta.",
                    "Binubuo ang hukbong rebolusyonaryo ng mga karaniwang tao: magsasaka, guro, manggagawa at mediko. Iilan lamang ang sanay na sundalo.\n\nSa Kabite, dalawang sanggunian ng Katipunan ang namuno sa laban: ang Magdalo, na nakabase sa Kawit, at ang Magdiwang, na nakabase sa Noveleta.")),
            new Lesson("l04", 1,
                new LocString("An Army Must Eat", "Kailangang Kumain ng Hukbo"),
                new LocString(
                    "Every soldier needs food, and every rifle needs iron and powder. The rice fields of Cavite fed the revolution.\n\nLogistics - gathering, storing and moving supplies - decides how long an army can fight. In the game, Rations are spent to enter a battle.",
                    "Kailangan ng bawat sundalo ang pagkain, at ng bawat riple ang bakal at pulbura. Pinakain ng palayan ng Kabite ang himagsikan.\n\nAng lohistika - pagtitipon, pag-iimbak at paglilipat ng panustos - ang nagpapasya kung gaano katagal makalalaban ang hukbo. Sa laro, gumagastos ng Rasyon upang pumasok sa labanan.")),
            new Lesson("l05", 2,
                new LocString("Kapatiran", "Kapatiran"),
                new LocString(
                    "Kapatiran means brotherhood. Katipuneros swore to treat one another as brothers, and memoirs of the revolution tell of the bonds between them.\n\nIn the game, bonded pairs placed side by side gain support bonuses, like a medic beside a vanguard.",
                    "Ang Kapatiran ay nangangahulugang pagkakapatiran. Nanumpa ang mga Katipunero na ituring ang isa't isa bilang magkapatid, at isinasalaysay ng mga gunita ng himagsikan ang kanilang mga buklod.\n\nSa laro, may dagdag na lakas ang magkabuklod na magkatabi, tulad ng mediko sa tabi ng taliba.")),
            new Lesson("l06", 2,
                new LocString("Evangelista's Trenches", "Ang mga Trinsera ni Evangelista"),
                new LocString(
                    "Evangelista designed networks of trenches and earthworks, with bamboo defenses, across the approaches to Binakayan and Dalahican.\n\nA trench protects soldiers from rifle fire and shell fragments. In the game, a unit in a trench gains +20% Defense and +15% Evasion.",
                    "Nagdisenyo si Evangelista ng mga hanay ng trinsera at muog, na may depensang kawayan, sa mga daanan patungong Binakayan at Dalahican.\n\nPinoprotektahan ng trinsera ang sundalo mula sa bala at pira-piraso ng bomba. Sa laro, may +20% Depensa at +15% Iwas ang yunit na nasa trinsera.")),
            new Lesson("l07", 2,
                new LocString("Scouts and Secrets", "Mga Tanod at Lihim"),
                new LocString(
                    "Knowing where the enemy is, and keeping your own plans hidden, wins battles before they start.\n\nThe Katipunan itself began as a secret society, with codes and passwords to protect its members from the colonial government.",
                    "Ang pagkaalam kung nasaan ang kaaway, at ang pagtatago ng sariling plano, ay nagpapanalo ng labanan bago pa ito magsimula.\n\nNagsimula ang Katipunan bilang lihim na samahan, na may mga kodigo at hudyat upang ipagtanggol ang mga kasapi mula sa pamahalaang kolonyal.")),
            new Lesson("l08", 3,
                new LocString("November 9, 1896", "Nobyembre 9, 1896"),
                new LocString(
                    "Governor-General Ramón Blanco launched a major offensive on Cavite, with soldiers on land and warships in Manila Bay.\n\nThe Spanish attacked on two fronts at once: Binakayan, in Kawit, and Dalahican, in Noveleta.",
                    "Naglunsad si Gobernador-Heneral Ramón Blanco ng malaking opensiba sa Kabite, may mga sundalo sa lupa at barkong pandigma sa Look ng Maynila.\n\nSabay na sumalakay ang Kastila sa dalawang harapan: Binakayan, sa Kawit, at Dalahican, sa Noveleta.")),
            new Lesson("l09", 3,
                new LocString("The Shore at Dalahican", "Ang Baybayin ng Dalahican"),
                new LocString(
                    "At Dalahican the enemy had to cross the shallows and the shore under fire from the defenders' earthworks.\n\nWater slows soldiers and leaves them exposed. In the game, coastal shallows cost -15% Movement and -10% Defense.",
                    "Sa Dalahican, kinailangang tawirin ng kaaway ang mababaw na tubig at baybayin habang pinapuputukan mula sa mga muog ng tagapagtanggol.\n\nPinababagal ng tubig ang sundalo at inilalantad sila. Sa laro, may -15% Galaw at -10% Depensa sa mababaw na baybayin.")),
            new Lesson("l10", 3,
                new LocString("Victory at Binakayan-Dalahican", "Tagumpay sa Binakayan-Dalahican"),
                new LocString(
                    "After three days of fighting, from November 9 to 11, 1896, the Spanish offensive was thrown back.\n\nIt is remembered as the first major Filipino victory against Spanish forces in the revolution, won by planning, engineering and the courage of ordinary people.",
                    "Matapos ang tatlong araw ng labanan, mula Nobyembre 9 hanggang 11, 1896, naitaboy ang opensiba ng Kastila.\n\nGinugunita ito bilang unang malaking tagumpay ng mga Pilipino laban sa puwersang Kastila sa himagsikan, na napagtagumpayan sa pagpaplano, inhinyeriya at tapang ng karaniwang tao."))
        };

        private static readonly List<Question> questions = new List<Question>
        {
            // ---------------------------------------------------------------- Level 1
            Q("k1_01", 1, "Where did Edilberto Evangelista study engineering?", "Saan nag-aral ng inhinyeriya si Edilberto Evangelista?",
                C("Madrid, Spain", "Madrid, Espanya"), C("Ghent, Belgium", "Ghent, Belhika"), C("Hong Kong", "Hong Kong"), C("Manila", "Maynila"), 1,
                "He studied civil engineering at the University of Ghent.", "Nag-aral siya ng inhinyeriyang sibil sa Unibersidad ng Ghent."),
            Q("k1_02", 1, "What did Evangelista study?", "Ano ang pinag-aralan ni Evangelista?",
                C("Medicine", "Medisina"), C("Law", "Batas"), C("Civil engineering", "Inhinyeriyang sibil"), C("Painting", "Pagpipinta"), 2,
                "He was a civil engineer, which is why he could design trenches and earthworks.", "Inhinyerong sibil siya, kaya nakapagdisenyo siya ng trinsera at muog."),
            Q("k1_03", 1, "Who founded the Katipunan?", "Sino ang nagtatag ng Katipunan?",
                C("Andrés Bonifacio", "Andrés Bonifacio"), C("Emilio Aguinaldo", "Emilio Aguinaldo"), C("José Rizal", "José Rizal"), C("Antonio Luna", "Antonio Luna"), 0,
                "Andrés Bonifacio founded the Katipunan in 1892.", "Itinatag ni Andrés Bonifacio ang Katipunan noong 1892."),
            Q("k1_04", 1, "In what year was the Katipunan founded?", "Anong taon itinatag ang Katipunan?",
                C("1872", "1872"), C("1892", "1892"), C("1896", "1896"), C("1898", "1898"), 1,
                "It was founded in 1892; the revolution began in 1896.", "Itinatag ito noong 1892; nagsimula ang himagsikan noong 1896."),
            Q("k1_05", 1, "When did the Katipunan's revolution begin?", "Kailan nagsimula ang himagsikan ng Katipunan?",
                C("August 1896", "Agosto 1896"), C("June 1898", "Hunyo 1898"), C("December 1896", "Disyembre 1896"), C("January 1872", "Enero 1872"), 0,
                "The revolution broke out in August 1896.", "Sumiklab ang himagsikan noong Agosto 1896."),
            Q("k1_06", 1, "Why did early charges across open fields fail?", "Bakit nabigo ang mga unang pagsugod sa bukas na parang?",
                C("The fighters were cowards", "Duwag ang mga mandirigma"), C("It rained too much", "Masyadong umulan"), C("There was no cover from rifles and artillery", "Walang masisilungan mula sa riple at kanyon"), C("They had too many soldiers", "Sobra ang kanilang sundalo"), 2,
                "Courage alone could not stop modern firepower; cover and planning could.", "Hindi kayang pigilan ng tapang lamang ang makabagong armas; kaya ito ng silungan at pagpaplano."),
            Q("k1_07", 1, "Which Katipunan council was based in Kawit?", "Aling sanggunian ng Katipunan ang nakabase sa Kawit?",
                C("Magdiwang", "Magdiwang"), C("Magdalo", "Magdalo"), C("Balintawak", "Balintawak"), C("La Liga", "La Liga"), 1,
                "The Magdalo was based in Kawit; the Magdiwang in Noveleta.", "Nakabase sa Kawit ang Magdalo; sa Noveleta ang Magdiwang."),
            Q("k1_08", 1, "Who made up most of the revolutionary army?", "Sino ang bumubuo sa karamihan ng hukbong rebolusyonaryo?",
                C("Foreign soldiers", "Mga dayuhang sundalo"), C("Only rich landowners", "Mga mayamang may-lupa lamang"), C("Ordinary people: farmers, teachers, workers", "Karaniwang tao: magsasaka, guro, manggagawa"), C("Spanish deserters only", "Mga tumalikod na Kastila lamang"), 2,
                "It was a people's army of ordinary Filipinos.", "Hukbo ito ng bayan, ng mga karaniwang Pilipino."),
            Q("k1_09", 1, "Why does an army need logistics?", "Bakit kailangan ng hukbo ang lohistika?",
                C("To feed and supply soldiers so they can keep fighting", "Upang pakainin at tustusan ang sundalo para patuloy na lumaban"), C("To write letters home", "Upang sumulat ng liham pauwi"), C("To choose uniforms", "Upang pumili ng uniporme"), C("It does not need it", "Hindi nito kailangan"), 0,
                "Food, iron and powder decide how long an army can fight.", "Ang pagkain, bakal at pulbura ang nagpapasya kung gaano katagal makalalaban ang hukbo."),
            Q("k1_10", 1, "In what province did the Battle of Binakayan-Dalahican take place?", "Sa anong lalawigan naganap ang Labanan sa Binakayan-Dalahican?",
                C("Manila", "Maynila"), C("Bulacan", "Bulakan"), C("Laguna", "Laguna"), C("Cavite", "Kabite"), 3,
                "Binakayan is in Kawit and Dalahican is in Noveleta, both in Cavite.", "Nasa Kawit ang Binakayan at nasa Noveleta ang Dalahican, kapwa sa Kabite."),

            // ---------------------------------------------------------------- Level 2
            Q("k2_01", 2, "Who was the primary engineer of the trench networks in Cavite?", "Sino ang pangunahing inhinyero ng mga trinsera sa Kabite?",
                C("Andrés Bonifacio", "Andrés Bonifacio"), C("Emilio Aguinaldo", "Emilio Aguinaldo"), C("Edilberto Evangelista", "Edilberto Evangelista"), C("Antonio Luna", "Antonio Luna"), 2,
                "Edilberto Evangelista designed the trenches and earthworks.", "Si Edilberto Evangelista ang nagdisenyo ng mga trinsera at muog."),
            Q("k2_02", 2, "What does \"Kapatiran\" mean?", "Ano ang ibig sabihin ng \"Kapatiran\"?",
                C("Victory", "Tagumpay"), C("Brotherhood", "Pagkakapatiran"), C("Trench", "Trinsera"), C("Harvest", "Ani"), 1,
                "Katipuneros swore to treat each other as brothers.", "Nanumpa ang mga Katipunero na ituring ang isa't isa bilang magkapatid."),
            Q("k2_03", 2, "What does a trench protect soldiers from?", "Mula saan pinoprotektahan ng trinsera ang sundalo?",
                C("Rifle fire and shell fragments", "Bala at pira-piraso ng bomba"), C("Hunger", "Gutom"), C("Rain only", "Ulan lamang"), C("Disease", "Sakit"), 0,
                "Earth stops bullets and fragments that open ground cannot.", "Pinipigilan ng lupa ang bala at pira-pirasong hindi kaya ng bukas na lupa."),
            Q("k2_04", 2, "In the game, what does a trench tile give a unit?", "Sa laro, ano ang ibinibigay ng trinsera sa yunit?",
                C("+20% Defense, +15% Evasion", "+20% Depensa, +15% Iwas"), C("+50% Attack", "+50% Atake"), C("Extra Rations", "Dagdag na Rasyon"), C("Nothing", "Wala"), 0,
                "Table 2: Evangelista's Trench, +20% Defense and +15% Evasion.", "Talahanayan 2: Trinsera ni Evangelista, +20% Depensa at +15% Iwas."),
            Q("k2_05", 2, "What material was used for the sharp barricades?", "Anong materyales ang ginamit sa matutulis na harang?",
                C("Steel", "Bakal"), C("Bamboo", "Kawayan"), C("Glass", "Salamin"), C("Brick", "Ladrilyo"), 1,
                "Bamboo was plentiful and made strong, sharp barricades.", "Sagana ang kawayan at nakagagawa ng matibay at matulis na harang."),
            Q("k2_06", 2, "Which Katipunan council was based in Noveleta?", "Aling sanggunian ng Katipunan ang nakabase sa Noveleta?",
                C("Magdalo", "Magdalo"), C("Magdiwang", "Magdiwang"), C("Tejeros", "Tejeros"), C("Biak-na-Bato", "Biak-na-Bato"), 1,
                "The Magdiwang was based in Noveleta, near Dalahican.", "Nakabase sa Noveleta, malapit sa Dalahican, ang Magdiwang."),
            Q("k2_07", 2, "Why did the Katipunan use codes and passwords?", "Bakit gumamit ang Katipunan ng kodigo at hudyat?",
                C("For fun", "Para sa kasiyahan"), C("To protect members from the colonial government", "Upang ipagtanggol ang mga kasapi mula sa pamahalaang kolonyal"), C("To trade goods", "Upang makipagkalakalan"), C("To count soldiers", "Upang bilangin ang sundalo"), 1,
                "It began as a secret society; discovery meant arrest.", "Nagsimula ito bilang lihim na samahan; ang pagkabunyag ay nangangahulugang pagdakip."),
            Q("k2_08", 2, "Why is scouting important before a battle?", "Bakit mahalaga ang pagmamanman bago ang labanan?",
                C("It shows where the enemy is and how to prepare", "Ipinapakita nito kung nasaan ang kaaway at paano maghanda"), C("It is not important", "Hindi ito mahalaga"), C("It makes the battle shorter", "Pinaiikli nito ang labanan"), C("It feeds the army", "Pinakakain nito ang hukbo"), 0,
                "Knowing the enemy's movements lets you place your defenses well.", "Ang pagkaalam sa galaw ng kaaway ay tumutulong sa mahusay na pagpuwesto ng depensa."),
            Q("k2_09", 2, "In the game, what happens when bonded units stand side by side?", "Sa laro, ano ang nangyayari kapag magkatabi ang magkabuklod na yunit?",
                C("They argue", "Nagtatalo sila"), C("They gain support bonuses", "Nagkakaroon sila ng dagdag na lakas"), C("They lose health", "Nababawasan ang kanilang buhay"), C("Nothing", "Wala"), 1,
                "Kapatiran support bonuses reward keeping trusted comrades together.", "Ginagantimpalaan ng Kapatiran ang pagsasama ng magkakatiwalang kasama."),
            Q("k2_10", 2, "What was the engineer's main advantage over the Spanish firepower?", "Ano ang pangunahing bentahe ng inhinyero laban sa lakas-putok ng Kastila?",
                C("More cannons", "Mas maraming kanyon"), C("Faster ships", "Mas mabilis na barko"), C("Earthworks that neutralized superior firepower", "Mga muog na nagpawalang-bisa sa nakahihigit na lakas-putok"), C("Bigger uniforms", "Mas malaking uniporme"), 2,
                "Agoncillo credits the trench networks for neutralizing Spanish firepower.", "Ayon kay Agoncillo, ang mga trinsera ang nagpawalang-bisa sa lakas-putok ng Kastila."),

            // ---------------------------------------------------------------- Level 3
            Q("k3_01", 3, "When did the Battle of Binakayan-Dalahican take place?", "Kailan naganap ang Labanan sa Binakayan-Dalahican?",
                C("August 1896", "Agosto 1896"), C("November 9-11, 1896", "Nobyembre 9-11, 1896"), C("June 12, 1898", "Hunyo 12, 1898"), C("December 30, 1896", "Disyembre 30, 1896"), 1,
                "The battle lasted three days, November 9 to 11, 1896.", "Tatlong araw ang labanan, Nobyembre 9 hanggang 11, 1896."),
            Q("k3_02", 3, "Who was the Spanish Governor-General who launched the attack on Cavite?", "Sino ang Gobernador-Heneral na Kastila na naglunsad ng salakay sa Kabite?",
                C("Ramón Blanco", "Ramón Blanco"), C("Camilo de Polavieja", "Camilo de Polavieja"), C("Miguel López de Legazpi", "Miguel López de Legazpi"), C("Fernando Primo de Rivera", "Fernando Primo de Rivera"), 0,
                "Governor-General Ramón Blanco ordered the offensive.", "Si Gobernador-Heneral Ramón Blanco ang nag-utos ng opensiba."),
            Q("k3_03", 3, "The victory at Binakayan-Dalahican was the first major Filipino victory in which province?", "Ang tagumpay sa Binakayan-Dalahican ay unang malaking tagumpay ng Pilipino sa anong lalawigan?",
                C("Manila", "Maynila"), C("Bulacan", "Bulakan"), C("Laguna", "Laguna"), C("Cavite", "Kabite"), 3,
                "It was won in Cavite.", "Napagtagumpayan ito sa Kabite."),
            Q("k3_04", 3, "Binakayan is part of which town?", "Bahagi ng anong bayan ang Binakayan?",
                C("Kawit", "Kawit"), C("Noveleta", "Noveleta"), C("Imus", "Imus"), C("Bacoor", "Bacoor"), 0,
                "Binakayan is in Kawit; Dalahican is in Noveleta.", "Nasa Kawit ang Binakayan; nasa Noveleta ang Dalahican."),
            Q("k3_05", 3, "Dalahican is part of which town?", "Bahagi ng anong bayan ang Dalahican?",
                C("Kawit", "Kawit"), C("Noveleta", "Noveleta"), C("Rosario", "Rosario"), C("Silang", "Silang"), 1,
                "Dalahican is on the shore of Noveleta.", "Nasa baybayin ng Noveleta ang Dalahican."),
            Q("k3_06", 3, "How did the Spanish attack in this battle?", "Paano sumalakay ang Kastila sa labanang ito?",
                C("Only by sea", "Sa dagat lamang"), C("Only from the mountains", "Mula sa bundok lamang"), C("On two fronts at once, by land and sea", "Sa dalawang harapan nang sabay, sa lupa at dagat"), C("They did not attack", "Hindi sila sumalakay"), 2,
                "It was a simultaneous two-front battle with naval support.", "Sabayang labanan ito sa dalawang harapan na may suporta ng hukbong-dagat."),
            Q("k3_07", 3, "In the game, what do coastal shallows do to a unit?", "Sa laro, ano ang epekto ng mababaw na baybayin sa yunit?",
                C("-15% Movement, -10% Defense", "-15% Galaw, -10% Depensa"), C("+20% Attack", "+20% Atake"), C("Full healing", "Buong paggaling"), C("Nothing", "Wala"), 0,
                "Table 2: water slows soldiers and leaves them exposed.", "Talahanayan 2: pinababagal ng tubig ang sundalo at inilalantad sila."),
            Q("k3_08", 3, "How many days did the battle last?", "Ilang araw tumagal ang labanan?",
                C("One", "Isa"), C("Two", "Dalawa"), C("Three", "Tatlo"), C("Ten", "Sampu"), 2,
                "November 9, 10 and 11: three days.", "Nobyembre 9, 10 at 11: tatlong araw."),
            Q("k3_09", 3, "What was the result of the Spanish offensive?", "Ano ang kinalabasan ng opensiba ng Kastila?",
                C("It captured all of Cavite", "Nasakop nito ang buong Kabite"), C("It was thrown back", "Naitaboy ito"), C("It never started", "Hindi ito nagsimula"), C("It ended in a treaty", "Nagtapos ito sa kasunduan"), 1,
                "The defenders held and the Spanish withdrew.", "Nanindigan ang mga tagapagtanggol at umatras ang Kastila."),
            Q("k3_10", 3, "What does the victory teach about how battles are won?", "Ano ang itinuturo ng tagumpay tungkol sa pagkapanalo sa labanan?",
                C("Only numbers matter", "Bilang lamang ang mahalaga"), C("Planning, engineering and cooperation matter", "Mahalaga ang pagpaplano, inhinyeriya at pagtutulungan"), C("Luck decides everything", "Swerte ang nagpapasya ng lahat"), C("Weapons alone win", "Sandata lamang ang nagpapanalo"), 1,
                "Trenches, supply and brotherhood beat superior firepower.", "Tinalo ng trinsera, panustos at kapatiran ang nakahihigit na lakas-putok.")
        };

        public static IReadOnlyList<Lesson> Lessons
        {
            get { return lessons; }
        }

        public static IReadOnlyList<Question> Questions
        {
            get { return questions; }
        }

        public static Lesson FindLesson(string id)
        {
            for (int i = 0; i < lessons.Count; i++)
            {
                if (lessons[i].Id == id)
                {
                    return lessons[i];
                }
            }

            return null;
        }

        public static Question FindQuestion(string id)
        {
            for (int i = 0; i < questions.Count; i++)
            {
                if (questions[i].Id == id)
                {
                    return questions[i];
                }
            }

            return null;
        }

        /// <summary>The questions of campaign level <paramref name="level"/>.</summary>
        public static List<Question> QuestionsIn(int level)
        {
            var found = new List<Question>();
            for (int i = 0; i < questions.Count; i++)
            {
                if (questions[i].Level == level)
                {
                    found.Add(questions[i]);
                }
            }

            return found;
        }

        /// <summary>
        /// The mid-battle question for <paramref name="level"/>: the first not yet asked, so a
        /// player meets each once before any repeats; after that, one picked by <paramref name="seed"/>.
        /// </summary>
        public static Question NextQuiz(int level, ICollection<string> asked, int seed)
        {
            List<Question> pool = QuestionsIn(level);
            if (pool.Count == 0)
            {
                return null;
            }

            int start = (seed & 0x7FFFFFFF) % pool.Count;
            for (int i = 0; i < pool.Count; i++)
            {
                Question q = pool[(start + i) % pool.Count];
                if (asked == null || !asked.Contains(q.Id))
                {
                    return q;
                }
            }

            return pool[start];
        }

        /// <summary>
        /// <paramref name="count"/> distinct questions of <paramref name="level"/> in an order set by
        /// <paramref name="seed"/> (Fisher-Yates over a small LCG, so tests are repeatable).
        /// </summary>
        public static List<Question> Test(int level, int count, int seed)
        {
            List<Question> pool = QuestionsIn(level);
            uint state = (uint)seed * 2654435761u + 1u;
            for (int i = pool.Count - 1; i > 0; i--)
            {
                state = (state * 1664525u) + 1013904223u;
                int j = (int)((state >> 8) % (uint)(i + 1));
                Question swap = pool[i];
                pool[i] = pool[j];
                pool[j] = swap;
            }

            if (count < pool.Count)
            {
                pool.RemoveRange(count, pool.Count - count);
            }

            return pool;
        }

        private static LocString C(string en, string fil)
        {
            return new LocString(en, fil);
        }

        private static Question Q(string id, int level, string en, string fil,
            LocString a, LocString b, LocString c, LocString d, int answer, string whyEn, string whyFil)
        {
            return new Question(id, level, new LocString(en, fil), new[] { a, b, c, d }, answer, new LocString(whyEn, whyFil));
        }
    }
}
