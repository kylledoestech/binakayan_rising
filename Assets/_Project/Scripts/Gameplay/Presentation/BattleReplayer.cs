using System;
using System.Collections;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Gameplay.Adapters;
using UnityEngine;
using UnityEngine.Events;

namespace BinakayanRising.Gameplay.Presentation
{
    /// <summary>Raised with an AI turn number.</summary>
    [Serializable]
    public sealed class BattleTurnUnityEvent : UnityEvent<int>
    {
    }

    /// <summary>Raised with an AI turn number and the outcome evaluated at its end.</summary>
    [Serializable]
    public sealed class BattleTurnEndedUnityEvent : UnityEvent<int, BattleOutcome>
    {
    }

    /// <summary>Raised with the finished battle.</summary>
    [Serializable]
    public sealed class BattleFinishedUnityEvent : UnityEvent<BattleResult>
    {
    }

    /// <summary>
    /// Replays a finished battle's event log as an animation, turn by turn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The simulation has already happened.</b> By the time this component sees a
    /// <see cref="BattleResult"/>, every die has been rolled and every unit's fate is decided; the
    /// log is a complete, ordered record and this class is a player for it. Nothing here calls back
    /// into <c>BinakayanRising.Core</c> mid-battle, and nothing here can change the outcome.
    /// </para>
    /// <para>
    /// That separation is what makes the same battle watchable twice. The resolver is deterministic
    /// from its seed, so the log is fixed; because the visuals are a pure function of the log, the
    /// replay is fixed too. Pausing, stepping and skipping change only how fast the log is consumed,
    /// never what it says.
    /// </para>
    /// <para>
    /// Every member of <see cref="BattleEventType"/> is handled explicitly, including the ones with
    /// no visual — silently dropping an event type is how a replay quietly desyncs from the log it
    /// is supposed to be showing.
    /// </para>
    /// <para>
    /// TODO(design): not specified in capstone document. The document describes the combat phase as
    /// automatic and turn-based but sets no pacing, so every duration below is a serialized field.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleReplayer : MonoBehaviour
    {
        [Header("Scene Wiring")]
        [Tooltip("Prefab carrying a UnitView. One is instantiated per unit in the battle.")]
        [SerializeField] private UnitView unitViewPrefab;

        [Tooltip("Parent for spawned unit views. Defaults to this object.")]
        [SerializeField] private Transform unitRoot;

        [Tooltip("Terrain renderer supplying the cell-to-world projection, and optionally painting the map.")]
        [SerializeField] private IsoTerrainRenderer terrainRenderer;

        [Tooltip("Pool that supplies floating damage and healing numbers.")]
        [SerializeField] private FloatingTextPool floatingTextPool;

        [Header("Pacing")]
        [Tooltip("Base seconds one event occupies. Each event type scales this by its own multiplier below.")]
        [Min(0f)]
        [SerializeField] private float secondsPerEvent = 0.28f;

        [Tooltip("Divides every duration. 1 is normal, 2 is double speed, 0.5 is half.")]
        [Min(0.05f)]
        [SerializeField] private float speedMultiplier = 1f;

        [Tooltip("Begin replaying as soon as a battle is loaded.")]
        [SerializeField] private bool autoPlayOnLoad = false;

        [Header("Per-Event Timing (multiples of Seconds Per Event)")]
        [Tooltip("Pause when an AI turn begins, for a turn banner to read.")]
        [Min(0f)]
        [SerializeField] private float turnStartedScale = 1f;

        [Tooltip("Pause when a modifier is applied. Zero unless modifier popups are on.")]
        [Min(0f)]
        [SerializeField] private float modifierAppliedScale = 0f;

        [Tooltip("Pause while a unit regenerates health on encampment terrain.")]
        [Min(0f)]
        [SerializeField] private float hpRegeneratedScale = 0.7f;

        [Tooltip("Time one cell of movement takes.")]
        [Min(0f)]
        [SerializeField] private float unitMovedScale = 0.65f;

        [Tooltip("Time the attack lunge takes before the damage event resolves.")]
        [Min(0f)]
        [SerializeField] private float unitAttackedScale = 0.5f;

        [Tooltip("Pause while a damage number is readable.")]
        [Min(0f)]
        [SerializeField] private float damageDealtScale = 0.9f;

        [Tooltip("Pause while a unit's death fade plays.")]
        [Min(0f)]
        [SerializeField] private float unitDiedScale = 1.4f;

        [Tooltip("Pause when an AI turn ends and the win/lose check runs.")]
        [Min(0f)]
        [SerializeField] private float turnEndedScale = 0.6f;

        [Tooltip("Pause after the battle reaches its outcome, before the victory or defeat overlay.")]
        [Min(0f)]
        [SerializeField] private float battleEndedScale = 1.8f;

        [Header("Presentation Rules")]
        [Tooltip("Hide every unit's deployment chrome on the first turn. The capstone document requires this.")]
        [SerializeField] private bool hideDeploymentChromeOnFirstTurn = true;

        [Tooltip("Hide the deployment zone highlight on the first turn, alongside the unit chrome.")]
        [SerializeField] private bool hideDeploymentOverlayOnFirstTurn = true;

        [Tooltip("Pop a floating label for each modifier applied. Verbose; useful when debugging a bond.")]
        [SerializeField] private bool showModifierPopups = false;

        [Tooltip("Label shown when an attack fails its accuracy roll.")]
        [SerializeField] private string missLabel = "MISS";

        [Tooltip("Label shown when a target dodges an attack.")]
        [SerializeField] private string dodgeLabel = "DODGE";

        [Tooltip("Colour of a modifier popup, when they are enabled.")]
        [SerializeField] private Color modifierPopupColor = new Color(0.62f, 0.78f, 1f, 1f);

        [Header("Events")]
        [Tooltip("Raised when an AI turn begins. Carries the turn number.")]
        [SerializeField] private BattleTurnUnityEvent onTurnStarted = new BattleTurnUnityEvent();

        [Tooltip("Raised when an AI turn ends. Carries the turn number and the outcome evaluated at its end.")]
        [SerializeField] private BattleTurnEndedUnityEvent onTurnEnded = new BattleTurnEndedUnityEvent();

        [Tooltip("Raised once the whole log has been replayed. Hook the Victory/Defeat overlay here.")]
        [SerializeField] private BattleFinishedUnityEvent onBattleFinished = new BattleFinishedUnityEvent();

        private readonly Dictionary<int, UnitView> viewsByUnitId = new Dictionary<int, UnitView>();
        private readonly Dictionary<int, float> currentHpByUnitId = new Dictionary<int, float>();
        private readonly Dictionary<int, float> maxHpByUnitId = new Dictionary<int, float>();
        private readonly List<UnitView> spawnedViews = new List<UnitView>();

        private BattleResult result;
        private IBattleGrid grid;
        private IBattleWorldSpace worldSpace;
        private Coroutine playbackRoutine;
        private int eventIndex;
        private int currentTurn;
        private bool paused;
        private bool stepToEndOfTurn;
        private bool finished;
        private bool chromeHidden;

        /// <summary>Raised when an AI turn begins. Carries the turn number.</summary>
        public event Action<int> TurnStarted;

        /// <summary>Raised when an AI turn ends. Carries the turn number and the post-turn outcome.</summary>
        public event Action<int, BattleOutcome> TurnEnded;

        /// <summary>Raised once the whole log has been replayed.</summary>
        public event Action<BattleResult> BattleFinished;

        /// <summary>Raised for every event, in log order, just before it is animated.</summary>
        public event Action<BattleEvent> EventPlayed;

        /// <summary>The battle being replayed, or null before <see cref="Load"/>.</summary>
        public BattleResult Result
        {
            get { return result; }
        }

        /// <summary>True while the replay coroutine is running and not paused.</summary>
        public bool IsPlaying
        {
            get { return playbackRoutine != null && !paused; }
        }

        /// <summary>True while the replay is loaded but held.</summary>
        public bool IsPaused
        {
            get { return paused; }
        }

        /// <summary>True once every event has been consumed.</summary>
        public bool IsFinished
        {
            get { return finished; }
        }

        /// <summary>The AI turn currently being shown. Zero before the first turn starts.</summary>
        public int CurrentTurn
        {
            get { return currentTurn; }
        }

        /// <summary>Fraction of the event log consumed, 0..1.</summary>
        public float Progress
        {
            get
            {
                if (result == null || result.Events.Count == 0)
                {
                    return 1f;
                }

                return Mathf.Clamp01((float)eventIndex / result.Events.Count);
            }
        }

        /// <summary>Playback rate. 1 is normal, 2 is double speed.</summary>
        public float SpeedMultiplier
        {
            get { return speedMultiplier; }
        }

        /// <summary>The spawned unit views, keyed by runtime unit id.</summary>
        public IReadOnlyDictionary<int, UnitView> Views
        {
            get { return viewsByUnitId; }
        }

        private void Awake()
        {
            if (unitRoot == null)
            {
                unitRoot = transform;
            }
        }

        /// <summary>
        /// Loads a finished battle, spawns a view per unit, and resets to the first event.
        /// </summary>
        /// <param name="battleResult">The finished battle. Must not be null.</param>
        /// <param name="battleGrid">The grid the battle ran on. May be null if the terrain is already painted.</param>
        /// <param name="roster">
        /// Identity, sprite, starting cell and Max HP per unit, snapshotted <em>before</em> the
        /// simulation ran. See <see cref="BattleSetupResult.CaptureReplayRoster"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the result or the roster is null.</exception>
        public void Load(
            BattleResult battleResult, IBattleGrid battleGrid, IReadOnlyList<UnitReplayEntry> roster)
        {
            if (battleResult == null)
            {
                throw new ArgumentNullException(nameof(battleResult));
            }

            if (roster == null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            Stop();
            DespawnViews();

            result = battleResult;
            grid = battleGrid;
            worldSpace = ResolveWorldSpace();
            eventIndex = 0;
            currentTurn = 0;
            paused = false;
            stepToEndOfTurn = false;
            finished = false;
            chromeHidden = false;

            if (terrainRenderer != null && battleGrid != null)
            {
                terrainRenderer.Paint(battleGrid);
                terrainRenderer.SetDeploymentOverlayVisible(true);
            }

            SpawnViews(roster);

            if (autoPlayOnLoad)
            {
                Play();
            }
        }

        /// <summary>Starts, or restarts, playback from the current position.</summary>
        public void Play()
        {
            if (result == null)
            {
                Debug.LogWarning("BattleReplayer.Play was called before a battle was loaded.");
                return;
            }

            paused = false;

            if (playbackRoutine == null && !finished)
            {
                playbackRoutine = StartCoroutine(PlaybackRoutine());
            }
        }

        /// <summary>Holds playback after the event currently animating.</summary>
        public void Pause()
        {
            paused = true;
        }

        /// <summary>Releases a hold placed by <see cref="Pause"/> or <see cref="StepOneTurn"/>.</summary>
        public void Resume()
        {
            paused = false;
            Play();
        }

        /// <summary>Toggles between paused and playing.</summary>
        public void TogglePause()
        {
            if (paused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        /// <summary>
        /// Plays forward until the end of the current AI turn, then holds.
        /// </summary>
        /// <remarks>
        /// The unit of stepping is a turn rather than an event because a turn is the unit the
        /// simulation itself resolves in: the win/lose check runs at the end of every AI turn, so
        /// turn boundaries are the only points where the battle state is meaningful to a viewer.
        /// </remarks>
        public void StepOneTurn()
        {
            if (result == null || finished)
            {
                return;
            }

            stepToEndOfTurn = true;
            paused = false;

            if (playbackRoutine == null)
            {
                playbackRoutine = StartCoroutine(PlaybackRoutine());
            }
        }

        /// <summary>
        /// Consumes every remaining event without animating, leaving the board in its final state and
        /// raising the finish events.
        /// </summary>
        public void SkipToEnd()
        {
            if (result == null || finished)
            {
                return;
            }

            StopRoutineOnly();

            if (floatingTextPool != null)
            {
                floatingTextPool.RecycleAll();
            }

            IReadOnlyList<BattleEvent> events = result.Events;

            while (eventIndex < events.Count)
            {
                BattleEvent battleEvent = events[eventIndex];
                eventIndex++;
                ApplyInstant(battleEvent);
            }

            CompletePlayback();
        }

        /// <summary>Halts playback where it stands, without finishing the battle.</summary>
        public void Stop()
        {
            StopRoutineOnly();
            paused = false;
            stepToEndOfTurn = false;
        }

        /// <summary>Rewinds to the first event and re-snaps every unit to its deployment cell.</summary>
        /// <param name="roster">The same roster passed to <see cref="Load"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="roster"/> is null.</exception>
        public void Rewind(IReadOnlyList<UnitReplayEntry> roster)
        {
            if (roster == null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            Stop();
            DespawnViews();

            eventIndex = 0;
            currentTurn = 0;
            finished = false;
            chromeHidden = false;

            if (terrainRenderer != null)
            {
                terrainRenderer.SetDeploymentOverlayVisible(true);
            }

            SpawnViews(roster);
        }

        /// <summary>Sets the playback rate.</summary>
        /// <param name="multiplier">1 is normal, 2 is double speed. Clamped to at least 0.05.</param>
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0.05f, multiplier);
        }

        /// <summary>Sets the base seconds one event occupies.</summary>
        /// <param name="seconds">Base duration, in seconds. Clamped to at least zero.</param>
        public void SetSecondsPerEvent(float seconds)
        {
            secondsPerEvent = Mathf.Max(0f, seconds);
        }

        /// <summary>Finds the view for a runtime unit id.</summary>
        /// <param name="unitId">Runtime unit id, as it appears in the event log.</param>
        /// <param name="view">Receives the view, or null when the id is unknown.</param>
        /// <returns>True when a view exists for the id.</returns>
        public bool TryGetView(int unitId, out UnitView view)
        {
            return viewsByUnitId.TryGetValue(unitId, out view);
        }

        private IEnumerator PlaybackRoutine()
        {
            IReadOnlyList<BattleEvent> events = result.Events;

            while (eventIndex < events.Count)
            {
                while (paused)
                {
                    yield return null;
                }

                BattleEvent battleEvent = events[eventIndex];
                eventIndex++;

                yield return PlayEvent(battleEvent);

                if (stepToEndOfTurn && battleEvent.Type == BattleEventType.TurnEnded)
                {
                    stepToEndOfTurn = false;
                    paused = true;
                }
            }

            playbackRoutine = null;
            CompletePlayback();
        }

        /// <summary>
        /// Animates one event. Every <see cref="BattleEventType"/> member has a branch; the default
        /// case exists only to make a future enum member fail loudly rather than vanish.
        /// </summary>
        private IEnumerator PlayEvent(BattleEvent battleEvent)
        {
            RaiseEventPlayed(battleEvent);

            switch (battleEvent.Type)
            {
                case BattleEventType.TurnStarted:
                    currentTurn = battleEvent.Turn;
                    HideDeploymentChromeOnce();
                    RaiseTurnStarted(battleEvent.Turn);
                    yield return Wait(turnStartedScale);
                    break;

                case BattleEventType.ModifierApplied:
                    ShowModifierPopup(battleEvent);
                    yield return Wait(modifierAppliedScale);
                    break;

                case BattleEventType.HpRegenerated:
                    yield return PlayRegen(battleEvent);
                    break;

                case BattleEventType.UnitHealed:
                    yield return PlayHeal(battleEvent);
                    break;

                case BattleEventType.UnitMoved:
                    yield return PlayMove(battleEvent);
                    break;

                case BattleEventType.UnitAttacked:
                    yield return PlayAttack(battleEvent);
                    break;

                case BattleEventType.DamageDealt:
                    yield return PlayDamage(battleEvent);
                    break;

                case BattleEventType.UnitDied:
                    yield return PlayDeath(battleEvent);
                    break;

                case BattleEventType.TurnEnded:
                    RaiseTurnEnded(battleEvent.Turn, ParseOutcome(battleEvent.Detail));
                    yield return Wait(turnEndedScale);
                    break;

                case BattleEventType.BattleEnded:
                    yield return Wait(battleEndedScale);
                    break;

                default:
                    Debug.LogWarning(
                        "BattleReplayer met an unhandled BattleEventType '" + battleEvent.Type
                            + "'. The Core layer has grown an event the presentation layer does not "
                            + "know how to show. Event: " + battleEvent);
                    break;
            }
        }

        /// <summary>
        /// Applies an event's state change with no animation and no waiting. Used by
        /// <see cref="SkipToEnd"/>, and mirrors <see cref="PlayEvent"/> branch for branch.
        /// </summary>
        private void ApplyInstant(BattleEvent battleEvent)
        {
            RaiseEventPlayed(battleEvent);

            UnitView view;

            switch (battleEvent.Type)
            {
                case BattleEventType.TurnStarted:
                    currentTurn = battleEvent.Turn;
                    HideDeploymentChromeOnce();
                    RaiseTurnStarted(battleEvent.Turn);
                    break;

                case BattleEventType.ModifierApplied:
                    break;

                case BattleEventType.HpRegenerated:
                    ApplyHealthDelta(battleEvent.ActorId, battleEvent.Amount);
                    break;

                case BattleEventType.UnitHealed:
                    ApplyHealthDelta(battleEvent.TargetId, battleEvent.Amount);
                    break;

                case BattleEventType.UnitMoved:
                    if (viewsByUnitId.TryGetValue(battleEvent.ActorId, out view))
                    {
                        view.SnapToCell(battleEvent.To);
                    }

                    break;

                case BattleEventType.UnitAttacked:
                    break;

                case BattleEventType.DamageDealt:
                    if (!battleEvent.WasEvaded && !battleEvent.WasMissed)
                    {
                        ApplyHealthDelta(battleEvent.TargetId, -battleEvent.Amount);
                    }

                    break;

                case BattleEventType.UnitDied:
                    SetHealth(battleEvent.ActorId, 0f);

                    if (viewsByUnitId.TryGetValue(battleEvent.ActorId, out view))
                    {
                        view.SnapToCell(battleEvent.From);
                        view.SetHealthBarVisible(false);
                        view.SetDeploymentChromeVisible(false);
                        view.StopAllCoroutines();
                        view.gameObject.SetActive(false);
                    }

                    break;

                case BattleEventType.TurnEnded:
                    RaiseTurnEnded(battleEvent.Turn, ParseOutcome(battleEvent.Detail));
                    break;

                case BattleEventType.BattleEnded:
                    break;

                default:
                    Debug.LogWarning(
                        "BattleReplayer met an unhandled BattleEventType '" + battleEvent.Type
                            + "' while skipping to the end. Event: " + battleEvent);
                    break;
            }
        }

        private IEnumerator PlayRegen(BattleEvent battleEvent)
        {
            UnitView view;

            if (viewsByUnitId.TryGetValue(battleEvent.ActorId, out view))
            {
                ApplyHealthDelta(battleEvent.ActorId, battleEvent.Amount);
                view.SetHealth(GetHealth(battleEvent.ActorId));

                if (battleEvent.Amount > 0f)
                {
                    view.ShowHeal(battleEvent.Amount);
                }
            }

            yield return Wait(hpRegeneratedScale);
        }

        private IEnumerator PlayHeal(BattleEvent battleEvent)
        {
            UnitView patient;

            if (viewsByUnitId.TryGetValue(battleEvent.TargetId, out patient))
            {
                ApplyHealthDelta(battleEvent.TargetId, battleEvent.Amount);
                patient.SetHealth(GetHealth(battleEvent.TargetId));
                patient.ShowHeal(battleEvent.Amount);
            }

            yield return Wait(hpRegeneratedScale);
        }

        private IEnumerator PlayMove(BattleEvent battleEvent)
        {
            UnitView view;

            if (!viewsByUnitId.TryGetValue(battleEvent.ActorId, out view))
            {
                yield return Wait(unitMovedScale);
                yield break;
            }

            yield return view.MoveToCell(battleEvent.To, Duration(unitMovedScale));
        }

        private IEnumerator PlayAttack(BattleEvent battleEvent)
        {
            UnitView attacker;

            if (!viewsByUnitId.TryGetValue(battleEvent.ActorId, out attacker))
            {
                yield return Wait(unitAttackedScale);
                yield break;
            }

            Vector3 targetPosition = ResolveWorldPosition(battleEvent.TargetId, battleEvent.To);
            yield return attacker.PlayAttack(targetPosition);
            yield return Wait(unitAttackedScale);
        }

        private IEnumerator PlayDamage(BattleEvent battleEvent)
        {
            UnitView target;

            if (!viewsByUnitId.TryGetValue(battleEvent.TargetId, out target))
            {
                yield return Wait(damageDealtScale);
                yield break;
            }

            Vector3 attackerPosition = ResolveWorldPosition(battleEvent.ActorId, target.Cell);

            if (battleEvent.WasEvaded)
            {
                target.ShowMiss(dodgeLabel);
            }
            else if (battleEvent.WasMissed)
            {
                target.ShowMiss(missLabel);
            }
            else
            {
                ApplyHealthDelta(battleEvent.TargetId, -battleEvent.Amount);
                target.PlayHitFlash(attackerPosition);
                target.SetHealth(GetHealth(battleEvent.TargetId));
                target.ShowDamage(battleEvent.Amount, battleEvent.WasCrit);
            }

            yield return Wait(damageDealtScale);
        }

        private IEnumerator PlayDeath(BattleEvent battleEvent)
        {
            UnitView view;
            SetHealth(battleEvent.ActorId, 0f);

            if (!viewsByUnitId.TryGetValue(battleEvent.ActorId, out view))
            {
                yield return Wait(unitDiedScale);
                yield break;
            }

            view.SetHealth(0f);
            yield return view.PlayDeathFade();
            yield return Wait(unitDiedScale);
        }

        private void CompletePlayback()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            paused = false;
            stepToEndOfTurn = false;

            RaiseBattleFinished(result);
        }

        private void StopRoutineOnly()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }
        }

