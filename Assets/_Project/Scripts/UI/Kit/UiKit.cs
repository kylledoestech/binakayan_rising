using System;
using System.Collections.Generic;
using BinakayanRising.Core.Localization;
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
        // One engraved material per source font material, shared by every engraved label.
        private static readonly Dictionary<Material, Material> EngravedMaterials = new Dictionary<Material, Material>();

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
        /// <param name="raycaster">
        /// False for a canvas that only displays, such as floating world labels. A canvas without a
        /// raycaster is skipped entirely by the event system instead of being tested and missed.
        /// </param>
        public static Canvas Screen(
            string name,
            int sortOrder,
            RenderMode renderMode = RenderMode.ScreenSpaceOverlay,
            Camera worldCamera = null,
            bool raycaster = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            if (raycaster)
            {
                go.AddComponent<GraphicRaycaster>();
            }

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
            // Expand scales by whichever axis is tighter, so the canvas is never smaller than the
            // reference in either direction. A 50/50 match let a 4:3 or 5:4 window shrink the
            // canvas below 1920 wide, and the full-width top bar ran off the right edge.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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
        /// <param name="blocksClicks">
        /// True when the well floats over the board on its own, so a click or a wheel over it
        /// belongs to it rather than falling through to the battlefield.
        /// </param>
        public static RectTransform Well(Transform parent, string name, bool blocksClicks = false)
        {
            RectTransform root = NewRect(parent, name);
            Image image = AddImage(root, "Fill", Theme.Panel, Theme.ParchmentDeep);
            image.type = Image.Type.Sliced;
            image.raycastTarget = blocksClicks;
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
        /// <para>
        /// Every engraved label shares one material per font, built on first use. The previous
        /// version read <c>fontMaterial</c> and set <c>outlineWidth</c>, and each of those quietly
        /// clones the material for that one label: every rebuild of a panel leaked a fresh set of
        /// materials, and every engraved label was its own draw call.
        /// </para>
        /// <para>
        /// The shared material is a copy, never the font asset's own, so plain labels using the
        /// same font stay unengraved.
        /// </para>
        /// </remarks>
        private static void ApplyEngraving(TextMeshProUGUI label)
        {
            Material source = label.fontSharedMaterial;
            if (source == null)
            {
                return;
            }

            Material engraved;
            if (!EngravedMaterials.TryGetValue(source, out engraved) || engraved == null)
            {
                ShaderUtilities.GetShaderPropertyIDs();
                engraved = new Material(source)
                {
                    name = source.name + " (Engraved)",
                    hideFlags = HideFlags.DontSave,
                };

                engraved.EnableKeyword(ShaderUtilities.Keyword_Outline);
                engraved.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
                engraved.SetColor(ShaderUtilities.ID_OutlineColor, Theme.RevolutionDark);

                engraved.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                engraved.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.45f));
                engraved.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
                engraved.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
                engraved.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);

                EngravedMaterials[source] = engraved;
            }

            label.fontSharedMaterial = engraved;

            // Glyph quads are sized from the material the mesh was built with; an outline added
            // afterwards is clipped at the glyph edge until the padding is recomputed.
            label.UpdateMeshPadding();
        }

        /// <summary>Binds a label to a string key so it re-renders when the language changes.</summary>
        public static LocalizedText Localize(TextMeshProUGUI label, TextKey key)
        {
            LocalizedText localized = label.GetComponent<LocalizedText>();
            if (localized == null)
            {
                localized = label.gameObject.AddComponent<LocalizedText>();
            }

            localized.Bind(label, key);
            return localized;
        }

        /// <summary>Drops the shared engraved materials so a second play session rebuilds them.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            foreach (KeyValuePair<Material, Material> pair in EngravedMaterials)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.DestroyImmediate(pair.Value);
                }
            }

            EngravedMaterials.Clear();
        }

        // ------------------------------------------------------------------ controls

        /// <summary>
        /// A wax-seal button whose label follows the current language.
        /// </summary>
        public static Button SealButton(
            Transform parent,
            TextKey label,
            UnityAction onClick,
            float width = 260f,
            float height = 64f,
            float textSize = 0f,
            string name = null)
        {
            Button button = SealButton(parent, Loc.Get(label), onClick, width, height, textSize, name ?? "Button " + label);
            Localize(button.GetComponentInChildren<TextMeshProUGUI>(), label);
            return button;
        }

        /// <summary>
        /// A wax-seal button: ornate plate, engraved caps, hover and press feedback, click sound.
        /// </summary>
        /// <param name="name">
        /// Object name. Defaults to one derived from the label, which is fine for symbols but not
        /// for words: a localized label would give the same button a different name per language.
        /// </param>
        public static Button SealButton(
            Transform parent,
            string label,
            UnityAction onClick,
            float width = 260f,
            float height = 64f,
            float textSize = 0f,
            string name = null)
        {
            RectTransform root = NewRect(parent, name ?? "Button " + label);
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
            float size = textSize > 0f ? textSize : Theme.Type.Heading;
            TextMeshProUGUI text = Display(root, label, size);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Theme.Parchment;
            Stretch(text.rectTransform);

            // The gold rim eats most of a wide plate's 16px frame border, so a long label keeps
            // clear of it; compact symbol buttons cannot spare that much.
            float inset = width >= 160f ? Theme.Space.Base : Theme.Space.Tight;
            text.margin = new Vector4(inset, 0f, inset, 0f);

            // Filipino labels run noticeably longer than English ones — "Ipuwesto Muli" against
            // "Redeploy" — so the label shrinks to fit its plate rather than spilling past it.
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Max(9f, size * 0.6f);
            text.overflowMode = TextOverflowModes.Ellipsis;

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

        /// <summary>
        /// A full-width list row that can be clicked: a parchment fill that darkens on hover, and a
        /// frame whose tint the caller drives to show selection.
        /// </summary>
        /// <param name="rim">The frame image, for selection tinting.</param>
        /// <remarks>
        /// The roster used a hollow <see cref="Frame"/> with a <see cref="Button"/> on it. Every
        /// graphic in a frame has raycasts off, so the button had nothing to be hit through and
        /// clicking a unit in the roster did nothing at all. Here the fill is the hit area.
        /// </remarks>
        public static Button SelectableRow(Transform parent, string name, out Image rim)
        {
            RectTransform root = NewRect(parent, name);

            Image fill = AddImage(root, "Fill", Theme.Panel, Theme.ParchmentDeep);
            fill.type = Image.Type.Sliced;
            fill.raycastTarget = true;
            Stretch(fill.rectTransform);

            rim = AddImage(root, "Rim", Theme.FrameHollow, Theme.ParchmentDeep);
            rim.type = Image.Type.Sliced;
            rim.raycastTarget = false;
            Stretch(rim.rectTransform);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.ColorTint;

            // The tint multiplies alpha too, so the resting row is a faint wash and hover firms it
            // up — a hover state that does not need a second sprite.
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.35f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.15f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            root.gameObject.AddComponent<UiRowFeel>();
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
    /// <para>
    /// A scale punch on hover and press costs nothing and is most of the difference between UI that
    /// reads as a prototype and UI that reads as a product.
    /// </para>
    /// <para>
    /// Scale only. The press used to nudge <c>anchoredPosition</c> down two units, but every button
    /// here lives in a layout group, which owns that position: the nudge fought the layout and a
    /// button could be left permanently offset.
    /// </para>
    /// <para>
    /// Releasing clears the event system's selection. A clicked uGUI button otherwise stays
    /// selected, and the UI module's Submit action — bound to Space — would press it again the
    /// next time Space was used as the Begin Assault hotkey.
    /// </para>
    /// </remarks>
    public sealed class UiButtonFeel : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.04f;
        private const float PressScale = 0.97f;

        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            Scale(1f);
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
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable())
            {
                return;
            }

            Scale(PressScale);
            UiSfx.Play(UiSfx.Cue.Click);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Scale(eventData != null && eventData.hovered.Contains(gameObject) ? HoverScale : 1f);
            UiRowFeel.ReleaseSelection(gameObject);
        }

        private bool IsInteractable()
        {
            return selectable == null || selectable.IsInteractable();
        }

        private void Scale(float factor)
        {
            transform.localScale = new Vector3(factor, factor, 1f);
        }
    }

    /// <summary>
    /// Click sound and selection release for list rows, which tint rather than scale.
    /// </summary>
    public sealed class UiRowFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable == null || selectable.IsInteractable())
            {
                UiSfx.Play(UiSfx.Cue.Click);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ReleaseSelection(gameObject);
        }

        /// <summary>Deselects <paramref name="owner"/> if the event system still has it selected.</summary>
        public static void ReleaseSelection(GameObject owner)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == owner)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }
    }
}
