using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;

namespace BinakayanRising.Tools
{
    /// <summary>
    /// A headless playable slice of the Combat phase: builds the Binakayan-Dalahican trench line,
    /// deploys both rosters, and runs the deterministic resolver turn by turn with an ASCII render.
    /// No Unity Editor, no scene, no assets.
    /// </summary>
    public static class BattleDemo
    {
        private const int MapWidth = 14;
        private const int MapHeight = 9;

        public static int Main(string[] args)
        {
            int seed = 1896;
            int maxTurns = 120;
            bool quiet = false;
            bool showLog = false;
            int spanishCount = 6;
            string jsonPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--seed" && i + 1 < args.Length)
                {
                    seed = int.Parse(args[++i], CultureInfo.InvariantCulture);
                }
                else if (arg == "--turns" && i + 1 < args.Length)
                {
                    maxTurns = int.Parse(args[++i], CultureInfo.InvariantCulture);
                }
                else if (arg == "--spanish" && i + 1 < args.Length)
                {
                    spanishCount = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    if (spanishCount < 1)
                    {
                        spanishCount = 1;
                    }
                    else if (spanishCount > 14)
                    {
                        spanishCount = 14;
                    }
                }
                else if (arg == "--json" && i + 1 < args.Length)
                {
                    jsonPath = args[++i];
                    quiet = true;
                }
                else if (arg == "--quiet")
                {
                    quiet = true;
                }
                else if (arg == "--log")
                {
                    showLog = true;
                }
                else if (arg == "--help" || arg == "-h")
                {
                    PrintUsage();
                    return 0;
                }
            }

            BattleGrid grid = BuildMap();
            List<CombatUnit> units = BuildRosters(spanishCount);

            CombatConfig config = new CombatConfig
            {
                RandomSeed = seed,
                MaxTurns = maxTurns,
                MinimumDamage = 1f,
                CriticalHitMultiplier = 2f,
                AllowDiagonalMovement = false,
                StackingPolicy = ModifierStackingPolicy.AdditivePercent,
                MitigationMode = DamageMitigationMode.Subtractive,
                LogModifierEvents = false
            };

            KapatiranResolver kapatiran = new KapatiranResolver(
                BuildBonds(),
                KapatiranProximityRule.Orthogonal);

            BattleSimulator simulator = new BattleSimulator(
                grid,
                units,
                config,
                terrain: TerrainModifierProvider.CreateCapstoneTable2ForTesting(),
                kapatiran: kapatiran);

            Console.WriteLine();
            Console.WriteLine("  BINAKAYAN RISING - combat resolver, headless slice");
            Console.WriteLine("  Battle of Binakayan-Dalahican, 9-11 November 1896");
            Console.WriteLine("  seed " + seed + ", turn cap " + maxTurns
                + ", assaulting column " + spanishCount + " strong");
            Console.WriteLine();
            PrintLegend(simulator);
            Console.WriteLine();
            Console.WriteLine("  DEPLOYMENT");
            PrintMap(grid, simulator.Units);
            PrintRoster(simulator);

            while (!simulator.IsFinished)
            {
                BattleTurnResult turn = simulator.ExecuteTurn();

                if (quiet)
                {
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine("  ---- AI TURN " + turn.TurnNumber + " ----");
                PrintTurnEvents(turn, simulator);
                PrintMap(grid, simulator.Units);
                PrintRoster(simulator);
            }

            BattleResult result = simulator.GetResultSoFar();

            Console.WriteLine();
            Console.WriteLine("  =====================================================");
            Console.WriteLine("  OUTCOME: " + result.Outcome);
            Console.WriteLine("  " + result);
            Console.WriteLine("  events logged: " + result.Events.Count
                + "   (this log is what the Unity layer will replay to animate sprites)");
            Console.WriteLine("  =====================================================");
            Console.WriteLine();

            if (showLog)
            {
                Console.WriteLine(result.ToLogText());
                Console.WriteLine();
            }

            if (jsonPath != null)
            {
                System.IO.File.WriteAllText(jsonPath, ToJson(grid, simulator, result, seed));
                Console.WriteLine("  wrote replay JSON -> " + jsonPath);
            }

            return 0;
        }