        private void SpawnViews(IReadOnlyList<UnitReplayEntry> roster)
        {
            if (unitViewPrefab == null)
            {
                Debug.LogError(
                    "BattleReplayer on '" + name + "' has no UnitView prefab assigned, so no unit "
                        + "will appear on screen. Assign one in the Inspector.");
                return;
            }

            if (unitRoot == null)
            {
                unitRoot = transform;
            }

            for (int i = 0; i < roster.Count; i++)
            {
                UnitReplayEntry entry = roster[i];

                if (viewsByUnitId.ContainsKey(entry.UnitId))
                {
                    Debug.LogWarning(
                        "Two roster entries share unit id " + entry.UnitId
                            + ". The second is ignored; ids must be unique within one battle.");
                    continue;
                }

                UnitView view = Instantiate(unitViewPrefab, unitRoot);
                view.Bind(entry, worldSpace, floatingTextPool);

                viewsByUnitId[entry.UnitId] = view;
                spawnedViews.Add(view);
                maxHpByUnitId[entry.UnitId] = entry.MaxHP > 0f ? entry.MaxHP : 1f;
                currentHpByUnitId[entry.UnitId] = maxHpByUnitId[entry.UnitId];
            }
        }

        private void DespawnViews()
        {
            for (int i = 0; i < spawnedViews.Count; i++)
            {
                if (spawnedViews[i] != null)
                {
                    Destroy(spawnedViews[i].gameObject);
                }
            }

            spawnedViews.Clear();
            viewsByUnitId.Clear();
            currentHpByUnitId.Clear();
            maxHpByUnitId.Clear();

            if (floatingTextPool != null)
            {
                floatingTextPool.RecycleAll();
            }
        }

