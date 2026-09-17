using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BinakayanRising.EditorTools
{
    /// <summary>
    /// Applies import settings to everything under <c>Assets/_Project/Art/</c> automatically,
    /// so no imported sprite ever depends on someone remembering to set the Inspector by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The vendored Kenney packs are hundreds of individual PNGs. Clicking through them is not a
    /// realistic option, and a half-configured atlas is the usual reason UI art looks subtly wrong
    /// — blurry from mipmaps, washed out from a missing sRGB flag, or smeared from a bad 9-slice
    /// border. This postprocessor makes those settings a property of the folder a file lives in.
    /// </para>
    /// <para>
    /// <b>Nine-slice borders are derived, not guessed.</b> Kenney's Fantasy UI Borders frames are
    /// square and slice on exact thirds: the <c>Default</c> weight ships at 48x48 and the
    /// <c>Double</c> weight at 96x96, so the border is always <c>size / 3</c>. Dividers are
    /// horizontal strips and slice only on X. Deriving the value from the texture instead of
    /// hardcoding 16 or 32 means the rule survives Kenney re-exporting the pack at another size.
    /// </para>
    /// <para>
    /// The RPG pack's buttons and insets are not square and not symmetric — a button carries an
    /// 8-pixel drop shadow under it and 4 pixels of bevel on top — so their borders are measured
    /// from the pixels instead: see <see cref="MeasureBorder"/>.
    /// </para>
    /// </remarks>
    public sealed class UiArtPostprocessor : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/_Project/Art/";

        /// <summary>UI is authored against a 1920x1080 canvas at 1:1, so one pixel is one unit.</summary>
        private const float UiPixelsPerUnit = 100f;

        /// <summary>
        /// Board art must match <see cref="BinakayanRising.Gameplay.PlaceholderArt"/>, whose
        /// procedural sprites are built at 128 PPU. A mismatch would make imported tiles and
        /// fallback tiles different sizes on the same grid.
        /// </summary>
        private const float BoardPixelsPerUnit = 128f;

        /// <summary>
        /// Unit bodies are 64 pixels tall; at 80 PPU a figure stands 0.8 world units, most of a
        /// tile's width, which keeps a hat from reaching into the row of cells behind it.
        /// </summary>
        private const float UnitBodyPixelsPerUnit = 80f;

        /// <summary>
        /// Where a body sprite's ground point sits, as a fraction of its 48x64 canvas. It must
        /// match <c>GROUND_PIXEL</c> in <c>Tools/sprites/build_and_render.py</c>, which renders
        /// every figure with its feet on exactly this pixel so a unit stands on its cell centre.
        /// </summary>
        private static readonly Vector2 UnitBodyPivot = new Vector2(24f / 48f, 5f / 64f);

        /// <summary>
        /// Bumped whenever the rules below change, so Unity reimports the art they apply to
        /// instead of keeping settings baked by an older version.
        /// </summary>
        public override uint GetVersion()
        {
            return 4;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = IsUnitBody(assetPath)
                ? UnitBodyPixelsPerUnit
                : IsBoardArt(assetPath) ? BoardPixelsPerUnit : UiPixelsPerUnit;

            // Unit sprites are pixel art: bilinear filtering would blur each texel into its
            // neighbours and turn the one-pixel outline into a soft brown halo.
            importer.filterMode = IsUnitArt(assetPath) ? FilterMode.Point : FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;

            // Bleeds colour outward into fully transparent texels. Without it, bilinear filtering
            // samples the black those texels default to and every 9-sliced frame picks up a dark
            // one-pixel halo along its edges — roughly twice as visible in Linear as in Gamma.
            importer.alphaIsTransparency = true;

            // The Kenney PNGs are authored in sRGB. The project renders Linear, so leaving this
            // false makes the whole palette read washed out and pushes gold toward neon.
            importer.sRGBTexture = true;

            // Sliced Images REQUIRE a full-rect mesh. Unity's default tight mesh crops the sprite
            // to its opaque pixels, which silently defeats 9-slicing: the corners stretch instead
            // of holding. Nothing warns about this — the frame just looks smeared.
            var settingsForMesh = new TextureImporterSettings();
            importer.ReadTextureSettings(settingsForMesh);
            settingsForMesh.spriteMeshType = SpriteMeshType.FullRect;
            ApplyPivotOverride(assetPath, settingsForMesh);

            // The border has to be in place before the sprite is built. Set after import, in
            // OnPostprocessTexture, it only reaches the .meta and the sprite keeps a zero border
            // until something happens to reimport the file a second time.
            if (TryGetSliceBorder(assetPath, out Vector4 border))
            {
                settingsForMesh.spriteBorder = border;
            }

            importer.SetTextureSettings(settingsForMesh);

            // The project renders in Linear colour space. Compressing UI art costs more in
            // gradient banding on large parchment fills than it saves in memory at this size:
            // the whole vendored set is about 3 MB.
            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }

        private static bool IsBoardArt(string path)
        {
            return path.StartsWith(ArtRoot + "Board/", StringComparison.Ordinal);
        }

        private static bool IsUnitArt(string path)
        {
            return path.StartsWith(ArtRoot + "Units/", StringComparison.Ordinal);
        }

        private static bool IsUnitBody(string path)
        {
            return IsUnitArt(path) && path.EndsWith("/body.png", StringComparison.Ordinal);
        }

        /// <summary>
        /// Moves a sprite's pivot off centre where the consuming code requires it.
        /// </summary>
        /// <remarks>
        /// <see cref="BinakayanRising.Gameplay.Presentation.HealthBarView"/> scales its fill sprite
        /// along X to show remaining health, so the fill has to grow from its left edge. With the
        /// default centre pivot the bar drains symmetrically from the middle outward, which looks
        /// deliberate enough that it can survive review without anyone spotting it as a bug.
        /// Unit bodies pivot on their feet, so placing one at a cell centre stands it on the tile.
        /// </remarks>
        private static void ApplyPivotOverride(string path, TextureImporterSettings settings)
        {
            if (IsUnitBody(path))
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = UnitBodyPivot;
                return;
            }

            bool isLeftPivoted = path.EndsWith("/bar_fill.png", StringComparison.Ordinal)
                              || path.EndsWith("/barFill.png", StringComparison.Ordinal);

            if (!isLeftPivoted)
            {
                return;
            }

            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0f, 0.5f);
        }

        /// <summary>
        /// Returns the border a sprite should stretch from, or false if it should not be sliced.
        /// </summary>
        /// <remarks>
        /// Icons, unit tokens and terrain tiles must never be sliced — stretching their interior
        /// would distort the artwork rather than the frame around it.
        /// </remarks>
        private static bool TryGetSliceBorder(string path, out Vector4 border)
        {
            border = Vector4.zero;

            bool isFrame = path.StartsWith(ArtRoot + "UI/Frames/", StringComparison.Ordinal)
                        || path.StartsWith(ArtRoot + "UI/FramesDouble/", StringComparison.Ordinal);
            bool isMeasured = IsMeasuredSlice(path);
            if (!isFrame && !isMeasured)
            {
                return false;
            }

            // Preprocessing runs before Unity has decoded the file, so the source PNG is read
            // directly. LoadImage yields RGBA32 rows bottom-up, the same layout GetPixels32 has.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    return false;
                }

                return isMeasured ? TryMeasure(texture, out border) : TryThirds(path, texture, out border);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool TryMeasure(Texture2D texture, out Vector4 border)
        {
            border = MeasureBorder(texture);
            return true;
        }

        private static bool TryThirds(string path, Texture2D texture, out Vector4 border)
        {
            border = Vector4.zero;

            // Dividers are wide, short strips: they tile horizontally and must keep their
            // full height, so the vertical border stays zero.
            if (path.Contains("/Divider/"))
            {
                float x = Mathf.Floor(texture.width / 3f);
                border = new Vector4(x, 0f, x, 0f);
                return true;
            }

            // Panels, borders and hollow centres are square and slice on exact thirds.
            float inset = Mathf.Floor(Mathf.Min(texture.width, texture.height) / 3f);
            border = new Vector4(inset, inset, inset, inset);
            return true;
        }

        private static bool IsMeasuredSlice(string path)
        {
            const string Rpg = ArtRoot + "UI/Rpg/";
            return path.StartsWith(Rpg + "button", StringComparison.Ordinal)
                || path.StartsWith(Rpg + "panelInset", StringComparison.Ordinal);
        }

        /// <summary>
        /// Measures how far in from each edge a sprite stops being edge art, and returns that as its
        /// 9-slice border (left, bottom, right, top).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Bands.</b> Walking in from an edge along the quarter lines, the border starts where the
        /// sprite's interior colour starts. Quarter lines rather than the centre line, because the
        /// beige inset has a crack drawn at its top centre that is not part of the edge.
        /// </para>
        /// <para>
        /// <b>Corners.</b> A rounded corner reaches further along the outer rows than the band does:
        /// the button's top row only turns opaque six pixels in, past its four-pixel bevel. Every
        /// row inside the top and bottom bands (and a few rows beyond) is compared against the same
        /// row at the quarter line, and the side border grows to hold the last pixel that differs.
        /// Columns are treated the same way for the top and bottom borders.
        /// </para>
        /// <para>
        /// One pixel of margin is added so bilinear filtering at the seam samples uniform texels.
        /// </para>
        /// </remarks>
        private static Vector4 MeasureBorder(Texture2D texture)
        {
            const int CornerSlack = 3;

            int width = texture.width;
            int height = texture.height;
            Color32[] pixels = texture.GetPixels32();

            // Texture rows run bottom-up.
            Color32 interior = pixels[(height / 2 * width) + (width / 4)];
            int qx0 = width / 4;
            int qx1 = (3 * width) / 4;
            int qy0 = height / 4;
            int qy1 = (3 * height) / 4;

            int top = Mathf.Max(Band(pixels, width, qx0, height - 1, 0, -1, height, interior), Band(pixels, width, qx1, height - 1, 0, -1, height, interior));
            int bottom = Mathf.Max(Band(pixels, width, qx0, 0, 0, 1, height, interior), Band(pixels, width, qx1, 0, 0, 1, height, interior));
            int left = Mathf.Max(Band(pixels, width, 0, qy0, 1, 0, width, interior), Band(pixels, width, 0, qy1, 1, 0, width, interior));
            int right = Mathf.Max(Band(pixels, width, width - 1, qy0, -1, 0, width, interior), Band(pixels, width, width - 1, qy1, -1, 0, width, interior));

            int maxX = width / 3;
            int maxY = height / 3;

            // Corners are looked for only near the bands, so the small nicks the pack draws along
            // the middle of an edge stay in the stretched strip where they belong.
            int bandTop = top;
            int bandBottom = bottom;
            int bandLeft = left;
            int bandRight = right;
            int reachX = Mathf.Min(maxX, Mathf.Max(bandLeft, bandRight) + CornerSlack);
            int reachY = Mathf.Min(maxY, Mathf.Max(bandTop, bandBottom) + CornerSlack);

            for (int i = 0; i < reachY; i++)
            {
                if (i < bandTop + CornerSlack)
                {
                    int row = height - 1 - i;
                    left = Mathf.Max(left, Reach(pixels, width, 0, row, 1, 0, Mathf.Min(maxX, bandLeft + CornerSlack), pixels[(row * width) + qx0]));
                    right = Mathf.Max(right, Reach(pixels, width, width - 1, row, -1, 0, Mathf.Min(maxX, bandRight + CornerSlack), pixels[(row * width) + qx1]));
                }

                if (i < bandBottom + CornerSlack)
                {
                    left = Mathf.Max(left, Reach(pixels, width, 0, i, 1, 0, Mathf.Min(maxX, bandLeft + CornerSlack), pixels[(i * width) + qx0]));
                    right = Mathf.Max(right, Reach(pixels, width, width - 1, i, -1, 0, Mathf.Min(maxX, bandRight + CornerSlack), pixels[(i * width) + qx1]));
                }
            }

            for (int i = 0; i < reachX; i++)
            {
                if (i < bandLeft + CornerSlack)
                {
                    top = Mathf.Max(top, Reach(pixels, width, i, height - 1, 0, -1, Mathf.Min(maxY, bandTop + CornerSlack), pixels[(qy1 * width) + i]));
                    bottom = Mathf.Max(bottom, Reach(pixels, width, i, 0, 0, 1, Mathf.Min(maxY, bandBottom + CornerSlack), pixels[(qy0 * width) + i]));
                }

                if (i < bandRight + CornerSlack)
                {
                    int column = width - 1 - i;
                    top = Mathf.Max(top, Reach(pixels, width, column, height - 1, 0, -1, Mathf.Min(maxY, bandTop + CornerSlack), pixels[(qy1 * width) + column]));
                    bottom = Mathf.Max(bottom, Reach(pixels, width, column, 0, 0, 1, Mathf.Min(maxY, bandBottom + CornerSlack), pixels[(qy0 * width) + column]));
                }
            }

            return new Vector4(
                Mathf.Min(left + 1, maxX),
                Mathf.Min(bottom + 1, maxY),
                Mathf.Min(right + 1, maxX),
                Mathf.Min(top + 1, maxY));
        }

        /// <summary>Steps from an edge until the interior colour; returns how many steps that took.</summary>
        private static int Band(Color32[] pixels, int width, int x, int y, int dx, int dy, int limit, Color32 interior)
        {
            int steps = 0;
            while (steps < limit / 2 && !Same(pixels[(y * width) + x], interior))
            {
                x += dx;
                y += dy;
                steps++;
            }

            return steps;
        }

        /// <summary>
        /// Returns one past the furthest pixel, within <paramref name="limit"/> steps of the edge,
        /// that differs from <paramref name="reference"/>.
        /// </summary>
        private static int Reach(Color32[] pixels, int width, int x, int y, int dx, int dy, int limit, Color32 reference)
        {
            int reach = 0;
            for (int step = 0; step < limit; step++)
            {
                if (!Same(pixels[((y + (dy * step)) * width) + x + (dx * step)], reference))
                {
                    reach = step + 1;
                }
            }

            return reach;
        }

        private static bool Same(Color32 a, Color32 b)
        {
            // Tight: the button bevel is 233 against a 229 face, so a looser match reads the bevel as face.
            const int Tolerance = 2;
            return Mathf.Abs(a.r - b.r) <= Tolerance
                && Mathf.Abs(a.g - b.g) <= Tolerance
                && Mathf.Abs(a.b - b.b) <= Tolerance
                && Mathf.Abs(a.a - b.a) <= Tolerance;
        }
    }
}
