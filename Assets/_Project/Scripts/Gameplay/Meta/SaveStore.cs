using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BinakayanRising.Core.Meta;
using UnityEngine;

namespace BinakayanRising.Gameplay.Meta
{
    /// <summary>
    /// Reads and writes the single campaign save, so that no crash, power cut or hand edit can
    /// leave the player with a file the game cannot open (Table 5: zero corrupted saves).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Writing.</b> The whole save is serialised to a temporary file beside the real one,
    /// flushed to disk, then swapped in with <see cref="File.Replace(string,string,string)"/>, which
    /// keeps the previous save as <c>.bak</c>. A write that dies half-way leaves a broken temporary
    /// file and an untouched save, never a half-written save.
    /// </para>
    /// <para>
    /// <b>Reading.</b> The payload carries a SHA-256 of its own JSON. A file that fails to parse or
    /// fails its checksum is set aside as <c>.corrupt</c> and the backup is tried instead, so the
    /// worst case is losing the last few minutes rather than the campaign.
    /// </para>
    /// </remarks>
    public sealed class SaveStore
    {
        private const string FileName = "campaign.json";

        [Serializable]
        private sealed class Envelope
        {
            public int format = 1;
            public string checksum;
            public string payload;
        }

        public SaveStore()
            : this(Application.persistentDataPath)
        {
        }

        public SaveStore(string directory)
        {
            Directory = directory;
        }

        public string Directory { get; private set; }

        public string MainPath
        {
            get { return Path.Combine(Directory, FileName); }
        }

        public string BackupPath
        {
            get { return MainPath + ".bak"; }
        }

        private string TempPath
        {
            get { return MainPath + ".tmp"; }
        }

        /// <summary>Why the last <see cref="Load"/> fell back or failed, for the log. Empty when clean.</summary>
        public string LastProblem { get; private set; }

        /// <summary>True when the last <see cref="Load"/> had to use the backup file.</summary>
        public bool LastLoadUsedBackup { get; private set; }

        public bool Exists
        {
            get { return File.Exists(MainPath) || File.Exists(BackupPath); }
        }

        /// <summary>Loads the save, falling back to the backup when the main file is damaged.</summary>
        /// <returns>The save, repaired against <paramref name="rules"/>, or null when there is none.</returns>
        public SaveData Load(MetaRules rules)
        {
            LastProblem = string.Empty;
            LastLoadUsedBackup = false;

            SaveData data = TryRead(MainPath);
            if (data == null && File.Exists(BackupPath))
            {
                if (File.Exists(MainPath))
                {
                    Quarantine(MainPath);
                }

                data = TryRead(BackupPath);
                if (data != null)
                {
                    LastLoadUsedBackup = true;
                    LastProblem += " Restored from backup.";
                }
            }

            if (data != null && data.Repair(rules))
            {
                LastProblem += " Save needed repair.";
            }

            if (!string.IsNullOrEmpty(LastProblem))
            {
                Debug.LogWarning("[SaveStore]" + LastProblem);
            }

            return data;
        }

        /// <summary>Writes the save. Never throws: a failed write is logged and reported.</summary>
        public bool Save(SaveData data)
        {
            if (data == null)
            {
                return false;
            }

            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                data.savedUtcTicks = DateTime.UtcNow.Ticks;

                string payload = JsonUtility.ToJson(data);
                var envelope = new Envelope { checksum = Hash(payload), payload = payload };
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(envelope, true));

                using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(MainPath))
                {
                    File.Replace(TempPath, MainPath, BackupPath, true);
                }
                else
                {
                    File.Move(TempPath, MainPath);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[SaveStore] Could not write the save: " + exception.Message);
                return false;
            }
        }

        /// <summary>Deletes the save and its backup. Used by "Delete save" in Settings.</summary>
        public void Delete()
        {
            TryDelete(MainPath);
            TryDelete(BackupPath);
            TryDelete(TempPath);
        }

        private SaveData TryRead(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                Envelope envelope = JsonUtility.FromJson<Envelope>(text);
                if (envelope == null || string.IsNullOrEmpty(envelope.payload))
                {
                    LastProblem += " " + Path.GetFileName(path) + " is empty or unreadable.";
                    return null;
                }

                if (envelope.checksum != Hash(envelope.payload))
                {
                    LastProblem += " " + Path.GetFileName(path) + " failed its checksum.";
                    return null;
                }

                SaveData data = JsonUtility.FromJson<SaveData>(envelope.payload);
                if (data == null)
                {
                    LastProblem += " " + Path.GetFileName(path) + " has no campaign in it.";
                }

                return data;
            }
            catch (Exception exception)
            {
                LastProblem += " " + Path.GetFileName(path) + ": " + exception.Message;
                return null;
            }
        }

        private static void Quarantine(string path)
        {
            try
            {
                string corrupt = path + ".corrupt";
                TryDelete(corrupt);
                File.Move(path, corrupt);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SaveStore] Could not set the damaged save aside: " + exception.Message);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[SaveStore] Could not delete " + path + ": " + exception.Message);
            }
        }

        private static string Hash(string payload)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var builder = new StringBuilder(digest.Length * 2);
                for (int i = 0; i < digest.Length; i++)
                {
                    builder.Append(digest[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
