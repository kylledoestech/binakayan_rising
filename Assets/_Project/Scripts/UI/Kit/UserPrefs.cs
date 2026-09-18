using BinakayanRising.Core.Localization;
using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// The player choices that outlive a play session and are not part of a campaign: language,
    /// text speed, audio levels, and whether the guided tutorial has been seen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Backed by <see cref="PlayerPrefs"/>, which is plenty for two values and needs no save-file
    /// plumbing, and deleting a campaign save leaves them alone. Keys carry a version suffix where the meaning could change: bumping
    /// <c>TutorialDone.v1</c> to <c>v2</c> after rewriting the tutorial shows it once more to
    /// everyone, without touching anyone's language.
    /// </para>
    /// <para>
    /// In the editor these persist between play sessions like any build would. Use
    /// <c>Tools &gt; Binakayan Rising &gt; Reset First-Run Tutorial</c> to see the first run again.
    /// </para>
    /// </remarks>
    public static class UserPrefs
    {
        private const string LanguageKey = "BinakayanRising.Language";
        private const string TutorialDoneKey = "BinakayanRising.TutorialDone.v1";
        private const string MusicVolumeKey = "BinakayanRising.MusicVolume";
        private const string SfxVolumeKey = "BinakayanRising.SfxVolume";
        private const string MutedKey = "BinakayanRising.Muted";
        private const string TextSpeedKey = "BinakayanRising.TextSpeed";

        /// <summary>How fast dialogue types itself out.</summary>
        public enum TextSpeed
        {
            Slow = 0,
            Normal = 1,
            Fast = 2,
            Instant = 3,
        }

        /// <summary>Raised after an audio setting changes, so music players can follow it.</summary>
        public static event System.Action AudioChanged;

        /// <summary>The saved language, English when none has been chosen.</summary>
        public static Language Language
        {
            get
            {
                int stored = PlayerPrefs.GetInt(LanguageKey, (int)Language.English);
                return stored == (int)Language.Filipino ? Language.Filipino : Language.English;
            }

            set
            {
                PlayerPrefs.SetInt(LanguageKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>True once the guided tutorial has been finished or skipped.</summary>
        public static bool TutorialDone
        {
            get { return PlayerPrefs.GetInt(TutorialDoneKey, 0) == 1; }

            set
            {
                PlayerPrefs.SetInt(TutorialDoneKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Music volume, 0..1.</summary>
        public static float MusicVolume
        {
            get { return Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f)); }
            set { SetAudio(MusicVolumeKey, Mathf.Clamp01(value)); }
        }

        /// <summary>Interface and battle sound volume, 0..1.</summary>
        public static float SfxVolume
        {
            get { return Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 0.7f)); }
            set { SetAudio(SfxVolumeKey, Mathf.Clamp01(value)); }
        }

        /// <summary>Silences music and sound together, without losing either level.</summary>
        public static bool Muted
        {
            get { return PlayerPrefs.GetInt(MutedKey, 0) == 1; }
            set { SetAudio(MutedKey, value ? 1f : 0f); }
        }

        /// <summary>Music volume after mute is applied. What a music player should use.</summary>
        public static float EffectiveMusicVolume
        {
            get { return Muted ? 0f : MusicVolume; }
        }

        /// <summary>Dialogue typing speed.</summary>
        public static TextSpeed DialogueSpeed
        {
            get
            {
                int stored = PlayerPrefs.GetInt(TextSpeedKey, (int)TextSpeed.Normal);
                return stored < 0 || stored > (int)TextSpeed.Instant ? TextSpeed.Normal : (TextSpeed)stored;
            }

            set
            {
                PlayerPrefs.SetInt(TextSpeedKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Characters typed per second at a speed, or 0 for all at once.</summary>
        public static float CharactersPerSecond(TextSpeed speed)
        {
            switch (speed)
            {
                case TextSpeed.Slow: return 25f;
                case TextSpeed.Fast: return 90f;
                case TextSpeed.Instant: return 0f;
                default: return 50f;
            }
        }

        private static void SetAudio(string key, float value)
        {
            if (key == MutedKey)
            {
                PlayerPrefs.SetInt(key, value > 0.5f ? 1 : 0);
            }
            else
            {
                PlayerPrefs.SetFloat(key, value);
            }

            PlayerPrefs.Save();
            ApplyAudio();
        }

        /// <summary>Pushes the saved levels into the sound players.</summary>
        public static void ApplyAudio()
        {
            UiSfx.Volume = SfxVolume;
            UiSfx.Muted = Muted;

            System.Action handler = AudioChanged;
            if (handler != null)
            {
                handler();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AudioChanged = null;
        }

        /// <summary>Forgets that the tutorial was seen, so the next battle opens with it.</summary>
        public static void ResetTutorial()
        {
            PlayerPrefs.DeleteKey(TutorialDoneKey);
            PlayerPrefs.Save();
        }

        /// <summary>Switches language, saves the choice and re-renders every localized label.</summary>
        public static void ChooseLanguage(Language language)
        {
            Language = language;
            Loc.SetLanguage(language);
        }
    }
}
