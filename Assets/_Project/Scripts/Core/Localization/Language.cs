namespace BinakayanRising.Core.Localization
{
    /// <summary>The languages every player-facing string is authored in.</summary>
    /// <remarks>
    /// Values are explicit because the choice is persisted in player preferences as an integer.
    /// Reordering members would silently switch a saved player's language.
    /// </remarks>
    public enum Language
    {
        /// <summary>English, the default.</summary>
        English = 0,

        /// <summary>Filipino.</summary>
        Filipino = 1
    }
}
