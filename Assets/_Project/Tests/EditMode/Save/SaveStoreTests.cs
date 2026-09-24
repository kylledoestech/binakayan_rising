using System;
using System.IO;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay.Meta;
using NUnit.Framework;

namespace BinakayanRising.Tests.Save
{
    /// <summary>
    /// The save file survives the failures it was built for: a truncated or hand-edited main file,
    /// a temporary file left by a write that died, and the plain save-then-load path.
    /// </summary>
    /// <remarks>
    /// These need Unity's <c>JsonUtility</c>, so they run in the editor's Test Runner only, not in
    /// <c>Tools/battle-sim/test.sh</c>. Each test gets its own temporary directory.
    /// </remarks>
    [TestFixture]
    public class SaveStoreTests
    {
        private string directory;
        private MetaRules rules;
        private SaveStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "br-save-" + Guid.NewGuid().ToString("N"));
            rules = MetaRules.Default();
            store = new SaveStore(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private SaveData NewCampaign(int reales)
        {
            SaveData data = MetaGame.NewGame(rules, new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc), 42);
            data.reales = reales;
            return data;
        }

        /// <summary>Two saves, so the main file holds <paramref name="second"/> and the backup <paramref name="first"/>.</summary>
        private void SaveTwice(int first, int second)
        {
            Assert.IsTrue(store.Save(NewCampaign(first)));
            Assert.IsTrue(store.Save(NewCampaign(second)));
            Assert.IsTrue(File.Exists(store.BackupPath), "second save keeps a backup");
        }

        [Test]
        public void SaveThenLoadRoundTrips()
        {
            SaveData saved = NewCampaign(321);
            saved.clearedQuests.Add("q01");

            Assert.IsTrue(store.Save(saved));
            SaveData loaded = store.Load(rules);

            Assert.IsNotNull(loaded);
            Assert.IsFalse(store.LastLoadUsedBackup);
            Assert.AreEqual(321, loaded.reales);
            Assert.AreEqual(saved.units.Count, loaded.units.Count);
            CollectionAssert.AreEqual(saved.clearedQuests, loaded.clearedQuests);
            Assert.IsFalse(File.Exists(store.MainPath + ".tmp"), "no temporary file left behind");
        }

        [Test]
        public void LoadWithNoFilesReturnsNull()
        {
            Assert.IsFalse(store.Exists);
            Assert.IsNull(store.Load(rules));
        }

        [Test]
        public void TruncatedMainFileLoadsFromBackup()
        {
            SaveTwice(111, 222);
            byte[] bytes = File.ReadAllBytes(store.MainPath);
            Array.Resize(ref bytes, bytes.Length / 2);
            File.WriteAllBytes(store.MainPath, bytes);

            SaveData loaded = store.Load(rules);

            Assert.IsNotNull(loaded);
            Assert.IsTrue(store.LastLoadUsedBackup);
            Assert.AreEqual(111, loaded.reales);
            Assert.IsTrue(File.Exists(store.MainPath + ".corrupt"), "damaged file set aside");
            Assert.IsFalse(File.Exists(store.MainPath));
        }

        [Test]
        public void GarbageMainFileLoadsFromBackup()
        {
            SaveTwice(111, 222);
            File.WriteAllText(store.MainPath, "not json at all {{{");

            SaveData loaded = store.Load(rules);

            Assert.IsNotNull(loaded);
            Assert.IsTrue(store.LastLoadUsedBackup);
            Assert.AreEqual(111, loaded.reales);
        }

        [Test]
        public void EditedPayloadFailsChecksumAndIsQuarantined()
        {
            SaveTwice(111, 222);
            string text = File.ReadAllText(store.MainPath);
            StringAssert.Contains("\\\"reales\\\":222", text);
            File.WriteAllText(store.MainPath, text.Replace("\\\"reales\\\":222", "\\\"reales\\\":99999"));

            SaveData loaded = store.Load(rules);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(111, loaded.reales, "the edited value must not be accepted");
            Assert.IsTrue(store.LastLoadUsedBackup);
            StringAssert.Contains("checksum", store.LastProblem);
            Assert.IsTrue(File.Exists(store.MainPath + ".corrupt"));
        }

        [Test]
        public void EditedPayloadWithNoBackupIsRejected()
        {
            Assert.IsTrue(store.Save(NewCampaign(222)));
            string text = File.ReadAllText(store.MainPath);
            File.WriteAllText(store.MainPath, text.Replace("\\\"reales\\\":222", "\\\"reales\\\":99999"));

            Assert.IsNull(store.Load(rules));
            StringAssert.Contains("checksum", store.LastProblem);
        }

        [Test]
        public void LeftoverTempFileIsIgnoredOnLoadAndReplacedOnSave()
        {
            Assert.IsTrue(store.Save(NewCampaign(111)));
            string temp = store.MainPath + ".tmp";
            File.WriteAllText(temp, "{ \"format\": 1, \"checksum\": \"half-writ");

            SaveData loaded = store.Load(rules);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(111, loaded.reales);
            Assert.IsFalse(store.LastLoadUsedBackup);

            Assert.IsTrue(store.Save(NewCampaign(222)));
            Assert.IsFalse(File.Exists(temp), "the next save consumes the stale temporary file");
            Assert.AreEqual(222, store.Load(rules).reales);
        }
    }
}
