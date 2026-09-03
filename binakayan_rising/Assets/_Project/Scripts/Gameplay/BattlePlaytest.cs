using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BinakayanRising.Gameplay
{
    /// <summary>
    /// A playable prototype of a full mission: the Deployment phase, then the autonomous Combat
    /// phase, rendered with procedurally generated placeholder art.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Architecture note, and the whole point of the exercise.</b> This component does not
    /// simulate anything. When the player presses Begin Assault it hands the deployment to
    /// <see cref="BattleSimulator"/>, which resolves the entire battle to completion in a fraction
    /// of a millisecond and hands back a <see cref="BattleResult"/>. Everything you then watch on
    /// screen is this class replaying that event log. The simulation is already over before the
    /// first sprite moves.
    /// </para>
    /// <para>
    /// That separation is what makes the battle deterministic and unit-testable with the editor
    /// closed, and it is why nothing in <c>BinakayanRising.Core</c> references UnityEngine. Keep it
    /// that way: presentation reads the log, it never asks the simulation a question mid-animation.
    /// </para>
    /// <para>
    /// <b>Placeholder-grade on purpose.</b> Art is generated at runtime by
    /// <see cref="PlaceholderArt"/> and the HUD is IMGUI, so the whole slice runs with no imported
    /// assets, no prefabs, no Canvas and no scene wiring. Replace both when the real presentation
    /// layer lands.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Binakayan Rising/Battle Playtest")]
    public sealed class BattlePlaytest : MonoBehaviour
    {
        /// <summary>Which stage of the mission the prototype is in.</summary>
        private enum Phase
        {
            Deployment,
            Combat,
            Finished
        }

        /// <summary>Screen-space feedback tied to a world position, e.g. a damage number.</summary>
        private struct Popup
        {
            public string Text;
            public Vector3 World;
            public Color Tint;
            public float Age;
        }

        /// <summary>
        /// The view state of one unit, driven entirely by the event log rather than by the
        /// simulation objects, which have already finished mutating by the time replay starts.
        /// </summary>
        private sealed class UnitView
        {
            public int Id;
            public string DisplayName;
            public string ShortName;
            public Team Team;
            public float MaxHP;
            public float CurrentHP;
            public bool Alive;
            public GridCoord Cell;
            public GameObject Root;
            public SpriteRenderer Body;
            public Vector3 AnimateFrom;
            public Vector3 AnimateTo;
            public float AnimateProgress;
            public Vector3 Lunge;
        }

        private static readonly Color KatipunanColor = new Color(0.78f, 0.24f, 0.20f);
        private static readonly Color SpanishColor = new Color(0.83f, 0.66f, 0.16f);
        private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.09f, 0.88f);
        private static readonly Color AccentColor = new Color(0.95f, 0.78f, 0.35f);

        private const float MoveSeconds = 0.16f;
        private const float AttackSeconds = 0.12f;
        private const float DamageSeconds = 0.16f;
        private const float DeathSeconds = 0.30f;
        private const float TurnSeconds = 0.10f;

        private readonly Dictionary<int, UnitView> views = new Dictionary<int, UnitView>();
        private readonly List<Popup> popups = new List<Popup>();
        private readonly List<string> ticker = new List<string>();
        private readonly Dictionary<int, GridCoord> placements = new Dictionary<int, GridCoord>();

        private IsoGridLayout layout;
        private BattleGrid grid;
        private List<RosterEntry> roster;
        private Camera view;
        private Transform boardRoot;
        private Transform unitRoot;
        private readonly List<SpriteRenderer> deployHighlights = new List<SpriteRenderer>();

        private Phase phase = Phase.Deployment;
        private BattleResult result;
        private int replayIndex;
        private float eventTimer;
        private float eventDuration;
        private int currentTurn;
        private int seed = 1896;
        private int spanishCount = 6;
        private float speed = 1f;
        private int selectedSlot = -1;
        private bool showHelp = true;

        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle centeredStyle;

        /// <summary>
        /// Drops the prototype into whatever scene is running, so that pressing Play is all it takes
        /// to see a battle.
        /// </summary>
        /// <remarks>
        /// It stands down in two cases: when the scene already holds a <see cref="BattlePlaytest"/>
        /// (it was placed deliberately through the Tools menu), and when the scene holds any other
        /// <c>BinakayanRising.Gameplay</c> behaviour. The second check matters because this is
        /// scaffolding: the moment the real mission flow exists in a scene, this prototype must get
        /// out of its way rather than spawning a competing board on top of it.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneAlreadyDriven())
            {
                return;
            }

            GameObject host = new GameObject("Binakayan Rising Playtest");
            host.AddComponent<BattlePlaytest>();
        }

        /// <summary>True when something in this project's Gameplay assembly is already running the scene.</summary>
        private static bool SceneAlreadyDriven()
        {
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                System.Type type = behaviour.GetType();
                if (type.Namespace != null && type.Namespace.StartsWith("BinakayanRising.Gameplay"))
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>Adds the prototype to the open scene without entering play mode.</summary>
        [MenuItem("Tools/Binakayan Rising/Add Battle Playtest To Scene")]
        private static void AddToScene()
        {
            if (FindFirstObjectByType<BattlePlaytest>() != null)
            {
                Debug.Log("Battle Playtest is already in this scene.");
                return;
            }


            GameObject host = new GameObject("Binakayan Rising Playtest");
            host.AddComponent<BattlePlaytest>();
            Undo.RegisterCreatedObjectUndo(host, "Add Battle Playtest");
            Selection.activeGameObject = host;
        }
#endif

        private void Awake()
        {
            layout = new IsoGridLayout(1f, 0.5f);
            grid = PlaytestScenario.CreateGrid();
            roster = PlaytestScenario.KatipunanRoster();

            BuildCamera();
            BuildBoard();
            // Start ready to play. Players can still lift, rearrange, or redeploy every unit,
            // but pressing Play no longer opens on an empty battlefield.
            AutoDeploy();
        }

        private void OnDestroy()
        {
            if (boardRoot != null)
            {
                Destroy(boardRoot.gameObject);
            }

            if (unitRoot != null)
            {
                Destroy(unitRoot.gameObject);
            }
        }

        /// <summary>
        /// Creates or reuses an orthographic camera framed on the whole board. Reusing an existing
        /// camera means the prototype drops into a scene that already has one without producing two.
        /// </summary>
        private void BuildCamera()
        {
            view = Camera.main;
            if (view == null)
            {
                GameObject cameraObject = new GameObject("Battle Camera");
                cameraObject.transform.SetParent(transform, false);
                view = cameraObject.AddComponent<Camera>();
                view.tag = "MainCamera";
            }

            view.orthographic = true;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.09f, 0.10f, 0.11f);

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Vector3 world = CellToWorld(new GridCoord(x, y));
                    min = Vector3.Min(min, world);
                    max = Vector3.Max(max, world);
                }
            }

            Vector3 centre = (min + max) * 0.5f;
            float halfHeight = ((max.y - min.y) * 0.5f) + 1.2f;
            float halfWidth = ((max.x - min.x) * 0.5f) + 1.2f;
            float aspect = view.aspect <= 0f ? 16f / 9f : view.aspect;

            view.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);
            view.transform.position = new Vector3(centre.x + 1.6f, centre.y, -10f);
        }

        /// <summary>Instantiates one tinted diamond per cell, plus a highlight overlay per deployable cell.</summary>
        private void BuildBoard()
        {
            boardRoot = new GameObject("Board").transform;
            unitRoot = new GameObject("Units").transform;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    GridCoord cell = new GridCoord(x, y);
                    GameObject tile = new GameObject("Tile " + x + "," + y);
                    tile.transform.SetParent(boardRoot, false);
                    tile.transform.position = CellToWorld(cell);

                    SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sprite = PlaceholderArt.Tile;
                    renderer.color = TerrainColor(grid.GetTerrain(cell));
                    renderer.sortingOrder = SortingFor(cell);

                    if (!grid.IsDeployable(cell))
                    {
                        continue;
                    }

                    GameObject highlight = new GameObject("Deployable " + x + "," + y);
                    highlight.transform.SetParent(boardRoot, false);
                    highlight.transform.position = CellToWorld(cell);

                    SpriteRenderer highlightRenderer = highlight.AddComponent<SpriteRenderer>();
                    highlightRenderer.sprite = PlaceholderArt.Tile;
                    highlightRenderer.color = new Color(1f, 1f, 1f, 0.18f);
                    highlightRenderer.sortingOrder = SortingFor(cell) + 1;
                    deployHighlights.Add(highlightRenderer);
                }
            }
        }

        /// <summary>Resets to an empty deployment, clearing any units left over from a previous battle.</summary>
        private void EnterDeployment()
        {
            phase = Phase.Deployment;
            result = null;
            replayIndex = 0;
            currentTurn = 0;
            eventTimer = 0f;
            eventDuration = 0f;
            selectedSlot = roster.Count > 0 ? 0 : -1;

            popups.Clear();
            ticker.Clear();
            ClearViews();

            foreach (CombatUnit spanish in PlaytestScenario.SpanishColumn(spanishCount))
            {
                CreateView(spanish.Id, spanish.Name, "REG", Team.Spanish, spanish.BaseStats.MaxHP, spanish.Position);
            }

            foreach (KeyValuePair<int, GridCoord> placement in placements)
            {
                RosterEntry entry;
                if (TryGetEntry(placement.Key, out entry))
                {
                    CreateView(entry.Id, entry.DisplayName, entry.ShortName, Team.Katipunan, entry.Stats.MaxHP, placement.Value);
                }
            }
        }

        /// <summary>
        /// Hands the deployment to the resolver, which runs the entire battle to completion
        /// immediately, then rewinds the view so the log can be replayed.
        /// </summary>
        private void BeginAssault()
        {
            List<CombatUnit> units = new List<CombatUnit>();
            CombatConfig config = PlaytestScenario.Config(seed);

            foreach (KeyValuePair<int, GridCoord> placement in placements)
            {
                RosterEntry entry;
                if (!TryGetEntry(placement.Key, out entry))
                {
                    continue;
                }

                units.Add(new CombatUnit(
                    entry.Id,
                    entry.DisplayName,
                    entry.ArchetypeId,
                    Team.Katipunan,
                    entry.Stats,
                    placement.Value,
                    config.StackingPolicy));
            }

            units.AddRange(PlaytestScenario.SpanishColumn(spanishCount));

            // Capture the deployment before the simulator mutates anything: replay must start from
            // where the units stood, not from where they ended up.
            Dictionary<int, GridCoord> startCells = new Dictionary<int, GridCoord>();
            foreach (CombatUnit unit in units)
            {
                startCells[unit.Id] = unit.Position;
            }

            BattleSimulator simulator = new BattleSimulator(
                grid,
                units,
                config,
                terrain: PlaytestScenario.Terrain(),
                kapatiran: new KapatiranResolver(PlaytestScenario.Bonds()));

            result = simulator.RunToCompletion();

            ClearViews();
            foreach (CombatUnit unit in units)
            {
                string shortName = "REG";
                RosterEntry entry;
                if (TryGetEntry(unit.Id, out entry))
                {
                    shortName = entry.ShortName;
                }

                CreateView(unit.Id, unit.Name, shortName, unit.Team, unit.BaseStats.MaxHP, startCells[unit.Id]);
            }

            popups.Clear();
            ticker.Clear();
            replayIndex = 0;
            currentTurn = 0;
            eventTimer = 0f;
            eventDuration = 0f;
            phase = Phase.Combat;
            Log("The Spanish column advances on the trench line.");
        }

        private void Update()
        {
            AdvanceAnimations();

            if (phase != Phase.Combat || result == null)
            {
                return;
            }

            if (speed <= 0f)
            {
                while (replayIndex < result.Events.Count)
                {
                    ApplyEvent(result.Events[replayIndex++]);
                }

                FinishReplay();
                return;
            }

            eventTimer += Time.deltaTime * speed;

            int guard = 0;
            while (eventTimer >= eventDuration && replayIndex < result.Events.Count && guard++ < 512)
            {
                eventTimer -= eventDuration;
                BattleEvent battleEvent = result.Events[replayIndex++];
                ApplyEvent(battleEvent);
                eventDuration = DurationFor(battleEvent.Type);
            }

            if (replayIndex >= result.Events.Count && eventTimer >= eventDuration)
            {
                FinishReplay();
            }
        }

        private void FinishReplay()
        {
            phase = Phase.Finished;
            Log("Battle resolved: " + result.Outcome + " after " + result.TurnsElapsed + " AI turns.");
        }

        private static float DurationFor(BattleEventType type)
        {
            switch (type)
            {
                case BattleEventType.UnitMoved:
                    return MoveSeconds;
                case BattleEventType.UnitAttacked:
                    return AttackSeconds;
                case BattleEventType.DamageDealt:
                    return DamageSeconds;
                case BattleEventType.UnitDied:
                    return DeathSeconds;
                case BattleEventType.TurnStarted:
                    return TurnSeconds;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Applies one logged event to the view. This is the entire bridge between simulation and
        /// presentation: no other method reads simulation state.
        /// </summary>
        private void ApplyEvent(BattleEvent battleEvent)
        {
            UnitView actor = Find(battleEvent.ActorId);
            UnitView target = Find(battleEvent.TargetId);

            switch (battleEvent.Type)
            {
                case BattleEventType.TurnStarted:
                    currentTurn = battleEvent.Turn;
                    break;

                case BattleEventType.UnitMoved:
                    if (actor != null)
                    {
                        actor.Cell = battleEvent.To;
                        actor.AnimateFrom = CellToWorld(battleEvent.From);
                        actor.AnimateTo = CellToWorld(battleEvent.To);
                        actor.AnimateProgress = 0f;
                        actor.Body.sortingOrder = SortingFor(battleEvent.To) + 10;
                    }

                    break;

                case BattleEventType.UnitAttacked:
                    if (actor != null && target != null)
                    {
                        Vector3 toward = (CellToWorld(target.Cell) - CellToWorld(actor.Cell)).normalized;
                        actor.Lunge = toward * 0.18f;
                    }

                    break;

                case BattleEventType.DamageDealt:
                    ApplyDamageEvent(battleEvent, actor, target);
                    break;

                case BattleEventType.HpRegenerated:
                    if (actor != null)
                    {
                        actor.CurrentHP = Mathf.Min(actor.MaxHP, actor.CurrentHP + battleEvent.Amount);
                        AddPopup("+" + battleEvent.Amount.ToString("0"), actor, new Color(0.55f, 0.85f, 0.55f));
                    }

                    break;

                case BattleEventType.UnitDied:
                    if (actor != null)
                    {
                        actor.Alive = false;
                        actor.CurrentHP = 0f;
                        Log(actor.DisplayName + " is routed.");
                    }

                    break;
            }
        }

        private void ApplyDamageEvent(BattleEvent battleEvent, UnitView actor, UnitView target)
        {
            if (target == null)
            {
                return;
            }

            if (battleEvent.WasEvaded)
            {
                AddPopup("DODGE", target, new Color(0.65f, 0.80f, 0.95f));
                return;
            }

            if (battleEvent.WasMissed)
            {
                AddPopup("MISS", target, new Color(0.70f, 0.70f, 0.70f));
                return;
            }

            target.CurrentHP = Mathf.Max(0f, target.CurrentHP - battleEvent.Amount);

            AddPopup(
                battleEvent.Amount.ToString("0.#") + (battleEvent.WasCrit ? "!" : string.Empty),
                target,
                battleEvent.WasCrit ? new Color(1f, 0.72f, 0.25f) : new Color(1f, 0.45f, 0.40f));

            if (battleEvent.WasCrit && actor != null)
            {
                Log(actor.DisplayName + " lands a critical hit on " + target.DisplayName + ".");
            }
        }

        /// <summary>Drives movement lerps, attack lunges, death fades and popup lifetimes.</summary>
        private void AdvanceAnimations()
        {
            float step = Time.deltaTime * Mathf.Max(speed, 1f);

            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                UnitView unit = pair.Value;
                if (unit.Root == null)
                {
                    continue;
                }

                unit.AnimateProgress = Mathf.Min(1f, unit.AnimateProgress + (step / Mathf.Max(MoveSeconds, 0.01f)));
                Vector3 position = Vector3.Lerp(unit.AnimateFrom, unit.AnimateTo, Smooth(unit.AnimateProgress));

                unit.Lunge = Vector3.Lerp(unit.Lunge, Vector3.zero, Mathf.Min(1f, step * 8f));
                unit.Root.transform.position = position + unit.Lunge + new Vector3(0f, 0.12f, 0f);

                Color tint = unit.Team == Team.Katipunan ? KatipunanColor : SpanishColor;
                if (!unit.Alive)
                {
                    tint = new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.35f, 0.35f);
                }

                unit.Body.color = tint;
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup popup = popups[i];
                popup.Age += Time.deltaTime;
                if (popup.Age > 1.1f)
                {
                    popups.RemoveAt(i);
                }
                else
                {
                    popups[i] = popup;
                }
            }
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - (2f * t));
        }

        private void OnGUI()
        {
            EnsureStyles();

            float panelWidth = 268f;
            Rect topBar = new Rect(0f, 0f, Screen.width, 38f);
            Rect sidePanel = new Rect(0f, 38f, panelWidth, Screen.height - 38f);
            Rect logPanel = new Rect(Screen.width - 340f, Screen.height - 156f, 340f, 156f);

            HandleBoardInput(topBar, sidePanel, logPanel);

            DrawWorldOverlays();

            DrawPanel(topBar, PanelColor);
            DrawTopBar(topBar);

            DrawPanel(sidePanel, PanelColor);
            if (phase == Phase.Deployment)
            {
                DrawDeploymentPanel(sidePanel);
            }
            else
            {
                DrawUnitPanel(sidePanel);
            }

            DrawPanel(logPanel, PanelColor);
            DrawLog(logPanel);

            if (phase == Phase.Finished && result != null)
            {
                DrawResultOverlay();
            }
        }

        private void HandleBoardInput(Rect topBar, Rect sidePanel, Rect logPanel)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            if (phase != Phase.Deployment)
            {
                return;
            }

            Vector2 mouse = current.mousePosition;
            if (topBar.Contains(mouse) || sidePanel.Contains(mouse) || logPanel.Contains(mouse))
            {
                return;
            }

            Vector3 screen = new Vector3(mouse.x, Screen.height - mouse.y, 0f);
            Vector3 world = view.ScreenToWorldPoint(screen);
            GridCoord cell = layout.WorldToCell(new IsoVector(world.x, world.y));

            if (!grid.InBounds(cell))
            {
                return;
            }

            TryPlaceOrLift(cell);
            current.Use();
        }

        /// <summary>Places the selected roster unit, or lifts one already standing on the cell.</summary>
        private void TryPlaceOrLift(GridCoord cell)
        {
            foreach (KeyValuePair<int, GridCoord> placement in placements)
            {
                if (placement.Value == cell)
                {
                    int liftedId = placement.Key;
                    placements.Remove(liftedId);
                    selectedSlot = IndexOfEntry(liftedId);
                    EnterDeployment();
                    return;
                }
            }

            if (!grid.IsDeployable(cell) || selectedSlot < 0 || selectedSlot >= roster.Count)
            {
                return;
            }

            placements[roster[selectedSlot].Id] = cell;
            selectedSlot = NextUnplacedSlot(selectedSlot);
            EnterDeployment();
        }

        private void DrawTopBar(Rect bar)
        {
            GUI.Label(new Rect(12f, 8f, 320f, 24f), "BINAKAYAN RISING  -  playable slice", titleStyle);

            string phaseText = phase == Phase.Deployment
                ? "DEPLOYMENT"
                : (phase == Phase.Combat ? "COMBAT  -  AI turn " + currentTurn : "RESOLVED");
            GUI.Label(new Rect(348f, 10f, 260f, 22f), phaseText, labelStyle);

            float x = Screen.width - 470f;
            GUI.Label(new Rect(x, 10f, 40f, 22f), "Seed", smallStyle);
            if (GUI.Button(new Rect(x + 38f, 8f, 24f, 22f), "-"))
            {
                seed--;
            }

            GUI.Label(new Rect(x + 66f, 10f, 60f, 22f), seed.ToString(), labelStyle);
            if (GUI.Button(new Rect(x + 118f, 8f, 24f, 22f), "+"))
            {
                seed++;
            }

            GUI.Label(new Rect(x + 154f, 10f, 46f, 22f), "Speed", smallStyle);
            DrawSpeedButton(new Rect(x + 198f, 8f, 34f, 22f), 0.5f, "0.5x");
            DrawSpeedButton(new Rect(x + 234f, 8f, 28f, 22f), 1f, "1x");
            DrawSpeedButton(new Rect(x + 264f, 8f, 28f, 22f), 3f, "3x");
            DrawSpeedButton(new Rect(x + 294f, 8f, 46f, 22f), 0f, "skip");

            if (GUI.Button(new Rect(Screen.width - 118f, 8f, 106f, 22f), "Redeploy"))
            {
                EnterDeployment();
            }
        }

        private void DrawSpeedButton(Rect rect, float value, string label)
        {
            Color previous = GUI.color;
            GUI.color = Mathf.Approximately(speed, value) ? AccentColor : Color.white;
            if (GUI.Button(rect, label))
            {
                speed = value;
            }

            GUI.color = previous;
        }

        private void DrawDeploymentPanel(Rect panel)
        {
            float y = panel.y + 12f;
            GUI.Label(new Rect(14f, y, 240f, 22f), "DEPLOY YOUR KATIPUNEROS", titleStyle);
            y += 26f;
            GUI.Label(new Rect(14f, y, 244f, 34f), "Pick a unit, then click a lit tile on the trench line or a tent.", smallStyle);
            y += 40f;

            for (int i = 0; i < roster.Count; i++)
            {
                RosterEntry entry = roster[i];
                bool placed = placements.ContainsKey(entry.Id);
                Rect row = new Rect(12f, y, 244f, 46f);

                DrawPanel(row, i == selectedSlot ? new Color(0.20f, 0.18f, 0.12f, 0.95f) : new Color(1f, 1f, 1f, 0.05f));

                if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    selectedSlot = i;
                }

                Color previous = GUI.color;
                GUI.color = placed ? new Color(0.55f, 0.75f, 0.55f) : Color.white;
                GUI.Label(new Rect(row.x + 10f, row.y + 5f, 224f, 20f), entry.ShortName + "  " + entry.DisplayName, labelStyle);
                GUI.color = previous;

                GUI.Label(
                    new Rect(row.x + 10f, row.y + 24f, 224f, 18f),
                    "HP " + entry.Stats.MaxHP.ToString("0") + "   ATK " + entry.Stats.AttackDamage.ToString("0")
                        + "   DEF " + entry.Stats.Defense.ToString("0") + "   RNG " + entry.Stats.AttackRange.ToString("0")
                        + (placed ? "   [deployed]" : string.Empty),
                    smallStyle);

                y += 50f;
            }

            y += 8f;
            GUI.Label(new Rect(14f, y, 244f, 20f), "Spanish column: " + spanishCount, labelStyle);
            y += 22f;
            if (GUI.Button(new Rect(12f, y, 60f, 24f), "-") && spanishCount > 1)
            {
                spanishCount--;
                EnterDeployment();
            }

            if (GUI.Button(new Rect(76f, y, 60f, 24f), "+") && spanishCount < 14)
            {
                spanishCount++;
                EnterDeployment();
            }

            if (GUI.Button(new Rect(140f, y, 116f, 24f), "Auto-deploy"))
            {
                AutoDeploy();
            }

            y += 34f;

            GUI.enabled = placements.Count > 0;
            if (GUI.Button(new Rect(12f, y, 244f, 34f), "BEGIN ASSAULT"))
            {
                BeginAssault();
            }

            GUI.enabled = true;
            y += 44f;

            if (showHelp)
            {
                GUI.Label(
                    new Rect(14f, y, 244f, 150f),
                    "Kapatiran bonds:\n"
                        + "MRK + ENG adjacent  ->  +20% accuracy, +1 attack range (flat)\n\n"
                        + "EVA + AGU adjacent  ->  +15% attack, +10% defense\n\n"
                        + "Trench: +20% DEF, +15% EVA.  Tent: +5% HP per turn.",
                    smallStyle);
                y += 156f;
                if (GUI.Button(new Rect(12f, y, 116f, 22f), "Hide tips"))
                {
                    showHelp = false;
                }
            }
            else if (GUI.Button(new Rect(12f, y, 116f, 22f), "Show tips"))
            {
                showHelp = true;
            }
        }

        private void DrawUnitPanel(Rect panel)
        {
            float y = panel.y + 12f;
            GUI.Label(new Rect(14f, y, 240f, 22f), "ORDER OF BATTLE", titleStyle);
            y += 30f;

            y = DrawTeamRows(y, Team.Katipunan, "KATIPUNAN");
            y += 10f;
            DrawTeamRows(y, Team.Spanish, "SPANISH");
        }

        private float DrawTeamRows(float y, Team team, string heading)
        {
            GUI.Label(new Rect(14f, y, 240f, 18f), heading, smallStyle);
            y += 20f;

            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                UnitView unit = pair.Value;
                if (unit.Team != team)
                {
                    continue;
                }

                Rect bar = new Rect(14f, y + 15f, 200f, 7f);
                Color previous = GUI.color;
                GUI.color = unit.Alive ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                GUI.Label(new Rect(14f, y, 240f, 16f), unit.ShortName + "  " + unit.DisplayName, smallStyle);
                GUI.color = previous;

                DrawPanel(bar, new Color(1f, 1f, 1f, 0.12f));
                float fraction = unit.MaxHP <= 0f ? 0f : Mathf.Clamp01(unit.CurrentHP / unit.MaxHP);
                DrawPanel(
                    new Rect(bar.x, bar.y, bar.width * fraction, bar.height),
                    team == Team.Katipunan ? KatipunanColor : SpanishColor);

                GUI.Label(new Rect(220f, y + 10f, 44f, 16f), unit.CurrentHP.ToString("0"), smallStyle);
                y += 28f;
            }

            return y;
        }

        private void DrawLog(Rect panel)
        {
            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, 300f, 18f), "FIELD REPORT", smallStyle);

            float y = panel.y + 28f;
            int start = Mathf.Max(0, ticker.Count - 6);
            for (int i = start; i < ticker.Count; i++)
            {
                GUI.Label(new Rect(panel.x + 12f, y, panel.width - 24f, 18f), ticker[i], smallStyle);
                y += 19f;
            }
        }

        private void DrawResultOverlay()
        {
            Rect card = new Rect((Screen.width * 0.5f) - 210f, (Screen.height * 0.5f) - 96f, 420f, 192f);
            DrawPanel(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
            DrawPanel(card, new Color(0.10f, 0.11f, 0.13f, 0.98f));

            Color previous = GUI.color;
            GUI.color = result.Outcome == BattleOutcome.Victory ? new Color(0.6f, 0.85f, 0.55f) : AccentColor;
            GUI.Label(new Rect(card.x, card.y + 22f, card.width, 30f), result.Outcome.ToString().ToUpperInvariant(), centeredStyle);
            GUI.color = previous;

            GUI.Label(
                new Rect(card.x, card.y + 62f, card.width, 24f),
                result.TurnsElapsed + " AI turns   -   " + result.KatipunanAlive + " Katipuneros standing   -   "
                    + result.SpanishAlive + " Spanish left",
                centeredStyle);

            GUI.Label(
                new Rect(card.x, card.y + 88f, card.width, 24f),
                result.Events.Count + " events replayed from seed " + seed,
                centeredStyle);

            if (GUI.Button(new Rect(card.x + 30f, card.y + 128f, 170f, 34f), "Same deployment, new seed"))
            {
                seed++;
                BeginAssault();
            }

            if (GUI.Button(new Rect(card.x + 220f, card.y + 128f, 170f, 34f), "Redeploy"))
            {
                EnterDeployment();
            }
        }

        /// <summary>Draws unit initials, deployment hints and floating damage numbers over the board.</summary>
        private void DrawWorldOverlays()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                UnitView unit = pair.Value;
                if (unit.Root == null || !unit.Alive)
                {
                    continue;
                }

                Vector2 point = WorldToGui(unit.Root.transform.position);
                GUI.Label(new Rect(point.x - 30f, point.y - 9f, 60f, 18f), unit.ShortName, centeredStyle);
            }

            foreach (Popup popup in popups)
            {
                Vector2 point = WorldToGui(popup.World);
                float rise = popup.Age * 26f;
                Color tint = popup.Tint;
                tint.a = Mathf.Clamp01(1.2f - popup.Age);

                Color previous = GUI.color;
                GUI.color = tint;
                GUI.Label(new Rect(point.x - 40f, point.y - 28f - rise, 80f, 20f), popup.Text, centeredStyle);
                GUI.color = previous;
            }
        }

        private Vector2 WorldToGui(Vector3 world)
        {
            Vector3 screen = view.WorldToScreenPoint(world);
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, PlaceholderArt.WhitePixel);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 13;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = AccentColor;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = new Color(0.92f, 0.92f, 0.90f);

            smallStyle = new GUIStyle(GUI.skin.label);
            smallStyle.fontSize = 10;
            smallStyle.wordWrap = true;
            smallStyle.normal.textColor = new Color(0.75f, 0.76f, 0.74f);

            centeredStyle = new GUIStyle(GUI.skin.label);
            centeredStyle.fontSize = 12;
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.fontStyle = FontStyle.Bold;
            centeredStyle.normal.textColor = Color.white;
        }

        private void AutoDeploy()
        {
            placements.Clear();

            List<GridCoord> cells = new List<GridCoord>();
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    GridCoord cell = new GridCoord(x, y);
                    if (grid.IsDeployable(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            // Deploy down the trench so the two bonded pairs land adjacent to one another, which is
            // the arrangement the Kapatiran rules reward.
            int index = 0;
            foreach (RosterEntry entry in roster)
            {
                if (index >= cells.Count)
                {
                    break;
                }

                placements[entry.Id] = cells[index++];
            }

            EnterDeployment();
        }

        private int NextUnplacedSlot(int from)
        {
            for (int step = 1; step <= roster.Count; step++)
            {
                int candidate = (from + step) % roster.Count;
                if (!placements.ContainsKey(roster[candidate].Id))
                {
                    return candidate;
                }
            }

            return from;
        }

        private int IndexOfEntry(int id)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool TryGetEntry(int id, out RosterEntry entry)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Id == id)
                {
                    entry = roster[i];
                    return true;
                }
            }

            entry = default(RosterEntry);
            return false;
        }

        private void CreateView(int id, string displayName, string shortName, Team team, float maxHP, GridCoord cell)
        {
            GameObject token = new GameObject("Unit " + id + " " + displayName);
            token.transform.SetParent(unitRoot, false);

            SpriteRenderer body = token.AddComponent<SpriteRenderer>();
            body.sprite = PlaceholderArt.Token;
            body.color = team == Team.Katipunan ? KatipunanColor : SpanishColor;
            body.sortingOrder = SortingFor(cell) + 10;

            GameObject ring = new GameObject("Ring");
            ring.transform.SetParent(token.transform, false);
            SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = PlaceholderArt.Ring;
            ringRenderer.color = new Color(0f, 0f, 0f, 0.45f);
            ringRenderer.sortingOrder = SortingFor(cell) + 9;

            Vector3 world = CellToWorld(cell);
            token.transform.position = world;

            views[id] = new UnitView
            {
                Id = id,
                DisplayName = displayName,
                ShortName = shortName,
                Team = team,
                MaxHP = maxHP,
                CurrentHP = maxHP,
                Alive = true,
                Cell = cell,
                Root = token,
                Body = body,
                AnimateFrom = world,
                AnimateTo = world,
                AnimateProgress = 1f,
                Lunge = Vector3.zero
            };
        }

        private void ClearViews()
        {
            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                if (pair.Value.Root != null)
                {
                    Destroy(pair.Value.Root);
                }
            }

            views.Clear();
        }

        private UnitView Find(int id)
        {
            UnitView found;
            return views.TryGetValue(id, out found) ? found : null;
        }

        private void AddPopup(string text, UnitView unit, Color tint)
        {
            popups.Add(new Popup
            {
                Text = text,
                World = unit.Root != null ? unit.Root.transform.position : CellToWorld(unit.Cell),
                Tint = tint,
                Age = 0f
            });
        }

        private void Log(string message)
        {
            ticker.Add("T" + currentTurn + "  " + message);
            if (ticker.Count > 64)
            {
                ticker.RemoveAt(0);
            }
        }

        private Vector3 CellToWorld(GridCoord cell)
        {
            IsoVector iso = layout.CellToWorld(cell);
            return new Vector3(iso.X, iso.Y, 0f);
        }

        /// <summary>
        /// Depth order for an isometric grid keys off <c>X + Y</c>, per the projection contract
        /// documented on <see cref="IsoGridLayout"/>. Multiplied so units can slot between tiles.
        /// </summary>
        private static int SortingFor(GridCoord cell)
        {
            return (cell.X + cell.Y) * 4;
        }

        private static Color TerrainColor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Trench:
                    return new Color(0.47f, 0.40f, 0.29f);
                case TerrainType.CoastalShallows:
                    return new Color(0.27f, 0.45f, 0.52f);
                case TerrainType.BambooBarricade:
                    return new Color(0.20f, 0.24f, 0.18f);
                case TerrainType.EncampmentTent:
                    return new Color(0.62f, 0.54f, 0.35f);
                default:
                    return new Color(0.38f, 0.45f, 0.32f);
            }
        }
    }
}
