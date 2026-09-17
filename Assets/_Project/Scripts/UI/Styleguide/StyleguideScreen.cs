using System.Collections.Generic;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Styleguide
{
    /// <summary>
    /// Renders every token and widget in the UI kit on one scrollable page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a test harness, not a game screen — it has no <c>GameState</c> and the router never
    /// shows it. It exists so the look can be judged, and so the failures that are otherwise
    /// invisible become obvious:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>9-slice borders.</b> Every frame is drawn at three aspect ratios. A wrong
    /// <c>spriteBorder</c> looks perfectly fine at the size it was authored for and only smears
    /// when stretched, so a single-size preview proves nothing.
    /// </description></item>
    /// <item><description>
    /// <b>Font baking.</b> Missing glyphs render as blank boxes. The type section deliberately
    /// includes the Spanish and Tagalog diacritics that the historical names need.
    /// </description></item>
    /// <item><description>
    /// <b>Linear-space colour.</b> Swatches are labelled with their source hex, so a palette that
    /// has been double-converted is visible rather than merely suspected.
    /// </description></item>
    /// </list>
    /// </remarks>
    public sealed class StyleguideScreen : MonoBehaviour
    {
        private RectTransform content;

        private void Start()
        {
            Build();
        }

        /// <summary>Builds the whole page. Safe to call again; it rebuilds from scratch.</summary>
        public void Build()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            Canvas canvas = UiKit.Screen("Styleguide Canvas", sortOrder: 0);
            canvas.transform.SetParent(transform, worldPositionStays: false);
            UiKit.EnsureEventSystem();

            RectTransform viewport = BuildScrollViewport(canvas.transform);

            SectionHeader("Binakayan Rising — Interface Styleguide");
            BuildPalette();
            BuildType();
            BuildFrames();
            BuildControls();
            BuildBars();
            BuildIcons();
            BuildMotifs();

            // Content height is driven by the layout group, but the scroll rect needs the rect
            // itself resized or the page clips to the viewport regardless of how much is in it.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            _ = viewport;
        }

        // ------------------------------------------------------------------ scaffolding

        private RectTransform BuildScrollViewport(Transform parent)
        {
            RectTransform backdrop = UiKit.NewRect(parent, "Backdrop");
            UiKit.Stretch(backdrop);

            Image fill = backdrop.gameObject.AddComponent<Image>();
            fill.color = Theme.Backdrop;
            fill.raycastTarget = false;

            RectTransform viewport = UiKit.NewRect(backdrop, "Viewport");
            UiKit.Stretch(viewport, Theme.Space.Loose);
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = backdrop.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;

            content = Col(viewport, "Content", Theme.Space.Loose, Theme.Space.Wide, TextAnchor.UpperLeft);

            // Top-anchored with a driven height: the vertical layout group sets sizeDelta.y as
            // sections are added, and the scroll rect reads that to size the scrollbar.
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            // With the anchors stretched across X, sizeDelta.x is an inset from both parent edges,
            // not a width. A fresh RectTransform carries 100 there, which makes the content 100px
            // wider than the viewport and hangs 50px off each side — so the first characters of
            // every line sit outside the mask and are silently clipped away.
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            return viewport;
        }

        private void SectionHeader(string title)
        {
            RectTransform row = Col(content, "Header " + title, Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
            Fixed(row, 96f);

            TextMeshProUGUI label = UiKit.Display(row, title, Theme.Type.Title, TextAlignmentOptions.Left);
            Fixed((RectTransform)label.transform, 48f);

            RectTransform rule = (RectTransform)UiKit.Divider(row).transform;
            Fixed(rule, 16f);
        }

        /// <summary>A titled band that each section's samples sit inside.</summary>
        private RectTransform Section(string title, float height)
        {
            RectTransform column = Col(content, "Section " + title, Theme.Space.Snug, 0f, TextAnchor.UpperLeft);
            Fixed(column, height + 40f);

            TextMeshProUGUI caption = UiKit.Caption(column, title.ToUpperInvariant(), TextAlignmentOptions.Left);
            caption.color = Theme.Gold;
            Fixed((RectTransform)caption.transform, 24f);

            RectTransform body = UiKit.NewRect(column, "Body");
            Fixed(body, height);
            return body;
        }

        /// <summary>
        /// A column whose children span its full width.
        /// </summary>
        /// <remarks>
        /// <see cref="UiKit.Column"/> leaves <c>childForceExpandWidth</c> off, which is correct for
        /// a row of fixed-size cells but wrong for a page: without it every section shrinks to its
        /// widest child, and a paragraph of body copy collapses to a one-character-per-line ribbon.
        /// </remarks>
        private static RectTransform Col(
            Transform parent,
            string name,
            float spacing = Theme.Space.Snug,
            float padding = 0f,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            RectTransform rect = UiKit.Column(parent, name, spacing, padding, alignment);
            var layout = rect.GetComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth = true;
            return rect;
        }

        /// <summary>Pins a child's height inside a vertical layout group.</summary>
        private static void Fixed(RectTransform rect, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
            element.flexibleWidth = 1f;
        }

        private static void FixedWidth(RectTransform rect, float width)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>()
                                    ?? rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }

        // ------------------------------------------------------------------ sections

        private void BuildPalette()
        {
            RectTransform body = Section("Palette", 150f);
            RectTransform row = UiKit.Row(body, "Swatches", Theme.Space.Snug, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(row);

            var swatches = new List<(string Name, string Hex, Color Value)>
            {
                ("Parchment", "E8DCC0", Theme.Parchment),
                ("Deep", "D6C39C", Theme.ParchmentDeep),
                ("Ink", "2B2118", Theme.Ink),
                ("Ink Soft", "5A4A38", Theme.InkSoft),
                ("Revolution", "8C2E22", Theme.Revolution),
                ("Rev. Dark", "5E1D15", Theme.RevolutionDark),
                ("Gold", "C9A227", Theme.Gold),
                ("Gold Bright", "F0D264", Theme.GoldBright),
                ("Colonial", "2E4A6B", Theme.Colonial),
                ("Success", "4E7A3A", Theme.Success),
                ("Warn", "C08A2E", Theme.Warn),
                ("Danger", "A32E22", Theme.Danger),
            };

            foreach ((string name, string hex, Color value) in swatches)
            {
                RectTransform cell = Col(row, name, Theme.Space.Hair, 0f, TextAnchor.UpperCenter);
                FixedWidth(cell, 120f);

                RectTransform chip = UiKit.NewRect(cell, "Chip");
                Fixed(chip, 84f);

                Image image = chip.gameObject.AddComponent<Image>();
                image.color = value;
                image.raycastTarget = false;

                TextMeshProUGUI label = UiKit.Caption(cell, $"{name}\n#{hex}", TextAlignmentOptions.Center);
                label.color = Theme.Parchment;
                Fixed((RectTransform)label.transform, 40f);
            }
        }

        private void BuildType()
        {
            RectTransform body = Section("Type — Cinzel display, Spectral body", 300f);
            RectTransform panel = UiKit.Panel(body, "Type Panel", blocksClicks: false);
            UiKit.Stretch(panel);

            RectTransform column = Col(panel, "Lines", Theme.Space.Tight, Theme.Space.FramePadding, TextAnchor.UpperLeft);
            UiKit.Stretch(column);

            Fixed((RectTransform)UiKit.Display(column, "Binakayan", Theme.Type.Display, TextAlignmentOptions.Left).transform, 76f);
            Fixed((RectTransform)UiKit.Display(column, "Battle of Binakayan", Theme.Type.Title, TextAlignmentOptions.Left).transform, 44f);
            Fixed((RectTransform)UiKit.Display(column, "Lock Formation", Theme.Type.Heading, TextAlignmentOptions.Left).transform, 32f);

            // The diacritics are the point of this line: if the atlas was baked without them these
            // render as blank boxes, and a proper noun in the game would silently lose a letter.
            TextMeshProUGUI body1 = UiKit.Body(
                column,
                "Emilio Aguinaldo led the Caviteño forces at Binakayan on 9 November 1896. " +
                "Diacritics: ñÑ áéíóú ÁÉÍÓÚ üÜ ¡¿ — dash, ellipsis…, “quotes”, ₱1,240.",
                Theme.Type.Body,
                TextAlignmentOptions.TopLeft);
            Fixed((RectTransform)body1.transform, 64f);

            TextMeshProUGUI small = UiKit.Caption(
                column,
                "Small caption text — used for stat labels, timestamps and log lines.",
                TextAlignmentOptions.TopLeft);
            small.color = Theme.InkSoft;
            Fixed((RectTransform)small.transform, 24f);
        }

        /// <summary>
        /// Draws each frame sprite at three aspect ratios.
        /// </summary>
        /// <remarks>
        /// A 9-slice with the wrong border renders correctly at its authored size and only fails
        /// when stretched — so the wide and tall variants here are the actual test, and the square
        /// one is the control.
        /// </remarks>
        private void BuildFrames()
        {
            RectTransform body = Section("Frames — 9-slice at three aspect ratios", 260f);
            RectTransform row = UiKit.Row(body, "Frames", Theme.Space.Wide, 0f, TextAnchor.UpperLeft);
            UiKit.Stretch(row);

            var frames = new List<(string Name, Sprite Sprite, Color Tint)>
            {
                ("Panel", Theme.Panel, Theme.Parchment),
                ("Panel Heavy", Theme.PanelHeavy, Theme.Parchment),
                ("Hollow", Theme.FrameHollow, Theme.Gold),
                ("Inset", Theme.Inset, Theme.ParchmentDeep),
            };

            foreach ((string name, Sprite sprite, Color tint) in frames)
            {
                // Wide sample + tall sample + the gap between them. Sizing the cell to anything
                // less makes neighbouring frames overlap rather than clip, which reads as a
                // broken 9-slice when the slicing is in fact fine.
                RectTransform cell = Col(row, name, Theme.Space.Tight, 0f, TextAnchor.UpperCenter);
                FixedWidth(cell, 236f + 60f + Theme.Space.Tight + Theme.Space.Base);

                TextMeshProUGUI label = UiKit.Caption(cell, name, TextAlignmentOptions.Center);
                label.color = Theme.Parchment;
                Fixed((RectTransform)label.transform, 22f);

                RectTransform strip = UiKit.Row(cell, "Sizes", Theme.Space.Tight, 0f, TextAnchor.UpperLeft);
                Fixed(strip, 200f);

                AddFrameSample(strip, sprite, tint, 236f, 60f);   // wide — stretches X
                AddFrameSample(strip, sprite, tint, 60f, 200f);   // tall — stretches Y
            }
        }

        private static void AddFrameSample(Transform parent, Sprite sprite, Color tint, float width, float height)
        {
            RectTransform rect = UiKit.NewRect(parent, "Sample");
            UiKit.SetSize(rect, width, height);
            FixedWidth(rect, width);

            var element = rect.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
        }

        private void BuildControls()
        {
            RectTransform body = Section("Controls", 180f);
            RectTransform row = UiKit.Row(body, "Controls", Theme.Space.Wide, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(row);

            Button primary = UiKit.SealButton(row, "Advance", () => Debug.Log("Styleguide: Advance"));
            FixedWidth((RectTransform)primary.transform, 260f);

            Button secondary = UiKit.SealButton(row, "Retreat", () => Debug.Log("Styleguide: Retreat"), 200f);
            FixedWidth((RectTransform)secondary.transform, 200f);

            Button disabled = UiKit.SealButton(row, "Locked", null, 200f);
            disabled.interactable = false;
            FixedWidth((RectTransform)disabled.transform, 200f);

            Button icon = UiKit.IconButton(row, Theme.Assets != null ? Theme.Assets.iconSettings : null,
                () => Debug.Log("Styleguide: Settings"));
            FixedWidth((RectTransform)icon.transform, 56f);

            Button close = UiKit.IconButton(row, Theme.Assets != null ? Theme.Assets.iconClose : null,
                () => Debug.Log("Styleguide: Close"));
            FixedWidth((RectTransform)close.transform, 56f);
        }

        private void BuildBars()
        {
            RectTransform body = Section("Bars — health thresholds", 190f);
            RectTransform panel = UiKit.Well(body, "Bars Well");
            UiKit.Stretch(panel);

            RectTransform column = Col(panel, "Bars", Theme.Space.Snug, Theme.Space.Wide, TextAnchor.UpperLeft);
            UiKit.Stretch(column);

            AddBar(column, "Full", 1.00f, Theme.Success);
            AddBar(column, "Wounded", 0.55f, Theme.Warn);
            AddBar(column, "Critical", 0.18f, Theme.Danger);

            // Zero is the case that reveals a bar which leaves a rounded stub behind when empty.
            AddBar(column, "Dead", 0.00f, Theme.Danger);
        }

        private static void AddBar(Transform parent, string label, float value, Color color)
        {
            RectTransform row = UiKit.Row(parent, "Bar " + label, Theme.Space.Snug, 0f, TextAnchor.MiddleLeft);
            Fixed(row, 28f);

            TextMeshProUGUI caption = UiKit.Caption(row, label, TextAlignmentOptions.Left);
            FixedWidth((RectTransform)caption.transform, 90f);

            BarView bar = UiKit.Bar(row, 420f, 20f, color);
            FixedWidth((RectTransform)bar.transform, 420f);
            bar.Value = value;

            TextMeshProUGUI readout = UiKit.Caption(row, $"{value:P0}", TextAlignmentOptions.Left);
            readout.color = Theme.InkSoft;
            FixedWidth((RectTransform)readout.transform, 70f);
        }

        private void BuildIcons()
        {
            RectTransform body = Section("Icons", 110f);
            RectTransform row = UiKit.Row(body, "Icons", Theme.Space.Base, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(row);

            ThemeAssets assets = Theme.Assets;
            if (assets == null)
            {
                TextMeshProUGUI warning = UiKit.Body(
                    row, "ThemeAssets not found — run Tools ▸ Binakayan Rising ▸ Rebuild Theme Assets.");
                warning.color = Theme.Danger;
                return;
            }

            var icons = new List<(string Name, Sprite Sprite)>
            {
                ("Reales", assets.iconReales),
                ("Rations", assets.iconRations),
                ("Scrap", assets.iconScrap),
                ("Attack", assets.iconAttack),
                ("Defense", assets.iconDefense),
                ("Move", assets.iconMovement),
                ("Range", assets.iconRange),
                ("Back", assets.iconBack),
                ("Settings", assets.iconSettings),
                ("Info", assets.iconInfo),
                ("Check", assets.iconCheck),
                ("Star", assets.iconStar),
            };

            foreach ((string name, Sprite sprite) in icons)
            {
                RectTransform cell = Col(row, name, Theme.Space.Hair, 0f, TextAnchor.UpperCenter);
                FixedWidth(cell, 72f);

                Image image = UiKit.Icon(cell, sprite, 48f, Theme.Gold);
                Fixed(image.rectTransform, 48f);

                TextMeshProUGUI label = UiKit.Caption(cell, name, TextAlignmentOptions.Center);
                label.color = Theme.Parchment;
                Fixed((RectTransform)label.transform, 20f);
            }
        }

        private void BuildMotifs()
        {
            RectTransform body = Section("Motifs — drawn in code, not imported", 220f);
            RectTransform row = UiKit.Row(body, "Motifs", Theme.Space.Wide, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(row);

            Image sigil = UiKit.Sigil(row, 180f, Theme.Gold);
            FixedWidth(sigil.rectTransform, 180f);

            Image sigilRed = UiKit.Sigil(row, 120f, Theme.Revolution);
            FixedWidth(sigilRed.rectTransform, 120f);

            RectTransform grainCell = UiKit.NewRect(row, "Grain");
            UiKit.SetSize(grainCell, 320f, 180f);
            FixedWidth(grainCell, 320f);

            var grain = grainCell.gameObject.AddComponent<Image>();
            grain.sprite = Theme.Grain;
            grain.type = Image.Type.Tiled;
            grain.color = Theme.Parchment;
            grain.raycastTarget = false;

            RectTransform diamondCell = UiKit.NewRect(row, "Diamond");
            UiKit.SetSize(diamondCell, 256f, 128f);
            FixedWidth(diamondCell, 256f);

            var diamond = diamondCell.gameObject.AddComponent<Image>();
            diamond.sprite = Theme.SoftDiamond;
            diamond.color = Theme.GoldBright;
            diamond.raycastTarget = false;
        }
    }
}
