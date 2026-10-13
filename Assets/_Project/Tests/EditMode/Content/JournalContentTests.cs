using System.Collections.Generic;
using BinakayanRising.Core.Content;
using NUnit.Framework;

namespace BinakayanRising.Tests.Content
{
    /// <summary>
    /// The panel's feedback, held as content: the Journal's people and ranks, the place and date
    /// of every battle, and the animated first act.
    /// </summary>
    [TestFixture]
    public class JournalContentTests
    {
        [Test]
        public void EveryFigureIsMetInAQuestThatExists()
        {
            var ids = new HashSet<string>();
            foreach (Figure figure in Figures.All)
            {
                Assert.IsTrue(ids.Add(figure.Id), "Duplicate figure " + figure.Id);
                Assert.IsNotNull(Campaign.Find(figure.MetIn), figure.Id + " is met in a missing quest " + figure.MetIn);
            }
        }

        [Test]
        public void EveryHistoricalFigureHasTheirYearsAndTheNarratorIsMarkedAsStory()
        {
            foreach (Figure figure in Figures.All)
            {
                if (figure.Historical)
                {
                    StringAssert.IsMatch(@"^1[89]\d\d–1[89]\d\d$", figure.Life, figure.Id + " years");
                }
            }

            Figure narrator = Figures.Find(Characters.Senador);
            Assert.IsNotNull(narrator, "The narrator is not in the Journal.");
            Assert.IsFalse(narrator.Historical, "The narrator would read as a figure from history.");
        }

        [Test]
        public void FiguresAreListedInTheOrderTheyAreMet()
        {
            int last = -1;
            foreach (Figure figure in Figures.All)
            {
                int at = IndexOf(figure.MetIn);
                Assert.GreaterOrEqual(at, last, figure.Id + " is listed before someone met earlier.");
                last = at;
            }
        }

        [Test]
        public void EveryQuestSaysWhenItHappensIn1896()
        {
            foreach (Quest quest in Campaign.Quests)
            {
                StringAssert.Contains("1896", quest.When.English, quest.Id);
                StringAssert.Contains("1896", quest.When.Filipino, quest.Id);
            }

            StringAssert.Contains("November 9", Campaign.Find("q08").When.English);
            StringAssert.Contains("November 10", Campaign.Find("q09").When.English);
            StringAssert.Contains("November 11", Campaign.Find("q10").When.English);
        }

        [Test]
        public void EveryRankHasItsOwnDuty()
        {
            var duties = new HashSet<string>();
            for (int i = 0; i < PlayerRanks.Count; i++)
            {
                Assert.IsTrue(duties.Add(PlayerRanks.At(i).Duty.English), PlayerRanks.At(i).Title + " repeats another rank's duty.");
            }
        }

        [Test]
        public void OnlyActOneIsAnimatedAndItsCastAreKnownPeople()
        {
            foreach (Cutscene scene in Cutscenes.All)
            {
                Assert.AreEqual(scene.Id == Cutscenes.Act1, scene.Animated, scene.Id);
                foreach (CutsceneSlide slide in scene.Slides)
                {
                    Assert.LessOrEqual(slide.Cast.Length, 2, slide.Image + " has more people than the stage has marks.");
                    foreach (string id in slide.Cast)
                    {
                        Assert.IsTrue(UnitCatalog.Find(id) != null || Characters.Find(id) != null, slide.Image + " casts unknown " + id);
                    }
                }
            }

            Cutscene act1 = Cutscenes.Find(Cutscenes.Act1);
            CollectionAssert.Contains(act1.Slides[0].Cast, UnitCatalog.Evangelista, "Act 1 opens on its hero.");
        }

        private static int IndexOf(string questId)
        {
            IReadOnlyList<Quest> quests = Campaign.Quests;
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].Id == questId)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
