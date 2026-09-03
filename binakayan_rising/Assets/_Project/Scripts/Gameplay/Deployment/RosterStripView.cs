using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BinakayanRising.Data;

namespace BinakayanRising.Gameplay.Deployment
{
    /// <summary>
    /// Availability of one hero portrait in the deployment roster strip.
    /// </summary>
    public enum RosterEntryState
    {
        /// <summary>Owned, affordable and not yet on the board: the player may drag it out.</summary>
        Available = 0,

        /// <summary>Already deployed onto a grid tile. Still pickable, to move it.</summary>
        Placed = 1,

        /// <summary>
        /// Owned but not currently payable — the squad cap is full, or a currency cost is not met.
        /// The document says the Rations text flashes red when a stage cannot be afforded; this is
        /// the per-portrait equivalent.
        /// </summary>
        Unaffordable = 2,

        /// <summary>Not owned, locked by campaign progress, or otherwise not usable this mission.</summary>
        Unavailable = 3
    }

    /// <summary>
    /// The strip of hero portraits along the bottom of the deployment screen, which the player drags
    /// units out of and onto the isometric board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Straight from the capstone document: "the player drags hero portraits from a roster strip
    /// along the bottom of the screen onto highlighted blue valid grid tiles". This view owns only
    /// the strip; the drag itself is <see cref="UnitDragHandler"/> and the placement rules are
    /// <see cref="DeploymentController"/>.
    /// </para>
    /// <para>
    /// Entries are built in code as uGUI objects under <see cref="entryContainer"/>, so the team
    /// does not have to author and maintain a portrait prefab. Everything visual — size, spacing,
    /// tints, background sprite, label font — is a serialized field. Attach a
    /// <c>HorizontalLayoutGroup</c> to the container and clear <see cref="useManualLayout"/> if you
    /// would rather Unity do the arranging.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class RosterStripView : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("RectTransform the portrait entries are created under. Anchor it to the bottom of the canvas.")]
        [SerializeField] private RectTransform entryContainer;

        [Tooltip("Optional background sprite behind each portrait. Leave empty for a plain tinted quad.")]
        [SerializeField] private Sprite entryBackgroundSprite;

        [Tooltip("Optional font for the name label under each portrait. Leave empty to omit labels.")]
        [SerializeField] private Font labelFont;

        [Header("Layout")]
        [Tooltip("Arrange entries manually left to right. Clear this if the container has a LayoutGroup.")]
        [SerializeField] private bool useManualLayout = true;

        [Tooltip("Pixel size of one portrait entry. TODO(design): not specified in capstone document.")]
        [SerializeField] private Vector2 entrySize = new Vector2(96f, 112f);

        [Tooltip("Pixel gap between entries. TODO(design): not specified in capstone document.")]
        [SerializeField] private float entrySpacing = 8f;

        [Tooltip("Font size of the name label, when a font is assigned.")]
        [Min(1)]
        [SerializeField] private int labelFontSize = 12;

        [Header("State Tints")]
        [Tooltip("Tint of a portrait the player may deploy.")]
        [SerializeField] private Color availableTint = Color.white;

        [Tooltip("Tint of a portrait already standing on the board.")]
        [SerializeField] private Color placedTint = new Color(0.24f, 0.55f, 1f, 1f);

        [Tooltip("Tint of a portrait the player cannot currently pay for or fit in the squad.")]
        [SerializeField] private Color unaffordableTint = new Color(0.85f, 0.35f, 0.35f, 1f);

        [Tooltip("Tint of a portrait the player does not own or has not unlocked.")]
        [SerializeField] private Color unavailableTint = new Color(0.35f, 0.35f, 0.35f, 1f);

        [Tooltip("Alpha applied on top of the tint for entries that cannot be picked.")]
        [Range(0f, 1f)]
        [SerializeField] private float disabledAlpha = 0.45f;

        private readonly List<UnitData> roster = new List<UnitData>();
        private readonly Dictionary<UnitData, Entry> entries = new Dictionary<UnitData, Entry>();

        /// <summary>
        /// Raised when the player left-clicks a portrait that is currently pickable. The document's
        /// control scheme makes left-click the select/pick action.
        /// </summary>
        public event Action<UnitData> EntryPicked;

        /// <summary>The units currently shown, in display order.</summary>
        public IReadOnlyList<UnitData> Roster
        {
            get { return roster; }
        }

        /// <summary>
        /// Rebuilds the strip from a roster. Nulls and duplicates are skipped so a half-authored
        /// roster asset cannot produce a broken strip.
        /// </summary>
        /// <param name="units">Units the player may deploy on this mission, in display order.</param>
        public void SetRoster(IReadOnlyList<UnitData> units)
        {
            Clear();

            if (units == null)
            {
                return;
            }

            for (int i = 0; i < units.Count; i++)
            {
                UnitData unit = units[i];

                if (unit == null || entries.ContainsKey(unit))
                {
                    continue;
                }

                roster.Add(unit);
                entries.Add(unit, CreateEntry(unit, roster.Count - 1));
            }

            SetAllStates(RosterEntryState.Available);
        }

        /// <summary>Destroys every entry and empties the roster.</summary>
        public void Clear()
        {
            foreach (KeyValuePair<UnitData, Entry> pair in entries)
            {
                if (pair.Value.Root != null)
                {
                    Destroy(pair.Value.Root);
                }
            }

            entries.Clear();
            roster.Clear();
        }

