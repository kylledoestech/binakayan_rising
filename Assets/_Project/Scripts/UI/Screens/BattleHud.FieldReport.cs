using System.Text;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
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
        private readonly StringBuilder logBuilder = new StringBuilder(512);

        private TextMeshProUGUI logText;
        private bool logDirty = true;

        private RectTransform outcomeRoot;
        private RectTransform outcomeCard;
        private TextMeshProUGUI outcomeTitle;
        private TextMeshProUGUI outcomeSummary;

        private void BuildLogPanel(Transform parent)
        {
            logPanel = Register("log", UiKit.Well(parent, "Field Report", blocksClicks: true));
            Pin(logPanel, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Theme.Space.Base, Theme.Space.Base), new Vector2(LogWidth, LogHeight));

            TextMeshProUGUI heading = UiKit.Caption(logPanel, Loc.Get(TextKey.FieldReport), TextAlignmentOptions.TopLeft);
            heading.color = Theme.RevolutionDark;
            heading.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            UiKit.Localize(heading, TextKey.FieldReport);
            Pin(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Theme.Space.Wide, -Theme.Space.Base), new Vector2(LogWidth - (2f * Theme.Space.Wide), 22f));

            // Clipped, and anchored to the bottom so the newest line always sits on the last row.
            // Six lines of long Filipino sentences wrap to more than six rows; the oldest scroll
            // out of the top instead of spilling out of the well.
            RectTransform viewport = UiKit.NewRect(logPanel, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(Theme.Space.Wide, Theme.Space.Base);
            viewport.offsetMax = new Vector2(-Theme.Space.Wide, -(Theme.Space.Base + 26f));
            viewport.gameObject.AddComponent<RectMask2D>();

            logText = UiKit.Body(viewport, string.Empty, Theme.Type.Small, TextAlignmentOptions.BottomLeft);
            logText.color = Theme.Ink;
            UiKit.Stretch(logText.rectTransform);
        }

        private void RefreshLog()
        {
            logBuilder.Length = 0;

            int count = battle.FieldReportCount;
            for (int i = Mathf.Max(0, count - LogLines); i < count; i++)
            {
                if (logBuilder.Length > 0)
                {
                    logBuilder.Append('\n');
                }

                BattleText.AppendReportLine(logBuilder, battle.GetFieldReport(i));
            }

            logText.SetText(logBuilder);
        }

        private void BuildOutcome(Transform parent)
        {
            // Stops at the top bar rather than covering it, so Redeploy, the language switch and
            // help stay usable while the result is on screen.
            outcomeRoot = UiKit.NewRect(parent, "Outcome");
            UiKit.Stretch(outcomeRoot);
            outcomeRoot.offsetMax = new Vector2(0f, -TopBarHeight);

            UiKit.Scrim(outcomeRoot);

            outcomeCard = Register("outcome.card", UiKit.Panel(outcomeRoot, "Card"));
            Pin(outcomeCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 440f));

            RectTransform column = UiKit.Column(outcomeCard, "Body", Theme.Space.Base, Theme.Space.Loose, TextAnchor.UpperCenter);
            UiKit.Stretch(column);
            ExpandChildren(column);

            Image sigil = UiKit.Sigil(column, 84f, Theme.Gold);
            FixHeight(sigil.rectTransform, 84f);

            outcomeTitle = UiKit.Display(column, string.Empty, Theme.Type.Display, TextAlignmentOptions.Center);
            FitLine(outcomeTitle, Theme.Type.Display);
            FixHeight(outcomeTitle.rectTransform, 76f);

            outcomeSummary = UiKit.Body(column, string.Empty, Theme.Type.Body, TextAlignmentOptions.Center);
            FixHeight(outcomeSummary.rectTransform, 96f);

            RectTransform actions = UiKit.Row(column, "Actions", Theme.Space.Base, 0f, TextAnchor.MiddleCenter);
            FixHeight(actions, 64f);

            if (battle.IsMission)
            {
                // A campaign battle is settled once: the way on is back to camp.
                UiKit.SealButton(actions, TextKey.MissionReturn, ReturnToCamp, 320f, 60f, 0f, "Button Return To Camp");
            }
            else
            {
                UiKit.SealButton(actions, TextKey.NewSeed, NewSeed, 280f, 60f, 0f, "Button New Seed");
                UiKit.SealButton(actions, TextKey.HudRedeploy, Redeploy, 280f, 60f, 0f, "Button Outcome Redeploy");
            }

            outcomeRoot.gameObject.SetActive(false);
        }

        private void ReturnToCamp()
        {
            Activated("outcome.return");
            battle.EndMission();
        }

        private void NewSeed()
        {
            battle.RequestNewSeed();
            Activated("outcome.newseed");
        }

        private void RefreshOutcome(bool announce)
        {
            BattleResult result = battle.Result;
            bool show = battle.CurrentPhase == BattlePlaytest.Phase.Finished && result != null;

            if (outcomeRoot.gameObject.activeSelf != show)
            {
                outcomeRoot.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            // Under the Hold rule a battle still going at the turn cap is won: the line held.
            BattleOutcome shown = battle.MissionWon ? BattleOutcome.Victory : result.Outcome;

            Color titleColor;
            UiSfx.Cue cue;
            switch (shown)
            {
                case BattleOutcome.Victory:
                    titleColor = Theme.Gold;
                    cue = UiSfx.Cue.Victory;
                    break;
                case BattleOutcome.Defeat:
                    titleColor = Theme.Revolution;
                    cue = UiSfx.Cue.Error;
                    break;
                default:
                    titleColor = Theme.Parchment;
                    cue = UiSfx.Cue.Close;
                    break;
            }

            outcomeTitle.text = BattleText.Outcome(shown);
            outcomeTitle.color = titleColor;

            // The seed the battle was resolved from, not the one the next battle will use: the two
            // differ as soon as anyone touches the seed control.
            string first = battle.MissionWon && result.Outcome == BattleOutcome.Draw
                ? Loc.Format(TextKey.OutcomeHeld, result.TurnsElapsed)
                : Loc.Format(TextKey.OutcomeTurns, result.TurnsElapsed);
            string last = battle.IsMission
                ? Loc.Get(battle.MissionWon ? TextKey.MissionWonNote : TextKey.MissionLostNote)
                : Loc.Format(TextKey.OutcomeSeed, result.Events.Count, battle.ResultSeed);
            // The cart counts among the Katipunan still standing in the simulation, but it is not
            // a soldier: the survivors line counts soldiers, and the objective line reports the cart.
            string objective = null;
            int katipunanStanding = result.KatipunanAlive;
            if (battle.Rule == WinRule.Escort)
            {
                bool cartLost = UnitDied(result, PlaytestScenario.SupplyCartId);
                objective = Loc.Get(cartLost ? TextKey.OutcomeCartLost : TextKey.OutcomeCartSaved);
                katipunanStanding -= cartLost ? 0 : 1;
            }
            else if (battle.Rule == WinRule.Sabotage)
            {
                objective = Loc.Get(result.Outcome == BattleOutcome.Victory ? TextKey.OutcomeMagazineBlown : TextKey.OutcomeMagazineMissed);
            }

            outcomeSummary.text =
                (objective != null ? objective + "\n" : string.Empty)
                + first + "\n"
                + Loc.Format(TextKey.OutcomeSurvivors, Mathf.Max(0, katipunanStanding), result.SpanishAlive) + "\n"
                + last;

            if (announce)
            {
                UiSfx.Play(cue);
                CoroutineHost.Run(UiTween.Punch(outcomeCard));
            }
        }

        /// <summary>True when the log records <paramref name="unitId"/> falling.</summary>
        private static bool UnitDied(BattleResult result, int unitId)
        {
            for (int i = 0; i < result.Events.Count; i++)
            {
                if (result.Events[i].Type == BattleEventType.UnitDied && result.Events[i].ActorId == unitId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
