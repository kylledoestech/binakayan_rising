using System.Collections.Generic;
using System.Text;
using BinakayanRising.Core.Localization;
using TMPro;
using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// Puts the saved language in place before any screen builds, and keeps the language state
    /// honest across play sessions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Loc"/> lives in Core, which cannot see <see cref="PlayerPrefs"/> or Unity's load
    /// hooks, so this is where the two meet.
    /// </para>
    /// <para>
    /// <b>Domain reload is off in this project.</b> Without the subsystem-registration reset, the
    /// second press of Play would inherit the first session's <see cref="Loc.LanguageChanged"/>
    /// subscribers — delegates into destroyed labels.
    /// </para>
    /// </remarks>
    public static class LocalizationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Loc.ResetSubscribers();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadSavedLanguage()
        {
            Loc.SetLanguage(UserPrefs.Language, notify: false);

#if UNITY_EDITOR
            ReportMissingGlyphs();
#endif
        }

#if UNITY_EDITOR
        private static readonly System.Text.RegularExpressions.Regex RichTextTag =
            new System.Text.RegularExpressions.Regex("<[^>]+>");

        /// <summary>
        /// Warns about any character in either language that the static font atlases cannot draw.
        /// </summary>
        /// <remarks>
        /// The fonts are baked with a fixed character set, so a stray curly quote or a minus sign
        /// renders as an empty box with no error anywhere. Checking every string against the real
        /// font assets at the start of each play session turns that into a console warning naming
        /// the key and the character. Editor only: shipped strings cannot change at runtime.
        /// </remarks>
        public static int ReportMissingGlyphs()
        {
            var fonts = new List<TMP_FontAsset>();
            AddFont(fonts, Theme.DisplayFont);
            AddFont(fonts, Theme.BodyFont);
            AddFont(fonts, Theme.BodyFontBold);
            if (fonts.Count == 0)
            {
                return 0;
            }

            var problems = new StringBuilder();
            int count = 0;

            for (TextKey key = TextKey.None + 1; key < TextKey.Count; key++)
            {
                for (int language = 0; language <= (int)Language.Filipino; language++)
                {
                    string text = RichTextTag.Replace(StringTable.GetAuthored((Language)language, key) ?? string.Empty, string.Empty);
                    text = text.Replace("\n", string.Empty);

                    for (int f = 0; f < fonts.Count; f++)
                    {
                        uint[] missing;
                        if (fonts[f].HasCharacters(text, out missing, searchFallbacks: true, tryAddCharacter: false))
                        {
                            continue;
                        }

                        for (int m = 0; m < missing.Length; m++)
                        {
                            problems.AppendFormat(
                                "\n  {0} ({1}) U+{2:X4} '{3}' missing from {4}",
                                key, (Language)language, missing[m], char.ConvertFromUtf32((int)missing[m]), fonts[f].name);
                            count++;
                        }
                    }
                }
            }

            if (count > 0)
            {
                Debug.LogWarning("Localization: " + count + " glyphs cannot be drawn by the baked fonts." + problems);
            }

            return count;
        }

        private static void AddFont(List<TMP_FontAsset> fonts, TMP_FontAsset font)
        {
            if (font != null && !fonts.Contains(font))
            {
                fonts.Add(font);
            }
        }
#endif
    }
}
