using BinakayanRising.Gameplay;
using TMPro;
using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// The single source of truth for how Binakayan Rising looks: colour, type, spacing, and the
    /// sprites every widget is built from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The rule this class exists to enforce:</b> no colour literal, font size, or pixel margin
    /// appears anywhere else in the UI layer. The previous HUD failed on exactly this — roughly
    /// forty <c>new Color(...)</c> literals and every offset written as a raw pixel number, which
    /// is why it read as a debug harness and why it collapsed on any screen that was not 1080p.
    /// </para>
    /// <para>
    /// <b>Colour and the Linear pipeline.</b> The project renders in Linear colour space. Writing
    /// <c>new Color(0.55f, 0.18f, 0.13f)</c> there does not give the colour a designer picked in
    /// sRGB — it gives a noticeably darker one. Every palette entry is therefore declared as
    /// <see cref="Color32"/> from its sRGB hex bytes and implicitly converted, which is the
    /// conversion Unity applies to Inspector-picked colours too. Match hex values to the palette
    /// table, never to floats read off another screen.
    /// </para>
    /// <para>
    /// <b>Fallback.</b> Every sprite accessor degrades to
    /// <see cref="PlaceholderArt"/>'s procedural shapes when <see cref="ThemeAssets"/> is missing or
    /// incomplete. A fresh clone that has never run <c>ThemeSetup</c> still runs and is still
    /// legible; it just loses the ornament.
    /// </para>
    /// <para>
    /// <b>Statics and domain reload.</b> This project has <c>Enter Play Mode Options</c> enabled
    /// with domain reload disabled, so statics survive between play sessions and would otherwise
    /// hand the second run textures owned by a destroyed first run.
    /// <see cref="ResetStatics"/> runs on subsystem registration to clear them.
    /// </para>
    /// </remarks>
    public static class Theme
    {
        // ------------------------------------------------------------------ palette

        /// <summary>Aged paper. The default surface colour for every panel.</summary>
        public static readonly Color Parchment = new Color32(0xE8, 0xDC, 0xC0, 0xFF);

        /// <summary>Parchment in shadow — recessed wells, list stripes, panel undersides.</summary>
        public static readonly Color ParchmentDeep = new Color32(0xD6, 0xC3, 0x9C, 0xFF);

        /// <summary>Iron-gall ink. Body text and sprite outlines.</summary>
        public static readonly Color Ink = new Color32(0x2B, 0x21, 0x18, 0xFF);

        /// <summary>Faded ink, for secondary and disabled text.</summary>
        public static readonly Color InkSoft = new Color32(0x5A, 0x4A, 0x38, 0xFF);

        /// <summary>Katipunan red. The player's team, primary actions, health.</summary>
        public static readonly Color Revolution = new Color32(0x8C, 0x2E, 0x22, 0xFF);

        /// <summary>Revolution red in shadow — pressed states and frame borders.</summary>
        public static readonly Color RevolutionDark = new Color32(0x5E, 0x1D, 0x15, 0xFF);

        /// <summary>Tarnished gold. Ornament, rank, currency, the sun sigil.</summary>
        public static readonly Color Gold = new Color32(0xC9, 0xA2, 0x27, 0xFF);

        /// <summary>Lit gold, for hover states, highlights and critical-hit text.</summary>
        public static readonly Color GoldBright = new Color32(0xF0, 0xD2, 0x64, 0xFF);

        /// <summary>Colonial blue. The Spanish force.</summary>
        public static readonly Color Colonial = new Color32(0x2E, 0x4A, 0x6B, 0xFF);

        /// <summary>Colonial blue, lit — Spanish highlights and selection.</summary>
        public static readonly Color ColonialLight = new Color32(0x4A, 0x6E, 0x99, 0xFF);

        /// <summary>Healthy health bars, correct quiz answers, positive deltas.</summary>
        public static readonly Color Success = new Color32(0x4E, 0x7A, 0x3A, 0xFF);

        /// <summary>Wounded health bars and cautionary readouts.</summary>
        public static readonly Color Warn = new Color32(0xC0, 0x8A, 0x2E, 0xFF);

        /// <summary>Critical health, wrong answers, defeat.</summary>
        public static readonly Color Danger = new Color32(0xA3, 0x2E, 0x22, 0xFF);

        /// <summary>The scrim drawn behind a modal, dimming whatever is underneath.</summary>
        public static readonly Color Scrim = new Color32(0x1A, 0x14, 0x0E, 0xC4);

        /// <summary>Sky behind the board. A warm dusk, not a neutral grey.</summary>
        public static readonly Color Backdrop = new Color32(0x24, 0x1D, 0x18, 0xFF);

        /// <summary>
        /// The tutorial's dimming layer. Lighter than <see cref="Scrim"/>: the player still has to
        /// read the board through it, just not act on it.
        /// </summary>
        public static readonly Color SpotlightScrim = new Color32(0x0E, 0x0B, 0x08, 0xA8);

        /// <summary>The pulsing rim drawn around whatever the tutorial is pointing at.</summary>
        public static readonly Color SpotlightRing = new Color32(0xF0, 0xD2, 0x64, 0xFF);

        /// <summary>
        /// Recruiting tiers, used for a recruit's frame and label. Hero is the gold the rest of
        /// the interface already means "best" with; Rare is an indigo kept clear of the
        /// colonial blue, which marks the enemy.
        /// </summary>
        public static class Rarity
        {
            public static readonly Color Common = new Color32(0x7A, 0x68, 0x52, 0xFF);
            public static readonly Color Rare = new Color32(0x4B, 0x4F, 0x9C, 0xFF);
            public static readonly Color Hero = new Color32(0xC9, 0xA2, 0x27, 0xFF);

            public static Color Of(Core.Content.UnitRarity rarity)
            {
                switch (rarity)
                {
                    case Core.Content.UnitRarity.Hero: return Hero;
                    case Core.Content.UnitRarity.Rare: return Rare;
                    default: return Common;
                }
            }
        }

        /// <summary>
        /// The encampment's painted ground and markers. Pixel art: a few flat tones per surface,
        /// picked to sit under the Blender-rendered buildings without fighting their palette.
        /// </summary>
        public static class Camp
        {
            public static readonly Color32 JungleDark = new Color32(0x34, 0x4C, 0x24, 0xFF);
            public static readonly Color32 Jungle = new Color32(0x40, 0x5C, 0x2A, 0xFF);
            public static readonly Color32 JungleLight = new Color32(0x4C, 0x6A, 0x30, 0xFF);

            public static readonly Color32 ClearingDark = new Color32(0x6A, 0x7C, 0x3C, 0xFF);
            public static readonly Color32 Clearing = new Color32(0x78, 0x8A, 0x44, 0xFF);
            public static readonly Color32 ClearingLight = new Color32(0x86, 0x96, 0x4C, 0xFF);

            public static readonly Color32 PathDark = new Color32(0x8E, 0x76, 0x4E, 0xFF);
            public static readonly Color32 Path = new Color32(0xA2, 0x88, 0x5A, 0xFF);
            public static readonly Color32 PathLight = new Color32(0xB2, 0x98, 0x68, 0xFF);

            /// <summary>The marker arrow over the objective, and its outline.</summary>
            public static readonly Color32 Marker = new Color32(0xF0, 0xD2, 0x64, 0xFF);
            public static readonly Color32 MarkerShade = new Color32(0xC9, 0xA2, 0x27, 0xFF);
            public static readonly Color32 MarkerInk = new Color32(0x1E, 0x16, 0x12, 0xFF);

            /// <summary>The footprint drawn round a building under the cursor.</summary>
            public static readonly Color32 Hover = new Color32(0xF0, 0xD2, 0x64, 0xC0);

            /// <summary>The walk target's ring.</summary>
            public static readonly Color32 Target = new Color32(0xE9, 0xE2, 0xD0, 0xA0);

            /// <summary>A figure's contact shadow.</summary>
            public static readonly Color32 Shadow = new Color32(0x10, 0x14, 0x08, 0x60);
        }

        // ------------------------------------------------------------------ canvas order

        /// <summary>
        /// Sorting orders for the screen-space canvases, bottom to top.
        /// </summary>
        /// <remarks>
        /// The campaign screens sit under everything; world labels sit under the HUD so panels and
        /// the outcome scrim cover them; the tutorial sits over the HUD so its scrim can dim it; the
        /// How-to-Play deck sits over both. Settings, confirmations and toasts can open from
        /// anywhere, so they sit on top of all of it.
        /// </remarks>
        public static class Layer
        {
            public const int Shell = 80;
            public const int WorldLabels = 90;
            public const int Hud = 100;
            public const int Tutorial = 200;
            public const int Deck = 210;

            /// <summary>The battle's pause menu: over the HUD and tutorial, under the Settings it opens.</summary>
            public const int Pause = 215;
            public const int Settings = 220;
            public const int Modal = 230;
            public const int Toast = 240;
        }

        // ------------------------------------------------------------------ type

        /// <summary>
        /// Type sizes, in canvas units against the 1920x1080 reference resolution.
        /// </summary>
        /// <remarks>
        /// A four-step scale on roughly a 1.4 ratio. Anything needing a size not on this scale is
        /// a sign the layout wants rethinking rather than a fifth size adding.
        /// </remarks>
        public static class Type
        {
            /// <summary>Screen titles. Display face, letterspaced.</summary>
            public const float Display = 64f;

            /// <summary>Panel and section headings.</summary>
            public const float Title = 34f;

            /// <summary>Buttons, unit names, column headers.</summary>
            public const float Heading = 24f;

            /// <summary>Default body copy and numbers.</summary>
            public const float Body = 18f;

            /// <summary>Log lines, captions, stat labels.</summary>
            public const float Small = 14f;

            /// <summary>Display type is set in caps; tracking keeps it from crowding.</summary>
            public const float DisplayTracking = 12f;
        }

        // ------------------------------------------------------------------ spacing

        /// <summary>
        /// Spacing steps, in canvas units. Multiples of four so nothing lands on a half pixel.
        /// </summary>
        public static class Space
        {
            public const float Hair = 4f;
            public const float Tight = 8f;
            public const float Snug = 12f;
            public const float Base = 16f;
            public const float Wide = 24f;
            public const float Loose = 32f;
            public const float Huge = 48f;

            /// <summary>Inner padding from a 9-sliced frame's edge to its content.</summary>
            public const float FramePadding = 28f;
        }

        /// <summary>Canvas reference resolution. Every size above is expressed against this.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // ------------------------------------------------------------------ assets

        private static ThemeAssets assets;
        private static bool assetsProbed;
        private static Sprite sigilSprite;
        private static Sprite grainSprite;
        private static Sprite softDiamondSprite;
        private static Sprite barTrackSprite;
        private static Sprite barFillSprite;

        /// <summary>
        /// The generated theme asset, or null when setup has never been run.
        /// </summary>
        public static ThemeAssets Assets
        {
            get
            {
                if (!assetsProbed)
                {
                    assetsProbed = true;
                    assets = Resources.Load<ThemeAssets>(ThemeAssets.ResourcePath);

                    if (assets == null)
                    {
                        Debug.LogWarning(
                            "Theme: no ThemeAssets found at Resources/" + ThemeAssets.ResourcePath +
                            ". Falling back to procedural art. Run " +
                            "Tools > Binakayan Rising > Rebuild Theme Assets to generate it.");
                    }
                }

                return assets;
            }
        }

        /// <summary>True when themed art and fonts are available.</summary>
        public static bool IsThemed => Assets != null && Assets.IsUsable;

        /// <summary>Ornate filled panel. The default surface for dialogs and cards.</summary>
        public static Sprite Panel => Pick(Assets != null ? Assets.panel : null);

        /// <summary>Heavier frame, for the outermost edge of a full screen.</summary>
        public static Sprite PanelHeavy => Pick(Assets != null ? Assets.panelHeavy : null);

        /// <summary>Frame with a transparent centre, for overlaying the board or artwork.</summary>
        public static Sprite FrameHollow => Pick(Assets != null ? Assets.frameHollow : null);

        /// <summary>Recessed well, for list backgrounds and stat readouts.</summary>
        public static Sprite Inset => Pick(Assets != null ? Assets.inset : null);

        /// <summary>Horizontal rule with ornamental ends.</summary>
        public static Sprite Divider => Pick(Assets != null ? Assets.divider : null);

        /// <summary>Button face in its resting state.</summary>
        public static Sprite Button => Pick(Assets != null ? Assets.button : null);

        /// <summary>Button face while held.</summary>
        public static Sprite ButtonPressed => Pick(Assets != null ? Assets.buttonPressed : null);

        /// <summary>Button face when the action is unavailable.</summary>
        public static Sprite ButtonDisabled => Pick(Assets != null ? Assets.buttonDisabled : null);

        /// <summary>Small square frame for icon buttons and roster slots.</summary>
        public static Sprite Slot => Pick(Assets != null ? Assets.slot : null);

        /// <summary>
        /// Outer casing of a bar: a rounded, ink-rimmed trough.
        /// </summary>
        /// <remarks>
        /// Generated rather than imported. Kenney's bar art is a three-part strip (left cap, tiling
        /// middle, right cap) that does not 9-slice, and its fill sprite would need a hand-set
        /// left-edge pivot to drain correctly — a setting whose absence looks intentional rather
        /// than broken, so it can pass review while being wrong. Drawing both here makes the
        /// geometry and the pivot a property of the code instead of a property of a .meta file.
        /// </remarks>
        public static Sprite BarTrack
        {
            get
            {
                if (barTrackSprite == null)
                {
                    barTrackSprite = BuildBar(64, 16, rimThickness: 2f, pivotX: 0.5f);
                }

                return barTrackSprite;
            }
        }

        /// <summary>
        /// Fill drawn inside a bar casing. Pivoted on its <b>left edge</b> so scaling X drains the
        /// bar from right to left rather than symmetrically from the centre.
        /// </summary>
        public static Sprite BarFill
        {
            get
            {
                if (barFillSprite == null)
                {
                    barFillSprite = BuildBar(64, 16, rimThickness: 0f, pivotX: 0f);
                }

                return barFillSprite;
            }
        }

        /// <summary>Currency and interface icons. Null when setup has not run; callers skip the icon.</summary>
        public static Sprite IconReales => Assets != null ? Assets.iconReales : null;

        public static Sprite IconRations => Assets != null ? Assets.iconRations : null;

        public static Sprite IconScrap => Assets != null ? Assets.iconScrap : null;

        public static Sprite IconSettings => Assets != null ? Assets.iconSettings : null;

        public static Sprite IconClose => Assets != null ? Assets.iconClose : null;

        public static Sprite IconBack => Assets != null ? Assets.iconBack : null;

        public static Sprite IconCheck => Assets != null ? Assets.iconCheck : null;

        public static Sprite IconStar => Assets != null ? Assets.iconStar : null;

        /// <summary>Display face, for titles and buttons.</summary>
        public static TMP_FontAsset DisplayFont => Assets != null ? Assets.displayFont : null;

        /// <summary>Body face, for copy and numbers.</summary>
        public static TMP_FontAsset BodyFont => Assets != null ? Assets.bodyFont : null;

        /// <summary>Body face in bold.</summary>
        public static TMP_FontAsset BodyFontBold =>
            Assets != null && Assets.bodyFontBold != null ? Assets.bodyFontBold : BodyFont;

        /// <summary>Falls back to a plain stretched pixel when a themed sprite is absent.</summary>
        private static Sprite Pick(Sprite themed)
        {
            return themed != null ? themed : PlaceholderArt.Pixel;
        }

        // ------------------------------------------------------------------ generated motifs

        /// <summary>
        /// The eight-rayed sun of liberty, drawn procedurally and tinted at the use site.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the one mark that has to be ours. The vendored Kenney packs are deliberately
        /// generic fantasy furniture; nothing in them is Filipino, and dropping a stock crest onto
        /// a game about the Katipunan would be the wrong call both historically and visually. So
        /// the sun is rasterised here from its own geometry: eight rays for the eight provinces
        /// placed under martial law in 1896, around a plain disc.
        /// </para>
        /// <para>
        /// Drawn white so callers tint it — gold on parchment for ornament, ink for a watermark.
        /// </para>
        /// </remarks>
        public static Sprite Sigil
        {
            get
            {
                if (sigilSprite == null)
                {
                    sigilSprite = BuildSigil(256, rayCount: 8);
                }

                return sigilSprite;
            }
        }

        /// <summary>
        /// A tileable low-contrast noise field, laid over flat fills so parchment reads as paper
        /// rather than as a solid rectangle.
        /// </summary>
        /// <remarks>
        /// The single cheapest thing that stops a UI looking synthetic. Drawn at low alpha and
        /// tiled, so one 128px texture covers any panel size.
        /// </remarks>
        public static Sprite Grain
        {
            get
            {
                if (grainSprite == null)
                {
                    grainSprite = BuildGrain(128);
                }

                return grainSprite;
            }
        }

        /// <summary>
        /// A feathered isometric diamond, for deployment-zone glow and selection pulses.
        /// </summary>
        /// <remarks>
        /// <see cref="PlaceholderArt.Tile"/> has a hard darkened rim because it is a floor tile.
        /// This one falls off smoothly to nothing, so stacking it over a tile reads as light
        /// rather than as a second tile.
        /// </remarks>
        public static Sprite SoftDiamond
        {
            get
            {
                if (softDiamondSprite == null)
                {
                    softDiamondSprite = BuildSoftDiamond(128, 64);
                }

                return softDiamondSprite;
            }
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return texture;
        }

        /// <summary>
        /// Rasterises a rayed sun: a central disc, plus <paramref name="rayCount"/> tapering rays
        /// whose width falls off with angular distance from each ray's axis.
        /// </summary>
        private static Sprite BuildSigil(int size, int rayCount)
        {
            Texture2D texture = NewTexture(size, size);
            var pixels = new Color32[size * size];

            float half = size * 0.5f;
            float discRadius = size * 0.20f;
            float rayInner = size * 0.17f;
            float rayOuter = size * 0.47f;
            float feather = 1.5f / half;
            float rayStep = Mathf.PI * 2f / rayCount;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // The central disc, with a feathered edge so it does not alias.
                    float alpha = Mathf.InverseLerp(discRadius, discRadius - 2f, distance);

                    if (distance >= rayInner && distance <= rayOuter)
                    {
                        // Angular distance to the nearest ray axis, wrapped into +/- half a step.
                        float angle = Mathf.Atan2(dy, dx);
                        float offset = Mathf.Repeat(angle + rayStep * 0.5f, rayStep) - rayStep * 0.5f;

                        // Rays taper: wide at the hub, needle-thin at the tip.
                        float along = Mathf.InverseLerp(rayInner, rayOuter, distance);
                        float halfWidth = Mathf.Lerp(rayStep * 0.30f, rayStep * 0.055f, along);

                        float rayAlpha = Mathf.InverseLerp(halfWidth, halfWidth * 0.55f, Mathf.Abs(offset));
                        rayAlpha *= Mathf.InverseLerp(rayOuter, rayOuter - 6f, distance);
                        alpha = Mathf.Max(alpha, rayAlpha);
                    }

                    alpha *= Mathf.Clamp01(1f - feather);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        /// <summary>
        /// Builds a seamlessly tileable grain field from summed value noise.
        /// </summary>
        /// <remarks>
        /// Seamlessness comes from sampling <see cref="Mathf.PerlinNoise"/> on a torus: the
        /// coordinates wrap at the texture edge, so the left column continues into the right.
        /// </remarks>
        private static Sprite BuildGrain(int size)
        {
            Texture2D texture = NewTexture(size, size);
            texture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color32[size * size];

            const int Octaves = 3;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = 0f;
                    float amplitude = 0.5f;
                    float frequency = 4f;

                    for (int octave = 0; octave < Octaves; octave++)
                    {
                        // Wrap by sampling the same period twice and blending, which keeps the
                        // seam invisible without needing a true tiling noise implementation.
                        float u = x / (float)size * frequency;
                        float v = y / (float)size * frequency;
                        float a = Mathf.PerlinNoise(u, v);
                        float b = Mathf.PerlinNoise(u - frequency, v);
                        float c = Mathf.PerlinNoise(u, v - frequency);
                        float d = Mathf.PerlinNoise(u - frequency, v - frequency);

                        float fx = x / (float)size;
                        float fy = y / (float)size;
                        float top = Mathf.Lerp(a, b, fx);
                        float bottom = Mathf.Lerp(c, d, fx);
                        value += Mathf.Lerp(top, bottom, fy) * amplitude;

                        amplitude *= 0.5f;
                        frequency *= 2f;
                    }

                    // Centre on mid-grey: the grain is drawn as an overlay, so it must darken and
                    // lighten the surface underneath by equal amounts.
                    byte level = (byte)(Mathf.Clamp01(value) * 255f);
                    pixels[y * size + x] = new Color32(level, level, level, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        /// <summary>
        /// Rasterises an isometric diamond that fades smoothly to nothing at its edge.
        /// </summary>
        private static Sprite BuildSoftDiamond(int width, int height)
        {
            Texture2D texture = NewTexture(width, height);
            var pixels = new Color32[width * height];

            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f - halfWidth) / halfWidth;
                    float ny = (y + 0.5f - halfHeight) / halfHeight;

                    // Taxicab distance: 0 at the centre, 1 on the diamond's edge.
                    float distance = Mathf.Abs(nx) + Mathf.Abs(ny);
                    float alpha = Mathf.Clamp01(1f - distance);

                    // Square the falloff so the glow concentrates toward the middle instead of
                    // reading as a flat plate with a soft border.
                    alpha *= alpha;

                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                PlaceholderArt.PixelsPerUnit);
        }

        /// <summary>
        /// Rasterises a horizontal capsule, optionally with a darker rim.
        /// </summary>
        /// <param name="pivotX">
        /// Normalised X pivot. Zero puts the pivot on the left edge, which is what a bar fill needs
        /// so that scaling it drains from one side.
        /// </param>
        /// <remarks>
        /// Returned with a 9-slice border, so one 64x16 texture stretches to any bar length without
        /// distorting the rounded caps.
        /// </remarks>
        private static Sprite BuildBar(int width, int height, float rimThickness, float pivotX)
        {
            Texture2D texture = NewTexture(width, height);
            var pixels = new Color32[width * height];

            float radius = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Distance to the capsule's spine: the segment between the two cap centres.
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float spineX = Mathf.Clamp(px, radius, width - radius);
                    float distance = Mathf.Sqrt((px - spineX) * (px - spineX) + (py - radius) * (py - radius));

                    float alpha = Mathf.InverseLerp(radius, radius - 1f, distance);

                    // The rim is drawn as a darker band just inside the outer edge, so the trough
                    // reads as recessed rather than as a flat block of colour.
                    float shade = 1f;
                    if (rimThickness > 0f)
                    {
                        float rim = Mathf.InverseLerp(radius - rimThickness - 1f, radius - 1f, distance);
                        shade = Mathf.Lerp(1f, 0.45f, rim);
                    }

                    byte level = (byte)(Mathf.Clamp01(shade) * 255f);
                    pixels[y * width + x] = new Color32(level, level, level, (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(pivotX, 0.5f),
                100f,
                extrude: 0,
                meshType: SpriteMeshType.FullRect,
                // Slice past the rounded caps so only the straight middle stretches.
                border: new Vector4(radius + 1f, 0f, radius + 1f, 0f));

            return sprite;
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>
        /// Drops every cached texture and asset handle so a second play session rebuilds them.
        /// </summary>
        /// <remarks>
        /// Required because this project disables domain reload between play sessions. Without it,
        /// the second run inherits sprites whose textures Unity destroyed when the first run ended,
        /// which surfaces as pink or invisible UI that only reproduces on the second press of Play.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            assets = null;
            assetsProbed = false;
            sigilSprite = null;
            grainSprite = null;
            softDiamondSprite = null;
            barTrackSprite = null;
            barFillSprite = null;
        }
    }
}
