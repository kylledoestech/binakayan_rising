using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Gameplay
{
    /// <summary>
    /// One roster slot the player can deploy: who the unit is and what its stat block looks like.
    /// </summary>
    /// <remarks>
    /// This is the shape a <c>UnitData</c> ScriptableObject will convert into once art and authoring
    /// exist. Keeping the prototype on the same shape means swapping in real assets later is a
    /// change of source, not a change of code.
    /// </remarks>
    public struct RosterEntry
    {
        /// <summary>Stable unit id used by the simulation for every deterministic tie-break.</summary>
        public int Id;

        /// <summary>Name shown on the deployment panel and in the combat log.</summary>
        public string DisplayName;

        /// <summary>Short name for the unit token on the board.</summary>
        public string ShortName;

        /// <summary>Archetype id that <see cref="KapatiranResolver"/> matches bonds against.</summary>
        public string ArchetypeId;

        /// <summary>Base stat block.</summary>
        public UnitStats Stats;

        /// <summary>Creates a roster slot.</summary>
        public RosterEntry(int id, string displayName, string shortName, string archetypeId, UnitStats stats)
        {
            Id = id;
            DisplayName = displayName;
            ShortName = shortName;
            ArchetypeId = archetypeId;
            Stats = stats;
        }
    }

    /// <summary>
    /// The hand-authored playtest mission: the Binakayan-Dalahican trench line, the Katipunan roster
    /// the player deploys, the Spanish column that assaults it, and the Kapatiran bonds in play.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every number in this file is a placeholder.</b> The capstone document publishes no base
    /// stat block, no enemy roster and no mission layout, so these values exist only to make the
    /// slice playable. They are not a balance proposal and they live in the Gameplay assembly
    /// precisely so that Core stays free of content.
    /// </para>
    /// <para>
    /// This class is the seam a real mission pipeline replaces: swap it for something that reads a
    /// <c>MissionData</c> asset and nothing downstream changes.
    /// </para>
    /// </remarks>
    public static class PlaytestScenario
    {
        /// <summary>Map width in cells.</summary>
        public const int MapWidth = 14;

        /// <summary>Map height in cells.</summary>
        public const int MapHeight = 9;

        /// <summary>
        /// The first Spanish unit id. Far above any save's unit ids, so a campaign roster never
        /// collides with the column; every Katipunan id still sorts before every Spanish one.
        /// </summary>
        public const int SpanishIdBase = 1000;

        /// <summary>
        /// The supply cart's id under the Escort rule. Below <see cref="SpanishIdBase"/>, so it
        /// sorts with the Katipunan, and far above any save's unit ids.
        /// </summary>
        public const int SupplyCartId = 999;

        /// <summary>The supply cart's archetype id, for its name and view. Not in the unit catalog: nobody recruits a cart.</summary>
        public const string SupplyCartArchetype = "SupplyCart";

        /// <summary>
        /// Where the supply cart stands (#37): at the back of the camp, behind the trench end away
        /// from the shore. The Spanish must break the line or walk around its end to reach it, so
        /// the squad guards it by deploying nearest it (see <c>BattlePlaytest.AutoDeploy</c>).
        /// </summary>
        public static readonly GridCoord CartCell = new GridCoord(12, 7);

        /// <summary>
        /// The powder magazine the Sabotage squad makes for (#38), at the back of the Spanish
        /// ground, ten tiles from the trench.
        /// </summary>
        public static readonly GridCoord MagazineCell = new GridCoord(0, 4);

        /// <summary>The Spanish front rank, filled first by everyone but the guns and the marines.</summary>
        private static readonly GridCoord[] FrontCells = Column(1, 2, 3, 4, 5, 6, 7, 8);

        /// <summary>The rear rank, where the guns stand, centre first.</summary>
        private static readonly GridCoord[] GunCells = Column(0, 5, 4, 6, 3, 7, 2, 8);

        /// <summary>The rear rank in order, for whoever overflows the front.</summary>
        private static readonly GridCoord[] RearCells = Column(0, 2, 3, 4, 5, 6, 7, 8);

        /// <summary>The dry corner beside the shallows, where marines land first.</summary>
        private static readonly GridCoord[] ShoreCells =
        {
            new GridCoord(1, 1), new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(0, 0)
        };

        /// <summary>
        /// Builds the battlefield: Evangelista's trench line on the Katipunan right, the encampment
        /// tents behind it, the Dalahican tidal shallows along the shore, and bamboo barricades
        /// anchoring both flanks. Only the trench and the tents are marked deployable.
        /// </summary>
        public static BattleGrid CreateGrid()
        {
            BattleGrid grid = new BattleGrid(MapWidth, MapHeight, TerrainType.StandardGrid);

            for (int x = 2; x <= 9; x++)
            {
                grid.SetTerrain(new GridCoord(x, 0), TerrainType.CoastalShallows);
                grid.SetTerrain(new GridCoord(x, 1), TerrainType.CoastalShallows);
            }

            for (int y = 1; y <= 7; y++)
            {
                GridCoord cell = new GridCoord(10, y);
                grid.SetTerrain(cell, TerrainType.Trench);
                grid.SetDeployable(cell, true);
            }

            for (int y = 3; y <= 6; y++)
            {
                GridCoord cell = new GridCoord(11, y);
                grid.SetTerrain(cell, TerrainType.EncampmentTent);
                grid.SetDeployable(cell, true);
            }

            grid.SetTerrain(new GridCoord(5, 0), TerrainType.BambooBarricade);
            grid.SetTerrain(new GridCoord(6, 0), TerrainType.BambooBarricade);
            grid.SetTerrain(new GridCoord(5, 8), TerrainType.BambooBarricade);
            grid.SetTerrain(new GridCoord(6, 8), TerrainType.BambooBarricade);

            return grid;
        }

        /// <summary>
        /// The five Katipunan units the player deploys. Movement Speed is zero across the board:
        /// these are dug-in defenders holding a fortified line, which is what the battle was.
        /// </summary>
        public static List<RosterEntry> KatipunanRoster()
        {
            return new List<RosterEntry>
            {
                new RosterEntry(1, "Caviteño Marksman", "MRK", "Marksman",
                    new UnitStats(90f, 14f, 4f, 0.05f, 0.75f, 2f, 0.15f, 0f)),
                new RosterEntry(2, "Trench Engineer", "ENG", "Engineer",
                    new UnitStats(110f, 10f, 12f, 0.05f, 0.85f, 1f, 0.05f, 0f)),
                new RosterEntry(3, "Gen. Evangelista", "EVA", "Evangelista",
                    new UnitStats(140f, 16f, 8f, 0.05f, 0.90f, 1f, 0.10f, 0f)),
                new RosterEntry(4, "Emilio Aguinaldo", "AGU", "Aguinaldo",
                    new UnitStats(130f, 15f, 7f, 0.05f, 0.90f, 1f, 0.15f, 0f)),
                new RosterEntry(5, "Katipunero Vanguard", "VAN", "Vanguard",
                    new UnitStats(150f, 15f, 10f, 0.05f, 0.85f, 1f, 0.10f, 0f))
            };
        }

        /// <summary>
        /// The assaulting Spanish column, already positioned on the far side of the open ground.
        /// The player never places these.
        /// </summary>
        /// <param name="count">How many regulars advance, 1 to 14.</param>
        public static List<CombatUnit> SpanishColumn(int count)
        {
            int clamped = count < 1 ? 1 : (count > 14 ? 14 : count);
            List<string> regulars = new List<string>();
            for (int i = 0; i < clamped; i++)
            {
                regulars.Add(UnitCatalog.SpanishRegular);
            }

            return SpanishForce(regulars);
        }

        /// <summary>
        /// A mixed Spanish column (DESIGN-DECISIONS #19), formed up on the far side of the open
        /// ground. Ids follow the list order; each unit is numbered among its own archetype.
        /// </summary>
        /// <remarks>
        /// Guns take the rear rank, centre first, so they fire over the infantry; marines take the
        /// dry corner by the shallows; everyone else fills the front rank top to bottom, then the
        /// rear. A column of regulars alone therefore stands exactly where it always has.
        /// </remarks>
        /// <param name="archetypes">Spanish archetype ids, at most 18.</param>
        /// <param name="reserved">A cell no unit may start on, e.g. the Sabotage magazine; null for none.</param>
        public static List<CombatUnit> SpanishForce(IReadOnlyList<string> archetypes, GridCoord? reserved = null)
        {
            List<CombatUnit> column = new List<CombatUnit>();
            if (archetypes == null)
            {
                return column;
            }

            HashSet<GridCoord> taken = new HashSet<GridCoord>();
            if (reserved.HasValue)
            {
                taken.Add(reserved.Value);
            }

            GridCoord?[] cells = new GridCoord?[archetypes.Count];

            // Guns and marines claim their places first, so the infantry cannot crowd them out.
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < archetypes.Count; i++)
                {
                    UnitArchetype archetype = UnitCatalog.Find(archetypes[i]);
                    UnitRole role = archetype != null ? archetype.Role : UnitRole.Infantry;
                    int wantedPass = role == UnitRole.Artillery ? 0 : (role == UnitRole.Marine ? 1 : 2);
                    if (pass != wantedPass)
                    {
                        continue;
                    }

                    cells[i] = role == UnitRole.Artillery ? Claim(taken, GunCells, FrontCells, ShoreCells)
                        : role == UnitRole.Marine ? Claim(taken, ShoreCells, FrontCells, RearCells)
                        : Claim(taken, FrontCells, RearCells, ShoreCells);
                }
            }

            Dictionary<string, int> numbering = new Dictionary<string, int>();
            for (int i = 0; i < archetypes.Count; i++)
            {
                if (!cells[i].HasValue)
                {
                    continue;
                }

                UnitArchetype archetype = UnitCatalog.Find(archetypes[i]) ?? UnitCatalog.Find(UnitCatalog.SpanishRegular);
                int ordinal;
                numbering.TryGetValue(archetype.Id, out ordinal);
                numbering[archetype.Id] = ++ordinal;

                column.Add(new CombatUnit(
                    SpanishIdBase + i,
                    archetype.Name.English + " " + ordinal,
                    archetype.Id,
                    Team.Spanish,
                    archetype.BaseStats,
                    cells[i].Value)
                {
                    Abilities = archetype.Abilities
                });
            }

            return column;
        }

        /// <summary>The first free cell of the first list that has one, or null when all are full.</summary>
        private static GridCoord? Claim(HashSet<GridCoord> taken, params GridCoord[][] preferences)
        {
            for (int p = 0; p < preferences.Length; p++)
            {
                for (int c = 0; c < preferences[p].Length; c++)
                {
                    if (taken.Add(preferences[p][c]))
                    {
                        return preferences[p][c];
                    }
                }
            }

            return null;
        }

        private static GridCoord[] Column(int x, params int[] rows)
        {
            GridCoord[] cells = new GridCoord[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                cells[i] = new GridCoord(x, rows[i]);
            }

            return cells;
        }

        /// <summary>
        /// The supply cart of the Escort rule (#37): a Katipunan unit that never acts. Sturdy
        /// enough to take a few blows, not enough to be left alone with a column.
        /// </summary>
        public static CombatUnit SupplyCart(ModifierStackingPolicy policy = ModifierStackingPolicy.AdditivePercent)
        {
            return new CombatUnit(
                SupplyCartId,
                "Supply Cart",
                SupplyCartArchetype,
                Team.Katipunan,
                new UnitStats(160f, 0f, 6f, 0f, 0f, 0f, 0f, 0f),
                CartCell,
                policy)
            {
                Abilities = UnitAbilities.NonCombatantCargo()
            };
        }

        /// <summary>The simulator's objective for a quest's win rule. Hold stays a Rout battle whose draw the mission counts as a win.</summary>
        public static BattleObjective ObjectiveFor(WinRule rule)
        {
            switch (rule)
            {
                case WinRule.Escort:
                    return BattleObjective.Escort(SupplyCartId);
                case WinRule.Sabotage:
                    return BattleObjective.Sabotage(MagazineCell);
                default:
                    return BattleObjective.RoutAll;
            }
        }

        /// <summary>
        /// Every Kapatiran bond, already resolved to rank A: two rows of Capstone Table 3, then the
        /// Vanguard and Field Medic and the Magdalo and Magdiwang pairs from <see cref="BondCatalog"/>.
        /// </summary>
        /// <remarks>
        /// Rank progression is metagame state that lives in the save file, so by the time a battle
        /// starts the rank is known and Core only ever sees the resulting modifiers. Note that the
        /// Marksman and Engineer bond carries the document's one flat bonus, <c>+1 Attack Range</c>,
        /// alongside percentage ones.
        /// </remarks>
        public static List<KapatiranBond> Bonds()
        {
            return new List<KapatiranBond>
            {
                new KapatiranBond(
                    "Marksman_Engineer",
                    "Marksman",
                    "Engineer",
                    new List<StatModifier>
                    {
                        StatModifier.Percent(StatKind.RangedAccuracy, 0.20f, ModifierSource.Kapatiran, "Marksman + Engineer"),
                        StatModifier.Flat(StatKind.AttackRange, 1f, ModifierSource.Kapatiran, "Marksman + Engineer")
                    },
                    "A"),
                new KapatiranBond(
                    "Evangelista_Aguinaldo",
                    "Evangelista",
                    "Aguinaldo",
                    new List<StatModifier>
                    {
                        StatModifier.Percent(StatKind.AttackDamage, 0.15f, ModifierSource.Kapatiran, "Evangelista + Aguinaldo"),
                        StatModifier.Percent(StatKind.Defense, 0.10f, ModifierSource.Kapatiran, "Evangelista + Aguinaldo")
                    },
                    "A"),
                BondCatalog.VanguardAndMedic(),
                BondCatalog.MagdaloAndMagdiwang(BondCatalog.RankA)
            };
        }

        /// <summary>Terrain and Kapatiran tables for the prototype, using the document's Table 2 values.</summary>
        public static TerrainModifierProvider Terrain()
        {
            return TerrainModifierProvider.CreateCapstoneTable2ForTesting();
        }

        /// <summary>The battle tunables. See <see cref="CombatConfig"/> for what the document does and does not specify.</summary>
        /// <param name="seed">Battle seed. The same seed replays the same battle exactly.</param>
        public static CombatConfig Config(int seed)
        {
            return new CombatConfig
            {
                RandomSeed = seed,
                MaxTurns = 120,
                MinimumDamage = 1f,
                CriticalHitMultiplier = 2f,
                AllowDiagonalMovement = false,
                StackingPolicy = ModifierStackingPolicy.AdditivePercent,
                MitigationMode = DamageMitigationMode.Subtractive,
                LogModifierEvents = false,

                // Trenches and tents are Katipunan works. A regular who reaches an empty one should
                // not inherit its cover or its healing.
                SpanishReceivesTerrainBonuses = false,

                // The line never moves, so a gun or a Cazador that outranges it would be untouchable.
                // Whoever it fires on climbs out and goes for it, one tile a turn (#19).
                SortieSpeed = 1f
            };
        }
    }
}
