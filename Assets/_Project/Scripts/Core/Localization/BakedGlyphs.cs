namespace BinakayanRising.Core.Localization
{
    /// <summary>
    /// Characters baked into every font atlas: printable ASCII, the Spanish and Tagalog
    /// diacritics the historical names need, and the typographic punctuation the UI uses.
    /// </summary>
    /// <remarks>
    /// Static atlases only contain what is asked for. Omitting the tilde here would render
    /// "Caviteño" and "Cañacao" with a missing glyph box in the middle of a proper noun — a
    /// failure that is easy to miss until it appears in a screenshot at the defense. Lives in
    /// Core so the font baker and the text tests read the same list.
    /// </remarks>
    public static class BakedGlyphs
    {
        public const string All =
            " !\"#$%&'()*+,-./0123456789:;<=>?@" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
            "abcdefghijklmnopqrstuvwxyz{|}~" +
            "ñÑáéíóúÁÉÍÓÚüÜàÀèÈ¡¿" +
            "—–…‘’“”·•×÷°±₱" +
            "✓▸◂▴▾";

        /// <summary>The first character of <paramref name="text"/> that no atlas has, or '\0'.</summary>
        public static char FirstMissing(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return '\0';
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\n' && All.IndexOf(c) < 0)
                {
                    return c;
                }
            }

            return '\0';
        }
    }
}
