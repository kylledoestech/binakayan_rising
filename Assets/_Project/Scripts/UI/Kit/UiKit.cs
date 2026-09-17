using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// Factory for every widget in the game. Screens compose these; they never build
    /// <see cref="RectTransform"/> hierarchies by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two rules hold this together.</b> First, no screen may name a colour, a font size or a
    /// margin — those come from <see cref="Theme"/>, which is why the previous HUD's forty scattered
    /// colour literals cannot recur. Second, every graphic this class creates has
    /// <see cref="Graphic.raycastTarget"/> off unless it is genuinely interactive.
    /// </para>
    /// <para>
    /// That second rule is not a micro-optimisation. uGUI defaults <c>raycastTarget</c> to true on
    /// every <see cref="Graphic"/>, so a purely decorative full-screen parchment backdrop will
    /// silently swallow every click meant for the battlefield underneath it. There is no warning,
    /// no exception and no log line — deployment simply stops responding, and the cause is a
    /// checkbox on a background image.
    /// </para>
    /// </remarks>
    public static class UiKit
    {
        // ------------------------------------------------------------------ roots

        /// <summary>
        /// Creates a screen-space canvas already scaled against
        /// <see cref="Theme.ReferenceResolution"/>.
        /// </summary>
        /// <param name="renderMode">
        /// Overlay for normal play. The offscreen screenshot harness needs
        /// <see cref="RenderMode.ScreenSpaceCamera"/> instead, because overlay canvases are
        /// composited by the engine after camera rendering and never appear in a
        /// <see cref="RenderTexture"/>. Taking it as a parameter keeps capture and play on one code
        /// path rather than two that can drift.
        /// </param>
        public static Canvas Screen(
            string name,
            int sortOrder,
            RenderMode renderMode = RenderMode.ScreenSpaceOverlay,
            Camera worldCamera = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = renderMode;
            canvas.sortingOrder = sortOrder;
            if (renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = worldCamera != null ? worldCamera : Camera.main;
                canvas.planeDistance = 10f;
            }

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Theme.ReferenceResolution;
            // Balance width and height so neither a narrow window nor a short one wins outright.
            // The old HUD pinned everything to raw pixels and rendered at a quarter scale on 4K.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            return canvas;
        }

        /// <summary>
        /// Returns the scene's <see cref="EventSystem"/>, creating one if needed.
        /// </summary>
        /// <remarks>
        /// The module must be <see cref="InputSystemUIInputModule"/>. This project sets
        /// <c>activeInputHandler</c> to the Input System alone, so the legacy
        /// <c>StandaloneInputModule</c> throws at runtime rather than degrading. The project-wide
        /// actions asset is registered in <c>EditorBuildSettings</c>, so the module's
        /// <c>actionsAsset</c> populates itself with the full UI map.
        /// </remarks>
        public static EventSystem EnsureEventSystem()
        {
            EventSystem existing = EventSystem.current;
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return go.GetComponent<EventSystem>();
        }

        // ------------------------------------------------------------------ containers

        /// <summary>
        /// A parchment surface: solid fill, tiled paper grain, then an ornamental frame on top.
        /// </summary>
        /// <remarks>
        /// Three layers rather than one sprite. The frame art is a greyscale fret that has to be
        /// tinted, and tinting it alone would tint the fill with it; separating them lets the fill
        /// be flat parchment while the ornament is gold. The grain layer in between is what stops
        /// the result reading as a flat coloured rectangle — it is the cheapest single thing that
        /// makes a UI look drawn rather than generated.
        /// </remarks>
        public static RectTransform Panel(Transform parent, string name, bool blocksClicks = true)
        {
            RectTransform root = NewRect(parent, name);

            Image fill = AddImage(root, "Fill", Theme.Panel, Theme.Parchment);
            fill.type = Image.Type.Sliced;
            fill.raycastTarget = blocksClicks;
            Stretch(fill.rectTransform);

            Image grain = AddImage(root, "Grain", Theme.Grain, new Color(1f, 1f, 1f, 0.10f));
            grain.type = Image.Type.Tiled;
            grain.raycastTarget = false;
            Stretch(grain.rectTransform);

            Image ornament = AddImage(root, "Ornament", Theme.FrameHollow, Theme.RevolutionDark);
            ornament.type = Image.Type.Sliced;
            ornament.raycastTarget = false;
            Stretch(ornament.rectTransform);

            return root;
        }

        /// <summary>An outline-only frame, for laying over the board without hiding it.</summary>
        public static RectTransform Frame(Transform parent, string name, Color tint)
        {
            RectTransform root = NewRect(parent, name);
            Image image = AddImage(root, "Edge", Theme.FrameHollow, tint);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return root;
        }

        /// <summary>A recessed well, for list backgrounds and stat readouts.</summary>
        public static RectTransform Well(Transform parent, string name)
        {
            RectTransform root = NewRect(parent, name);
            Image image = AddImage(root, "Fill", Theme.Panel, Theme.ParchmentDeep);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return root;
        }

        /// <summary>A full-screen dimming layer that swallows clicks to whatever is behind it.</summary>
        public static Image Scrim(Transform parent)
        {
            RectTransform root = NewRect(parent, "Scrim");
            Stretch(root);
            Image image = AddImage(root, "Fill", null, Theme.Scrim);
            // The one place a decorative graphic SHOULD block: a modal scrim exists to stop
            // interaction with what it covers.
            image.raycastTarget = true;
            Stretch(image.rectTransform);
            return image;
        }

        /// <summary>A vertically stacking container.</summary>
        public static RectTransform Column(
            Transform parent,
            string name,
            float spacing = Theme.Space.Snug,
            float padding = 0f,
            TextAnchor alignment = TextAnchor.UpperCenter)
        {
            RectTransform root = NewRect(parent, name);
            var layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureLayout(layout, spacing, padding, alignment);
            return root;
        }

        /// <summary>A horizontally stacking container.</summary>
        public static RectTransform Row(
            Transform parent,
            string name,
            float spacing = Theme.Space.Snug,
            float padding = 0f,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            RectTransform root = NewRect(parent, name);
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureLayout(layout, spacing, padding, alignment);
            return root;
        }

        private static void ConfigureLayout(
            HorizontalOrVerticalLayoutGroup layout,
            float spacing,
            float padding,
            TextAnchor alignment)
        {
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.padding = new RectOffset(
                (int)padding, (int)padding, (int)padding, (int)padding);

            // Let children keep their own preferred size; the group only positions them.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        // ------------------------------------------------------------------ text

        /// <summary>Display type: Cinzel caps in gold, with an ink outline so it reads on parchment.</summary>
        public static TextMeshProUGUI Display(
            Transform parent,
            string text,
            float size = Theme.Type.Title,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI label = Text(parent, "Display", text, size, alignment, Theme.DisplayFont);
            label.color = Theme.Gold;
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = Theme.Type.DisplayTracking;

            // Outline and underlay are material properties on an SDF font, so they cost nothing
            // per glyph. The legacy Text equivalent needed a mesh-modifying component that
            // quadrupled vertex count and fell apart at display sizes.
            ApplyEngraving(label);
            return label;
        }

        /// <summary>Body type: Spectral in ink.</summary>
        public static TextMeshProUGUI Body(
            Transform parent,
            string text,
            float size = Theme.Type.Body,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            TextMeshProUGUI label = Text(parent, "Body", text, size, alignment, Theme.BodyFont);
            label.color = Theme.Ink;
            return label;
        }

        /// <summary>Secondary body type, for captions and disabled rows.</summary>
        public static TextMeshProUGUI Caption(
            Transform parent,
            string text,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            TextMeshProUGUI label = Body(parent, text, Theme.Type.Small, alignment);
            label.color = Theme.InkSoft;
            return label;
        }

        private static TextMeshProUGUI Text(
            Transform parent,
            string name,
            string text,
            float size,
            TextAlignmentOptions alignment,
            TMP_FontAsset font)
        {
            RectTransform root = NewRect(parent, name);
            var label = root.gameObject.AddComponent<TextMeshProUGUI>();

            if (font != null)
            {
                label.font = font;
            }

            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.richText = true;
            return label;
        }

        /// <summary>
        /// Gives text a dark rim and a soft drop shadow so it stays legible over textured parchment.
        /// </summary>
        /// <remarks>
        /// Mutates a material instance, not the shared font material, so one engraved title does
        /// not restyle every other label sharing the same font asset.
        /// </remarks>
        private static void ApplyEngraving(TextMeshProUGUI label)
        {
            label.outlineWidth = 0.18f;
            label.outlineColor = Theme.RevolutionDark;

            Material material = label.fontMaterial;
            if (material == null)
            {
                return;
            }

            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor(TMPro.ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.45f));
            material.SetFloat(TMPro.ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
            material.SetFloat(TMPro.ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            material.SetFloat(TMPro.ShaderUtilities.ID_UnderlaySoftness, 0.15f);
        }

        // ------------------------------------------------------------------ controls

        /// <summary>
        /// A wax-seal button: ornate plate, engraved caps, hover and press feedback, click sound.
        /// </summary>
        public static Button SealButton(
            Transform parent,
            string label,
            UnityAction onClick,
            float width = 260f,
            float height = 64f,
            float textSize = 0f)
        {
            RectTransform root = NewRect(parent, "Button " + label);
            SetSize(root, width, height);

            Image face = AddImage(root, "Face", Theme.Button, Theme.Revolution);
            face.type = Image.Type.Sliced;
            face.raycastTarget = true;
            Stretch(face.rectTransform);

            Image rim = AddImage(root, "Rim", Theme.FrameHollow, Theme.Gold);
            rim.type = Image.Type.Sliced;
            rim.raycastTarget = false;
            Stretch(rim.rectTransform);

            // Display type is set in caps with wide tracking, so a compact button needs to be
            // told a smaller size or the label wraps mid-word — "REDEPLOY" becomes "REDEPL/OY".
            TextMeshProUGUI text = Display(root, label, textSize > 0f ? textSize : Theme.Type.Heading);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Theme.Parchment;
            Stretch(text.rectTransform);
            text.margin = new Vector4(Theme.Space.Snug, 0f, Theme.Space.Snug, 0f);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            root.gameObject.AddComponent<UiButtonFeel>();
            return button;
        }

        /// <summary>A compact square button carrying an icon rather than a label.</summary>
        public static Button IconButton(
            Transform parent,
            Sprite icon,
            UnityAction onClick,
            float size = 56f,
            string name = "IconButton")
        {
            RectTransform root = NewRect(parent, name);
            SetSize(root, size, size);

            Image face = AddImage(root, "Face", Theme.Slot, Theme.ParchmentDeep);
            face.type = Image.Type.Sliced;
            face.raycastTarget = true;
            Stretch(face.rectTransform);

            if (icon != null)
            {
                Image glyph = AddImage(root, "Icon", icon, Theme.Ink);
                glyph.raycastTarget = false;
                glyph.preserveAspect = true;
                Stretch(glyph.rectTransform, Theme.Space.Snug);
            }

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            root.gameObject.AddComponent<UiButtonFeel>();
            return button;
        }

        /// <summary>A tinted icon, sized square.</summary>
        public static Image Icon(Transform parent, Sprite sprite, float size, Color tint)
        {
            RectTransform root = NewRect(parent, "Icon");
            SetSize(root, size, size);
            Image image = AddImage(root, "Glyph", sprite, tint);
            image.preserveAspect = true;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return image;
        }

        /// <summary>The sun of liberty, at the requested size.</summary>
        public static Image Sigil(Transform parent, float size, Color tint)
        {
            Image image = Icon(parent, Theme.Sigil, size, tint);
            image.name = "Sigil";
            return image;
        }

        /// <summary>A horizontal rule.</summary>
        public static Image Divider(Transform parent, float width = 0f)
        {
            RectTransform root = NewRect(parent, "Divider");
            SetSize(root, width > 0f ? width : 320f, 20f);
            Image image = AddImage(root, "Rule", Theme.Divider, Theme.Gold);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            Stretch(image.rectTransform);
            return image;
        }

        /// <summary>A labelled value bar — health, progress, a resource meter.</summary>
        public static BarView Bar(Transform parent, float width, float height, Color fillColor)
        {
            RectTransform root = NewRect(parent, "Bar");
            SetSize(root, width, height);

            Image track = AddImage(root, "Track", Theme.BarTrack, Theme.RevolutionDark);
            track.type = Image.Type.Sliced;
            track.raycastTarget = false;
            Stretch(track.rectTransform);

            Image fill = AddImage(root, "Fill", Theme.BarFill, fillColor);
            fill.type = Image.Type.Sliced;
            fill.raycastTarget = false;

            // Anchored to the left edge and driven by anchorMax.x, so the fill grows rightward
            // from a fixed origin. Scaling the transform instead would also scale the sliced
            // caps and pinch them as the bar empties.
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            var view = root.gameObject.AddComponent<BarView>();
            view.Bind(fill);
            return view;
        }

        // ------------------------------------------------------------------ rect helpers

        /// <summary>Creates an empty <see cref="RectTransform"/> parented and reset.</summary>
        public static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, worldPositionStays: false);
            rect.localScale = Vector3.one;
            rect.anchoredPosition3D = Vector3.zero;
            return rect;
        }

        /// <summary>Makes a rect fill its parent, optionally inset on all sides.</summary>
        public static RectTransform Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        /// <summary>Pins a rect to one corner or edge of its parent at a fixed size.</summary>
        public static RectTransform Anchor(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 offset,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Fixes a rect's size and adds a layout element so groups respect it.</summary>
        public static RectTransform SetSize(RectTransform rect, float width, float height)
        {
            rect.sizeDelta = new Vector2(width, height);

            var element = rect.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = rect.gameObject.AddComponent<LayoutElement>();
            }

            element.preferredWidth = width;
            element.preferredHeight = height;
            return rect;
        }

        /// <summary>Adds a <see cref="CanvasGroup"/>, or returns the existing one.</summary>
        public static CanvasGroup Group(GameObject go)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            return group != null ? group : go.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// Adds a child <see cref="Image"/>. Non-interactive by default — see the class remarks.
        /// </summary>
        private static Image AddImage(Transform parent, string name, Sprite sprite, Color color)
        {
            RectTransform rect = NewRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }

    /// <summary>
    /// Drives the fill width of a bar created by <see cref="UiKit.Bar"/>.
    /// </summary>
    public sealed class BarView : MonoBehaviour
    {
        [SerializeField] private Image fill;

        private float value = 1f;

        /// <summary>Binds the fill graphic. Called by the factory.</summary>
        public void Bind(Image fillImage)
        {
            fill = fillImage;
        }

        /// <summary>Fraction filled, clamped to 0..1.</summary>
        public float Value
        {
            get => value;
            set
            {
                this.value = Mathf.Clamp01(value);
                Apply();
            }
        }

        /// <summary>Sets the fill colour, for health-state thresholds.</summary>
        public void SetColor(Color color)
        {
            if (fill != null)
            {
                fill.color = color;
            }
        }

        private void Apply()
        {
            if (fill == null)
            {
                return;
            }

            RectTransform rect = fill.rectTransform;
            rect.anchorMax = new Vector2(value, 1f);

            // A fully drained bar should disappear rather than collapse to its rounded caps,
            // which would otherwise leave a visible stub at zero health.
            fill.enabled = value > 0.001f;
        }
    }

    /// <summary>
    /// Adds the small motions that make a button feel pressed rather than merely clicked.
    /// </summary>
    /// <remarks>
    /// A scale punch on hover and a downward nudge on press cost nothing and are most of the
    /// difference between UI that reads as a prototype and UI that reads as a product.
    /// </remarks>
    public sealed class UiButtonFeel : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.04f;
        private const float PressScale = 0.97f;

        private RectTransform rect;
        private Vector2 restingPosition;
        private bool captured;

        private void Awake()
        {
            rect = (RectTransform)transform;
            restingPosition = rect.anchoredPosition;
            captured = true;
        }

        private void OnEnable()
        {
            // Layout groups move the rect after Awake, so re-read the resting position once the
            // first layout pass has run. Otherwise the press nudge drifts the button permanently.
            if (rect != null)
            {
                restingPosition = rect.anchoredPosition;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable())
            {
                return;
            }

            Scale(HoverScale);
            UiSfx.Play(UiSfx.Cue.Hover);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Scale(1f);
            Restore();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable())
            {
                return;
            }

            Scale(PressScale);
            if (rect != null && captured)
            {
                rect.anchoredPosition = restingPosition + new Vector2(0f, -2f);
            }

            UiSfx.Play(UiSfx.Cue.Click);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Scale(HoverScale);
            Restore();
        }

        private void Restore()
        {
            if (rect != null && captured)
            {
                rect.anchoredPosition = restingPosition;
            }
        }

        private bool IsInteractable()
        {
            var selectable = GetComponent<Selectable>();
            return selectable == null || selectable.IsInteractable();
        }

        private void Scale(float factor)
        {
            transform.localScale = new Vector3(factor, factor, 1f);
        }
    }
}
