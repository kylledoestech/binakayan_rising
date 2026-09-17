using BinakayanRising.Core.Localization;
using TMPro;
using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// Keeps one label showing a string key in the current language.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For fixed text only — headings, button captions, hints. Text that mixes in live values, such
    /// as the phase readout or the field report, is formatted by the screen that owns it, which
    /// watches <see cref="Loc.Version"/> instead.
    /// </para>
    /// <para>
    /// Subscribes while enabled rather than for its whole lifetime, so a hidden panel does no work
    /// on a language switch and catches up the moment it is shown again.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class LocalizedText : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private TextKey key;
        private int renderedVersion = -1;

        /// <summary>The key being shown.</summary>
        public TextKey Key => key;

        /// <summary>Binds a label and a key, and renders immediately.</summary>
        public void Bind(TextMeshProUGUI target, TextKey textKey)
        {
            label = target;
            key = textKey;
            renderedVersion = -1;
            Refresh();
        }

        /// <summary>Changes the key, e.g. a toggle that reads "Show" or "Hide".</summary>
        public void SetKey(TextKey textKey)
        {
            if (key == textKey)
            {
                return;
            }

            key = textKey;
            renderedVersion = -1;
            Refresh();
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            if (label == null || key == TextKey.None || renderedVersion == Loc.Version)
            {
                return;
            }

            renderedVersion = Loc.Version;
            label.text = Loc.Get(key);
        }
    }
}
