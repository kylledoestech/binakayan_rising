using System.Collections.Generic;
using BinakayanRising.Core.Localization;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BinakayanRising.UI.Screens
{
    public sealed partial class BattleHud
    {
        private static readonly float[] SpeedSteps = { 0.5f, 1f, 3f };
        private static readonly string[] SpeedCaptions = { "0.5x", "1x", "3x" };

        private readonly List<TextMeshProUGUI> speedLabels = new List<TextMeshProUGUI>();

        private TextMeshProUGUI phaseLabel;
        private TextMeshProUGUI objectiveLabel;
        private TextMeshProUGUI seedLabel;
        private Button seedMinus;
        private Button seedPlus;
        private Button skipButton;
        private Button retreatButton;
        private TextMeshProUGUI englishLabel;
        private TextMeshProUGUI filipinoLabel;

        private bool topBarDirty = true;
        private BattlePlaytest.Phase shownPhase = (BattlePlaytest.Phase)(-1);
        private int shownTurn = -1;
        private int shownPhaseVersion = -1;
        private int shownSeed = int.MinValue;

        /// <summary>
        /// One full-width row, laid out by a layout group rather than two fixed-width halves.
        /// </summary>
        /// <remarks>
        /// The previous bar pinned a 760-unit left block and a 900-unit right block to opposite
        /// edges, which overlapped the moment the canvas was narrower than their sum and had no room
        /// for the language and help buttons. A single row with a flexible spacer lets the phase
        /// readout give way instead.
        /// </remarks>
        private void BuildTopBar(Transform parent)
        {
            topBar = Register("top.bar", UiKit.Panel(parent, "Top Bar"));
            EdgeStretch(topBar, top: true, size: TopBarHeight);

            RectTransform row = UiKit.Row(topBar, "Row", Theme.Space.Tight, 0f, TextAnchor.MiddleLeft);
            UiKit.Stretch(row);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)Theme.Space.Wide, (int)Theme.Space.Wide, (int)Theme.Space.Snug, (int)Theme.Space.Snug);
            layout.childForceExpandHeight = false;

            Image sigil = UiKit.Sigil(row, 40f, Theme.Gold);
            FixWidth(sigil.rectTransform, 40f);

            // A campaign battle names its quest, with the player's rank beneath it.
            MissionSetup mission = battle.Mission;
            if (mission != null)
            {
                RectTransform titles = UiKit.Column(row, "Titles", 0f, 0f, TextAnchor.MiddleLeft);
                FixWidth(titles, 330f);
                ExpandChildren(titles);

                TextMeshProUGUI quest = UiKit.Display(titles, mission.Title ?? string.Empty, Theme.Type.Body + 2f, TextAlignmentOptions.Left);
                FitLine(quest, Theme.Type.Body + 2f);
                FixHeight(quest.rectTransform, 30f);

                TextMeshProUGUI rank = UiKit.Caption(titles, mission.RankTitle ?? string.Empty, TextAlignmentOptions.Left);
                rank.color = Theme.Revolution;
                rank.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                FitLine(rank, Theme.Type.Small);
                FixHeight(rank.rectTransform, 22f);
                Register("top.rank", rank.rectTransform);
            }
            else
            {
                TextMeshProUGUI title = UiKit.Display(row, "Binakayan Rising", Theme.Type.Heading, TextAlignmentOptions.Left);
                FitLine(title, Theme.Type.Heading);
                FixWidth(title.rectTransform, 330f);
            }

            // Under an objective (#37, #38) the phase readout shares its slot with the standing
            // order, so the player never has to open the briefing again to know what wins.
            bool hasObjective = BattleText.Objective(battle.Rule, battle.TurnCap) != null;
            Transform phaseParent = row;
            if (hasObjective)
            {
                RectTransform phaseColumn = UiKit.Column(row, "Phase", 0f, 0f, TextAnchor.MiddleLeft);
                ExpandChildren(phaseColumn);
                phaseParent = phaseColumn;
                LayoutElement columnElement = Element(phaseColumn);
                columnElement.minWidth = 160f;
                columnElement.preferredWidth = 260f;
                columnElement.flexibleWidth = 1f;
            }

            phaseLabel = UiKit.Body(phaseParent, string.Empty, Theme.Type.Body, TextAlignmentOptions.Left);

            // Not gold: the top bar is parchment, and gold on parchment is close enough in value
            // that the phase readout disappears into its own background.
            phaseLabel.color = Theme.Revolution;
            phaseLabel.fontStyle = FontStyles.UpperCase;
            FitLine(phaseLabel, Theme.Type.Body);
            if (hasObjective)
            {
                FixHeight(phaseLabel.rectTransform, 28f);
                objectiveLabel = UiKit.Caption(phaseParent, string.Empty, TextAlignmentOptions.Left);
                objectiveLabel.color = Theme.InkSoft;
                objectiveLabel.fontStyle = FontStyles.Bold;
                FitLine(objectiveLabel, Theme.Type.Small);
                FixHeight(objectiveLabel.rectTransform, 22f);
                Register("top.objective", objectiveLabel.rectTransform);
            }
            else
            {
                LayoutElement phaseElement = Element(phaseLabel.rectTransform);
                phaseElement.minWidth = 160f;
                phaseElement.preferredWidth = 260f;
                phaseElement.flexibleWidth = 1f;
            }

            // Seed
            RectTransform seed = Register("top.seed", UiKit.Row(row, "Seed", Theme.Space.Hair, 0f, TextAnchor.MiddleCenter));
            Caption(seed, TextKey.HudSeed, 64f);
            seedMinus = UiKit.SealButton(seed, "-", () => battle.SetSeed(battle.Seed - 1), 40f, 40f, Theme.Type.Body, "Button Seed Minus");
            seedLabel = UiKit.Body(seed, "1896", Theme.Type.Body, TextAlignmentOptions.Center);
            FixWidth(seedLabel.rectTransform, 64f);
            seedPlus = UiKit.SealButton(seed, "+", () => battle.SetSeed(battle.Seed + 1), 40f, 40f, Theme.Type.Body, "Button Seed Plus");

            // A quest's seed is part of the quest, not a control.
            seed.gameObject.SetActive(mission == null);

            Spacer(row, Theme.Space.Snug);

            // Speed
            RectTransform speed = Register("top.speed", UiKit.Row(row, "Speed", Theme.Space.Hair, 0f, TextAnchor.MiddleCenter));
            Caption(speed, TextKey.HudSpeed, 70f);
            for (int i = 0; i < SpeedSteps.Length; i++)
            {
                int index = i;
                Button button = UiKit.SealButton(
                    speed, SpeedCaptions[i], () => SetSpeedStep(index), 64f, 40f, Theme.Type.Small, "Button Speed " + SpeedCaptions[i]);
                speedLabels.Add(button.GetComponentInChildren<TextMeshProUGUI>());
            }

            skipButton = UiKit.SealButton(speed, TextKey.HudSkip, SkipReplay, 96f, 40f, Theme.Type.Small, "Button Skip");

            Spacer(row, Theme.Space.Snug);

            // Language
            RectTransform language = Register("top.language", UiKit.Row(row, "Language", Theme.Space.Hair, 0f, TextAnchor.MiddleCenter));
            englishLabel = UiKit.SealButton(language, "EN", () => ChooseLanguage(Language.English), 52f, 40f, Theme.Type.Small, "Button English")
                .GetComponentInChildren<TextMeshProUGUI>();
            filipinoLabel = UiKit.SealButton(language, "FIL", () => ChooseLanguage(Language.Filipino), 60f, 40f, Theme.Type.Small, "Button Filipino")
                .GetComponentInChildren<TextMeshProUGUI>();

            Button help = UiKit.SealButton(row, "?", OpenHelp, 44f, 40f, Theme.Type.Heading, "Button Help");
            Register("top.help", RectOf(help));

            Button redeploy = UiKit.SealButton(row, TextKey.HudRedeploy, Redeploy, 170f, 44f, Theme.Type.Small, "Button Redeploy");
            Register("top.redeploy", RectOf(redeploy));

            if (mission != null)
            {
                // Before the assault the player may still turn back; nothing is spent.
                retreatButton = UiKit.SealButton(row, TextKey.MissionRetreat, Retreat, 150f, 44f, Theme.Type.Small, "Button Retreat");
                Register("top.retreat", RectOf(retreatButton));
            }
        }

        private static TextMeshProUGUI Caption(Transform parent, TextKey key, float width)
        {
            TextMeshProUGUI caption = UiKit.Caption(parent, Loc.Get(key), TextAlignmentOptions.Right);
            caption.color = Theme.InkSoft;
            caption.fontStyle = FontStyles.UpperCase;
            caption.margin = new Vector4(0f, 0f, Theme.Space.Hair, 0f);
            FitLine(caption, Theme.Type.Small);
            FixWidth(caption.rectTransform, width);
            UiKit.Localize(caption, key);
            return caption;
        }

        private static void Spacer(Transform parent, float width)
        {
            RectTransform spacer = UiKit.NewRect(parent, "Spacer");
            FixWidth(spacer, width);
        }

        // ------------------------------------------------------------------ actions

        private void SetSpeedStep(int index)
        {
            battle.SetSpeed(SpeedSteps[index]);
            Activated("top.speed");
        }

        private void SkipReplay()
        {
            battle.RequestSkip();
            Activated("top.skip");
        }

        private void ChooseLanguage(Language language)
        {
            UserPrefs.ChooseLanguage(language);
            Activated("top.language");
        }

        private void OpenHelp()
        {
            if (tutorial != null && tutorial.IsRunning && !tutorial.IsWaitingOffscreen)
            {
                return;
            }

            deck.Open();
            Activated("top.help");
        }

        private void Retreat()
        {
            if (battle.CurrentPhase != BattlePlaytest.Phase.Deployment || (tutorial != null && tutorial.IsRunning))
            {
                UiSfx.Play(UiSfx.Cue.Error);
                return;
            }

            Activated("top.retreat");
            battle.EndMission();
        }

        private void Redeploy()
        {
            battle.RequestRedeploy();
            Activated("top.redeploy");
        }

        // ------------------------------------------------------------------ refresh

        private void RefreshTopBar()
        {
            if (shownSeed != battle.Seed)
            {
                shownSeed = battle.Seed;
                seedLabel.SetText("{0}", battle.Seed);
            }

            // The seed decides the battle that is about to be fought. Changing it mid-replay used to
            // relabel a battle already resolved from a different one.
            bool deploying = battle.CurrentPhase == BattlePlaytest.Phase.Deployment;
            seedMinus.interactable = deploying;
            seedPlus.interactable = deploying;
            skipButton.interactable = battle.CurrentPhase == BattlePlaytest.Phase.Combat;
            if (retreatButton != null)
            {
                retreatButton.interactable = deploying;
            }

            // The active rate is shown by tinting the label rather than by swapping the button's
            // sprite, so the row keeps a single consistent silhouette.
            for (int i = 0; i < speedLabels.Count; i++)
            {
                speedLabels[i].color = Mathf.Approximately(battle.Speed, SpeedSteps[i]) ? Theme.GoldBright : Theme.Parchment;
            }

            englishLabel.color = Loc.Current == Language.English ? Theme.GoldBright : Theme.Parchment;
            filipinoLabel.color = Loc.Current == Language.Filipino ? Theme.GoldBright : Theme.Parchment;
        }

        private void RefreshPhaseLabel()
        {
            BattlePlaytest.Phase phase = battle.CurrentPhase;
            int turn = battle.CurrentTurn;
            if (phase == shownPhase && turn == shownTurn && shownPhaseVersion == Loc.Version)
            {
                return;
            }

            shownPhase = phase;
            shownTurn = turn;
            shownPhaseVersion = Loc.Version;
            phaseLabel.text = BattleText.Phase(phase, turn);
            if (objectiveLabel != null)
            {
                objectiveLabel.text = BattleText.Objective(battle.Rule, battle.TurnCap);
            }
        }
    }
}
