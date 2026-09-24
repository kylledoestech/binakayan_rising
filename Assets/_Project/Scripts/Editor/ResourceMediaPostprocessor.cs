using System;
using UnityEditor;
using UnityEngine;

namespace BinakayanRising.EditorTools
{
    /// <summary>
    /// Import settings for the pictures and sounds loaded by name from <c>Resources/</c>: the
    /// title splash, cutscene illustrations, music and the added sound effects (#46, #49).
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are loaded with <c>Resources.Load</c> rather than assigned in the Inspector, so a
    /// fresh file works the moment it is imported. What they need instead is the right import
    /// settings, and like <see cref="UiArtPostprocessor"/> this makes those a property of the
    /// folder rather than of someone remembering to click.
    /// </para>
    /// <para>
    /// The splash is pixel art scaled up by whole numbers: point filtering and no compression
    /// keep every block crisp; bilinear or DXT would smear the edges the render snapped to.
    /// Music streams from disk, so a three-minute loop is not held decoded in memory; the short
    /// effects are decompressed on load so they start on the frame they are asked for.
    /// </para>
    /// </remarks>
    public sealed class ResourceMediaPostprocessor : AssetPostprocessor
    {
        private const string ResourcesRoot = "Assets/_Project/Resources/";

        public override uint GetVersion()
        {
            return 1;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ResourcesRoot + "Splash/", StringComparison.Ordinal)
                && !assetPath.StartsWith(ResourcesRoot + "Cutscenes/", StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.isReadable = false;

            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.maxTextureSize = 2048;
            importer.SetPlatformTextureSettings(settings);
        }

        private void OnPreprocessAudio()
        {
            bool music = assetPath.StartsWith(ResourcesRoot + "Music/", StringComparison.Ordinal);
            bool effect = assetPath.StartsWith(ResourcesRoot + "Sfx/", StringComparison.Ordinal);
            if (!music && !effect)
            {
                return;
            }

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = music;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? 0.5f : 0.7f;
            importer.defaultSampleSettings = settings;
        }
    }
}
