using BinakayanRising.Core.Localization;
using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// The handful of player choices that outlive a play session: language, and whether the guided
    /// tutorial has been seen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Backed by <see cref="PlayerPrefs"/>, which is plenty for two values and needs no save-file
    /// plumbing. Keys carry a version suffix where the meaning could change: bumping
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
