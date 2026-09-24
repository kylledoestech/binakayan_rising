using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BinakayanRising.Core.Localization;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BinakayanRising.EditorTools
{
    /// <summary>
    /// One-shot project setup: imports TextMeshPro's resources, bakes SDF font assets from the
    /// vendored TTFs, generates <see cref="ThemeAssets"/>, and registers the sorting layers the
    /// board needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything here is idempotent and headless, so it can run from the editor menu, from the
    /// MCP bridge, or from <c>-batchmode -executeMethod</c>. Re-running after adding art picks the
    /// new files up; re-running with nothing changed does nothing.
    /// </para>
    /// <para>
    /// <b>It is deliberately two passes.</b> <see cref="AssetDatabase.ImportPackage"/> is
    /// asynchronous, and TMP's font-asset API needs <c>TMP Settings</c> to already exist on disk.
    /// Doing both in one call works from a live editor and fails from batchmode, because
    /// <c>-quit</c> tears the editor down before the import callback fires. Splitting them means
    /// the same two entry points behave identically either way.
    /// </para>
    /// </remarks>
    public static class ThemeSetup
    {
        private const string ArtRoot = "Assets/_Project/Art";
        private const string FontRoot = "Assets/_Project/Fonts";
        private const string ResourceRoot = "Assets/_Project/Resources";
        private const string ThemeAssetPath = ResourceRoot + "/ThemeAssets.asset";
        private const string UnitArtRoot = ArtRoot + "/Units";
        private const string CampArtRoot = ArtRoot + "/Encampment";

        /// <summary>
        /// Sampling size for the SDF atlas. Large enough that Cinzel's thin serifs survive, small
        /// enough that both families fit one 1024 atlas each.
        /// </summary>
        private const int FontSamplingSize = 90;

        private const int FontAtlasPadding = 9;
        private const int FontAtlasSize = 1024;

        /// <summary>Characters baked into every font atlas. See <see cref="BakedGlyphs"/>.</summary>
        private const string BakedCharacters = BakedGlyphs.All;

        // ------------------------------------------------------------------ entry points

        /// <summary>Runs both passes. Safe from a live editor; see the class remarks for batchmode.</summary>
        [MenuItem("Tools/Binakayan Rising/Rebuild Theme Assets", priority = 20)]
        public static void RunAll()
        {
            Pass1ImportResources();
            Pass2BuildTheme();
        }

        /// <summary>
        /// Pass one: import TMP's essential resources and force the vendored art through the
        /// import pipeline.
        /// </summary>
        [MenuItem("Tools/Binakayan Rising/Setup — Pass 1 (import resources)", priority = 21)]
        public static void Pass1ImportResources()
        {
            ImportTextMeshProEssentials();
            ReimportArt();
            AssetDatabase.SaveAssets();
            Debug.Log("ThemeSetup: pass 1 complete.");
        }

        /// <summary>
        /// Pass two: bake fonts, generate the theme asset, register sorting layers.
        /// </summary>
        [MenuItem("Tools/Binakayan Rising/Setup — Pass 2 (build theme)", priority = 22)]
        public static void Pass2BuildTheme()
        {
            if (TMP_Settings.instance == null)
            {
                Debug.LogError(
                    "ThemeSetup: TMP Settings are missing. Run pass 1 first — every TextMeshPro " +
                    "component created before the essential resources exist renders nothing.");
                return;
            }

            AddSortingLayers();
            List<TMP_FontAsset> fonts = BuildFontAssets();
            BuildThemeAssets(fonts);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ThemeSetup: pass 2 complete.");
        }

        // ------------------------------------------------------------------ TextMeshPro

        /// <summary>
        /// Imports TMP's shaders, default font and settings asset from the installed package.
        /// </summary>
        /// <remarks>
        /// The package folder carries a resolved-version hash in its name, so the path is asked for
        /// rather than hardcoded — a pinned hash breaks silently on the next package update.
        /// </remarks>
        private static void ImportTextMeshProEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                return;
            }

            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.unity.ugui");

            if (package == null)
            {
                Debug.LogError("ThemeSetup: com.unity.ugui is not installed; cannot import TMP resources.");
                return;
            }

            string packagePath = Path.Combine(
                package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");

            if (!File.Exists(packagePath))
            {
                Debug.LogError($"ThemeSetup: TMP essential resources not found at {packagePath}.");
                return;
            }

            AssetDatabase.ImportPackage(packagePath, interactive: false);
            Debug.Log("ThemeSetup: importing TMP essential resources (asynchronous).");
        }

        /// <summary>
        /// Bakes an SDF font asset for each vendored TTF that the theme needs.
        /// </summary>
        private static List<TMP_FontAsset> BuildFontAssets()
        {
            var built = new List<TMP_FontAsset>();

            foreach (string family in new[]
            {
                "Cinzel-Bold",
                "Spectral-Regular", "Spectral-Bold",
            })
            {
                TMP_FontAsset asset = BuildFontAsset(family);
                if (asset != null)
                {
                    built.Add(asset);
                }
            }

            return built;
        }

        private static TMP_FontAsset BuildFontAsset(string family)
        {
            string ttfPath = $"{FontRoot}/{family}.ttf";
            string assetPath = $"{FontRoot}/{family} SDF.asset";

            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null && existing.characterTable.Count > 0)
            {
                return existing;
            }

            if (existing != null)
            {
                // An atlas with no glyphs renders every string as nothing at all, with no error
                // at the use site. Rebuild rather than hand back a font asset that is silently
                // empty.
                AssetDatabase.DeleteAsset(assetPath);
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"ThemeSetup: no font at {ttfPath}; skipping.");
                return null;
            }

            // Created dynamic, then frozen to static once the glyphs are in. TMP refuses
            // TryAddCharacters on a static atlas, so asking for static up front produces a font
            // asset with an empty character table — which renders every string as nothing, with
            // no error at the use site. The order here is the whole trick.
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font,
                FontSamplingSize,
                FontAtlasPadding,
                GlyphRenderMode.SDFAA,
                FontAtlasSize,
                FontAtlasSize,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: false);

            if (asset == null)
            {
                Debug.LogError($"ThemeSetup: failed to create a font asset from {ttfPath}.");
                return null;
            }

            asset.name = $"{family} SDF";
            AssetDatabase.CreateAsset(asset, assetPath);

            // The atlas texture and material are sub-objects of the font asset. Without this they
            // are created in memory, never serialised, and the font renders as blank quads after
            // the next domain reload.
            if (asset.atlasTextures != null)
            {
                foreach (Texture2D atlas in asset.atlasTextures)
                {
                    atlas.name = $"{family} Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                }
            }

            if (asset.material != null)
            {
                asset.material.name = $"{family} Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            asset.TryAddCharacters(BakedCharacters, out string missing);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning($"ThemeSetup: {family} has no glyphs for: {missing}");
            }

            // Freeze. A dynamic atlas keeps growing itself at runtime as unseen glyphs appear,
            // which rewrites the asset on disk and leaves the working tree dirty after every play
            // session — noise in a repo that is meant to be reviewed.
            asset.atlasPopulationMode = AtlasPopulationMode.Static;

            if (asset.characterTable.Count == 0)
            {
                Debug.LogError(
                    $"ThemeSetup: {family} baked an empty atlas. Text using it will render blank.");
            }

            EditorUtility.SetDirty(asset);
            Debug.Log($"ThemeSetup: baked {assetPath} ({asset.characterTable.Count} glyphs).");
            return asset;
        }

        // ------------------------------------------------------------------ theme asset

        /// <summary>
        /// Creates or updates <c>ThemeAssets.asset</c> from whatever art is currently on disk.
        /// </summary>
        private static void BuildThemeAssets(List<TMP_FontAsset> fonts)
        {
            if (!AssetDatabase.IsValidFolder(ResourceRoot))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeAssets>(ThemeAssetPath);
            bool isNew = theme == null;
            if (isNew)
            {
                theme = ScriptableObject.CreateInstance<ThemeAssets>();
            }

            // Frames. Indices were chosen by eye from a contact sheet of all 32 variants: 009 is
            // the plainest square that still carries corner ornament, so it sits under dense
            // content without competing with it; 011 is a step more decorative for outer frames.
            theme.panel = Sprite($"{ArtRoot}/UI/Frames/Panel/panel-009.png");
            theme.panelHeavy = Sprite($"{ArtRoot}/UI/FramesDouble/Panel/panel-011.png");
            theme.frameHollow = Sprite($"{ArtRoot}/UI/Frames/Border/panel-border-009.png");
            theme.inset = Sprite($"{ArtRoot}/UI/Rpg/panelInset_beige.png");
            theme.divider = Sprite($"{ArtRoot}/UI/Frames/Divider/divider-002.png");

            // Controls. The grey family is the tintable one; the coloured variants would fight
            // the palette.
            theme.button = Sprite($"{ArtRoot}/UI/Rpg/buttonLong_grey.png");
            theme.buttonPressed = Sprite($"{ArtRoot}/UI/Rpg/buttonLong_grey_pressed.png");
            theme.buttonDisabled = Sprite($"{ArtRoot}/UI/Rpg/buttonLong_grey.png");
            theme.slot = Sprite($"{ArtRoot}/UI/Rpg/buttonSquare_grey.png");
            theme.checkboxOn = Sprite($"{ArtRoot}/UI/Widgets/check_square_grey_checkmark.png");
            theme.checkboxOff = Sprite($"{ArtRoot}/UI/Widgets/check_square_grey.png");

            // Bar sprites stay null on purpose: Theme generates them so the fill's left-edge
            // pivot is guaranteed by code rather than by an import setting.
            theme.barTrack = null;
            theme.barFill = null;

            theme.iconReales = Sprite($"{ArtRoot}/Icons/shoppingBasket.png");
            theme.iconRations = Sprite($"{ArtRoot}/Icons/home.png");
            theme.iconScrap = Sprite($"{ArtRoot}/Icons/wrench.png");
            theme.iconAttack = Sprite($"{ArtRoot}/Icons/target.png");
            theme.iconDefense = Sprite($"{ArtRoot}/Icons/medal1.png");
            theme.iconMovement = Sprite($"{ArtRoot}/Icons/fastForward.png");
            theme.iconRange = Sprite($"{ArtRoot}/Icons/zoomIn.png");
            theme.iconBack = Sprite($"{ArtRoot}/Icons/exitLeft.png");
            theme.iconClose = Sprite($"{ArtRoot}/Icons/cross.png");
            theme.iconSettings = Sprite($"{ArtRoot}/Icons/gear.png");
            theme.iconInfo = Sprite($"{ArtRoot}/Icons/information.png");
            theme.iconCheck = Sprite($"{ArtRoot}/Icons/checkmark.png");
            theme.iconCross = Sprite($"{ArtRoot}/Icons/cross.png");
            theme.iconStar = Sprite($"{ArtRoot}/Icons/star.png");

            theme.displayFont = Find(fonts, "Cinzel-Bold SDF")
                             ?? Load<TMP_FontAsset>($"{FontRoot}/Cinzel-Bold SDF.asset");
            theme.bodyFont = Find(fonts, "Spectral-Regular SDF")
                             ?? Load<TMP_FontAsset>($"{FontRoot}/Spectral-Regular SDF.asset");
            theme.bodyFontBold = Find(fonts, "Spectral-Bold SDF")
                             ?? Load<TMP_FontAsset>($"{FontRoot}/Spectral-Bold SDF.asset");

            LinkDisplayFallback(theme.displayFont, theme.bodyFontBold);

            theme.sfxClick = Load<AudioClip>($"{ArtRoot}/Sfx/ui_click.ogg");
            theme.sfxHover = Load<AudioClip>($"{ArtRoot}/Sfx/ui_hover.ogg");
            theme.sfxConfirm = Load<AudioClip>($"{ArtRoot}/Sfx/ui_confirm.ogg");
            theme.sfxError = Load<AudioClip>($"{ArtRoot}/Sfx/ui_error.ogg");
            theme.sfxOpen = Load<AudioClip>($"{ArtRoot}/Sfx/ui_open.ogg");
            theme.sfxClose = Load<AudioClip>($"{ArtRoot}/Sfx/ui_close.ogg");
            theme.sfxPlace = Load<AudioClip>($"{ArtRoot}/Sfx/ui_place.ogg");
            theme.sfxVictory = Load<AudioClip>($"{ArtRoot}/Sfx/ui_victory.ogg");
            theme.sfxQuiz = Load<AudioClip>($"{ArtRoot}/Sfx/ui_quiz.ogg");
            theme.sfxToggle = Load<AudioClip>($"{ArtRoot}/Sfx/ui_toggle.ogg");
            theme.sfxCoin = Load<AudioClip>($"{ArtRoot}/Sfx/ui_confirm.ogg");

            AssignUnitArt(theme);
            AssignCampArt(theme);

            if (isNew)
            {
                AssetDatabase.CreateAsset(theme, ThemeAssetPath);
            }

            EditorUtility.SetDirty(theme);
            ReportMissing(theme);
        }

        /// <summary>
        /// Re-reads the unit sprites into the existing theme asset without re-baking fonts.
        /// </summary>
        /// <remarks>
        /// Run this after <c>Tools/sprites/run.sh</c>. The full rebuild also works, but it bakes
        /// every font atlas again, which is minutes of work for a change that touches none.
        /// </remarks>
        [MenuItem("Tools/Binakayan Rising/Refresh Unit Art", priority = 23)]
        public static void RefreshUnitArt()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeAssets>(ThemeAssetPath);
            if (theme == null)
            {
                Debug.LogWarning($"ThemeSetup: no theme asset at {ThemeAssetPath}. Run Rebuild Theme Assets first.");
                return;
            }

            AssignUnitArt(theme);
            AssignCampArt(theme);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log($"ThemeSetup: {theme.units.Length} unit art sets and {theme.camp.Length} camp sprites assigned.");
        }

        /// <summary>
        /// Fills <see cref="ThemeAssets.units"/> from one folder per archetype under the art root.
        /// </summary>
        /// <remarks>
        /// The folder name is the archetype id, the same id the sprite pipeline writes, so a new
        /// unit needs no code here — rendering it into its own folder is enough.
        /// </remarks>
        private static void AssignUnitArt(ThemeAssets theme)
        {
            var entries = new List<ThemeAssets.UnitArt>();
            if (AssetDatabase.IsValidFolder(UnitArtRoot))
            {
                foreach (string folder in AssetDatabase.GetSubFolders(UnitArtRoot).OrderBy(f => f, StringComparer.Ordinal))
                {
                    entries.Add(new ThemeAssets.UnitArt
                    {
                        archetypeId = Path.GetFileName(folder),
                        body = Sprite(folder + "/body.png"),
                        portrait = Sprite(folder + "/portrait.png"),
                    });
                }
            }

            theme.units = entries.ToArray();
        }

        /// <summary>
        /// Fills <see cref="ThemeAssets.camp"/> with every sprite under <c>Art/Encampment/</c>.
        /// The file name is the name the layout in <c>Core/Content/Encampment.cs</c> uses.
        /// </summary>
        private static void AssignCampArt(ThemeAssets theme)
        {
            var sprites = new List<Sprite>();
            if (AssetDatabase.IsValidFolder(CampArtRoot))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { CampArtRoot }))
                {
                    Sprite sprite = Sprite(AssetDatabase.GUIDToAssetPath(guid));
                    if (sprite != null)
                    {
                        sprites.Add(sprite);
                    }
                }
            }

            theme.camp = sprites.OrderBy(s => s.name, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// Lets the display face borrow symbols it was never drawn with from the body face.
        /// </summary>
        /// <remarks>
        /// Cinzel is a titling face with no check mark, arrows or stars, so baking those characters
        /// into its atlas finds nothing. Spectral has them. With Spectral Bold as a fallback, a ✓
        /// inside a Cinzel label draws from Spectral instead of as an empty box. Public so the
        /// link can be restored without re-baking every atlas.
        /// </remarks>
        public static void LinkDisplayFallback(TMP_FontAsset display, TMP_FontAsset fallback)
        {
            if (display == null || fallback == null || display == fallback)
            {
                return;
            }

            if (display.fallbackFontAssetTable == null)
            {
                display.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }

            if (!display.fallbackFontAssetTable.Contains(fallback))
            {
                display.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(display);
            }
        }

        /// <summary>Names every unresolved reference, so a typo in a path is loud rather than blank.</summary>
        private static void ReportMissing(ThemeAssets theme)
        {
            var missing = new List<string>();

            if (theme.panel == null) missing.Add(nameof(theme.panel));
            if (theme.frameHollow == null) missing.Add(nameof(theme.frameHollow));
            if (theme.button == null) missing.Add(nameof(theme.button));
            if (theme.displayFont == null) missing.Add(nameof(theme.displayFont));
            if (theme.bodyFont == null) missing.Add(nameof(theme.bodyFont));

            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    "ThemeSetup: ThemeAssets is missing " + string.Join(", ", missing) +
                    ". The UI will fall back to procedural shapes for those.");
            }
            else
            {
                Debug.Log("ThemeSetup: ThemeAssets written with every required reference resolved.");
            }
        }

        private static TMP_FontAsset Find(List<TMP_FontAsset> fonts, string name)
        {
            return fonts.FirstOrDefault(f => f != null && f.name == name);
        }

        private static Sprite Sprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"ThemeSetup: no sprite at {path}.");
            }

            return sprite;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void ReimportArt()
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot))
            {
                Debug.LogWarning($"ThemeSetup: {ArtRoot} does not exist.");
                return;
            }

            // Unity caches import results, so any art that landed before the postprocessor
            // compiled kept the default settings and would keep them forever. Forcing the
            // reimport is what makes the postprocessor's rules retroactive.
            AssetDatabase.ImportAsset(
                ArtRoot, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
        }

        // ------------------------------------------------------------------ sorting layers

        /// <summary>
        /// Layers the board renders into, coarsest first. Order here is draw order.
        /// </summary>
        /// <remarks>
        /// The project ships with only "Default", which forces every renderer in the game to
        /// disambiguate itself purely by <c>sortingOrder</c> — and isometric depth already needs
        /// that whole number line for <c>X + Y</c>. Splitting the categories into layers lets
        /// <c>sortingOrder</c> mean one thing only: which row a cell is on.
        /// </remarks>
        private static readonly string[] RequiredSortingLayers =
        {
            "Terrain",
            "TerrainDecor",
            "Shadows",
            "Units",
            "UnitOverlay",
            "VFX",
            "WorldUI",
        };

        private static void AddSortingLayers()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");
            if (layers == null)
            {
                Debug.LogError("ThemeSetup: could not find m_SortingLayers in TagManager.asset.");
                return;
            }

            var existing = new HashSet<string>();
            for (int i = 0; i < layers.arraySize; i++)
            {
                existing.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue);
            }

            int added = 0;
            foreach (string name in RequiredSortingLayers)
            {
                if (existing.Contains(name))
                {
                    continue;
                }

                layers.InsertArrayElementAtIndex(layers.arraySize);
                SerializedProperty element = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                element.FindPropertyRelative("name").stringValue = name;

                // Unique IDs must not collide, and "Default" holds 0. Deriving from the name
                // keeps the id stable across machines, so the .asset does not churn in git.
                element.FindPropertyRelative("uniqueID").intValue = StableId(name);
                element.FindPropertyRelative("locked").boolValue = false;
                added++;
            }

            if (added > 0)
            {
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"ThemeSetup: added {added} sorting layer(s).");
            }
        }

        /// <summary>A deterministic non-zero id derived from a layer's name.</summary>
        private static int StableId(string name)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in name)
                {
                    hash = (hash * 31) + c;
                }

                // Keep it positive and clear of 0, which "Default" owns.
                return Math.Abs(hash) | 1;
            }
        }
    }
}
