using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using NUnit.Framework;

namespace BinakayanRising.Tests.Combat
{
    /// <summary>
    /// Covers the Spanish roster's abilities (DESIGN-DECISIONS #19): the artillery's splash and
    /// reload, the officer's aura, the marine's footing in the shallows, a non-combatant that never
    /// acts, and the sortie that lets a rooted line answer a unit that outranges it.
    /// </summary>
    [TestFixture]
    public class UnitAbilityTests
    {
        private static CombatUnit Gun(int id, int x, int y, float splash = 0.5f, int reload = 1)
        {
            CombatUnit gun = CombatTestFactory.Unit(id, Team.Spanish,
                CombatTestFactory.Stats(attackDamage: 20f, attackRange: 4f, movementSpeed: 0f), x, y);
            gun.Abilities = UnitAbilities.Artillery(splash, reload);
            return gun;
        }

        private static CombatUnit Rooted(int id, Team team, int x, int y, float maxHP = 1000f, float attack = 0f, float range = 1f)
        {
            return CombatTestFactory.Unit(id, team,
                CombatTestFactory.Stats(maxHP: maxHP, attackDamage: attack, attackRange: range, movementSpeed: 0f), x, y);
        }

        private static float DamageTo(IReadOnlyList<BattleEvent> events, int targetId)
        {
            float total = 0f;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == BattleEventType.DamageDealt && events[i].TargetId == targetId)
                {
                    total += events[i].Amount;
                }
            }

