using System;
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

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot, StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = IsBoardArt(assetPath) ? BoardPixelsPerUnit : UiPixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
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
            importer.SetTextureSettings(settingsForMesh);

            // The project renders in Linear colour space. Compressing UI art costs more in
            // gradient banding on large parchment fills than it saves in memory at this size:
            // the whole vendored set is about 3 MB.
            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }

        /// <summary>
        /// Sets the 9-slice border after import, when the texture's real dimensions are known.
        /// </summary>
        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.StartsWith(ArtRoot, StringComparison.Ordinal))
            {
                return;
            }

            if (!TryGetSliceBorder(assetPath, texture, out Vector4 border))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            if (importer.spriteBorder != border)
            {
                importer.spriteBorder = border;
            }
        }

        private static bool IsBoardArt(string path)
        {
            return path.StartsWith(ArtRoot + "Board/", StringComparison.Ordinal);
        }

        /// <summary>
        /// Moves a sprite's pivot off centre where the consuming code requires it.
        /// </summary>
        /// <remarks>
        /// <see cref="BinakayanRising.Gameplay.Presentation.HealthBarView"/> scales its fill sprite
        /// along X to show remaining health, so the fill has to grow from its left edge. With the
        /// default centre pivot the bar drains symmetrically from the middle outward, which looks
        /// deliberate enough that it can survive review without anyone spotting it as a bug.
        /// </remarks>
        private static void ApplyPivotOverride(string path, TextureImporterSettings settings)
        {
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
        private static bool TryGetSliceBorder(string path, Texture2D texture, out Vector4 border)
        {
            border = Vector4.zero;

            bool isFrame = path.StartsWith(ArtRoot + "UI/Frames/", StringComparison.Ordinal)
                        || path.StartsWith(ArtRoot + "UI/FramesDouble/", StringComparison.Ordinal);
            if (!isFrame)
            {
                return false;
            }

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
    }
}
