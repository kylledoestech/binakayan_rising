using System;
using BinakayanRising.Core.Meta;
using UnityEngine;

namespace BinakayanRising.Gameplay.Meta
{
    /// <summary>
    /// The campaign that is open right now, and when it gets written to disk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Owns one <see cref="MetaGame"/> at a time, or none while the player sits on the main menu.
    /// Every change the rules make raises <see cref="MetaGame.Changed"/>; the session answers by
    /// marking itself dirty and writing a few seconds later, so a burst of harvest clicks is one
    /// write rather than twenty. Leaving the encampment, pausing the app and quitting all write at
    /// once (<see cref="SaveNow"/>).
    /// </para>
    /// <para>
    /// Plain C# rather than a component, so the shell decides its lifetime and a test can drive it
    /// with a temporary directory.
    /// </para>
    /// </remarks>
    public sealed class GameSession
    {
        /// <summary>Seconds after the last change before the save is written.</summary>
        public const float SaveDelaySeconds = 2f;

        private readonly Func<DateTime> clock;
        private bool dirty;
        private float sinceChange;

        public GameSession(SaveStore store, MetaRules rules, Func<DateTime> clock)
        {
            Store = store;
            Rules = rules;
            this.clock = clock;
        }

        public SaveStore Store { get; private set; }

        public MetaRules Rules { get; private set; }

        /// <summary>The open campaign, or null on the main menu.</summary>
        public MetaGame Game { get; private set; }

        /// <summary>True when the last <see cref="Continue"/> had to fall back to the backup file.</summary>
        public bool RestoredFromBackup { get; private set; }

        /// <summary>Raised when a campaign is opened, started or closed.</summary>
        public event Action GameOpened;

        /// <summary>Raised after every successful write.</summary>
        public event Action Saved;

        public bool HasSave
        {
            get { return Store.Exists; }
        }

        /// <summary>
        /// Reads the save without opening it, for the main menu's summary line.
        /// </summary>
        /// <returns>A throwaway game over the save, or null when there is none.</returns>
        public MetaGame Peek()
        {
            SaveData data = Store.Load(Rules);
            return data == null ? null : new MetaGame(data, Rules, clock);
        }

        /// <summary>Opens the saved campaign.</summary>
        /// <returns>False when there is no readable save.</returns>
        public bool Continue()
        {
            SaveData data = Store.Load(Rules);
            if (data == null)
            {
                return false;
            }

            RestoredFromBackup = Store.LastLoadUsedBackup;
            Open(new MetaGame(data, Rules, clock));
            return true;
        }

        /// <summary>Starts a fresh campaign, replacing any save, and writes it at once.</summary>
        public void NewCampaign()
        {
            RestoredFromBackup = false;
            DateTime now = clock();
            int seed = unchecked((int)(now.Ticks ^ (now.Ticks >> 32)));
            Open(new MetaGame(MetaGame.NewGame(Rules, now, seed), Rules, clock));
            SaveNow();
        }

        /// <summary>Writes and closes the open campaign.</summary>
        public void Close()
        {
            if (Game == null)
            {
                return;
            }

            SaveNow();
            Game.Changed -= OnChanged;
            Game = null;
            RaiseOpened();
        }

        /// <summary>Deletes the save. Closes the open campaign first, without writing it.</summary>
        public void DeleteSave()
        {
            if (Game != null)
            {
                Game.Changed -= OnChanged;
                Game = null;
                dirty = false;
            }

            Store.Delete();
            RaiseOpened();
        }

        /// <summary>
        /// Advances the play clock and writes the save once changes have settled.
        /// Call every frame with unscaled time.
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (Game == null)
            {
                return;
            }

            Game.AddPlayTime(unscaledDeltaTime);

            if (dirty)
            {
                sinceChange += unscaledDeltaTime;
                if (sinceChange >= SaveDelaySeconds)
                {
                    SaveNow();
                }
            }
        }

        /// <summary>Writes the open campaign now, if there is one.</summary>
        public bool SaveNow()
        {
            if (Game == null)
            {
                return false;
            }

            dirty = false;
            sinceChange = 0f;
            bool ok = Store.Save(Game.Data);
            if (ok && Saved != null)
            {
                Saved();
            }

            return ok;
        }

        private void Open(MetaGame game)
        {
            if (Game != null)
            {
                Game.Changed -= OnChanged;
            }

            Game = game;
            Game.Changed += OnChanged;
            dirty = false;
            RaiseOpened();
        }

        private void OnChanged()
        {
            dirty = true;
            sinceChange = 0f;
        }

        private void RaiseOpened()
        {
            Action handler = GameOpened;
            if (handler != null)
            {
                handler();
            }
        }

        /// <summary>Formats seconds of play as "3h 07m" or "12m".</summary>
        public static string FormatPlayTime(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
            int hours = (int)span.TotalHours;
            return hours > 0
                ? string.Format("{0}h {1:00}m", hours, span.Minutes)
                : string.Format("{0}m", Mathf.Max(1, span.Minutes));
        }
    }
}
