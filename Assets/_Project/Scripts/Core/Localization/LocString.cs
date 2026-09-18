namespace BinakayanRising.Core.Localization
{
    /// <summary>
    /// English and Filipino text for authored content — dialogue, lessons, quiz questions, codex
    /// entries — that is too long or too numerous to live in <see cref="TextKey"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="TextKey"/> stays the home for interface chrome, where a key is referenced from
    /// code. Content is referenced from data tables instead, so it carries its own pair of
    /// strings. <c>ContentTextTests</c> holds both kinds to the same rules: both languages
    /// authored, the same placeholders, the same markup, no glyph outside the baked fonts.
    /// </remarks>
    public struct LocString
    {
        /// <summary>The English text.</summary>
        public readonly string English;

        /// <summary>The Filipino text. A first draft that needs review by a native speaker.</summary>
        public readonly string Filipino;

        public LocString(string english, string filipino)
        {
            English = english;
            Filipino = filipino;
        }

        /// <summary>True when neither language has any text.</summary>
        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(English) && string.IsNullOrEmpty(Filipino); }
        }

        /// <summary>The text in the current <see cref="Loc"/> language.</summary>
        public string Get()
        {
            return Get(Loc.Current);
        }

        /// <summary>
        /// The text in <paramref name="language"/>, falling back to English when the Filipino
        /// draft is missing so a gap shows as English rather than as nothing.
        /// </summary>
        public string Get(Language language)
        {
            if (language == Language.Filipino && !string.IsNullOrEmpty(Filipino))
            {
                return Filipino;
            }

            return English ?? string.Empty;
        }

        public override string ToString()
        {
            return Get();
        }
    }
}