        private void HideDeploymentChromeOnce()
        {
            if (chromeHidden || !hideDeploymentChromeOnFirstTurn)
            {
                return;
            }

            chromeHidden = true;

            for (int i = 0; i < spawnedViews.Count; i++)
            {
                if (spawnedViews[i] != null)
                {
                    spawnedViews[i].SetDeploymentChromeVisible(false);
                }
            }

            if (hideDeploymentOverlayOnFirstTurn && terrainRenderer != null)
            {
                terrainRenderer.SetDeploymentOverlayVisible(false);
            }
        }

        private void ShowModifierPopup(BattleEvent battleEvent)
        {
            if (!showModifierPopups)
            {
                return;
            }

            UnitView view;

            if (viewsByUnitId.TryGetValue(battleEvent.ActorId, out view))
            {
                view.ShowFloatingText(battleEvent.Detail, modifierPopupColor);
            }
        }

        private void ApplyHealthDelta(int unitId, float delta)
        {
            SetHealth(unitId, GetHealth(unitId) + delta);
        }

        private void SetHealth(int unitId, float value)
        {
            float max;

            if (!maxHpByUnitId.TryGetValue(unitId, out max) || max <= 0f)
            {
                max = 1f;
            }

            currentHpByUnitId[unitId] = Mathf.Clamp(value, 0f, max);
        }

