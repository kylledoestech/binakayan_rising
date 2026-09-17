using System;
using System.Globalization;

namespace BinakayanRising.Core.Localization
{
    /// <summary>
    /// The active language, and the one place player-facing text is looked up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plain C# in Core so every assembly can reach it and the EditMode suite can test the table
    /// with the editor closed. Unity's Localization package was considered and rejected: it brings
    /// Addressables and asynchronous string loading, which is a great deal of machinery for two
    /// languages and one screen.
    /// </para>
    /// <para>
    /// <b>Static state and domain reload.</b> The project enters play mode without a domain reload,
    /// so <see cref="LanguageChanged"/> subscribers survive between sessions. Core cannot hook
    /// Unity's startup, so the UI layer calls <see cref="ResetSubscribers"/> on subsystem
    /// registration.
    /// </para>
    /// </remarks>
    public static class Loc
    {
        private static Language current = Language.English;
        private static int version;

        /// <summary>Raised after the language changes, so labels can re-read their text.</summary>
        public static event Action LanguageChanged;

        /// <summary>The language text is currently returned in.</summary>
        public static Language Current
        {
            get { return current; }
        }

        /// <summary>
        /// Increments on every language change. Cheap for a per-frame caller to compare against a
        /// cached value instead of subscribing.
        /// </summary>
        public static int Version
        {
            get { return version; }
        }

        /// <summary>Switches language.</summary>
        /// <param name="language">The language to switch to.</param>
        /// <param name="notify">False to switch silently, for start-up before anything is listening.</param>
        public static void SetLanguage(Language language, bool notify = true)
        {
            if (current == language)
            {
                return;
            }

            current = language;
            version++;

            if (notify && LanguageChanged != null)
            {
                LanguageChanged();
            }
        }

        /// <summary>The text for a key in the current language.</summary>
        public static string Get(TextKey key)
        {
            return StringTable.Get(current, key);
        }

        /// <summary>The text for a key in a specific language.</summary>
        public static string Get(Language language, TextKey key)
        {
            return StringTable.Get(language, key);
        }

        /// <summary>Formats a keyed pattern with one argument.</summary>
        /// <remarks>Allocates. Call when a value changes, never every frame.</remarks>
        public static string Format(TextKey key, object arg0)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), arg0);
        }

        /// <summary>Formats a keyed pattern with two arguments.</summary>
        /// <remarks>Allocates. Call when a value changes, never every frame.</remarks>
        public static string Format(TextKey key, object arg0, object arg1)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), arg0, arg1);
        }

        /// <summary>Formats a keyed pattern with any number of arguments.</summary>
        /// <remarks>Allocates. Call when a value changes, never every frame.</remarks>
        public static string Format(TextKey key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }

        /// <summary>Drops every subscriber. For domain-reload-free play mode entry only.</summary>
        public static void ResetSubscribers()
        {
            LanguageChanged = null;
        }
    }
}
