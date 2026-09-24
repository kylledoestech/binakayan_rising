using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>One line of speech and who says it.</summary>
    public sealed class DialogueLine
    {
        /// <summary>A <see cref="Characters"/> id, or a <see cref="UnitCatalog"/> archetype id for the Kapatiran lore.</summary>
        public readonly string Speaker;

        public readonly LocString Text;

        public DialogueLine(string speaker, LocString text)
        {
            Speaker = speaker;
            Text = text;
        }
    }

    /// <summary>
    /// What the aide and the keepers say in the encampment: the aide's welcome, which is the
    /// first sub-quest, and each keeper's explanation of their building the first time the
    /// player walks up to it.
    /// </summary>
    /// <remarks>
    /// Explanations here are the "clear instructions" for the economy: each keeper says what
    /// their building makes, what that is for, and what to do next, in that order. Numbers are
    /// never written into a line — they are shown by the screen that follows, from the rules —
    /// so tuning a rate cannot leave a keeper stating the old one.
    /// </remarks>
    public static class Dialogue
    {
        /// <summary>Flag set once a keeper has explained their building.</summary>
        public static string MetFlag(string place)
        {
            return "met." + place;
        }

        private static DialogueLine Line(string speaker, string english, string filipino)
        {
            return new DialogueLine(speaker, new LocString(english, filipino));
        }

        /// <summary>The aide's welcome. Hearing it to the end completes sub-quest 1.</summary>
        public static readonly IReadOnlyList<DialogueLine> AideWelcome = new List<DialogueLine>
        {
            Line(Characters.Tomas,
                "Welcome home, Engineer Evangelista. I am Tomas, your aide. The Magdalo council wrote to you in Ghent, and you came.",
                "Maligayang pagbabalik, Inhinyero Evangelista. Ako si Tomas, ang iyong katuwang. Sumulat sa iyo sa Ghent ang sangguniang Magdalo, at dumating ka."),
            Line(Characters.Tomas,
                "This is our encampment at Kawit. Every building has a keeper who will explain their work the first time you visit.",
                "Ito ang ating kampo sa Kawit. May tagapangasiwa ang bawat gusali na magpapaliwanag ng kanilang gawain sa una mong pagbisita."),
            Line(Characters.Tomas,
                "The Farm grows Rations and the Mine gives Scrap. Ka Tasyo at the Exchange buys both for Reales, our coin.",
                "Nagbibigay ng Rasyon ang Bukid at ng Bakal ang Minahan. Binibili ni Ka Tasyo sa Palitan ang dalawa kapalit ng Reales, ang ating salapi."),
            Line(Characters.Tomas,
                "Reales recruit and train soldiers. Rations feed them on the march. Scrap arms them.",
                "Ang Reales ay pangangalap at pagsasanay ng sundalo. Ang Rasyon ay pagkain nila sa martsa. Ang Bakal ay pang-armas nila."),
            Line(Characters.Tomas,
                "Your orders are always written at the top left, and the gold arrow shows where to go. Click the ground to walk, or click a building to go straight to it.",
                "Laging nakasulat sa kaliwang itaas ang iyong utos, at itinuturo ng gintong palaso kung saan pupunta. I-click ang lupa para maglakad, o i-click ang gusali para dumiretso roon."),
            Line(Characters.Tomas,
                "When you are ready, the Mission Tent holds the map of Cavite. The Spanish will not wait long.",
                "Kapag handa ka na, nasa Tolda ng Misyon ang mapa ng Kabite. Hindi maghihintay nang matagal ang mga Kastila.")
        };

        /// <summary>What the aide says after the welcome: a pointer to the current objective.</summary>
        public static readonly LocString AideReminder = new LocString(
            "Our next task, Engineer: {0}.",
            "Ang susunod nating gawain, Inhinyero: {0}.");

        private static readonly Dictionary<string, IReadOnlyList<DialogueLine>> greetings =
            new Dictionary<string, IReadOnlyList<DialogueLine>>
            {
                {
                    Places.Farm, new List<DialogueLine>
                    {
                        Line(Characters.Farmer,
                            "Magandang araw, Engineer. This field grows Rations for the army, a little at a time, even while you are away.",
                            "Magandang araw, Inhinyero. Nagbibigay ng Rasyon para sa hukbo ang bukid na ito, paunti-unti, kahit wala ka."),
                        Line(Characters.Farmer,
                            "The storehouse only holds so much. When it is full, the field stops, so come back and harvest often.",
                            "May hangganan ang kamalig. Kapag puno na, titigil ang ani, kaya bumalik at umani nang madalas."),
                        Line(Characters.Farmer,
                            "Every mission costs Rations to march. What you do not need, Ka Tasyo will buy.",
                            "May bayad na Rasyon ang bawat misyon para makapagmartsa. Ang hindi mo kailangan, bibilhin ni Ka Tasyo.")
                    }
                },
                {
                    Places.Mine, new List<DialogueLine>
                    {
                        Line(Characters.Miner,
                            "Scrap, Engineer: old iron, nails, broken tools. Slower to dig than rice is to grow, and worth more for it.",
                            "Bakal, Inhinyero: lumang bakal, pako, sirang kasangkapan. Mas mabagal hukayin kaysa sa palay, kaya mas mahal."),
                        Line(Characters.Miner,
                            "Drills and the Armory's forge both eat Scrap. Harvest it here and sell the spare at the Exchange.",
                            "Kumakain ng Bakal ang pagsasanay at ang pandayan ng Taguan ng Armas. Kunin dito at ipagbili ang sobra sa Palitan.")
                    }
                },
                {
                    Places.Exchange, new List<DialogueLine>
                    {
                        Line(Characters.Trader,
                            "Rations, Scrap, I buy them all. I pay in Reales, and my prices are posted on the board.",
                            "Rasyon, Bakal, binibili ko lahat. Nagbabayad ako ng Reales, at nakapaskil sa pisara ang aking presyo."),
                        Line(Characters.Trader,
                            "I buy in lots, never a single grain. Sell what you do not need and keep enough Rations for the march.",
                            "Bumibili ako nang maramihan, hindi paisa-isang butil. Ipagbili ang hindi kailangan at magtira ng Rasyon para sa martsa.")
                    }
                },
                {
                    // The hall has no keeper of its own; the aide explains the odds before any
                    // coin is spent.
                    Places.Recruitment, new List<DialogueLine>
                    {
                        Line(Characters.Tomas,
                            "Volunteers come here to join us. Each recruit costs Reales, and who answers is a matter of chance.",
                            "Dito dumarating ang mga boluntaryong sasapi sa atin. May bayad na Reales ang bawat pangangalap, at pagkakataon ang magpapasya kung sino ang tutugon."),
                        Line(Characters.Tomas,
                            "The odds are posted on the wall, Engineer. If no Hero has come after nine recruits, the tenth is always one.",
                            "Nakapaskil sa dingding ang tsansa, Inhinyero. Kung walang Bayaning dumating sa siyam na pangangalap, tiyak na Bayani ang ikasampu.")
                    }
                },
                {
                    Places.Training, new List<DialogueLine>
                    {
                        Line(Characters.Sergeant,
                            "Soldiers grow stronger two ways, Engineer: in battle, or on my drill yard.",
                            "Dalawang paraan lumalakas ang sundalo, Inhinyero: sa labanan, o sa aking sanayan."),
                        Line(Characters.Sergeant,
                            "A drill costs Reales and Scrap. Each level raises health, attack and defence.",
                            "May bayad na Reales at Bakal ang bawat pagsasanay. Bawat antas ay nagpapataas ng buhay, atake at depensa.")
                    }
                }
            };

        /// <summary>A keeper's first-visit explanation for <paramref name="place"/>, or null when it has none.</summary>
        public static IReadOnlyList<DialogueLine> Greeting(string place)
        {
            IReadOnlyList<DialogueLine> lines;
            return place != null && greetings.TryGetValue(place, out lines) ? lines : null;
        }

        /// <summary>Every line, for the content tests.</summary>
        public static IEnumerable<DialogueLine> AllLines()
        {
            foreach (DialogueLine line in AideWelcome)
            {
                yield return line;
            }

            foreach (KeyValuePair<string, IReadOnlyList<DialogueLine>> pair in greetings)
            {
                foreach (DialogueLine line in pair.Value)
                {
                    yield return line;
                }
            }
        }
    }
}