        private float GetHealth(int unitId)
        {
            float value;
            return currentHpByUnitId.TryGetValue(unitId, out value) ? value : 0f;
        }

        private Vector3 ResolveWorldPosition(int unitId, GridCoord fallbackCell)
        {
            UnitView view;

            if (unitId != BattleEvent.NoUnit && viewsByUnitId.TryGetValue(unitId, out view))
            {
                return view.WorldPosition;
            }

            return worldSpace != null
                ? worldSpace.CellToWorld(fallbackCell)
                : IsoGridLayout.Default.CellToWorldPosition(fallbackCell);
        }

        private IBattleWorldSpace ResolveWorldSpace()
        {
            if (terrainRenderer != null)
            {
                return terrainRenderer;
            }

            return new IsoLayoutWorldSpace();
        }

        private float Duration(float scale)
        {
            return (secondsPerEvent * scale) / Mathf.Max(0.05f, speedMultiplier);
        }

        private IEnumerator Wait(float scale)
        {
            float seconds = Duration(scale);

            if (seconds <= 0f)
            {
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < seconds)
            {
                if (paused)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Reads the outcome back out of a <see cref="BattleEventType.TurnEnded"/> event's detail
        /// string, which Core writes as <c>outcome.ToString()</c>.
        /// </summary>
        private static BattleOutcome ParseOutcome(string detail)
        {
            if (string.IsNullOrEmpty(detail))
            {
                return BattleOutcome.InProgress;
            }

            switch (detail)
            {
                case "Victory": return BattleOutcome.Victory;
                case "Defeat": return BattleOutcome.Defeat;
                case "Draw": return BattleOutcome.Draw;
                case "InProgress": return BattleOutcome.InProgress;
                default: return BattleOutcome.InProgress;
            }
        }

        private void RaiseEventPlayed(BattleEvent battleEvent)
        {
            Action<BattleEvent> handler = EventPlayed;

            if (handler != null)
            {
                handler(battleEvent);
            }
        }

        private void RaiseTurnStarted(int turn)
        {
            onTurnStarted.Invoke(turn);

            Action<int> handler = TurnStarted;

            if (handler != null)
            {
                handler(turn);
            }
        }

        private void RaiseTurnEnded(int turn, BattleOutcome outcome)
        {
            onTurnEnded.Invoke(turn, outcome);

            Action<int, BattleOutcome> handler = TurnEnded;

            if (handler != null)
            {
                handler(turn, outcome);
            }
        }

        private void RaiseBattleFinished(BattleResult finishedResult)
        {
            onBattleFinished.Invoke(finishedResult);

            Action<BattleResult> handler = BattleFinished;

            if (handler != null)
            {
                handler(finishedResult);
            }
        }
    }
}
