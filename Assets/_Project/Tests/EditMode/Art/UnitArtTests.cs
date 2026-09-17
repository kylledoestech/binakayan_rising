using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BinakayanRising.Tests.Art
{
    /// <summary>
    /// Checks the imported unit sprites against the render pipeline that produced them.
    /// </summary>
    /// <remarks>
    /// The expected canvas sizes and the ground pixel are read out of
    /// <c>Tools/sprites/build_and_render.py</c> rather than restated here, so a change on either
    /// side — a new render size, or an importer rule drifting — fails this test instead of
    /// silently floating every unit off its cell.
    /// </remarks>
    [TestFixture]
    public class UnitArtTests
    {
        private const string ThemePath = "Assets/_Project/Resources/ThemeAssets.asset";
        private const float BodyPixelsPerUnit = 80f;

        private static IEnumerable<string> Archetypes()
        {
            foreach (RosterEntry entry in PlaytestScenario.KatipunanRoster())
            {
                yield return entry.ArchetypeId;
            }

            yield return PlaytestScenario.SpanishColumn(1)[0].ArchetypeId;
        }

        [TestCaseSource(nameof(Archetypes))]
        public void EveryUnitHasABodyAndAPortrait(string archetypeId)
        {
            ThemeAssets theme = LoadTheme();
            Assert.IsNotNull(theme.UnitBody(archetypeId), archetypeId + " has no body sprite in ThemeAssets.");
            Assert.IsNotNull(theme.UnitPortrait(archetypeId), archetypeId + " has no portrait sprite in ThemeAssets.");
        }

        [TestCaseSource(nameof(Archetypes))]
        public void BodyStandsOnTheRenderedGroundPixel(string archetypeId)
        {
            Sprite body = LoadTheme().UnitBody(archetypeId);
            Assume.That(body, Is.Not.Null);

            Vector2 size = PipelineVector("BODY_SIZE");
            Vector2 ground = PipelineVector("GROUND_PIXEL");

            Assert.AreEqual(size.x, body.rect.width, archetypeId + " body width");
            Assert.AreEqual(size.y, body.rect.height, archetypeId + " body height");
            Assert.AreEqual(ground.x, body.pivot.x, 0.001f, archetypeId + " pivot x (pixels)");
            Assert.AreEqual(ground.y, body.pivot.y, 0.001f, archetypeId + " pivot y (pixels)");
            Assert.AreEqual(BodyPixelsPerUnit, body.pixelsPerUnit, archetypeId + " pixels per unit");
            Assert.AreEqual(FilterMode.Point, body.texture.filterMode, archetypeId + " body filtering");
        }

        [TestCaseSource(nameof(Archetypes))]
        public void PortraitMatchesTheRenderedSize(string archetypeId)
        {
            Sprite portrait = LoadTheme().UnitPortrait(archetypeId);
            Assume.That(portrait, Is.Not.Null);

            Vector2 size = PipelineVector("PORTRAIT_SIZE");
            Assert.AreEqual(size.x, portrait.rect.width, archetypeId + " portrait width");
            Assert.AreEqual(size.y, portrait.rect.height, archetypeId + " portrait height");
            Assert.AreEqual(FilterMode.Point, portrait.texture.filterMode, archetypeId + " portrait filtering");
        }

        private static ThemeAssets LoadTheme()
        {
            ThemeAssets theme = AssetDatabase.LoadAssetAtPath<ThemeAssets>(ThemePath);
            Assert.IsNotNull(theme, "No theme asset at " + ThemePath + ".");
            return theme;
        }

        /// <summary>Reads a <c>NAME = (x, y)</c> constant from the sprite render script.</summary>
        private static Vector2 PipelineVector(string name)
        {
            string script = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tools", "sprites", "build_and_render.py");
            Assert.IsTrue(File.Exists(script), "Render script not found at " + script + ".");

            Match match = Regex.Match(
                File.ReadAllText(script),
                "^" + name + @"\s*=\s*\(\s*(\d+)\s*,\s*(\d+)\s*\)",
                RegexOptions.Multiline);
            Assert.IsTrue(match.Success, name + " not found in " + script + ".");
            return new Vector2(float.Parse(match.Groups[1].Value), float.Parse(match.Groups[2].Value));
        }
    }
}