        /// <summary>
        /// Serialises the finished battle - map, roster and the full event log - for the web replay
        /// viewer. This is the same data the Unity presentation layer will consume; the point of the
        /// exercise is that the simulation is renderable after the fact by anything at all.
        /// </summary>
        private static string ToJson(IBattleGrid grid, BattleSimulator simulator, BattleResult result, int seed)
        {
            StringBuilder json = new StringBuilder(result.Events.Count * 80);
            json.Append("{\"seed\":").Append(Num(seed));
            json.Append(",\"outcome\":\"").Append(result.Outcome).Append("\"");
            json.Append(",\"turns\":").Append(Num(result.TurnsElapsed));
            json.Append(",\"width\":").Append(Num(grid.Width));
            json.Append(",\"height\":").Append(Num(grid.Height));

            json.Append(",\"terrain\":[");
            for (int y = 0; y < grid.Height; y++)
            {
                if (y > 0)
                {
                    json.Append(',');
                }

                json.Append('[');
                for (int x = 0; x < grid.Width; x++)
                {
                    if (x > 0)
                    {
                        json.Append(',');
                    }

                    json.Append(Num((int)grid.GetTerrain(new GridCoord(x, y))));
                }

                json.Append(']');
            }

            json.Append("],\"units\":[");
            for (int i = 0; i < simulator.Units.Count; i++)
            {
                CombatUnit unit = simulator.Units[i];
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append("{\"id\":").Append(Num(unit.Id));
                json.Append(",\"name\":\"").Append(Escape(unit.Name)).Append("\"");
                json.Append(",\"glyph\":\"").Append(Escape(Glyph(unit))).Append("\"");
                json.Append(",\"team\":\"").Append(unit.Team).Append("\"");
                json.Append(",\"maxHp\":").Append(Num(unit.BaseStats.MaxHP));
                json.Append(",\"range\":").Append(Num(unit.BaseStats.AttackRange));
                json.Append(",\"x\":").Append(Num(StartX(result, unit)));
                json.Append(",\"y\":").Append(Num(StartY(result, unit)));
                json.Append('}');
            }

            json.Append("],\"events\":[");
            for (int i = 0; i < result.Events.Count; i++)
            {
                BattleEvent e = result.Events[i];
                if (i > 0)
                {
                    json.Append(',');
                }

                json.Append("{\"t\":").Append(Num(e.Turn));
                json.Append(",\"k\":\"").Append(e.Type).Append("\"");
                json.Append(",\"a\":").Append(Num(e.ActorId));
                json.Append(",\"g\":").Append(Num(e.TargetId));
                json.Append(",\"fx\":").Append(Num(e.From.X)).Append(",\"fy\":").Append(Num(e.From.Y));
                json.Append(",\"tx\":").Append(Num(e.To.X)).Append(",\"ty\":").Append(Num(e.To.Y));
                json.Append(",\"amt\":").Append(Num(e.Amount));
                json.Append(",\"crit\":").Append(e.WasCrit ? "true" : "false");
                json.Append(",\"eva\":").Append(e.WasEvaded ? "true" : "false");
                json.Append(",\"miss\":").Append(e.WasMissed ? "true" : "false");
                json.Append('}');
            }

            json.Append("]}");
            return json.ToString();
        }

        /// <summary>
        /// Recovers a unit's deployment cell by rewinding its first recorded move. The simulator
        /// mutates positions in place, so by the time the battle is over the starting cell only
        /// exists in the log - which is exactly the property the replay viewer depends on.
        /// </summary>
        private static int StartX(BattleResult result, CombatUnit unit)
        {
            for (int i = 0; i < result.Events.Count; i++)
            {
                BattleEvent e = result.Events[i];
                if (e.Type == BattleEventType.UnitMoved && e.ActorId == unit.Id)
                {
                    return e.From.X;
                }
            }

            return unit.Position.X;
        }