        /// <summary>Sets the availability state of one portrait.</summary>
        /// <param name="unit">Unit whose portrait should change.</param>
        /// <param name="state">New availability state.</param>
        public void SetState(UnitData unit, RosterEntryState state)
        {
            Entry entry;

            if (unit == null || !entries.TryGetValue(unit, out entry))
            {
                return;
            }

            entry.State = state;
            entries[unit] = entry;
            ApplyState(entry);
        }

        /// <summary>Sets every portrait to the same state.</summary>
        /// <param name="state">New availability state.</param>
        public void SetAllStates(RosterEntryState state)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                SetState(roster[i], state);
            }
        }

        /// <summary>
        /// Returns the state of a portrait, or <see cref="RosterEntryState.Unavailable"/> when the
        /// unit is not in the strip at all.
        /// </summary>
        /// <param name="unit">Unit to query.</param>
        public RosterEntryState GetState(UnitData unit)
        {
            Entry entry;

            if (unit != null && entries.TryGetValue(unit, out entry))
            {
                return entry.State;
            }

            return RosterEntryState.Unavailable;
        }

        /// <summary>True when the player is allowed to start a drag from this portrait.</summary>
        /// <param name="unit">Unit to query.</param>
        public bool IsPickable(UnitData unit)
        {
            RosterEntryState state = GetState(unit);
            return state == RosterEntryState.Available || state == RosterEntryState.Placed;
        }

        /// <summary>
        /// Convenience refresh: marks every placed unit <see cref="RosterEntryState.Placed"/>,
        /// every other owned unit <see cref="RosterEntryState.Available"/>, and — once the squad cap
        /// is reached — the still-unplaced ones <see cref="RosterEntryState.Unaffordable"/>.
        /// </summary>
        /// <param name="controller">The controller holding the current placements. Ignored when null.</param>
        public void RefreshFrom(DeploymentController controller)
        {
            if (controller == null)
            {
                return;
            }

            bool squadFull = controller.MaxSquadSize > 0 && controller.PlacedCount >= controller.MaxSquadSize;

            for (int i = 0; i < roster.Count; i++)
            {
                UnitData unit = roster[i];

                if (GetState(unit) == RosterEntryState.Unavailable)
                {
                    continue;
                }

                if (controller.IsPlaced(unit))
                {
                    SetState(unit, RosterEntryState.Placed);
                }
                else
                {
                    SetState(unit, squadFull ? RosterEntryState.Unaffordable : RosterEntryState.Available);
                }
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private Entry CreateEntry(UnitData unit, int index)
        {
            RectTransform parent = entryContainer != null ? entryContainer : GetComponent<RectTransform>();

            GameObject root = new GameObject(
                "RosterEntry " + unit.name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = entrySize;

            if (useManualLayout)
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(index * (entrySize.x + entrySpacing), 0f);
            }

            Image background = root.GetComponent<Image>();
            background.sprite = entryBackgroundSprite != null ? entryBackgroundSprite : PlaceholderArt.Pixel;
            background.type = Image.Type.Simple;

            Image portrait = CreatePortraitChild(rect, unit);
            Text label = CreateLabelChild(rect, unit);

            Button button = root.GetComponent<Button>();
            UnitData captured = unit;
            button.onClick.AddListener(delegate { OnEntryClicked(captured); });

            return new Entry
            {
                Root = root,
                Background = background,
                Portrait = portrait,
                Label = label,
                Button = button,
                State = RosterEntryState.Available
            };
        }

        private Image CreatePortraitChild(RectTransform parent, UnitData unit)
        {
            GameObject child = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, labelFont != null ? labelFontSize + 6f : 4f);
            rect.offsetMax = new Vector2(-4f, -4f);

            Image image = child.GetComponent<Image>();
            image.sprite = unit.Portrait != null ? unit.Portrait : PlaceholderArt.Token;
            image.preserveAspect = true;
            image.raycastTarget = false;

            return image;
        }

        private Text CreateLabelChild(RectTransform parent, UnitData unit)
        {
            if (labelFont == null)
            {
                return null;
            }

            GameObject child = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, labelFontSize + 4f);

            Text text = child.GetComponent<Text>();
            text.font = labelFont;
            text.fontSize = labelFontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = string.IsNullOrEmpty(unit.DisplayName) ? unit.name : unit.DisplayName;

            return text;
        }

        private void OnEntryClicked(UnitData unit)
        {
            if (!IsPickable(unit))
            {
                return;
            }

            Action<UnitData> handler = EntryPicked;

            if (handler != null)
            {
                handler(unit);
            }
        }

        private void ApplyState(Entry entry)
        {
            if (entry.Root == null)
            {
                return;
            }

            Color tint = TintFor(entry.State);
            bool pickable = entry.State == RosterEntryState.Available || entry.State == RosterEntryState.Placed;

            if (!pickable)
            {
                tint.a *= disabledAlpha;
            }

            if (entry.Background != null)
            {
                entry.Background.color = tint;
            }

            if (entry.Portrait != null)
            {
                Color portraitTint = Color.white;
                portraitTint.a = pickable ? 1f : disabledAlpha;
                entry.Portrait.color = portraitTint;
            }

            if (entry.Button != null)
            {
                entry.Button.interactable = pickable;
            }
        }

        private Color TintFor(RosterEntryState state)
        {
            switch (state)
            {
                case RosterEntryState.Available:
                    return availableTint;
                case RosterEntryState.Placed:
                    return placedTint;
                case RosterEntryState.Unaffordable:
                    return unaffordableTint;
                default:
                    return unavailableTint;
            }
        }

        /// <summary>The uGUI objects making up one portrait, plus its current state.</summary>
        private struct Entry
        {
            public GameObject Root;
            public Image Background;
            public Image Portrait;
            public Text Label;
            public Button Button;
            public RosterEntryState State;
        }
    }
}
