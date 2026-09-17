using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
        public enum Phase
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

        private static readonly Color KatipunanColor = new Color32(0x8C, 0x2E, 0x22, 0xFF);
        private static readonly Color SpanishColor = new Color32(0x2E, 0x4A, 0x6B, 0xFF);

        // Sorting layers, so the board stacks by role rather than by whoever happened to be
        // spawned last. Everything used to sit on Default and rely on sortingOrder alone, which
        // meant a shadow and a tile two rows away could trade places as the board grew.
        private const string TerrainLayer = "Terrain";
        private const string TerrainDecorLayer = "TerrainDecor";
        private const string ShadowsLayer = "Shadows";
        private const string UnitsLayer = "Units";
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

        /// <summary>True when something this project owns is already running the scene.</summary>
        /// <remarks>
        /// The check spans every <c>BinakayanRising.*</c> namespace, not just Gameplay. A scene
        /// driven by a UI screen — the styleguide harness, or any authored screen — is just as
        /// driven as one running the battle prototype, and bootstrapping the prototype on top of
        /// it draws the legacy HUD over whatever that scene was actually for.
        /// </remarks>
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
                if (type.Namespace != null && type.Namespace.StartsWith("BinakayanRising."))
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

        /// <summary>
        /// A unit's presentation state at one instant, copied out for the HUD to read.
        /// </summary>
        /// <remarks>
        /// The HUD is handed copies rather than the live view objects so it cannot reach into
        /// replay state and mutate it. Everything the interface needs to draw a unit is here;
        /// nothing it does not need is.
        /// </remarks>
        public readonly struct UnitSnapshot
        {
            /// <summary>Simulation id.</summary>
            public readonly int Id;

            /// <summary>Full name, for the order-of-battle list.</summary>
            public readonly string DisplayName;

            /// <summary>Abbreviation, for the board token label.</summary>
            public readonly string ShortName;

            /// <summary>Which side the unit fights for.</summary>
            public readonly Team Team;

            /// <summary>Health remaining.</summary>
            public readonly float CurrentHP;

            /// <summary>Health at full strength.</summary>
            public readonly float MaxHP;

            /// <summary>False once the unit has been removed from the board.</summary>
            public readonly bool Alive;

            /// <summary>Where the unit currently stands, in world space.</summary>
            public readonly Vector3 World;

            /// <summary>Creates a snapshot.</summary>
            public UnitSnapshot(
                int id, string displayName, string shortName, Team team,
                float currentHP, float maxHP, bool alive, Vector3 world)
            {
                Id = id;
                DisplayName = displayName;
                ShortName = shortName;
                Team = team;
                CurrentHP = currentHP;
                MaxHP = maxHP;
                Alive = alive;
                World = world;
            }

            /// <summary>Health as a 0..1 fraction, safe when the unit has no maximum.</summary>
            public float HealthFraction => MaxHP <= 0f ? 0f : Mathf.Clamp01(CurrentHP / MaxHP);
        }

        /// <summary>A floating damage or status number, copied out for the HUD to read.</summary>
        public readonly struct PopupSnapshot
        {
            /// <summary>What the number says.</summary>
            public readonly string Text;

            /// <summary>Where it is anchored, in world space.</summary>
            public readonly Vector3 World;

            /// <summary>Colour, before the age fade is applied.</summary>
            public readonly Color Tint;

            /// <summary>Seconds since the popup appeared.</summary>
            public readonly float Age;

            /// <summary>Creates a snapshot.</summary>
            public PopupSnapshot(string text, Vector3 world, Color tint, float age)
            {
                Text = text;
                World = world;
                Tint = tint;
                Age = age;
            }
        }

        /// <summary>
        /// Builds the heads-up display for a playtest, if a UI layer has registered one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is how the battle gets an interface without the Gameplay assembly referencing the
        /// UI assembly — which would invert the dependency and let simulation code reach into
        /// widgets. The UI assembly registers a factory before the first scene loads; this class
        /// calls it and never learns what it built.
        /// </para>
        /// <para>
        /// A null factory is a supported configuration, not an error: the prototype still runs,
        /// plays and resolves with no HUD at all, which is what the offline
        /// <c>Tools/battle-sim</c> path relies on.
        /// </para>
        /// </remarks>
        public static System.Func<GameObject, Component> HudFactory;

        /// <summary>
        /// Raised when something the interface draws has structurally changed — the phase, the
        /// roster selection, the log, the result.
        /// </summary>
        /// <remarks>
        /// Per-frame values such as health and unit positions are deliberately not announced here.
        /// They change on almost every frame of a replay, so the HUD polls
        /// <see cref="GetUnits"/> instead and this event stays a rebuild signal rather than a
        /// firehose.
        /// </remarks>
        public event System.Action StateChanged;

        /// <summary>Raised when a unit is set down on the board, for placement feedback.</summary>
        /// <remarks>
        /// Separate from <see cref="StateChanged"/> because it is a moment rather than a state:
        /// a sound should play once, on the placement, not on every rebuild that follows it.
        /// </remarks>
        public event System.Action UnitPlaced;

        /// <summary>Which stage of the mission is running.</summary>
        public Phase CurrentPhase => phase;

        /// <summary>Seed the next assault will be resolved from.</summary>
        public int Seed => seed;

        /// <summary>Size of the Spanish column the next assault will face.</summary>
        public int SpanishCount => spanishCount;

        /// <summary>Replay rate. Zero means resolve the whole battle instantly.</summary>
        public float Speed => speed;

        /// <summary>AI turn currently being replayed.</summary>
        public int CurrentTurn => currentTurn;

        /// <summary>Index of the roster slot awaiting placement, or -1.</summary>
        public int SelectedSlot => selectedSlot;

        /// <summary>Whether the Kapatiran bond tips are expanded.</summary>
        public bool ShowHelp => showHelp;

        /// <summary>The resolved battle, once one exists.</summary>
        public BattleResult Result => result;

        /// <summary>The Katipunan roster available for deployment.</summary>
        public IReadOnlyList<RosterEntry> Roster => roster;

        /// <summary>Most recent field-report lines, oldest first.</summary>
        public IReadOnlyList<string> Ticker => ticker;

        /// <summary>How many roster units are standing on the board.</summary>
        public int PlacementCount => placements.Count;

        /// <summary>The camera framing the board, for world-to-screen conversion.</summary>
        public Camera BoardCamera => view;

        /// <summary>True when the given roster unit has been placed.</summary>
        public bool IsPlaced(int unitId) => placements.ContainsKey(unitId);

        /// <summary>Copies the current unit states into <paramref name="into"/>.</summary>
        /// <remarks>
        /// Fills a caller-owned list rather than returning a new one: this runs every frame during
        /// a replay, and allocating a list per frame is how a smooth replay turns into a stuttering
        /// one once the garbage collector catches up.
        /// </remarks>
        public void GetUnits(List<UnitSnapshot> into)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();
            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                UnitView unit = pair.Value;
                into.Add(new UnitSnapshot(
                    unit.Id, unit.DisplayName, unit.ShortName, unit.Team,
                    unit.CurrentHP, unit.MaxHP, unit.Alive,
                    unit.Root != null ? unit.Root.transform.position : Vector3.zero));
            }
        }

        /// <summary>Copies the live floating numbers into <paramref name="into"/>.</summary>
        public void GetPopups(List<PopupSnapshot> into)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();
            for (int i = 0; i < popups.Count; i++)
            {
                Popup popup = popups[i];
                into.Add(new PopupSnapshot(popup.Text, popup.World, popup.Tint, popup.Age));
            }
        }

        /// <summary>Sets the seed for the next assault.</summary>
        public void SetSeed(int value)
        {
            if (seed == value)
            {
                return;
            }

            seed = value;
            RaiseStateChanged();
        }

        /// <summary>Sets the replay rate. Zero resolves the remaining events immediately.</summary>
        public void SetSpeed(float value)
        {
            if (Mathf.Approximately(speed, value))
            {
                return;
            }

            speed = value;
            RaiseStateChanged();
        }

        /// <summary>Sets the size of the Spanish column, clamped to what the map can hold.</summary>
        public void SetSpanishCount(int value)
        {
            int clamped = Mathf.Clamp(value, 1, 14);
            if (spanishCount == clamped)
            {
                return;
            }

            spanishCount = clamped;

            // The opposing column is spawned as part of entering deployment, so changing its size
            // has to rebuild the board or the number and the board disagree.
            EnterDeployment();
        }

        /// <summary>Selects a roster slot for placement.</summary>
        public void SelectSlot(int index)
        {
            if (selectedSlot == index)
            {
                return;
            }

            selectedSlot = index;
            RaiseStateChanged();
        }

        /// <summary>Expands or collapses the Kapatiran bond tips.</summary>
        public void SetShowHelp(bool value)
        {
            if (showHelp == value)
            {
                return;
            }

            showHelp = value;
            RaiseStateChanged();
        }

        /// <summary>Returns to deployment, clearing any resolved battle.</summary>
        public void RequestRedeploy()
        {
            EnterDeployment();
        }

        /// <summary>Resolves and begins replaying the battle.</summary>
        public void RequestAssault()
        {
            BeginAssault();
        }

        /// <summary>Fills every empty deployment slot automatically.</summary>
        public void RequestAutoDeploy()
        {
            AutoDeploy();
        }

        /// <summary>Replays the same formation against a fresh seed.</summary>
        public void RequestNewSeed()
        {
            seed++;
            BeginAssault();
        }

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke();
        }

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

            // Built last: the HUD reads the board, the roster and the camera as it builds itself,
            // so all three have to exist before it runs.
            HudFactory?.Invoke(gameObject);
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

                    TerrainType terrain = grid.GetTerrain(cell);

                    SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sprite = BoardArt.Tile(terrain);

                    // A painted tile already carries its own earth, water and timber tones. The
                    // terrain tint exists only to give the placeholder white diamond a colour, so
                    // applying it over real art multiplies the whole board down into mud.
                    renderer.color = BoardArt.TilesAreThemed ? Color.white : TerrainColor(terrain);
                    renderer.sortingLayerName = TerrainLayer;
                    renderer.sortingOrder = SortingFor(cell);

                    if (!grid.IsDeployable(cell))
                    {
                        continue;
                    }

                    GameObject highlight = new GameObject("Deployable " + x + "," + y);
                    highlight.transform.SetParent(boardRoot, false);
                    highlight.transform.position = CellToWorld(cell);

                    SpriteRenderer highlightRenderer = highlight.AddComponent<SpriteRenderer>();
                    highlightRenderer.sprite = BoardArt.DeployMarker();
                    highlightRenderer.color = BoardArt.DeployMarkerIsThemed
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0.18f);
                    highlightRenderer.sortingLayerName = TerrainDecorLayer;
                    highlightRenderer.sortingOrder = SortingFor(cell) + 1;
                    deployHighlights.Add(highlightRenderer);
                }
            }
        }

        /// <summary>Shows or hides the markers over every deployable cell.</summary>
        private void ShowDeployHighlights(bool visible)
        {
            for (int i = 0; i < deployHighlights.Count; i++)
            {
                if (deployHighlights[i] != null)
                {
                    deployHighlights[i].enabled = visible;
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
            ShowDeployHighlights(true);

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

            RaiseStateChanged();
        }

        /// <summary>
        /// Hands the deployment to the resolver, which runs the entire battle to completion
        /// immediately, then rewinds the view so the log can be replayed.
        /// </summary>
        private void BeginAssault()
        {
            // The deployment zone is an instruction, not scenery. Left lit through the replay it
            // keeps telling the player to place units on a board they can no longer place on.
            ShowDeployHighlights(false);

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
            RaiseStateChanged();
        }

        private void Update()
        {
            HandleBoardInput();
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
            RaiseStateChanged();
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

                // A themed token is painted in its own team colour already; only the fallback
                // white disc needs one multiplied over it.
                Color tint = BoardArt.TokensAreThemed
                    ? Color.white
                    : (unit.Team == Team.Katipunan ? KatipunanColor : SpanishColor);
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

        /// <summary>
        /// Turns a click on the board into a placement, ignoring clicks that landed on the HUD.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The interface is asked whether it consumed the pointer, rather than the board testing
        /// the click against a list of hardcoded panel rectangles as the IMGUI version did. That
        /// old approach silently broke every time a panel moved or resized, because the rectangle
        /// it tested and the rectangle it drew were two separate numbers that had to be kept
        /// in agreement by hand.
        /// </para>
        /// <para>
        /// <see cref="EventSystem.current"/> may legitimately be null — the offline harness runs
        /// this component with no interface at all — so a missing event system means "nothing is
        /// covering the board", not an error.
        /// </para>
        /// </remarks>
        private void HandleBoardInput()
        {
            if (phase != Phase.Deployment)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (view == null)
            {
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            GridCoord cell = layout.WorldToCell(new IsoVector(world.x, world.y));

            if (!grid.InBounds(cell))
            {
                return;
            }

            TryPlaceOrLift(cell);
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
            UnitPlaced?.Invoke();
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
            body.sprite = BoardArt.Token(team);
            body.color = BoardArt.TokensAreThemed
                ? Color.white
                : (team == Team.Katipunan ? KatipunanColor : SpanishColor);
            body.sortingLayerName = UnitsLayer;
            body.sortingOrder = SortingFor(cell) + 10;

            GameObject shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(token.transform, false);

            // Dropped below the token's centre. Centred, it reads as a halo drawn around the
            // piece; offset, it reads as the piece standing on the ground.
            shadowObject.transform.localPosition = new Vector3(0f, -0.20f, 0f);

            SpriteRenderer shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = BoardArt.Shadow();
            shadowRenderer.color = BoardArt.ShadowIsThemed
                ? Color.white
                : new Color(0f, 0f, 0f, 0.45f);
            shadowRenderer.sortingLayerName = ShadowsLayer;
            shadowRenderer.sortingOrder = SortingFor(cell) + 9;

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

            RaiseStateChanged();
        }

        private Vector3 CellToWorld(GridCoord cell)
        {
            IsoVector iso = layout.CellToWorld(cell);
            return new Vector3(iso.X, iso.Y, 0f);
        }

        /// <summary>
        /// Sorting orders reserved for each isometric row. Must exceed the number of renderers
        /// that can stack inside a single cell, and must match
        /// <see cref="BinakayanRising.Gameplay.Presentation.UnitView"/>'s <c>sortingStep</c> so the
        /// two rendering paths agree about which row a given order belongs to.
        /// </summary>
        private const int SortingStep = 16;

        /// <summary>
        /// Depth order for an isometric grid keys off <c>X + Y</c>, per the projection contract
        /// documented on <see cref="IsoGridLayout"/>. Multiplied so units can slot between tiles.
        /// </summary>
        /// <remarks>
        /// The multiplier is the width of one row's window, so every renderer belonging to a cell
        /// has to fit inside it. It was 4, which was too narrow: a unit sits at <c>+10</c>, so a
        /// unit on row <c>n</c> scored <c>4n + 10</c> while the tile on row <c>n + 3</c> scored
        /// <c>4n + 12</c> — the tile won, and the unit was drawn behind ground that is nearer the
        /// camera than it is. Widening to <see cref="SortingStep"/> gives each row an exclusive
        /// band and simultaneously reconciles this path with <c>UnitView</c>, which already
        /// reserved 16 and stacks sprite / health bar / floating text at +0 / +4 / +8 inside it.
        /// </remarks>
        private static int SortingFor(GridCoord cell)
        {
            return (cell.X + cell.Y) * SortingStep;
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