        private static int StartY(BattleResult result, CombatUnit unit)
        {
            for (int i = 0; i < result.Events.Count; i++)
            {
                BattleEvent e = result.Events[i];
                if (e.Type == BattleEventType.UnitMoved && e.ActorId == unit.Id)
                {
                    return e.From.Y;
                }
            }

            return unit.Position.Y;
        }

        private static string Num(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string Num(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void PrintUsage()
        {
            Console.WriteLine("usage: run.sh [--seed N] [--spanish N] [--turns N] [--quiet] [--log]");
            Console.WriteLine("  --seed N   battle seed; the same seed always replays the same battle");
            Console.WriteLine("  --spanish N  size of the assaulting Spanish column, 1-14 (default 6)");
            Console.WriteLine("  --turns N  turn cap before a draw is declared (default 120)");
            Console.WriteLine("  --quiet    only print the final outcome");
            Console.WriteLine("  --log      dump the raw structured event log at the end");
            Console.WriteLine("  --json P   write the battle to P as JSON for the web replay viewer");
        }

        /// <summary>
        /// The terrain of the engagement: Evangelista's trench line on the Katipunan right, the
        /// Dalahican tidal shallows along the shore, bamboo barricades on the flanks, and the
        /// encampment tents at the rear.
        /// </summary>
        private static BattleGrid BuildMap()
        {
            BattleGrid grid = new BattleGrid(MapWidth, MapHeight, TerrainType.StandardGrid);

            for (int x = 2; x <= 9; x++)
            {
                grid.SetTerrain(new GridCoord(x, 0), TerrainType.CoastalShallows);
                grid.SetTerrain(new GridCoord(x, 1), TerrainType.CoastalShallows);
            }

            for (int y = 1; y <= 7; y++)
            {
                grid.SetTerrain(new GridCoord(10, y), TerrainType.Trench);
                grid.SetDeployable(new GridCoord(10, y), true);
            }

            for (int y = 3; y <= 6; y++)
            {
                grid.SetTerrain(new GridCoord(11, y), TerrainType.EncampmentTent);
                grid.SetDeployable(new GridCoord(11, y), true);
            }

            grid.SetTerrain(new GridCoord(5, 0), TerrainType.BambooBarricade);
            grid.SetTerrain(new GridCoord(6, 0), TerrainType.BambooBarricade);
            grid.SetTerrain(new GridCoord(5, 8), TerrainType.BambooBarricade);
            grid.SetTerrain(new GridCoord(6, 8), TerrainType.BambooBarricade);

            return grid;
        }

        /// <summary>
        /// Both rosters. The Katipunan hold the trench line at Movement Speed 0 - they are dug in,
        /// which is what the battle was - while the Spanish column advances across the open ground.
        /// </summary>
        /// <remarks>
        /// Every number below is a placeholder. The capstone document publishes no base stat block,
        /// so these exist only to make the slice playable and are not a balance proposal.
        /// </remarks>
        private static List<CombatUnit> BuildRosters(int spanishCount)
        {
            List<CombatUnit> units = new List<CombatUnit>();

            units.Add(Entrenched(1, "Marksman", "Caviteno Marksman", 10, 2,
                maxHP: 90f, attack: 14f, defense: 4f, accuracy: 0.75f, range: 2f, crit: 0.15f));
            units.Add(Entrenched(2, "Engineer", "Trench Engineer", 10, 3,
                maxHP: 110f, attack: 10f, defense: 12f, accuracy: 0.85f, range: 1f, crit: 0.05f));
            units.Add(Entrenched(3, "Evangelista", "Gen. Evangelista", 10, 5,
                maxHP: 140f, attack: 16f, defense: 8f, accuracy: 0.90f, range: 1f, crit: 0.10f));
            units.Add(Entrenched(4, "Aguinaldo", "Emilio Aguinaldo", 11, 5,
                maxHP: 130f, attack: 15f, defense: 7f, accuracy: 0.90f, range: 1f, crit: 0.15f));
            units.Add(Entrenched(5, "Vanguard", "Katipunero Vanguard", 10, 6,
                maxHP: 150f, attack: 15f, defense: 10f, accuracy: 0.85f, range: 1f, crit: 0.10f));

            for (int i = 0; i < spanishCount; i++)
            {
                int id = 10 + i;
                int y = 2 + (i % 7);
                int x = 1 - (i / 7);

                units.Add(new CombatUnit(
                    id,
                    "Spanish Regular " + (i + 1),
                    "SpanishRegular",
                    Team.Spanish,
                    new UnitStats(100f, 14f, 5f, 0.05f, 0.85f, 1f, 0.10f, 1f),
                    new GridCoord(x, y)));
            }

            return units;
        }

        private static CombatUnit Entrenched(
            int id, string archetype, string displayName, int x, int y,
            float maxHP, float attack, float defense, float accuracy, float range, float crit)
        {
            return new CombatUnit(
                id,
                displayName,
                archetype,
                Team.Katipunan,
                new UnitStats(maxHP, attack, defense, 0.05f, accuracy, range, crit, 0f),
                new GridCoord(x, y));
        }

        /// <summary>
        /// Two rows of Capstone Table 3, already resolved to a rank. In the shipped game these
        /// arrive from KapatiranBondData assets; Core never hardcodes them.
        /// </summary>
        private static List<KapatiranBond> BuildBonds()
        {
            return new List<KapatiranBond>
            {
                new KapatiranBond(
                    "Marksman_Engineer",
                    "Marksman",
                    "Engineer",
                    new List<StatModifier>
                    {
                        StatModifier.Percent(StatKind.RangedAccuracy, 0.20f, ModifierSource.Kapatiran, "Marksman_Engineer"),
                        StatModifier.Flat(StatKind.AttackRange, 1f, ModifierSource.Kapatiran, "Marksman_Engineer")
                    },
                    "A"),
                new KapatiranBond(
                    "Evangelista_Aguinaldo",
                    "Evangelista",
                    "Aguinaldo",
                    new List<StatModifier>
                    {
                        StatModifier.Percent(StatKind.AttackDamage, 0.15f, ModifierSource.Kapatiran, "Evangelista_Aguinaldo"),
                        StatModifier.Percent(StatKind.Defense, 0.10f, ModifierSource.Kapatiran, "Evangelista_Aguinaldo")
                    },
                    "A")
            };
        }

        private static void PrintLegend(BattleSimulator simulator)
        {
            Console.WriteLine("  terrain   . open   = trench(+20% DEF, +15% EVA)   ~ shallows(-15% SPD, -10% DEF)");
            Console.WriteLine("            # bamboo barricade (impassable)         ^ encampment tent (+5% HP/turn)");
            Console.Write("  units     ");
            for (int i = 0; i < simulator.Units.Count; i++)
            {
                CombatUnit unit = simulator.Units[i];
                if (unit.Team != Team.Katipunan)
                {
                    continue;
                }

                Console.Write(Glyph(unit) + "=" + unit.Name + "  ");
            }

            Console.WriteLine();
            Console.WriteLine("            1-6 = Spanish Regulars");
        }

        /// <summary>One character per unit: Katipunan get a letter, Spanish get a digit.</summary>
        private static string Glyph(CombatUnit unit)
        {
            if (unit.Team == Team.Spanish)
            {
                return ((unit.Id - 9) % 10).ToString(CultureInfo.InvariantCulture);
            }

            switch (unit.ArchetypeId)
            {
                case "Marksman": return "M";
                case "Engineer": return "T";
                case "Evangelista": return "E";
                case "Aguinaldo": return "A";
                case "Vanguard": return "V";
                default: return "K";
            }
        }

        private static char TerrainGlyph(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Trench: return '=';
                case TerrainType.CoastalShallows: return '~';
                case TerrainType.BambooBarricade: return '#';
                case TerrainType.EncampmentTent: return '^';
                default: return '.';
            }
        }

        private static void PrintMap(IBattleGrid grid, IReadOnlyList<CombatUnit> units)
        {
            Console.WriteLine();
            for (int y = grid.Height - 1; y >= 0; y--)
            {
                StringBuilder row = new StringBuilder("   ");
                row.Append(y.ToString(CultureInfo.InvariantCulture)).Append(" ");

                for (int x = 0; x < grid.Width; x++)
                {
                    GridCoord cell = new GridCoord(x, y);
                    string glyph = null;

                    for (int i = 0; i < units.Count; i++)
                    {
                        if (units[i].IsAlive && units[i].Position == cell)
                        {
                            glyph = Glyph(units[i]);
                            break;
                        }
                    }

                    row.Append(' ').Append(glyph ?? TerrainGlyph(grid.GetTerrain(cell)).ToString());
                }

                Console.WriteLine(row.ToString());
            }

            StringBuilder axis = new StringBuilder("     ");
            for (int x = 0; x < grid.Width; x++)
            {
                axis.Append(' ').Append((x % 10).ToString(CultureInfo.InvariantCulture));
            }

            Console.WriteLine(axis.ToString());
        }

        private static void PrintRoster(BattleSimulator simulator)
        {
            Console.WriteLine();
            PrintTeam(simulator, Team.Katipunan, "KATIPUNAN");
            PrintTeam(simulator, Team.Spanish, "SPANISH  ");
        }

        private static void PrintTeam(BattleSimulator simulator, Team team, string label)
        {
            StringBuilder line = new StringBuilder("   " + label + " ");
            for (int i = 0; i < simulator.Units.Count; i++)
            {
                CombatUnit unit = simulator.Units[i];
                if (unit.Team != team)
                {
                    continue;
                }

                line.Append(Glyph(unit)).Append(':');
                if (!unit.IsAlive)
                {
                    line.Append("DEAD  ");
                    continue;
                }

                float max = unit.GetEffectiveStat(StatKind.MaxHP);
                int filled = max <= 0f ? 0 : (int)Math.Round((double)(unit.CurrentHP / max) * 8d);
                line.Append('[');
                for (int b = 0; b < 8; b++)
                {
                    line.Append(b < filled ? '#' : '-');
                }

                line.Append(']').Append(' ');
            }

            Console.WriteLine(line.ToString());
        }

        private static void PrintTurnEvents(BattleTurnResult turn, BattleSimulator simulator)
        {
            for (int i = 0; i < turn.Events.Count; i++)
            {
                BattleEvent e = turn.Events[i];
                switch (e.Type)
                {
                    case BattleEventType.UnitMoved:
                        Console.WriteLine("    " + Name(simulator, e.ActorId) + " advances " + e.From + " -> " + e.To);
                        break;
                    case BattleEventType.HpRegenerated:
                        Console.WriteLine("    " + Name(simulator, e.ActorId) + " recovers "
                            + e.Amount.ToString("0.#", CultureInfo.InvariantCulture) + " HP in the " + e.Detail);
                        break;
                    case BattleEventType.DamageDealt:
                        Console.WriteLine("    " + Describe(simulator, e));
                        break;
                    case BattleEventType.UnitDied:
                        Console.WriteLine("    *** " + Name(simulator, e.ActorId) + " is routed at " + e.From + " ***");
                        break;
                }
            }
        }

        private static string Describe(BattleSimulator simulator, BattleEvent e)
        {
            string attacker = Name(simulator, e.ActorId);
            string target = Name(simulator, e.TargetId);

            if (e.WasEvaded)
            {
                return attacker + " attacks " + target + " - dodged";
            }

            if (e.WasMissed)
            {
                return attacker + " attacks " + target + " - missed";
            }

            return attacker + " hits " + target + " for "
                + e.Amount.ToString("0.#", CultureInfo.InvariantCulture) + " damage"
                + (e.WasCrit ? "  ** CRITICAL **" : string.Empty);
        }

        private static string Name(BattleSimulator simulator, int unitId)
        {
            for (int i = 0; i < simulator.Units.Count; i++)
            {
                if (simulator.Units[i].Id == unitId)
                {
                    return simulator.Units[i].Name;
                }
            }

            return "#" + unitId;
        }
    }
}
