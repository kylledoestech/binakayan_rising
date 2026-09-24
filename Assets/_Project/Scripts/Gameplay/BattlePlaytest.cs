using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Grid;
using BinakayanRising.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BinakayanRising.Gameplay
{
    /// <summary>
    /// A playable prototype of a full mission: the Deployment phase, then the autonomous Combat
    /// phase, rendered with the board art registered through <see cref="BoardArt"/>.
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
    /// <b>Language-free on purpose.</b> The field report and floating numbers are recorded as
    /// structured entries — a kind plus the units involved — never as sentences. The interface
    /// turns them into text in the player's language, so switching language mid-battle re-renders
    /// the whole report rather than leaving half of it in the old one.
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

        /// <summary>What a floating number over a unit is reporting.</summary>
        public enum PopupKind
        {
            Damage,
            Critical,
            Heal,
            Dodge,
            Miss
        }

        /// <summary>What a field report line narrates.</summary>
        public enum FieldReportKind
        {
            AssaultBegan,
            UnitRouted,
            CriticalHit,
            BattleResolved
        }

        /// <summary>One field report line, recorded as data for the interface to phrase.</summary>
        public readonly struct FieldReportEntry
        {
            /// <summary>What happened.</summary>
            public readonly FieldReportKind Kind;

            /// <summary>AI turn it happened on.</summary>
            public readonly int Turn;

            /// <summary>Archetype of the acting unit, or null.</summary>
            public readonly string ActorArchetypeId;

            /// <summary>Numbering among same-archetype units (Spanish regulars), 0 when unnumbered.</summary>
            public readonly int ActorOrdinal;

            /// <summary>Archetype of the unit acted upon, or null.</summary>
            public readonly string TargetArchetypeId;

            /// <summary>Numbering of the unit acted upon.</summary>
            public readonly int TargetOrdinal;

            /// <summary>Battle outcome, for <see cref="FieldReportKind.BattleResolved"/>.</summary>
            public readonly BattleOutcome Outcome;

            /// <summary>Turns the battle lasted, for <see cref="FieldReportKind.BattleResolved"/>.</summary>
            public readonly int TurnsElapsed;

            /// <summary>Creates an entry.</summary>
            public FieldReportEntry(
                FieldReportKind kind, int turn,
                string actorArchetypeId, int actorOrdinal,
                string targetArchetypeId, int targetOrdinal,
                BattleOutcome outcome, int turnsElapsed)
            {
                Kind = kind;
                Turn = turn;
                ActorArchetypeId = actorArchetypeId;
                ActorOrdinal = actorOrdinal;
                TargetArchetypeId = targetArchetypeId;
                TargetOrdinal = targetOrdinal;
                Outcome = outcome;
                TurnsElapsed = turnsElapsed;
            }
        }

        /// <summary>Screen-space feedback tied to a world position, e.g. a damage number.</summary>
        private struct Popup
        {
            public PopupKind Kind;
            public float Amount;
            public Vector3 World;
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
            public string ArchetypeId;
            public int Ordinal;
            public Team Team;
            public float MaxHP;
            public float CurrentHP;
            public bool Alive;
            public GridCoord Cell;
            public GameObject Root;
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;
            public Vector3 AnimateFrom;
            public Vector3 AnimateTo;
            public float AnimateProgress;
            public Vector3 Lunge;

            /// <summary>True for a standing figure pivoted on its feet; false for a round token.</summary>
            public bool Figure;

            /// <summary>Figures are drawn facing right; this mirrors them.</summary>
            public bool FacingLeft;

            /// <summary>Seconds of hit tint left.</summary>
            public float HitTimer;

            /// <summary>Idle bob cycle position, offset per unit so a line does not bob in step.</summary>
            public float BobPhase;
        }

        private static readonly Color KatipunanColor = new Color32(0x8C, 0x2E, 0x22, 0xFF);
        private static readonly Color SpanishColor = new Color32(0x2E, 0x4A, 0x6B, 0xFF);
        private static readonly Color BackdropColor = new Color32(0x17, 0x19, 0x1C, 0xFF);

        // Sorting layers, so the board stacks by role rather than by whoever happened to be
        // spawned last. Everything used to sit on Default and rely on sortingOrder alone, which
        // meant a shadow and a tile two rows away could trade places as the board grew.
        private const string TerrainLayer = "Terrain";
        private const string TerrainDecorLayer = "TerrainDecor";
        private const string ShadowsLayer = "Shadows";
        private const string UnitsLayer = "Units";

        private const float MoveSeconds = 0.16f;
        private const float AttackSeconds = 0.12f;
        private const float DamageSeconds = 0.16f;
        private const float DeathSeconds = 0.30f;
        private const float TurnSeconds = 0.10f;

        private const float MinSpeed = 0.25f;
        private const float MaxSpeed = 8f;
        private const float TokenLift = 0.12f;

        // Figure motion. One figure texel is 1/80 of a world unit (the importer's PPU), so the
        // bob moves exactly one texel and never lands between two.
        private const float FigureTexel = 1f / 80f;
        private const float BobHertz = 1.5f;
        private const float HopHeight = 0.08f;
        private const float HitSeconds = 0.12f;
        private const float HitKnockback = 0.04f;
        private const float DeathSink = 0.1f;
        private const float HeadClearance = 0.08f;
        private static readonly Color HitTint = new Color(1f, 0.45f, 0.4f, 1f);
        private const float PopupLifetime = 1.1f;

        // World units of breathing room kept between the board's edge and the free screen area.
        private const float BoardPadding = 0.6f;

        private const int ReportCapacity = 64;

        private readonly Dictionary<int, UnitView> views = new Dictionary<int, UnitView>();
        private readonly List<Popup> popups = new List<Popup>();
        private readonly Dictionary<int, GridCoord> placements = new Dictionary<int, GridCoord>();
        private readonly FieldReportEntry[] report = new FieldReportEntry[ReportCapacity];
        private readonly HashSet<int> desiredScratch = new HashSet<int>();
        private readonly List<int> removeScratch = new List<int>();

        private IsoGridLayout layout;
        private BattleGrid grid;
        private List<RosterEntry> roster;
        private Camera view;
        private BattleCameraController cameraController;
        private bool addedCameraController;
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
        private int resultSeed = 1896;
        private int spanishCount = 6;
        private float speed = 1f;
        private int selectedSlot = -1;
        private bool showHelp = true;
        private bool paused;
        private bool skipRequested;
        private bool boardInputLocked;
        private MissionSetup mission;
        private bool quizAsked;
        private bool missionEnded;

        private int reportStart;
        private int reportCount;
        private int reportVersion;

        private Rect boardWorldRect;
        private Rect deployZoneWorldRect;
        private Rect boardSafeArea = new Rect(0f, 0f, 1f, 1f);
        private float framedAspect;

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
            if (!AutoBootstrap || SceneAlreadyDriven())
            {
                return;
            }

            GameObject host = new GameObject("Binakayan Rising Playtest");
            host.AddComponent<BattlePlaytest>();
        }

        /// <summary>
        /// False while the campaign shell runs the game, so the prototype waits to be launched as a
        /// mission instead of spawning its own battle at start-up. The shell's installer sets this
        /// before the first scene loads.
        /// </summary>
        public static bool AutoBootstrap = true;

        /// <summary>
        /// The campaign battle the next <see cref="BattlePlaytest"/> to wake plays, or null for the
        /// standalone playtest. Taken, and cleared, in <c>Awake</c>.
        /// </summary>
        public static MissionSetup PendingMission;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PendingMission = null;
        }

        /// <summary>True when something this project owns is already running the scene.</summary>
        /// <remarks>
        /// The check spans every <c>BinakayanRising.*</c> namespace, not just Gameplay. A scene
        /// driven by a UI screen — the styleguide harness, or any authored screen — is just as
        /// driven as one running the battle prototype, and bootstrapping the prototype on top of
        /// it draws the battle HUD over whatever that scene was actually for.
        /// </remarks>
        public static bool SceneAlreadyDriven()
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

            /// <summary>Authored English name, for logs and as a fallback.</summary>
            public readonly string DisplayName;

            /// <summary>Abbreviation, for the board token label.</summary>
            public readonly string ShortName;

            /// <summary>Archetype, which the interface maps to a localized name.</summary>
            public readonly string ArchetypeId;

            /// <summary>Numbering among same-archetype units, 0 when unnumbered.</summary>
            public readonly int Ordinal;

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

            /// <summary>
            /// Just above the top of the unit's sprite, where its name tag and damage numbers go.
            /// </summary>
            public readonly Vector3 Head;

            /// <summary>Creates a snapshot.</summary>
            public UnitSnapshot(
                int id, string displayName, string shortName, string archetypeId, int ordinal, Team team,
                float currentHP, float maxHP, bool alive, Vector3 world, Vector3 head)
            {
                Id = id;
                DisplayName = displayName;
                ShortName = shortName;
                ArchetypeId = archetypeId;
                Ordinal = ordinal;
                Team = team;
                CurrentHP = currentHP;
                MaxHP = maxHP;
                Alive = alive;
                World = world;
                Head = head;
            }

            /// <summary>Health as a 0..1 fraction, safe when the unit has no maximum.</summary>
            public float HealthFraction => MaxHP <= 0f ? 0f : Mathf.Clamp01(CurrentHP / MaxHP);
        }

        /// <summary>A floating damage or status number, copied out for the HUD to read.</summary>
        public readonly struct PopupSnapshot
        {
            /// <summary>What the number reports.</summary>
            public readonly PopupKind Kind;

            /// <summary>Damage or healing amount; unused for dodges and misses.</summary>
            public readonly float Amount;

            /// <summary>Where it is anchored, in world space.</summary>
            public readonly Vector3 World;

            /// <summary>Seconds of replay time since the popup appeared.</summary>
            public readonly float Age;

            /// <summary>Creates a snapshot.</summary>
            public PopupSnapshot(PopupKind kind, float amount, Vector3 world, float age)
            {
                Kind = kind;
                Amount = amount;
                World = world;
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
        /// Raised when something structural changed — phase, selection, deployment, seed, speed,
        /// the tips toggle. Not raised for field report lines; see <see cref="FieldReportAppended"/>.
        /// </summary>
        public event System.Action StateChanged;

        /// <summary>Raised when the phase changes, with the new phase.</summary>
        public event System.Action<Phase> PhaseChanged;

        /// <summary>Raised when the selected roster slot changes, with the new slot.</summary>
        public event System.Action<int> SelectionChanged;

        /// <summary>Raised when placements or the Spanish column change.</summary>
        public event System.Action DeploymentChanged;

        /// <summary>Raised when a unit is set down on the board, for placement feedback.</summary>
        /// <remarks>
        /// Separate from <see cref="StateChanged"/> because it is a moment rather than a state:
        /// a sound should play once, on the placement, not on every rebuild that follows it.
        /// </remarks>
        public event System.Action UnitPlaced;

        /// <summary>Raised when a placed unit is lifted off the board, with its id.</summary>
        public event System.Action<int> UnitLifted;

        /// <summary>Raised when a line is added to the field report.</summary>
        public event System.Action FieldReportAppended;

        /// <summary>Raised when the replay speed changes, with the new speed.</summary>
        public event System.Action<float> SpeedChanged;

        /// <summary>Which stage of the mission is running.</summary>
        public Phase CurrentPhase => phase;

        /// <summary>The campaign battle being played, or null in the standalone playtest.</summary>
        public MissionSetup Mission => mission;

        /// <summary>True for a campaign battle: its seed and column are the quest's, not the player's.</summary>
        public bool IsMission => mission != null;

        /// <summary>Most units that may be deployed.</summary>
        public int SquadCap => mission != null ? mission.SquadCap : int.MaxValue;

        /// <summary>True once a campaign battle has finished in the player's favour under its rule.</summary>
        public bool MissionWon => mission != null && result != null && phase == Phase.Finished && mission.IsWin(result.Outcome);

        /// <summary>Raised when a campaign battle reaches its quiz turn; the replay is paused until answered.</summary>
        public event System.Action QuizDue;

        /// <summary>Seed the next assault will be resolved from.</summary>
        public int Seed => seed;

        /// <summary>Seed the current or most recent battle was resolved from.</summary>
        public int ResultSeed => resultSeed;

        /// <summary>Size of the Spanish column the next assault will face.</summary>
        public int SpanishCount => spanishCount;

        /// <summary>Replay rate multiplier.</summary>
        public float Speed => speed;

        /// <summary>AI turn currently being replayed.</summary>
        public int CurrentTurn => currentTurn;

        /// <summary>Index of the roster slot awaiting placement, or -1.</summary>
        public int SelectedSlot => selectedSlot;

        /// <summary>Whether the Kapatiran bond tips are expanded.</summary>
        public bool ShowHelp => showHelp;

        /// <summary>True while the replay is frozen, e.g. under a tutorial card.</summary>
        public bool Paused => paused;

        /// <summary>True while board clicks are ignored.</summary>
        public bool BoardInputLocked => boardInputLocked;

        /// <summary>True while zoom and pan are ignored.</summary>
        public bool CameraInputLocked => cameraController != null && cameraController.InputLocked;

        /// <summary>The resolved battle, once one exists.</summary>
        public BattleResult Result => result;

        /// <summary>The Katipunan roster available for deployment.</summary>
        public IReadOnlyList<RosterEntry> Roster => roster;

        /// <summary>How many roster units are standing on the board.</summary>
        public int PlacementCount => placements.Count;

        /// <summary>The camera framing the board, for world-to-screen conversion.</summary>
        public Camera BoardCamera => view;

        /// <summary>World-space bounds of every tile.</summary>
        public Rect BoardWorldRect => boardWorldRect;

        /// <summary>The board's tiles, for the minimap. Null before the board is built.</summary>
        public BinakayanRising.Core.Grid.IBattleGrid Grid => grid;

        /// <summary>Glides the camera to <paramref name="world"/>, unless camera input is locked.</summary>
        public void LookAt(Vector3 world)
        {
            if (cameraController != null && !cameraController.InputLocked)
            {
                cameraController.MoveTo(world);
            }
        }

        /// <summary>World-space bounds of every deployable tile.</summary>
        public Rect DeployZoneWorldRect => deployZoneWorldRect;

        /// <summary>How many field report lines are held, up to 64.</summary>
        public int FieldReportCount => reportCount;

        /// <summary>Increments whenever the field report changes.</summary>
        public int FieldReportVersion => reportVersion;

        /// <summary>True when the given roster unit has been placed.</summary>
        public bool IsPlaced(int unitId) => placements.ContainsKey(unitId);

        /// <summary>A field report line, 0 being the oldest held.</summary>
        public FieldReportEntry GetFieldReport(int index)
        {
            return report[(reportStart + index) % ReportCapacity];
        }

        /// <summary>Where a roster unit is placed, if it is.</summary>
        public bool TryGetPlacement(int unitId, out GridCoord cell)
        {
            return placements.TryGetValue(unitId, out cell);
        }

        /// <summary>The world-space centre of a cell, if it is on the map.</summary>
        public bool TryGetCellWorld(GridCoord cell, out Vector3 world)
        {
            if (grid == null || !grid.InBounds(cell))
            {
                world = Vector3.zero;
                return false;
            }

            world = CellToWorld(cell);
            return true;
        }

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
                    unit.Id, unit.DisplayName, unit.ShortName, unit.ArchetypeId, unit.Ordinal, unit.Team,
                    unit.CurrentHP, unit.MaxHP, unit.Alive,
                    unit.Root != null ? unit.Root.transform.position : Vector3.zero,
                    HeadOf(unit)));
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
                into.Add(new PopupSnapshot(popup.Kind, popup.Amount, popup.World, popup.Age));
            }
        }

        // ------------------------------------------------------------------ commands

        /// <summary>Sets the seed for the next assault.</summary>
        public void SetSeed(int value)
        {
            if (seed == value || IsMission)
            {
                return;
            }

            seed = value;
            RaiseStateChanged();
        }

        /// <summary>Sets the replay rate, clamped to a sensible range. Use <see cref="RequestSkip"/> to jump to the end.</summary>
        public void SetSpeed(float value)
        {
            float clamped = Mathf.Clamp(value, MinSpeed, MaxSpeed);
            if (Mathf.Approximately(speed, clamped))
            {
                return;
            }

            speed = clamped;
            SpeedChanged?.Invoke(speed);
            RaiseStateChanged();
        }

        /// <summary>Sets the size of the Spanish column, clamped to what the map can hold.</summary>
        /// <remarks>
        /// Outside deployment the new size is only stored; it takes effect the next time a column
        /// is spawned. Rebuilding the board here would yank the player out of a battle or off the
        /// outcome card.
        /// </remarks>
        public void SetSpanishCount(int value)
        {
            int clamped = Mathf.Clamp(value, 1, 14);
            if (spanishCount == clamped || IsMission)
            {
                return;
            }

            spanishCount = clamped;

            if (phase == Phase.Deployment)
            {
                SyncDeploymentViews();
                DeploymentChanged?.Invoke();
            }

            RaiseStateChanged();
        }

        /// <summary>Selects a roster slot for placement, or -1 for none.</summary>
        public void SelectSlot(int index)
        {
            int clamped = roster == null || index < 0 || index >= roster.Count ? -1 : index;
            if (selectedSlot == clamped)
            {
                return;
            }

            selectedSlot = clamped;
            SelectionChanged?.Invoke(selectedSlot);
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

        /// <summary>Returns to deployment, clearing any resolved battle but keeping placements.</summary>
        public void RequestRedeploy()
        {
            EnterDeployment();
        }

        /// <summary>Resolves and begins replaying the battle.</summary>
        /// <returns>False when no unit is placed, which would be an instant defeat.</returns>
        public bool RequestAssault()
        {
            return BeginAssault();
        }

        /// <summary>Places every unit still in reserve on a free deployable tile.</summary>
        /// <remarks>Units the player already placed stay where they are.</remarks>
        public void RequestAutoDeploy()
        {
            if (phase != Phase.Deployment)
            {
                EnterDeployment();
            }

            AutoDeploy();
        }

        /// <summary>Replays the same formation against the next seed.</summary>
        public void RequestNewSeed()
        {
            if (IsMission)
            {
                return;
            }

            seed++;
            BeginAssault();
        }

        /// <summary>Places the selected roster unit on a cell.</summary>
        /// <returns>True when a unit was placed.</returns>
        public bool RequestPlace(GridCoord cell)
        {
            if (phase != Phase.Deployment || grid == null || !grid.IsDeployable(cell)
                || selectedSlot < 0 || selectedSlot >= roster.Count || IsOccupied(cell))
            {
                return false;
            }

            // A full squad takes no more; a unit already down may still be moved.
            if (placements.Count >= SquadCap && !placements.ContainsKey(roster[selectedSlot].Id))
            {
                return false;
            }

            placements[roster[selectedSlot].Id] = cell;
            SetSelectionSilently(NextUnplacedSlot(selectedSlot));
            SyncDeploymentViews();

            UnitPlaced?.Invoke();
            DeploymentChanged?.Invoke();
            SelectionChanged?.Invoke(selectedSlot);
            RaiseStateChanged();
            return true;
        }

        /// <summary>Lifts the unit standing on a cell back into reserve and selects it.</summary>
        /// <returns>True when a unit was lifted.</returns>
        public bool RequestLift(GridCoord cell)
        {
            if (phase != Phase.Deployment)
            {
                return false;
            }

            int liftedId = -1;
            foreach (KeyValuePair<int, GridCoord> placement in placements)
            {
                if (placement.Value == cell)
                {
                    liftedId = placement.Key;
                    break;
                }
            }

            if (liftedId < 0)
            {
                return false;
            }

            placements.Remove(liftedId);
            SetSelectionSilently(IndexOfEntry(liftedId));
            SyncDeploymentViews();

            UnitLifted?.Invoke(liftedId);
            DeploymentChanged?.Invoke();
            SelectionChanged?.Invoke(selectedSlot);
            RaiseStateChanged();
            return true;
        }

        /// <summary>Lifts every unit off the board and clears the selection.</summary>
        public void RequestClearDeployment()
        {
            if (phase != Phase.Deployment)
            {
                EnterDeployment();
            }

            placements.Clear();
            SetSelectionSilently(-1);
            SyncDeploymentViews();

            DeploymentChanged?.Invoke();
            SelectionChanged?.Invoke(selectedSlot);
            RaiseStateChanged();
        }

        /// <summary>Jumps the running replay straight to its result. A one-shot, not a speed.</summary>
        public void RequestSkip()
        {
            if (phase == Phase.Combat)
            {
                skipRequested = true;
            }
        }

        /// <summary>
        /// Leaves a campaign battle: reports how it went to the shell, once, then removes the board.
        /// Before the assault is fought the report is a retreat, and nothing is settled.
        /// </summary>
        public void EndMission()
        {
            if (mission == null || missionEnded)
            {
                return;
            }

            missionEnded = true;
            MissionReport report = new MissionReport();
            if (phase == Phase.Finished && result != null)
            {
                report.Outcome = result.Outcome;
                report.Won = mission.IsWin(result.Outcome);
                foreach (KeyValuePair<int, GridCoord> placement in placements)
                {
                    report.Deployed.Add(placement.Key);
                }
            }
            else
            {
                report.Retreated = true;
            }

            System.Action<MissionReport> finished = mission.Finished;
            Destroy(gameObject);
            finished?.Invoke(report);
        }

        /// <summary>Freezes or resumes the replay and its animations.</summary>
        public void SetPaused(bool value)
        {
            paused = value;
        }

        /// <summary>Ignores board clicks while true, e.g. while a modal is open.</summary>
        public void SetBoardInputLocked(bool locked)
        {
            boardInputLocked = locked;
        }

        /// <summary>Ignores zoom and pan input while true.</summary>
        public void SetCameraInputLocked(bool locked)
        {
            if (cameraController != null)
            {
                cameraController.SetInputLocked(locked);
            }
        }

        /// <summary>
        /// Tells the board which part of the screen is not covered by interface, in 0..1 viewport
        /// coordinates, and reframes the camera onto it.
        /// </summary>
        public void SetBoardSafeArea(Rect viewport01)
        {
            const float Tolerance = 0.002f;
            if (Mathf.Abs(viewport01.x - boardSafeArea.x) < Tolerance
                && Mathf.Abs(viewport01.y - boardSafeArea.y) < Tolerance
                && Mathf.Abs(viewport01.width - boardSafeArea.width) < Tolerance
                && Mathf.Abs(viewport01.height - boardSafeArea.height) < Tolerance)
            {
                return;
            }

            boardSafeArea = viewport01;
            FrameBoard(true);
        }

        /// <summary>Fits the whole board into the safe area and resets zoom and pan.</summary>
        public void FrameBoard(bool snap)
        {
            if (cameraController != null)
            {
                cameraController.FrameBounds(boardWorldRect, boardSafeArea, BoardPadding, snap);
                framedAspect = view != null ? view.aspect : 0f;
            }
        }

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke();
        }

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            layout = new IsoGridLayout(1f, 0.5f);
            grid = PlaytestScenario.CreateGrid();

            mission = PendingMission;
            PendingMission = null;
            if (mission != null)
            {
                roster = new List<RosterEntry>(mission.Roster);
                seed = mission.Seed;
                resultSeed = mission.Seed;
                spanishCount = Mathf.Clamp(mission.EnemyCount, 1, 14);
            }
            else
            {
                roster = PlaytestScenario.KatipunanRoster();
            }

            BuildBoard();
            BuildCamera();

            // Start ready to play. Players can still lift, rearrange, or redeploy every unit,
            // but pressing Play no longer opens on an empty battlefield.
            EnterDeployment();
            AutoDeploy();
            SetSelectionSilently(roster.Count > 0 ? 0 : -1);

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

            if (addedCameraController && cameraController != null)
            {
                Destroy(cameraController);
            }
        }

        /// <summary>
        /// Creates or reuses an orthographic camera and gives it zoom and pan. Reusing an existing
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
            view.backgroundColor = BackdropColor;

            // A first framing before the controller exists, because the controller reads the
            // camera's size and position in its Awake and clamps its zoom around them.
            float aspect = view.aspect <= 0f ? 16f / 9f : view.aspect;
            view.orthographicSize = Mathf.Max(
                (boardWorldRect.height * 0.5f) + BoardPadding,
                ((boardWorldRect.width * 0.5f) + BoardPadding) / aspect);
            view.transform.position = new Vector3(boardWorldRect.center.x, boardWorldRect.center.y, -10f);

            cameraController = view.GetComponent<BattleCameraController>();
            if (cameraController == null)
            {
                cameraController = view.gameObject.AddComponent<BattleCameraController>();
                addedCameraController = true;
            }

            FrameBoard(true);
        }

        /// <summary>Instantiates one tile per cell, plus a highlight overlay per deployable cell.</summary>
        private void BuildBoard()
        {
            boardRoot = new GameObject("Board").transform;
            unitRoot = new GameObject("Units").transform;

            Vector2 boardMin = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 boardMax = new Vector2(float.MinValue, float.MinValue);
            Vector2 zoneMin = boardMin;
            Vector2 zoneMax = boardMax;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    GridCoord cell = new GridCoord(x, y);
                    Vector3 world = CellToWorld(cell);
                    boardMin = Vector2.Min(boardMin, world);
                    boardMax = Vector2.Max(boardMax, world);

                    GameObject tile = new GameObject("Tile " + x + "," + y);
                    tile.transform.SetParent(boardRoot, false);
                    tile.transform.position = world;

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

                    zoneMin = Vector2.Min(zoneMin, world);
                    zoneMax = Vector2.Max(zoneMax, world);

                    GameObject highlight = new GameObject("Deployable " + x + "," + y);
                    highlight.transform.SetParent(boardRoot, false);
                    highlight.transform.position = world;

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

            // Cell positions are tile centres; a tile reaches half its width and height beyond.
            Vector2 halfTile = new Vector2(layout.TileWidth * 0.5f, layout.TileHeight * 0.5f);
            boardWorldRect = Rect.MinMaxRect(
                boardMin.x - halfTile.x, boardMin.y - halfTile.y, boardMax.x + halfTile.x, boardMax.y + halfTile.y);
            deployZoneWorldRect = zoneMax.x < zoneMin.x
                ? boardWorldRect
                : Rect.MinMaxRect(
                    zoneMin.x - halfTile.x, zoneMin.y - halfTile.y, zoneMax.x + halfTile.x, zoneMax.y + halfTile.y);
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

        // ------------------------------------------------------------------ phases

        /// <summary>
        /// Enters deployment. Coming back from a battle rebuilds every token at full health; within
        /// deployment only the tokens that changed are touched.
        /// </summary>
        private void EnterDeployment()
        {
            Phase previous = phase;
            bool rebuild = previous != Phase.Deployment || views.Count == 0;

            phase = Phase.Deployment;
            result = null;
            replayIndex = 0;
            currentTurn = 0;
            eventTimer = 0f;
            eventDuration = 0f;
            skipRequested = false;

            popups.Clear();
            ClearReport();

            if (rebuild)
            {
                ClearViews();
            }

            ShowDeployHighlights(true);
            SyncDeploymentViews();

            if (previous != Phase.Deployment)
            {
                PhaseChanged?.Invoke(phase);
            }

            DeploymentChanged?.Invoke();
            RaiseStateChanged();
        }

        /// <summary>
        /// Brings the tokens on the board in line with the Spanish column and the placements,
        /// creating, moving and destroying only what differs.
        /// </summary>
        /// <remarks>
        /// Every placement used to tear down and respawn every token, which on a full board meant
        /// forty-odd GameObjects and renderers churned per click.
        /// </remarks>
        private void SyncDeploymentViews()
        {
            desiredScratch.Clear();

            List<CombatUnit> column = PlaytestScenario.SpanishColumn(spanishCount);
            for (int i = 0; i < column.Count; i++)
            {
                CombatUnit spanish = column[i];
                desiredScratch.Add(spanish.Id);
                UpsertView(
                    spanish.Id, spanish.Name, "REG", spanish.ArchetypeId, SpanishOrdinal(spanish.Id),
                    Team.Spanish, spanish.BaseStats.MaxHP, spanish.Position);
            }

            foreach (KeyValuePair<int, GridCoord> placement in placements)
            {
                RosterEntry entry;
                if (!TryGetEntry(placement.Key, out entry))
                {
                    continue;
                }

                desiredScratch.Add(entry.Id);
                UpsertView(
                    entry.Id, entry.DisplayName, entry.ShortName, entry.ArchetypeId, 0,
                    Team.Katipunan, entry.Stats.MaxHP, placement.Value);
            }

            removeScratch.Clear();
            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                if (!desiredScratch.Contains(pair.Key))
                {
                    removeScratch.Add(pair.Key);
                }
            }

            for (int i = 0; i < removeScratch.Count; i++)
            {
                UnitView stale = views[removeScratch[i]];
                if (stale.Root != null)
                {
                    Destroy(stale.Root);
                }

                views.Remove(removeScratch[i]);
            }

            FaceEnemies();
        }

        /// <summary>
        /// Hands the deployment to the resolver, which runs the entire battle to completion
        /// immediately, then rewinds the view so the log can be replayed.
        /// </summary>
        private bool BeginAssault()
        {
            // Guarded here and not only by the button, because a hotkey reaches this too.
            if (placements.Count == 0)
            {
                return false;
            }

            // The deployment zone is an instruction, not scenery. Left lit through the replay it
            // keeps telling the player to place units on a board they can no longer place on.
            ShowDeployHighlights(false);

            List<CombatUnit> units = new List<CombatUnit>();
            CombatConfig config = PlaytestScenario.Config(seed);
            if (mission != null)
            {
                config.MaxTurns = mission.TurnCap;
            }

            quizAsked = false;

            foreach (KeyValuePair<int, GridCoord> placement in placements)
            {
                RosterEntry entry;
                if (!TryGetEntry(placement.Key, out entry))
                {
                    continue;
                }

                CombatUnit unit = new CombatUnit(
                    entry.Id,
                    entry.DisplayName,
                    entry.ArchetypeId,
                    Team.Katipunan,
                    entry.Stats,
                    placement.Value,
                    config.StackingPolicy);

                UnitArchetype archetype = UnitCatalog.Find(entry.ArchetypeId);
                if (archetype != null)
                {
                    unit.HealPower = archetype.HealPower;
                }

                units.Add(unit);
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
            resultSeed = seed;

            ClearViews();
            foreach (CombatUnit unit in units)
            {
                string shortName = "REG";
                int ordinal = SpanishOrdinal(unit.Id);
                RosterEntry entry;
                if (TryGetEntry(unit.Id, out entry))
                {
                    shortName = entry.ShortName;
                    ordinal = 0;
                }

                CreateView(unit.Id, unit.Name, shortName, unit.ArchetypeId, ordinal, unit.Team, unit.BaseStats.MaxHP, startCells[unit.Id]);
            }

            FaceEnemies();

            popups.Clear();
            ClearReport();
            replayIndex = 0;
            currentTurn = 0;
            eventTimer = 0f;
            eventDuration = 0f;
            skipRequested = false;
            phase = Phase.Combat;
            AppendReport(new FieldReportEntry(
                FieldReportKind.AssaultBegan, 0, null, 0, null, 0, BattleOutcome.InProgress, 0));

            PhaseChanged?.Invoke(phase);
            RaiseStateChanged();
            return true;
        }

        private void Update()
        {
            // A resized window changes how much board fits, so the framing is redone. Safe-area
            // fractions alone do not catch this: scaling a window uniformly leaves them unchanged.
            if (view != null && Mathf.Abs(view.aspect - framedAspect) > 0.01f)
            {
                FrameBoard(true);
            }

            HandleBoardInput();

            float rate = paused ? 0f : (phase == Phase.Combat ? speed : 1f);
            AdvanceAnimations(Time.deltaTime * rate);

            if (paused || phase != Phase.Combat || result == null)
            {
                return;
            }

            if (skipRequested)
            {
                skipRequested = false;
                while (replayIndex < result.Events.Count)
                {
                    ApplyEvent(result.Events[replayIndex++]);
                }

                SettleAnimations();
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

                // The quiz stops the replay where its turn begins; the HUD resumes it on an answer.
                if (!quizAsked && mission != null && mission.QuizTurn > 0 && currentTurn >= mission.QuizTurn
                    && QuizDue != null)
                {
                    quizAsked = true;
                    paused = true;
                    RaiseStateChanged();
                    QuizDue();
                    break;
                }
            }

            if (replayIndex >= result.Events.Count && eventTimer >= eventDuration)
            {
                FinishReplay();
            }
        }

        private void FinishReplay()
        {
            phase = Phase.Finished;
            AppendReport(new FieldReportEntry(
                FieldReportKind.BattleResolved, currentTurn, null, 0, null, 0, result.Outcome, result.TurnsElapsed));

            PhaseChanged?.Invoke(phase);
            RaiseStateChanged();
        }

        private static float DurationFor(BattleEventType type)
        {
            switch (type)
            {
                case BattleEventType.UnitMoved:
                    return MoveSeconds;
                case BattleEventType.UnitAttacked:
                case BattleEventType.UnitHealed:
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
                        Face(actor, actor.AnimateTo.x - actor.AnimateFrom.x);
                        SetSorting(actor, battleEvent.To);
                    }

                    break;

                case BattleEventType.UnitAttacked:
                    if (actor != null && target != null)
                    {
                        Vector3 toward = (CellToWorld(target.Cell) - CellToWorld(actor.Cell)).normalized;
                        actor.Lunge = toward * 0.18f;
                        Face(actor, toward.x);
                    }

                    break;

                case BattleEventType.DamageDealt:
                    ApplyDamageEvent(battleEvent, actor, target);
                    break;

                case BattleEventType.HpRegenerated:
                    if (actor != null)
                    {
                        actor.CurrentHP = Mathf.Min(actor.MaxHP, actor.CurrentHP + battleEvent.Amount);
                        AddPopup(PopupKind.Heal, battleEvent.Amount, actor);
                    }

                    break;

                case BattleEventType.UnitHealed:
                    if (actor != null && target != null)
                    {
                        Face(actor, CellToWorld(target.Cell).x - CellToWorld(actor.Cell).x);
                    }

                    if (target != null)
                    {
                        target.CurrentHP = Mathf.Min(target.MaxHP, target.CurrentHP + battleEvent.Amount);
                        AddPopup(PopupKind.Heal, battleEvent.Amount, target);
                    }

                    break;

                case BattleEventType.UnitDied:
                    if (actor != null)
                    {
                        actor.Alive = false;
                        actor.CurrentHP = 0f;
                        AppendReport(new FieldReportEntry(
                            FieldReportKind.UnitRouted, currentTurn, actor.ArchetypeId, actor.Ordinal,
                            null, 0, BattleOutcome.InProgress, 0));
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
                AddPopup(PopupKind.Dodge, 0f, target);
                return;
            }

            if (battleEvent.WasMissed)
            {
                AddPopup(PopupKind.Miss, 0f, target);
                return;
            }

            target.CurrentHP = Mathf.Max(0f, target.CurrentHP - battleEvent.Amount);
            target.HitTimer = HitSeconds;
            AddPopup(battleEvent.WasCrit ? PopupKind.Critical : PopupKind.Damage, battleEvent.Amount, target);

            if (battleEvent.WasCrit && actor != null)
            {
                AppendReport(new FieldReportEntry(
                    FieldReportKind.CriticalHit, currentTurn, actor.ArchetypeId, actor.Ordinal,
                    target.ArchetypeId, target.Ordinal, BattleOutcome.InProgress, 0));
            }
        }

        /// <summary>Drives movement lerps, attack lunges, death fades and popup lifetimes.</summary>
        /// <param name="step">Seconds to advance, already scaled by the replay speed.</param>
        /// <remarks>
        /// Scaled by the replay speed in both directions. It used to be floored at 1x, so at 0.5x a
        /// unit finished its step in half the time the replay gave it and then stood still waiting.
        /// </remarks>
        private void AdvanceAnimations(float step)
        {
            if (step <= 0f)
            {
                return;
            }

            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                UnitView unit = pair.Value;
                if (unit.Root == null)
                {
                    continue;
                }

                unit.AnimateProgress = Mathf.Min(1f, unit.AnimateProgress + (step / Mathf.Max(MoveSeconds, 0.01f)));
                unit.Lunge = Vector3.Lerp(unit.Lunge, Vector3.zero, Mathf.Min(1f, step * 8f));
                unit.HitTimer = Mathf.Max(0f, unit.HitTimer - step);
                unit.BobPhase = Mathf.Repeat(unit.BobPhase + (step * BobHertz), 1f);
                ApplyViewTransform(unit);
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup popup = popups[i];
                popup.Age += step;
                if (popup.Age > PopupLifetime)
                {
                    popups.RemoveAt(i);
                }
                else
                {
                    popups[i] = popup;
                }
            }
        }

        /// <summary>Snaps every token to where its animation would end, for a skipped replay.</summary>
        private void SettleAnimations()
        {
            popups.Clear();
            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                UnitView unit = pair.Value;
                unit.AnimateProgress = 1f;
                unit.Lunge = Vector3.zero;
                unit.HitTimer = 0f;
                ApplyViewTransform(unit);
            }
        }

        private void ApplyViewTransform(UnitView unit)
        {
            if (unit.Root == null)
            {
                return;
            }

            Vector3 position = Vector3.Lerp(unit.AnimateFrom, unit.AnimateTo, Smooth(unit.AnimateProgress));
            if (unit.Figure)
            {
                ApplyFigureTransform(unit, position);
                return;
            }

            unit.Root.transform.position = position + unit.Lunge + new Vector3(0f, TokenLift, 0f);

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

        /// <summary>
        /// Places a standing figure: the root and its ring stay on the ground, and only the
        /// body hops, bobs, lunges and recoils above them.
        /// </summary>
        private void ApplyFigureTransform(UnitView unit, Vector3 ground)
        {
            unit.Root.transform.position = ground;

            // Sorted from where the figure stands this frame, not from the cell it is walking to.
            // Keyed off the destination, a unit stepping past another swapped in front of or
            // behind it at the first frame of the step instead of as it went by.
            unit.Body.sortingOrder = SortingForHeight(ground.y) + 10;

            float lift = 0f;
            if (unit.Alive)
            {
                // A small arc over each step, and a one-texel bob while standing.
                lift += Mathf.Sin(Mathf.PI * unit.AnimateProgress) * HopHeight;
                lift += unit.BobPhase < 0.5f ? 0f : FigureTexel;
            }
            else
            {
                lift -= DeathSink;
            }

            float recoil = unit.HitTimer > 0f
                ? (unit.FacingLeft ? HitKnockback : -HitKnockback) * (unit.HitTimer / HitSeconds)
                : 0f;
            unit.Body.transform.localPosition = unit.Lunge + new Vector3(recoil, lift, 0f);
            unit.Body.flipX = unit.FacingLeft;

            Color tint = unit.HitTimer > 0f && unit.Alive ? HitTint : Color.white;
            if (!unit.Alive)
            {
                tint = new Color(0.35f, 0.35f, 0.35f, 0.35f);
            }

            unit.Body.color = tint;
            if (unit.Shadow != null)
            {
                unit.Shadow.color = unit.Alive ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        /// <summary>Turns a figure toward a horizontal direction; straight up or down keeps its facing.</summary>
        private static void Face(UnitView unit, float screenDeltaX)
        {
            if (Mathf.Abs(screenDeltaX) > 0.01f)
            {
                unit.FacingLeft = screenDeltaX < 0f;
            }
        }

        /// <summary>Points every unit at the middle of the opposing side, for the opening stance.</summary>
        private void FaceEnemies()
        {
            float katipunanX = 0f;
            float spanishX = 0f;
            int katipunanCount = 0;
            int spanishCount = 0;
            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                float x = CellToWorld(pair.Value.Cell).x;
                if (pair.Value.Team == Team.Katipunan)
                {
                    katipunanX += x;
                    katipunanCount++;
                }
                else
                {
                    spanishX += x;
                    spanishCount++;
                }
            }

            foreach (KeyValuePair<int, UnitView> pair in views)
            {
                UnitView unit = pair.Value;
                bool ours = unit.Team == Team.Katipunan;
                int enemies = ours ? spanishCount : katipunanCount;
                if (enemies == 0)
                {
                    continue;
                }

                float enemyX = (ours ? spanishX : katipunanX) / enemies;
                Face(unit, enemyX - CellToWorld(unit.Cell).x);
                ApplyViewTransform(unit);
            }
        }

        /// <summary>Where a unit's name tag and damage numbers anchor: just over its sprite.</summary>
        private Vector3 HeadOf(UnitView unit)
        {
            if (unit.Root == null)
            {
                return CellToWorld(unit.Cell);
            }

            Vector3 root = unit.Root.transform.position;
            if (!unit.Figure || unit.Body.sprite == null)
            {
                return root;
            }

            // The sprite's own bounds rather than the renderer's, so the tag does not bob with
            // the figure or sink when it falls.
            return new Vector3(root.x, root.y + unit.Body.sprite.bounds.max.y + HeadClearance, root.z);
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - (2f * t));
        }

        // ------------------------------------------------------------------ input

        /// <summary>
        /// Turns a click on the board into a placement or a lift, ignoring clicks that landed on
        /// the interface.
        /// </summary>
        /// <remarks>
        /// Left click lifts a placed unit or places the selected one; right click lifts, or clears
        /// the roster selection when it lands on an empty cell or off the board. The
        /// interface is asked whether it covers the pointer through <see cref="UiPointer"/> rather
        /// than the board testing hardcoded panel rectangles, which silently broke every time a
        /// panel moved.
        /// </remarks>
        private void HandleBoardInput()
        {
            if (phase != Phase.Deployment || boardInputLocked || view == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            bool left = mouse.leftButton.wasPressedThisFrame;
            bool right = mouse.rightButton.wasPressedThisFrame;
            if (!left && !right)
            {
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            if (UiPointer.IsOverUi(screen))
            {
                return;
            }

            Vector3 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
            GridCoord cell = layout.WorldToCell(new IsoVector(world.x, world.y));

            // Right click is "cancel": lift the unit under it, or, over an empty cell or off the
            // board, put down the roster unit in hand. Claimed so a panel closing on the same
            // press does not also count as a board click.
            if (right && !left)
            {
                if (UiPointer.TryClaimRightClick() && !(grid.InBounds(cell) && RequestLift(cell)))
                {
                    SelectSlot(-1);
                }

                return;
            }

            if (!grid.InBounds(cell))
            {
                return;
            }

            if (RequestLift(cell) || right)
            {
                return;
            }

            RequestPlace(cell);
        }

        // ------------------------------------------------------------------ deployment helpers

        /// <summary>Fills unplaced roster units into free deployable cells, trench first.</summary>
        private void AutoDeploy()
        {
            List<GridCoord> cells = new List<GridCoord>();
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    GridCoord cell = new GridCoord(x, y);
                    if (grid.IsDeployable(cell) && !IsOccupied(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            // Walking the free cells in grid order deploys down the trench, so the two bonded pairs
            // land adjacent to one another — the arrangement the Kapatiran rules reward.
            int index = 0;
            foreach (RosterEntry entry in roster)
            {
                if (index >= cells.Count || placements.Count >= SquadCap)
                {
                    break;
                }

                if (placements.ContainsKey(entry.Id))
                {
                    continue;
                }

                placements[entry.Id] = cells[index++];
            }

            SyncDeploymentViews();
            UnitPlaced?.Invoke();
            DeploymentChanged?.Invoke();
            RaiseStateChanged();
        }

        private bool IsOccupied(GridCoord cell)
        {
            foreach (KeyValuePair<int, GridCoord> placement in placements)
            {
                if (placement.Value == cell)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Changes the selection without announcing it, for callers that announce once at the end.</summary>
        private void SetSelectionSilently(int index)
        {
            selectedSlot = roster == null || index < 0 || index >= roster.Count ? -1 : index;
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

        /// <summary>Spanish regulars are numbered from <see cref="PlaytestScenario.SpanishIdBase"/>, matching their authored names.</summary>
        private static int SpanishOrdinal(int id)
        {
            return id >= PlaytestScenario.SpanishIdBase ? id - PlaytestScenario.SpanishIdBase + 1 : 0;
        }

        // ------------------------------------------------------------------ views

        private void UpsertView(
            int id, string displayName, string shortName, string archetypeId, int ordinal,
            Team team, float maxHP, GridCoord cell)
        {
            UnitView existing;
            if (!views.TryGetValue(id, out existing) || existing.Root == null)
            {
                CreateView(id, displayName, shortName, archetypeId, ordinal, team, maxHP, cell);
                return;
            }

            if (existing.Cell == cell)
            {
                return;
            }

            Vector3 world = CellToWorld(cell);
            existing.Cell = cell;
            existing.AnimateFrom = world;
            existing.AnimateTo = world;
            existing.AnimateProgress = 1f;
            existing.Lunge = Vector3.zero;
            SetSorting(existing, cell);
            ApplyViewTransform(existing);
        }

        private void CreateView(
            int id, string displayName, string shortName, string archetypeId, int ordinal,
            Team team, float maxHP, GridCoord cell)
        {
            GameObject token = new GameObject("Unit " + id + " " + displayName);
            token.transform.SetParent(unitRoot, false);

            Sprite figure = BoardArt.UnitBody(archetypeId);
            if (figure != null)
            {
                CreateFigure(token, figure, id, displayName, shortName, archetypeId, ordinal, team, maxHP, cell);
                return;
            }

            SpriteRenderer body = token.AddComponent<SpriteRenderer>();
            body.sprite = BoardArt.Token(team);
            body.color = BoardArt.TokensAreThemed
                ? Color.white
                : (team == Team.Katipunan ? KatipunanColor : SpanishColor);
            body.sortingLayerName = UnitsLayer;

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

            Vector3 world = CellToWorld(cell);

            UnitView unit = new UnitView
            {
                Id = id,
                DisplayName = displayName,
                ShortName = shortName,
                ArchetypeId = archetypeId,
                Ordinal = ordinal,
                Team = team,
                MaxHP = maxHP,
                CurrentHP = maxHP,
                Alive = true,
                Cell = cell,
                Root = token,
                Body = body,
                Shadow = shadowRenderer,
                AnimateFrom = world,
                AnimateTo = world,
                AnimateProgress = 1f,
                Lunge = Vector3.zero
            };

            SetSorting(unit, cell);
            ApplyViewTransform(unit);
            views[id] = unit;
        }

        /// <summary>
        /// Builds a standing figure: a ground ring in the team colour with the body above it.
        /// </summary>
        /// <remarks>
        /// The body is a child rather than a renderer on the root so it can hop, bob and recoil
        /// while the ring stays planted on the cell.
        /// </remarks>
        private void CreateFigure(
            GameObject root, Sprite figure, int id, string displayName, string shortName, string archetypeId,
            int ordinal, Team team, float maxHP, GridCoord cell)
        {
            GameObject ringObject = new GameObject("Ring");
            ringObject.transform.SetParent(root.transform, false);
            SpriteRenderer ring = ringObject.AddComponent<SpriteRenderer>();
            ring.sprite = BoardArt.TeamRing(team);
            ring.color = BoardArt.TeamRingIsThemed
                ? Color.white
                : (team == Team.Katipunan ? KatipunanColor : SpanishColor);
            ring.sortingLayerName = ShadowsLayer;

            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(root.transform, false);
            SpriteRenderer body = bodyObject.AddComponent<SpriteRenderer>();
            body.sprite = figure;
            body.sortingLayerName = UnitsLayer;

            Vector3 world = CellToWorld(cell);
            UnitView unit = new UnitView
            {
                Id = id,
                DisplayName = displayName,
                ShortName = shortName,
                ArchetypeId = archetypeId,
                Ordinal = ordinal,
                Team = team,
                MaxHP = maxHP,
                CurrentHP = maxHP,
                Alive = true,
                Cell = cell,
                Root = root,
                Body = body,
                Shadow = ring,
                AnimateFrom = world,
                AnimateTo = world,
                AnimateProgress = 1f,
                Lunge = Vector3.zero,
                Figure = true,
                BobPhase = Mathf.Repeat(id * 0.37f, 1f),
            };

            SetSorting(unit, cell);
            ApplyViewTransform(unit);
            views[id] = unit;
        }

        private static void SetSorting(UnitView unit, GridCoord cell)
        {
            // A figure's body is sorted every frame from where it stands, in ApplyFigureTransform.
            // Setting it here too, on a move, would put it on its destination row for one frame.
            if (!unit.Figure)
            {
                unit.Body.sortingOrder = SortingFor(cell) + 10;
            }

            if (unit.Shadow != null)
            {
                unit.Shadow.sortingOrder = SortingFor(cell) + 9;
            }
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

        private void AddPopup(PopupKind kind, float amount, UnitView unit)
        {
            popups.Add(new Popup
            {
                Kind = kind,
                Amount = amount,
                World = HeadOf(unit),
                Age = 0f
            });
        }

        // ------------------------------------------------------------------ field report

        /// <summary>Adds a line to the fixed-size report, overwriting the oldest once full.</summary>
        private void AppendReport(FieldReportEntry entry)
        {
            if (reportCount < ReportCapacity)
            {
                report[(reportStart + reportCount) % ReportCapacity] = entry;
                reportCount++;
            }
            else
            {
                report[reportStart] = entry;
                reportStart = (reportStart + 1) % ReportCapacity;
            }

            reportVersion++;
            FieldReportAppended?.Invoke();
        }

        private void ClearReport()
        {
            if (reportCount == 0)
            {
                return;
            }

            reportStart = 0;
            reportCount = 0;
            reportVersion++;
            FieldReportAppended?.Invoke();
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
        /// <para>
        /// The key is negated. <see cref="IsoGridLayout"/> opens the grid upward, so a larger
        /// <c>X + Y</c> is higher on screen and further from the camera, and has to draw first.
        /// Flat discs never overlapped, so the sign did not show; standing figures do, and with
        /// it positive the unit behind stood on the head of the unit in front.
        /// </para>
        /// </remarks>
        private static int SortingFor(GridCoord cell)
        {
            return -(cell.X + cell.Y) * SortingStep;
        }

        /// <summary>
        /// <see cref="SortingFor"/> for a point between rows. Equal to it at a cell centre, where
        /// <c>worldY / (TileHeight / 2)</c> is exactly <c>X + Y</c>.
        /// </summary>
        private int SortingForHeight(float worldY)
        {
            return -Mathf.RoundToInt(worldY / (layout.TileHeight * 0.5f) * SortingStep);
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
