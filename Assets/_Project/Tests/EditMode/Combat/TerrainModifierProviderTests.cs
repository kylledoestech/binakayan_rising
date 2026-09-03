using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers <see cref="TerrainModifierProvider"/>, including the transcription of Capstone Table 2
    /// used by the tests.
    /// </summary>
    [TestFixture]
    public class TerrainModifierProviderTests
    {
        [Test]
        public void StandardGridGrantsNothing()
        {
            TerrainModifierProvider provider = TerrainModifierProvider.CreateCapstoneTable2ForTesting();

            Assert.AreEqual(0, provider.GetModifiers(TerrainType.StandardGrid).Count);
            Assert.AreEqual(0f, provider.GetHpRegenFraction(TerrainType.StandardGrid), 0.0001f);
        }

        [Test]
        public void TrenchGrantsTwentyPercentDefenseAndFifteenPercentEvasion()
        {
            TerrainModifierProvider provider = TerrainModifierProvider.CreateCapstoneTable2ForTesting();
            ModifierStack stack = new ModifierStack();
            stack.AddRange(provider.GetModifiers(TerrainType.Trench));

            Assert.AreEqual(120f, stack.Resolve(StatKind.Defense, 100f), 0.0001f);
            Assert.AreEqual(0.23f, stack.Resolve(StatKind.Evasion, 0.2f), 0.0001f);
        }

        [Test]
        public void CoastalShallowsCostsFifteenPercentMovementAndTenPercentDefense()
        {
            TerrainModifierProvider provider = TerrainModifierProvider.CreateCapstoneTable2ForTesting();
            ModifierStack stack = new ModifierStack();
            stack.AddRange(provider.GetModifiers(TerrainType.CoastalShallows));

            Assert.AreEqual(0.85f, stack.Resolve(StatKind.MovementSpeed, 1f), 0.0001f);
            Assert.AreEqual(90f, stack.Resolve(StatKind.Defense, 100f), 0.0001f);
        }

        [Test]
        public void EncampmentTentRegeneratesFivePercentPerTurn()
        {
            TerrainModifierProvider provider = TerrainModifierProvider.CreateCapstoneTable2ForTesting();

            Assert.AreEqual(0.05f, provider.GetHpRegenFraction(TerrainType.EncampmentTent), 0.0001f);
            Assert.AreEqual(0, provider.GetModifiers(TerrainType.EncampmentTent).Count,
                "Regeneration is not a stat modifier and must not appear as one.");
        }

        [Test]
        public void BambooBarricadeCarriesNoStatModifiersBecausePassabilityLivesOnTheGrid()
        {
            TerrainModifierProvider provider = TerrainModifierProvider.CreateCapstoneTable2ForTesting();
            BattleGrid grid = CombatTestFactory.FlatGrid(3, 3);
            grid.SetTerrain(new GridCoord(1, 1), TerrainType.BambooBarricade);

            Assert.AreEqual(0, provider.GetModifiers(TerrainType.BambooBarricade).Count);
            Assert.IsFalse(grid.IsWalkable(new GridCoord(1, 1)));
        }

        [Test]
        public void AnUnknownTerrainReturnsAnEmptySetRatherThanThrowing()
        {
            TerrainModifierProvider provider = TerrainModifierProvider.CreateEmpty();

            Assert.AreEqual(0, provider.GetModifiers(TerrainType.Trench).Count);
            Assert.AreEqual(0f, provider.GetHpRegenFraction(TerrainType.Trench), 0.0001f);
        }

        [Test]
        public void TheInjectedTableIsCopiedSoLaterEditsCannotChangeARunningBattle()
        {
            Dictionary<TerrainType, IReadOnlyList<StatModifier>> table =
                new Dictionary<TerrainType, IReadOnlyList<StatModifier>>
                {
                    { TerrainType.Trench, new[] { StatModifier.Percent(StatKind.Defense, 0.20f, ModifierSource.Terrain) } }
                };

            TerrainModifierProvider provider = new TerrainModifierProvider(table);
            table[TerrainType.Trench] = new StatModifier[0];

            Assert.AreEqual(1, provider.GetModifiers(TerrainType.Trench).Count);
        }
    }
}
