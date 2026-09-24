using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// Plays short interface sounds through one pooled, persistent <see cref="AudioSource"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Widgets call <see cref="Play"/> directly rather than holding clip references, so a button
    /// created anywhere in the codebase makes the right noise without being wired to anything.
    /// </para>
    /// <para>
    /// Silence is a valid state. If <see cref="ThemeAssets"/> has no clips — a fresh clone that has
    /// never run setup — every call is a no-op rather than an error.
    /// </para>
    /// </remarks>
    public static class UiSfx
    {
        /// <summary>The interface events that have a sound.</summary>
        public enum Cue
        {
            /// <summary>Pointer entering an interactive element.</summary>
            Hover,

            /// <summary>Any button press.</summary>
            Click,

            /// <summary>An action that commits something — locking a formation, confirming a purchase.</summary>
            Confirm,

            /// <summary>A rejected action.</summary>
            Error,

            /// <summary>A panel or screen appearing.</summary>
            Open,

            /// <summary>A panel or screen dismissing.</summary>
            Close,

            /// <summary>A unit being placed on the board.</summary>
            Place,

            /// <summary>Victory.</summary>
            Victory,

            /// <summary>A quiz appearing mid-battle.</summary>
            Quiz,

            /// <summary>A toggle or tab changing state.</summary>
            Toggle,

            /// <summary>Reales coming into the purse.</summary>
            Coin,
        }

        private static AudioSource source;

        /// <summary>Master volume for interface sound, 0..1.</summary>
        public static float Volume { get; set; } = 0.7f;

        /// <summary>Silences interface sound without unwiring anything.</summary>
        public static bool Muted { get; set; }

        /// <summary>Plays the clip bound to a cue, if there is one.</summary>
        public static void Play(Cue cue)
        {
            if (Muted || Volume <= 0f)
            {
                return;
            }

            AudioClip clip = ClipFor(cue);
            if (clip == null)
            {
                return;
            }

            EnsureSource();
            if (source != null)
            {
                // PlayOneShot rather than Play, so overlapping cues layer instead of cutting
                // each other off — a rapid click sequence should sound like a rapid click
                // sequence, not like one clipped blip.
                source.PlayOneShot(clip, Volume);
            }
        }

        private static AudioClip ClipFor(Cue cue)
        {
            ThemeAssets assets = Theme.Assets;
            if (assets == null)
            {
                return null;
            }

            switch (cue)
            {
                case Cue.Hover: return assets.sfxHover;
                case Cue.Click: return assets.sfxClick;
                case Cue.Confirm: return assets.sfxConfirm;
                case Cue.Error: return assets.sfxError;
                case Cue.Open: return assets.sfxOpen;
                case Cue.Close: return assets.sfxClose;
                case Cue.Place: return assets.sfxPlace;
                case Cue.Victory: return assets.sfxVictory;
                case Cue.Quiz: return assets.sfxQuiz;
                case Cue.Toggle: return assets.sfxToggle;
                case Cue.Coin: return assets.sfxCoin != null ? assets.sfxCoin : assets.sfxConfirm;
                default: return null;
            }
        }

        private static void EnsureSource()
        {
            if (source != null)
            {
                return;
            }

            // Not parented to any screen: interface sound has to outlive the screen that
            // triggered it, or a close sound is cut off by the close it is describing.
            var go = new GameObject("UI Audio")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            Object.DontDestroyOnLoad(go);

            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.bypassEffects = true;
            source.bypassReverbZones = true;
        }

        /// <summary>
        /// Drops the cached source so the next play session builds a fresh one.
        /// </summary>
        /// <remarks>
        /// Domain reload is disabled in this project, so without this the second play session
        /// holds an <see cref="AudioSource"/> whose GameObject was destroyed when the first ended,
        /// and every subsequent cue silently fails.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            source = null;
            Muted = false;
            Volume = 0.7f;
        }
    }
}