            return total;
        }

        private static int AttacksBy(IReadOnlyList<BattleEvent> events, int actorId)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == BattleEventType.UnitAttacked && events[i].ActorId == actorId)
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void ArtillerySplashesHalfItsHitOntoEnemiesBesideTheTarget()
        {
            // Gun at (0,4), target at (4,4); (4,3) and (5,4) are next to it, (5,5) is diagonal.
            var units = new List<CombatUnit>
            {
                Rooted(1, Team.Katipunan, 4, 4),
                Rooted(2, Team.Katipunan, 4, 3),
                Rooted(3, Team.Katipunan, 5, 4),
                Rooted(4, Team.Katipunan, 5, 5),
                Gun(10, 0, 4)
            };

            BattleTurnResult turn = new BattleSimulator(CombatTestFactory.FlatGrid(8, 8), units, CombatTestFactory.Config()).ExecuteTurn();

            Assert.AreEqual(20f, DamageTo(turn.Events, 1), 0.0001f, "direct hit");
            Assert.AreEqual(10f, DamageTo(turn.Events, 2), 0.0001f, "orthogonal neighbour");
            Assert.AreEqual(10f, DamageTo(turn.Events, 3), 0.0001f, "orthogonal neighbour");
            Assert.AreEqual(0f, DamageTo(turn.Events, 4), 0.0001f, "diagonal is spared");
        }

        [Test]
        public void SplashNeverCatchesTheGunsOwnSide()
        {
            var units = new List<CombatUnit>
            {
                Rooted(1, Team.Katipunan, 4, 4),
                Rooted(11, Team.Spanish, 4, 3),
                Gun(10, 0, 4)
            };

            BattleTurnResult turn = new BattleSimulator(CombatTestFactory.FlatGrid(8, 8), units, CombatTestFactory.Config()).ExecuteTurn();

            // The defender beside it still trades blows with it; only the splash is checked.
            for (int i = 0; i < turn.Events.Count; i++)
            {
                Assert.IsFalse(turn.Events[i].IsSplash && turn.Events[i].TargetId == 11, "the gun's own infantry was caught");
            }
        }

        [Test]
        public void SplashIsLoggedAsSplashAndCanKill()
        {
            var units = new List<CombatUnit>
            {
                Rooted(1, Team.Katipunan, 4, 4),
                Rooted(2, Team.Katipunan, 4, 3, maxHP: 5f),
                Gun(10, 0, 4)
            };

            BattleTurnResult turn = new BattleSimulator(CombatTestFactory.FlatGrid(8, 8), units, CombatTestFactory.Config()).ExecuteTurn();

            bool splashLogged = false;
            for (int i = 0; i < turn.Events.Count; i++)
            {
                splashLogged |= turn.Events[i].IsSplash && turn.Events[i].TargetId == 2;
            }

            Assert.IsTrue(splashLogged);
            Assert.IsTrue(CombatTestFactory.HasEvent(turn.Events, BattleEventType.UnitDied, 2));
        }

        [Test]
        public void ArtilleryFiresEveryOtherTurn()
        {
            var units = new List<CombatUnit>
            {
                Rooted(1, Team.Katipunan, 4, 4),
                Gun(10, 0, 4)
            };

            var simulator = new BattleSimulator(CombatTestFactory.FlatGrid(8, 8), units, CombatTestFactory.Config());

            Assert.AreEqual(1, AttacksBy(simulator.ExecuteTurn().Events, 10), "turn 1 fires");
            Assert.AreEqual(0, AttacksBy(simulator.ExecuteTurn().Events, 10), "turn 2 reloads");
            Assert.AreEqual(1, AttacksBy(simulator.ExecuteTurn().Events, 10), "turn 3 fires");
        }

        [Test]
        public void OfficerAuraRaisesAttackOfAlliesWithinTwoTiles()
        {
            var officer = CombatTestFactory.Unit(20, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 0, 0);
            officer.Abilities = UnitAbilities.Officer(2, new[]
            {
                StatModifier.Percent(StatKind.AttackDamage, 0.10f, ModifierSource.Ability, "OfficerAura")
            });

            // Regular 2 tiles away hits a Katipunan dummy next to it for 10 + 10%.
            var units = new List<CombatUnit>
            {
                Rooted(1, Team.Katipunan, 3, 0),
                CombatTestFactory.Unit(21, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 2, 0),
                officer
            };

            BattleTurnResult turn = new BattleSimulator(CombatTestFactory.FlatGrid(6, 6), units, CombatTestFactory.Config()).ExecuteTurn();

            Assert.AreEqual(11f, CombatTestFactory.FirstDamageBy(turn.Events, 21), 0.0001f);
            Assert.AreEqual(10f, officer.GetEffectiveStat(StatKind.AttackDamage), 0.0001f, "the aura does not buff the officer himself");
        }

        [Test]
        public void OfficerAuraStopsAtItsRadiusAndTwoAurasStack()
        {
            UnitAbilities aura = UnitAbilities.Officer(2, new[]
            {
                StatModifier.Percent(StatKind.AttackDamage, 0.10f, ModifierSource.Ability, "OfficerAura")
            });

            var first = CombatTestFactory.Unit(20, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 0, 0);
            first.Abilities = aura;
            var second = CombatTestFactory.Unit(21, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 0, 4);
            second.Abilities = aura;
            var between = CombatTestFactory.Unit(22, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 0, 2);
            var outside = CombatTestFactory.Unit(23, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 3, 0);

            var units = new List<CombatUnit> { Rooted(1, Team.Katipunan, 5, 5), first, second, between, outside };
            new BattleSimulator(CombatTestFactory.FlatGrid(6, 6), units, CombatTestFactory.Config()).ExecuteTurn();

            Assert.AreEqual(12f, between.GetEffectiveStat(StatKind.AttackDamage), 0.0001f, "+10% twice, additive");
            Assert.AreEqual(10f, outside.GetEffectiveStat(StatKind.AttackDamage), 0.0001f, "three tiles is outside");
        }

        [Test]
        public void MarineInTheShallowsTakesNoPenaltyAndGainsItsBonus()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(6, 1);
            grid.SetTerrain(new GridCoord(1, 0), TerrainType.CoastalShallows);

            var marine = CombatTestFactory.Unit(20, Team.Spanish,
                CombatTestFactory.Stats(attackDamage: 20f, defense: 10f, movementSpeed: 1f), 1, 0);
            marine.Abilities = UnitAbilities.HomeTerrain(TerrainType.CoastalShallows, new[]
            {
                StatModifier.Percent(StatKind.AttackDamage, 0.15f, ModifierSource.Ability, "MarineFooting"),
                StatModifier.Percent(StatKind.Defense, 0.15f, ModifierSource.Ability, "MarineFooting")
            });

            CombatConfig config = CombatTestFactory.Config();
            config.SpanishReceivesTerrainBonuses = false;

            var units = new List<CombatUnit> { Rooted(1, Team.Katipunan, 5, 0), marine };
            new BattleSimulator(grid, units, config, terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting()).ExecuteTurn();

            // Checked after the turn's recompute but before the marine left the water: it moved one
            // tile, and modifiers are rebuilt only at the start of a turn.
            Assert.AreEqual(23f, marine.GetEffectiveStat(StatKind.AttackDamage), 0.0001f);
            Assert.AreEqual(11.5f, marine.GetEffectiveStat(StatKind.Defense), 0.0001f);
            Assert.AreEqual(1f, marine.GetEffectiveStat(StatKind.MovementSpeed), 0.0001f, "no -15% movement");
        }

        [Test]
        public void ARegularInTheShallowsStillSuffersThePenalty()
        {
            BattleGrid grid = CombatTestFactory.FlatGrid(6, 1);
            grid.SetTerrain(new GridCoord(1, 0), TerrainType.CoastalShallows);
            var regular = CombatTestFactory.Unit(20, Team.Spanish, CombatTestFactory.Stats(defense: 10f, movementSpeed: 1f), 1, 0);

            var units = new List<CombatUnit> { Rooted(1, Team.Katipunan, 5, 0), regular };
            new BattleSimulator(grid, units, CombatTestFactory.Config(), terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting()).ExecuteTurn();

            Assert.AreEqual(9f, regular.GetEffectiveStat(StatKind.Defense), 0.0001f);
        }

        [Test]
        public void ANonCombatantNeverActs()
        {
            var cart = CombatTestFactory.Unit(1, Team.Katipunan, CombatTestFactory.Stats(attackDamage: 50f), 0, 0);
            cart.Abilities = UnitAbilities.NonCombatantCargo();
            var units = new List<CombatUnit>
            {
                cart,
                CombatTestFactory.Unit(20, Team.Spanish, CombatTestFactory.Stats(attackDamage: 0f, movementSpeed: 0f), 1, 0)
            };

            var simulator = new BattleSimulator(CombatTestFactory.FlatGrid(4, 1), units, CombatTestFactory.Config(0, 5));
            BattleResult result = simulator.RunToCompletion();

            for (int i = 0; i < result.Events.Count; i++)
            {
                BattleEventType type = result.Events[i].Type;
                bool action = type == BattleEventType.UnitAttacked || type == BattleEventType.UnitMoved || type == BattleEventType.UnitHealed;
                Assert.IsFalse(action && result.Events[i].ActorId == 1, "the cart acted: " + result.Events[i]);
            }
        }

        [Test]
        public void AnEntrenchedUnitFiredOnFromBeyondItsReachSortiesAgainstTheShooter()
        {
            // A range-1 defender that cannot move, and a range-3 skirmisher three tiles off.
            var units = new List<CombatUnit>
            {
                Rooted(1, Team.Katipunan, 0, 0, attack: 10f),
                CombatTestFactory.Unit(20, Team.Spanish, CombatTestFactory.Stats(attackRange: 3f, movementSpeed: 0f), 3, 0)
            };

            CombatConfig config = CombatTestFactory.Config();
            config.SortieSpeed = 1f;
            var simulator = new BattleSimulator(CombatTestFactory.FlatGrid(6, 1), units, config);

            simulator.ExecuteTurn();
            Assert.AreEqual(20, units[0].ProvokedBy);

            BattleTurnResult second = simulator.ExecuteTurn();
            Assert.IsTrue(CombatTestFactory.HasEvent(second.Events, BattleEventType.UnitMoved, 1), "it climbs out and advances");
            Assert.AreEqual(new GridCoord(1, 0), units[0].Position);
        }

        [Test]
        public void WithoutSortiesAnEntrenchedUnitStaysRooted()
        {
            var units = new List<CombatUnit>
            {
                Rooted(1, Team.Katipunan, 0, 0, attack: 10f),
                CombatTestFactory.Unit(20, Team.Spanish, CombatTestFactory.Stats(attackRange: 3f, movementSpeed: 0f), 3, 0)
            };

            var simulator = new BattleSimulator(CombatTestFactory.FlatGrid(6, 1), units, CombatTestFactory.Config());
            simulator.ExecuteTurn();
            simulator.ExecuteTurn();

            Assert.AreEqual(new GridCoord(0, 0), units[0].Position);
        }

        [Test]
        public void AnEntrenchedUnitHitFromWithinItsReachFightsInPlace()
        {
            var units = new List<CombatUnit>
            {
                Rooted(1, Team.Katipunan, 0, 0, attack: 10f),
                CombatTestFactory.Unit(20, Team.Spanish, CombatTestFactory.Stats(movementSpeed: 0f), 1, 0)
            };

            CombatConfig config = CombatTestFactory.Config();
            config.SortieSpeed = 1f;
            new BattleSimulator(CombatTestFactory.FlatGrid(6, 1), units, config).ExecuteTurn();

            Assert.AreEqual(-1, units[0].ProvokedBy);
        }

        [Test]
        public void CloneKeepsObjectiveAndSortie()
        {
            CombatConfig config = CombatTestFactory.Config();
            config.SortieSpeed = 1f;
            config.Objective = BattleObjective.Escort(7);

            CombatConfig copy = config.Clone();

            Assert.AreEqual(1f, copy.SortieSpeed);
            Assert.AreEqual(ObjectiveKind.Escort, copy.Objective.Kind);
            Assert.AreEqual(7, copy.Objective.EscortUnitId);
        }
    }
}
