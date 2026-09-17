using TMPro;
using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// The one asset that binds imported art, fonts and audio to the names the UI kit uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists.</b> The project builds its UI in C# rather than from prefabs, so there
    /// is no serialized object anywhere to hang sprite references off. The alternatives were
    /// scattering <c>Resources.Load</c> string literals through every widget, or pulling in
    /// Addressables for what is a few hundred kilobytes of sprites. This asset is the compromise:
    /// exactly one string literal exists in the codebase — the <see cref="ResourcePath"/> below —
    /// and everything downstream of it is a typed field the compiler checks.
    /// </para>
    /// <para>
    /// It is <b>generated</b> by <c>BinakayanRising.EditorTools.ThemeSetup</c> rather than authored
    /// by hand, so adding art to <c>Assets/_Project/Art/</c> and re-running setup keeps it correct.
    /// It is still a normal asset, so any single reference can be overridden in the Inspector
    /// afterwards without touching code.
    /// </para>
    /// <para>
    /// Every field is allowed to be null. <c>Theme</c> falls back to
    /// <see cref="BinakayanRising.Gameplay.PlaceholderArt"/>'s procedural shapes when a sprite is
    /// missing, which keeps the project runnable on a fresh clone before setup has been run.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "ThemeAssets",
        menuName = "Binakayan Rising/Theme Assets",
        order = 0)]
    public sealed class ThemeAssets : ScriptableObject
    {
        /// <summary>
        /// Path passed to <see cref="Resources.Load"/>. The asset must live at
        /// <c>Assets/_Project/Resources/ThemeAssets.asset</c> for this to resolve.
        /// </summary>
        public const string ResourcePath = "ThemeAssets";

        [Header("Frames — 9-sliced, tinted at use site")]
        [Tooltip("Filled ornate panel. The default surface for any dialog or card.")]
        public Sprite panel;

        [Tooltip("Heavier double-weight panel, for the outermost frame of a full screen.")]
        public Sprite panelHeavy;

        [Tooltip("Frame with a transparent centre, for laying over artwork or the board.")]
        public Sprite frameHollow;

        [Tooltip("Plain recessed well, for list backgrounds and stat readouts.")]
        public Sprite inset;

        [Tooltip("Horizontal rule with ornamental ends. Slices on X only.")]
        public Sprite divider;

        [Header("Controls")]
        public Sprite button;
        public Sprite buttonPressed;
        public Sprite buttonDisabled;

        [Tooltip("Small square frame for icon-only buttons and roster slots.")]
        public Sprite slot;

        [Tooltip("Outer casing of a progress or health bar.")]
        public Sprite barTrack;

        [Tooltip("Fill drawn inside the bar casing. Tinted per state.")]
        public Sprite barFill;

        public Sprite checkboxOn;
        public Sprite checkboxOff;

        [Header("Icons — white, tinted at use site")]
        public Sprite iconReales;
        public Sprite iconRations;
        public Sprite iconScrap;
        public Sprite iconAttack;
        public Sprite iconDefense;
        public Sprite iconMovement;
        public Sprite iconRange;
        public Sprite iconBack;
        public Sprite iconClose;
        public Sprite iconSettings;
        public Sprite iconInfo;
        public Sprite iconCheck;
        public Sprite iconCross;
        public Sprite iconStar;

        [Header("Type")]
        [Tooltip("Cinzel. Inscriptional Roman capitals — titles, buttons, unit names.")]
        public TMP_FontAsset displayFont;

        [Tooltip("Spectral. Body copy, numbers, log lines.")]
        public TMP_FontAsset bodyFont;

        [Tooltip("Spectral Bold, for emphasis inside body copy.")]
        public TMP_FontAsset bodyFontBold;

        [Header("Audio")]
        public AudioClip sfxClick;
        public AudioClip sfxHover;
        public AudioClip sfxConfirm;
        public AudioClip sfxError;
        public AudioClip sfxOpen;
        public AudioClip sfxClose;
        public AudioClip sfxPlace;
        public AudioClip sfxVictory;
        public AudioClip sfxQuiz;
        public AudioClip sfxToggle;

        [Header("Units — pixel art rendered by Tools/sprites, one entry per archetype")]
        [Tooltip("Filled from Assets/_Project/Art/Units/<ArchetypeId>/ by Tools → Binakayan Rising → Refresh Unit Art.")]
        public UnitArt[] units = new UnitArt[0];

        /// <summary>The board figure and HUD portrait for one unit archetype.</summary>
        [System.Serializable]
        public struct UnitArt
        {
            /// <summary>Matches <c>RosterEntry.ArchetypeId</c> and the art folder name.</summary>
            public string archetypeId;

            /// <summary>48x64 standing figure, pivoted on its feet.</summary>
            public Sprite body;

            /// <summary>24x24 head-and-shoulders portrait.</summary>
            public Sprite portrait;
        }

        /// <summary>The board figure for an archetype, or null when none was rendered.</summary>
        public Sprite UnitBody(string archetypeId)
        {
            int index = IndexOfUnit(archetypeId);
            return index < 0 ? null : units[index].body;
        }

        /// <summary>The HUD portrait for an archetype, or null when none was rendered.</summary>
        public Sprite UnitPortrait(string archetypeId)
        {
            int index = IndexOfUnit(archetypeId);
            return index < 0 ? null : units[index].portrait;
        }

        private int IndexOfUnit(string archetypeId)
        {
            if (units == null || string.IsNullOrEmpty(archetypeId))
            {
                return -1;
            }

            // Six entries, looked up once when a view is built; a dictionary would be more
            // code than the scan it replaces and would need rebuilding after every domain reload.
            for (int i = 0; i < units.Length; i++)
            {
                if (string.Equals(units[i].archetypeId, archetypeId, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// True when the minimum needed to render legible themed UI is present.
        /// </summary>
        /// <remarks>
        /// Sprites degrade gracefully to procedural shapes, but text does not: a null
        /// <see cref="TMP_FontAsset"/> renders nothing at all rather than falling back, so the
        /// fonts are what this check actually guards.
        /// </remarks>
        public bool IsUsable => displayFont != null && bodyFont != null;
    }
}
