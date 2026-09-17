using System;
using System.Collections.Generic;
using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using BinakayanRising.UI.Screens.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Screens
{
    /// <summary>
    /// The in-battle interface: top bar, deployment roster, order of battle, field report and
    /// outcome card.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It reads <see cref="BattlePlaytest"/> through that class's public snapshot API and drives it
    /// through its command methods, so the dependency runs one way — interface depends on
    /// simulation, never the reverse.
    /// </para>
    /// <para>
    /// <b>Built once, updated in place.</b> Every widget exists from the first frame; phases show
    /// and hide whole sections, and values are written into the widgets that are already there, only
    /// when they change. The previous HUD tore the side panel down and rebuilt it on every state
    /// change, which flickered, leaked a material per engraved label per rebuild, and left the
    /// tutorial nothing stable to point at.
    /// </para>
    /// <para>
    /// <b>Anchors.</b> Controls the tutorial needs to find are registered under fixed ids such as
    /// <c>deploy.assault</c> — never looked up by object name, which would change with the
    /// language of the label on it.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    public sealed partial class BattleHud : MonoBehaviour
    {
        private const float TopBarHeight = 72f;
        private const float SidePanelWidth = 400f;
        private const float LogWidth = 560f;
        private const float LogHeight = 200f;
        private const int LogLines = 6;

        private readonly Dictionary<string, RectTransform> anchors = new Dictionary<string, RectTransform>();
        private readonly List<BattlePlaytest.UnitSnapshot> units = new List<BattlePlaytest.UnitSnapshot>();
        private readonly Vector3[] corners = new Vector3[4];

        private BattlePlaytest battle;
        private Canvas canvas;
        private WorldLabelLayer worldLabels;
        private TutorialDirector tutorial;
        private HowToPlayDeck deck;

        private RectTransform topBar;
        private RectTransform sidePanel;
        private RectTransform logPanel;

        /// <summary>Raised after a HUD control has done its job, with the control's anchor id.</summary>
        /// <remarks>Raised for hotkeys too, so a tutorial step waiting on a button is satisfied either way.</remarks>
        public event Action<string> ControlActivated;

        /// <summary>
        /// Decides whether a hotkey may act. Null allows everything; the tutorial narrows it to the
        /// keys its current step teaches.
        /// </summary>
        public Func<HudHotkey, bool> HotkeyFilter;

        /// <summary>The battle this HUD describes.</summary>
        public BattlePlaytest Battle => battle;

        /// <summary>The HUD canvas.</summary>
        public Canvas Canvas => canvas;

        /// <summary>The guided tutorial.</summary>
        public TutorialDirector Tutorial => tutorial;

        /// <summary>The How-to-Play card deck.</summary>
        public HowToPlayDeck Deck => deck;

        private void Awake()
        {
            battle = GetComponent<BattlePlaytest>();
            if (battle == null)
            {
                battle = FindFirstObjectByType<BattlePlaytest>();
            }

            if (battle == null)
            {
                Debug.LogError("BattleHud: no BattlePlaytest to bind to; the HUD will not build.");
                enabled = false;
                return;
            }

            Build();

            battle.StateChanged += OnStateChanged;
            battle.PhaseChanged += OnPhaseChanged;
            battle.SelectionChanged += OnSelectionChanged;
            battle.DeploymentChanged += OnStateChanged;
            battle.UnitPlaced += OnUnitPlaced;
            battle.UnitLifted += OnUnitLifted;
            battle.FieldReportAppended += OnFieldReportAppended;
            battle.SpeedChanged += OnSpeedChanged;
            Loc.LanguageChanged += OnLanguageChanged;

            OnPhaseChanged(battle.CurrentPhase);
        }

        private void Start()
        {
            if (tutorial != null)
            {
                tutorial.BeginIfFirstRun();
            }
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= OnLanguageChanged;

            if (battle != null)
            {
                battle.StateChanged -= OnStateChanged;
                battle.PhaseChanged -= OnPhaseChanged;
                battle.SelectionChanged -= OnSelectionChanged;
                battle.DeploymentChanged -= OnStateChanged;
                battle.UnitPlaced -= OnUnitPlaced;
                battle.UnitLifted -= OnUnitLifted;
                battle.FieldReportAppended -= OnFieldReportAppended;
                battle.SpeedChanged -= OnSpeedChanged;
            }
        }

        // ------------------------------------------------------------------ events

        private void OnStateChanged()
        {
            deploymentDirty = true;
            topBarDirty = true;
        }

        private void OnSelectionChanged(int slot)
        {
            deploymentDirty = true;
        }

        private void OnSpeedChanged(float speed)
        {
            topBarDirty = true;
        }

        private void OnPhaseChanged(BattlePlaytest.Phase phase)
        {
            bool deploying = phase == BattlePlaytest.Phase.Deployment;
            deploymentSection.gameObject.SetActive(deploying);
            combatSection.gameObject.SetActive(!deploying);

            if (!deploying)
            {
                BindOrderOfBattle();
            }

            deploymentDirty = true;
            topBarDirty = true;
            logDirty = true;
            RefreshOutcome(announce: true);
        }

        private void OnUnitPlaced()
        {
            UiSfx.Play(UiSfx.Cue.Place);
        }

        private void OnUnitLifted(int unitId)
        {
            UiSfx.Play(UiSfx.Cue.Toggle);
        }

        private void OnFieldReportAppended()
        {
            logDirty = true;
        }

        private void OnLanguageChanged()
        {
            deploymentDirty = true;
            topBarDirty = true;
            logDirty = true;
            orderNamesDirty = true;
            RefreshOutcome(announce: false);
        }

        /// <summary>Performs nothing itself; tells listeners a control did its job.</summary>
        private void Activated(string anchorId)
        {
            ControlActivated?.Invoke(anchorId);
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            UiKit.EnsureEventSystem();

            // Parented to the playtest so every canvas dies with the board it describes rather
            // than outliving it as an orphan.
            worldLabels = WorldLabelLayer.Create(transform, battle);

            canvas = UiKit.Screen("Battle HUD", Theme.Layer.Hud);
            canvas.transform.SetParent(transform, worldPositionStays: false);

            BuildTopBar(canvas.transform);
            BuildSidePanel(canvas.transform);
            BuildLogPanel(canvas.transform);
            BuildOutcome(canvas.transform);

            deck = HowToPlayDeck.Create(this);
            tutorial = gameObject.AddComponent<TutorialDirector>();
            tutorial.Bind(this);

            Canvas.ForceUpdateCanvases();
        }

        // ------------------------------------------------------------------ per-frame

        private void Update()
        {
            ReadHotkeys();
        }

        private void LateUpdate()
        {
            if (battle == null)
            {
                return;
            }

            if (topBarDirty)
            {
                topBarDirty = false;
                RefreshTopBar();
            }

            // The phase readout carries the turn number, which advances without an event.
            RefreshPhaseLabel();

            if (deploymentDirty)
            {
                deploymentDirty = false;
                RefreshDeployment();
            }

            RefreshOrderOfBattle();

            if (logDirty)
            {
                logDirty = false;
                RefreshLog();
            }

            PushBoardSafeArea();
        }

        /// <summary>
        /// Tells the board which part of the screen the panels leave free, so the camera frames
        /// the battlefield there instead of under the side panel.
        /// </summary>
        private void PushBoardSafeArea()
        {
            float width = UnityEngine.Screen.width;
            float height = UnityEngine.Screen.height;
            if (width <= 0f || height <= 0f)
            {
                return;
            }

            float margin = Theme.Space.Base * (canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f);

            Rect side = ScreenRectOf(sidePanel);
            Rect top = ScreenRectOf(topBar);
            Rect log = ScreenRectOf(logPanel);

            float xMin = side.width > 0f ? side.xMax + margin : 0f;
            float xMax = width - margin;
            float yMin = log.height > 0f ? log.yMax + margin : 0f;
            float yMax = top.height > 0f ? top.yMin - margin : height;

            if (xMax <= xMin || yMax <= yMin)
            {
                return;
            }

            battle.SetBoardSafeArea(Rect.MinMaxRect(xMin / width, yMin / height, xMax / width, yMax / height));
        }

        // ------------------------------------------------------------------ anchors

        /// <summary>Registers a control under a stable id for the tutorial to find.</summary>
        private RectTransform Register(string id, RectTransform rect)
        {
            anchors[id] = rect;
            return rect;
        }

        /// <summary>Looks up a registered control.</summary>
        public bool TryGetAnchor(string id, out RectTransform rect)
        {
            return anchors.TryGetValue(id, out rect) && rect != null;
        }

        /// <summary>A registered control's rectangle in screen pixels; empty when missing or hidden.</summary>
        public Rect AnchorScreenRect(string id)
        {
            RectTransform rect;
            return TryGetAnchor(id, out rect) ? ScreenRectOf(rect) : Rect.zero;
        }

        /// <summary>A world-space rectangle on the board, in screen pixels.</summary>
        public Rect WorldScreenRect(Rect world)
        {
            Camera camera = battle != null ? battle.BoardCamera : null;
            if (camera == null || world.width <= 0f)
            {
                return Rect.zero;
            }

            Vector3 min = camera.WorldToScreenPoint(new Vector3(world.xMin, world.yMin, 0f));
            Vector3 max = camera.WorldToScreenPoint(new Vector3(world.xMax, world.yMax, 0f));
            return Rect.MinMaxRect(
                Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        /// <summary>
        /// A rect's screen-pixel bounds. On an overlay canvas world corners are already pixels.
        /// </summary>
        private Rect ScreenRectOf(RectTransform rect)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
            {
                return Rect.zero;
            }

            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        // ------------------------------------------------------------------ layout helpers

        /// <summary>Stretches a rect across the top or bottom edge of its parent.</summary>
        private static void EdgeStretch(RectTransform rect, bool top, float size)
        {
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.offsetMin = new Vector2(0f, top ? -size : 0f);
            rect.offsetMax = new Vector2(0f, top ? 0f : size);
        }

        /// <summary>Pins a rect at a fixed size against an anchor point.</summary>
        private static void Pin(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            UiKit.Anchor(rect, anchor, pivot, offset, size);
        }

        private static LayoutElement Element(RectTransform rect)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>();
            return element != null ? element : rect.gameObject.AddComponent<LayoutElement>();
        }

        private static void FixHeight(RectTransform rect, float height)
        {
            LayoutElement element = Element(rect);
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
        }

        private static void FixWidth(RectTransform rect, float width)
        {
            LayoutElement element = Element(rect);
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }

        /// <summary>Makes a column's children span its full width.</summary>
        private static void ExpandChildren(RectTransform rect)
        {
            var layout = rect.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childForceExpandWidth = true;
            }
        }

        /// <summary>Keeps a one-line label on one line, shrinking it rather than wrapping or spilling.</summary>
        private static void FitLine(TextMeshProUGUI label, float maxSize)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = maxSize;
            label.fontSizeMin = Mathf.Max(10f, maxSize * 0.6f);
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static RectTransform RectOf(Component component)
        {
            return (RectTransform)component.transform;
        }
    }

    /// <summary>Keyboard shortcuts the HUD answers.</summary>
    public enum HudHotkey
    {
        /// <summary>Space: Begin Assault.</summary>
        Assault,

        /// <summary>1: half speed.</summary>
        SpeedSlow,

        /// <summary>2: normal speed.</summary>
        SpeedNormal,

        /// <summary>3: triple speed.</summary>
        SpeedFast,

        /// <summary>Esc: close, skip.</summary>
        Escape
    }
}
