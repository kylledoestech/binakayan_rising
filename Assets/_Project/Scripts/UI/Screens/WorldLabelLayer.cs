using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Screens
{
    /// <summary>
    /// Unit name tags and floating damage numbers, drawn over the board on a canvas of their own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a separate canvas.</b> These labels move every frame, and moving a graphic marks its
    /// whole canvas for rebatching. On the HUD canvas that meant the entire interface was rebuilt
    /// sixty times a second during a replay. On their own they rebatch alone.
    /// </para>
    /// <para>
    /// It also puts them in the right place in the stack: under the HUD, so the side panel and the
    /// outcome scrim cover a name tag rather than the tag floating on top of a modal.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class WorldLabelLayer : MonoBehaviour
    {
        private readonly List<BattlePlaytest.UnitSnapshot> units = new List<BattlePlaytest.UnitSnapshot>();
        private readonly List<BattlePlaytest.PopupSnapshot> popups = new List<BattlePlaytest.PopupSnapshot>();

        private BattlePlaytest battle;
        private Canvas canvas;
        private WorldLabelPool nameLabels;
        private WorldLabelPool popupLabels;

        /// <summary>Creates the layer under <paramref name="parent"/>, bound to a battle.</summary>
        public static WorldLabelLayer Create(Transform parent, BattlePlaytest battle)
        {
            Canvas canvas = UiKit.Screen("World Labels", Theme.Layer.WorldLabels, raycaster: false);
            canvas.transform.SetParent(parent, worldPositionStays: false);

            var layer = canvas.gameObject.AddComponent<WorldLabelLayer>();
            layer.battle = battle;
            layer.canvas = canvas;
            layer.nameLabels = new WorldLabelPool(canvas, "Unit Labels", Theme.Type.Small, Theme.Parchment, plated: true);

            // Damage numbers are set two steps larger than the names they fly off. At body size
            // over a painted board they are gone before the eye finds them, which is the whole
            // point of a damage number.
            layer.popupLabels = new WorldLabelPool(canvas, "Popups", Theme.Type.Heading, Theme.GoldBright, plated: false);
            return layer;
        }

        private void LateUpdate()
        {
            Camera camera = battle != null ? battle.BoardCamera : null;
            if (camera == null)
            {
                return;
            }

            battle.GetUnits(units);
            battle.GetPopups(popups);

            nameLabels.Begin();
            for (int i = 0; i < units.Count; i++)
            {
                BattlePlaytest.UnitSnapshot unit = units[i];
                if (!unit.Alive)
                {
                    continue;
                }

                // Over the head: a figure's name across its chest hides the outfit that tells
                // units apart. For a round token, Head is its centre, as the label always was.
                Vector3 screen = camera.WorldToScreenPoint(unit.Head);
                if (screen.z < 0f)
                {
                    continue;
                }

                nameLabels.PlaceText(screen, unit.ShortName, Theme.Parchment, 0f);
            }

            nameLabels.End();

            popupLabels.Begin();
            for (int i = 0; i < popups.Count; i++)
            {
                BattlePlaytest.PopupSnapshot popup = popups[i];
                Vector3 screen = camera.WorldToScreenPoint(popup.World);
                if (screen.z < 0f)
                {
                    continue;
                }

                Color tint = PopupColor(popup.Kind);
                tint.a = Mathf.Clamp01(1.4f - popup.Age);

                // Rising as it fades is what separates a damage number from a label that happens
                // to be sitting on a unit. It starts clear of the token so the first frame of a
                // hit is readable rather than stamped across the unit's own name.
                float rise = 26f + (popup.Age * 56f);
                popupLabels.PlacePopup(screen, popup.Kind, popup.Amount, tint, rise);
            }

            popupLabels.End();
        }

        private static Color PopupColor(BattlePlaytest.PopupKind kind)
        {
            switch (kind)
            {
                case BattlePlaytest.PopupKind.Critical:
                    return Theme.GoldBright;
                case BattlePlaytest.PopupKind.Heal:
                    return new Color32(0x8C, 0xD9, 0x8C, 0xFF);
                case BattlePlaytest.PopupKind.Dodge:
                case BattlePlaytest.PopupKind.Miss:
                    return Theme.Parchment;
                default:
                    return new Color32(0xFF, 0x73, 0x66, 0xFF);
            }
        }
    }

    /// <summary>
    /// A recycled set of screen-space labels used to annotate world positions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unit names and damage numbers appear and vanish constantly, and creating a
    /// <see cref="TextMeshProUGUI"/> per unit per frame would allocate continuously. The pool keeps
    /// the labels it has made, hides the surplus, and reuses the rest.
    /// </para>
    /// <para>
    /// Each slot remembers what it last showed, so text is only re-set when it changes. A TMP
    /// label regenerates its mesh on every text assignment, even an identical one formatted fresh.
    /// </para>
    /// <para>
    /// A plated pool sets each label on a dark pill. Unit figures stand close enough that a name
    /// tag often lands on the figure behind, and an outline alone cannot hold parchment type
    /// against a white shirt. The pills share their own root under the labels, so the pool
    /// still draws in two batches however many units are on the field.
    /// </para>
    /// </remarks>
    internal sealed class WorldLabelPool
    {
        private const float PlateHeight = 20f;

        // The project blends in linear space, which lightens a dark overlay: 0.9 here covers
        // like about 0.8 would in sRGB. Measured off a white shirt, 0.75 left the name unreadable.
        private static readonly Color PlateColor = new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.9f);

        private sealed class Slot
        {
            public TextMeshProUGUI Label;
            public Image Plate;
            public string Text;
            public BattlePlaytest.PopupKind Kind;
            public float Amount;
            public int Version = -1;
        }

        private readonly RectTransform root;
        private readonly RectTransform plates;
        private readonly Canvas canvas;
        private readonly List<Slot> slots = new List<Slot>();
        private readonly float size;

        private Material outlineMaterial;
        private int used;

        public WorldLabelPool(Canvas canvas, string name, float size, Color defaultColor, bool plated)
        {
            this.canvas = canvas;
            if (plated)
            {
                plates = UiKit.Stretch(UiKit.NewRect(canvas.transform, name + " Plates"));
            }

            root = UiKit.NewRect(canvas.transform, name);
            UiKit.Stretch(root);
            this.size = size;
        }

        /// <summary>Starts a frame's worth of placements.</summary>
        public void Begin()
        {
            used = 0;
        }

        /// <summary>Positions a plain text label.</summary>
        public void PlaceText(Vector3 screenPoint, string text, Color color, float rise)
        {
            Slot slot = Next(screenPoint, color, rise);
            if (!ReferenceEquals(slot.Text, text))
            {
                slot.Text = text;
                slot.Version = -1;
                slot.Label.text = text;

                // Measured only when the text changes: preferredWidth runs a layout pass.
                if (slot.Plate != null)
                {
                    slot.Plate.rectTransform.sizeDelta = new Vector2(
                        slot.Label.preferredWidth + (Theme.Space.Tight * 2f), PlateHeight);
                }
            }
        }

        /// <summary>Positions a floating number, formatting it only when it changes.</summary>
        public void PlacePopup(Vector3 screenPoint, BattlePlaytest.PopupKind kind, float amount, Color color, float rise)
        {
            Slot slot = Next(screenPoint, color, rise);
            if (slot.Text == null && slot.Kind == kind && Mathf.Approximately(slot.Amount, amount) && slot.Version == Loc.Version)
            {
                return;
            }

            slot.Text = null;
            slot.Kind = kind;
            slot.Amount = amount;
            slot.Version = Loc.Version;

            switch (kind)
            {
                case BattlePlaytest.PopupKind.Dodge:
                    slot.Label.text = Loc.Get(TextKey.PopupDodge);
                    break;
                case BattlePlaytest.PopupKind.Miss:
                    slot.Label.text = Loc.Get(TextKey.PopupMiss);
                    break;
                case BattlePlaytest.PopupKind.Heal:
                    slot.Label.SetText("+{0:0}", amount);
                    break;
                case BattlePlaytest.PopupKind.Critical:
                    slot.Label.SetText("{0:0.0}!", amount);
                    break;
                default:
                    slot.Label.SetText("{0:0.0}", amount);
                    break;
            }
        }

        /// <summary>Hides whatever was not used this frame.</summary>
        public void End()
        {
            for (int i = used; i < slots.Count; i++)
            {
                SetActive(slots[i].Label, false);
                SetActive(slots[i].Plate, false);
            }
        }

        private Slot Next(Vector3 screenPoint, Color color, float rise)
        {
            Slot slot;
            if (used < slots.Count)
            {
                slot = slots[used];
            }
            else
            {
                TextMeshProUGUI label = UiKit.Body(root, string.Empty, size, TextAlignmentOptions.Center);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.fontSharedMaterial = Outline(label);

                // TMP sizes each glyph's quad from the material it had when the mesh was built, so
                // an outline added afterwards is clipped away at the glyph edge until the padding
                // is recomputed. Skip this and the material change looks like it did nothing.
                label.UpdateMeshPadding();
                UiKit.SetSize(label.rectTransform, 200f, 34f);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.zero;
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);

                slot = new Slot { Label = label };
                if (plates != null)
                {
                    slot.Plate = NewPlate();
                }

                slots.Add(slot);
            }

            SetActive(slot.Label, true);
            SetActive(slot.Plate, true);
            slot.Label.color = color;

            // WorldToScreenPoint returns device pixels, but the canvas scales itself to a
            // 1920x1080 reference. Placing raw pixels into a scaled canvas puts every label at
            // the wrong spot on any display that is not exactly the reference size.
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            var position = new Vector2(
                Mathf.Round(screenPoint.x / scale), Mathf.Round((screenPoint.y / scale) + rise));
            slot.Label.rectTransform.anchoredPosition = position;
            if (slot.Plate != null)
            {
                slot.Plate.rectTransform.anchoredPosition = position;
            }

            used++;
            return slot;
        }

        private Image NewPlate()
        {
            RectTransform rect = UiKit.NewRect(plates, "Plate");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var plate = rect.gameObject.AddComponent<Image>();
            plate.sprite = Theme.BarFill;
            plate.type = Image.Type.Sliced;
            plate.color = PlateColor;
            plate.raycastTarget = false;
            return plate;
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null && component.gameObject.activeSelf != active)
            {
                component.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// The ink-outlined material every label in this pool shares.
        /// </summary>
        /// <remarks>
        /// Parchment type over a painted board has nothing to sit against — it reads over grass and
        /// vanishes over canvas. An outline fixes that. It is built once and shared rather than set
        /// through <c>TMP_Text.outlineWidth</c>, which instances a material per label and would
        /// turn a pooled overlay into one draw call per unit on the field.
        /// </remarks>
        private Material Outline(TextMeshProUGUI sample)
        {
            if (outlineMaterial != null)
            {
                return outlineMaterial;
            }

            outlineMaterial = new Material(sample.fontSharedMaterial)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            outlineMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Theme.Ink);
            outlineMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            return outlineMaterial;
        }
    }
}
