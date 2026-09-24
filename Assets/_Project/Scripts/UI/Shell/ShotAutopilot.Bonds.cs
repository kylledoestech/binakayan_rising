using System.Collections;
using System.Collections.Generic;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Grid;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using UnityEngine;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The "bonds" route (#43, #19, #20): a right quiz answer and the Tactician's Command pick, the
    /// battle after the command lands, the Kapatiran rank card after the battle, a lore dialogue,
    /// and the Lore list in the Training Grounds.
    /// </summary>
    public sealed partial class ShotAutopilot
    {
        private IEnumerator Bonds()
        {
            Shell.Session.DeleteSave();
            Shell.StartNewCampaign();
            yield return Wait(0.6f);

            MetaGame game = Shell.Session.Game;
            Hub().Dialogue.Finish();
            game.Earn(Currency.Rations, 60);
            foreach (string id in new[] { "q01", "q02", "q03", "q04", "q05" })
            {
                game.Data.clearedQuests.Add(id);
            }

            // Evangelista and Aguinaldo one battle short of rank B, so the card shows a bonus; the
            // Marksman and Engineer at nothing yet, so their first battle opens their lore.
            game.Data.bonds.Add(new BondRecord { bond = BondCatalog.EvangelistaAguinaldo, support = 2 });

            yield return WaitWhile(() => RankUpCard.Current == null, 3f);
            for (int i = 0; RankUpCard.Current != null && i < 8; i++)
            {
                RankUpCard.Current.Continue();
                RankUpCard.Current?.Continue();
                yield return Wait(0.4f);
            }

            // A quest launches only from the Mission Tent (the MissionPortal state).
            Shell.Camp.ClickSite(Places.MissionTent);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Hub().Dialogue.Finish();
            yield return WaitWhile(() => !(Shell.Router.Current is MissionMapScreen), 4f);

            bool launched = Shell.LaunchQuest(Campaign.Find("q06"));
            yield return Wait(0.5f);
            CutscenePlayer.Current?.Skip();
            yield return WaitWhile(() => Shell.Battle == null, 4f);
            yield return Wait(1.5f);
            BattlePlaytest battle = Shell.Battle;
            if (battle == null)
            {
                Note("bonds", "the q06 battle did not open (launch " + launched + ", screen "
                    + (Shell.Router.Current != null ? Shell.Router.Current.GetType().Name : "none")
                    + ", rations " + game.Data.rations + ")");
                yield break;
            }

            battle.RequestAutoDeploy();
            yield return Wait(0.5f);
            MeasureBondAdjacency(game, battle);
            battle.RequestAssault();
            battle.SetSpeed(4f);
            yield return WaitWhile(() => QuizCard.Current == null && Shell.Battle != null
                && Shell.Battle.CurrentPhase != BattlePlaytest.Phase.Finished, 90f);
            if (QuizCard.Current == null)
            {
                Note("bonds", "the battle ended before its quiz turn");
                yield break;
            }

            // Answer right: the command card only follows a right answer.
            Question asking = QuizCard.Current.Asking;
            QuizCard.Current.Choose(asking != null ? asking.Answer : 0);
            yield return Shot("b_01_quiz_right");
            QuizCard.Current?.Continue();
            yield return WaitWhile(() => TacticianCommandCard.Current == null, 3f);
            if (TacticianCommandCard.Current == null)
            {
                Note("bonds", "no Tactician's Command card after a right answer");
                yield break;
            }

            yield return Shot("b_02_command_card");
            MeasureCard("command", TacticianCommandCard.Current.Card);
            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Shot("b_03_command_card_fil");
            MeasureCard("command_fil", TacticianCommandCard.Current.Card);
            UserPrefs.ChooseLanguage(Language.English);

            // Reset the enemy line: the one command whose effect is visible on the board at once.
            BattleResult before = battle.Result;
            battle.SetSpeed(1f);
            TacticianCommandCard.Current.Pick(2);
            yield return WaitWhile(() => TacticianCommandCard.Current != null, 3f);
            if (battle.IssuedCommand != TacticianCommand.ResetEnemyPositions || ReferenceEquals(before, battle.Result))
            {
                Note("bonds", "the command was not applied (issued " + battle.IssuedCommand + ")");
            }
            else
            {
                Line("bonds: command re-simulated, outcome " + before.Outcome + " after " + before.TurnsElapsed
                    + " turns -> " + battle.Result.Outcome + " after " + battle.Result.TurnsElapsed);
            }

            yield return Shot("b_04_battle_after_command", 1.2f);
            battle.SetSpeed(8f);
            yield return WaitWhile(() => Shell.Battle != null && Shell.Battle.CurrentPhase != BattlePlaytest.Phase.Finished, 120f);
            yield return Shot("b_05_battle_report", 1f);
            Shell.Battle?.EndMission();
            yield return Wait(1f);
            yield return DrainPromotions("b_05");
            yield return WaitWhile(() => BondRankCard.Current == null, 3f);
            if (BondRankCard.Current == null)
            {
                Note("bonds", "no bond rank card after the battle");
            }

            yield return ShootBondCards();

            // Whatever follows the bond cards: the player's own rank, the closing scene.
            for (int i = 0; i < 8 && (RankUpCard.Current != null || CutscenePlayer.Current != null); i++)
            {
                RankUpCard.Current?.Continue();
                RankUpCard.Current?.Continue();
                CutscenePlayer.Current?.Skip();
                yield return Wait(0.5f);
            }

            Line("bonds: ranks after q06 — " + RankSummary(game));

            // The Training Grounds bond line, then the Lore list it opens.
            Shell.Camp.ClickSite(Places.Training);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Hub().Dialogue.Finish();
            yield return Wait(0.5f);
            OwnedUnit evangelista = FirstUnit(game, UnitCatalog.Evangelista);
            if (evangelista != null)
            {
                Training().SelectedUnit = evangelista.id;
            }

            yield return Shot("b_09_training_bond_line");
            ClickIn("Button Lore");
            yield return WaitWhile(() => BondLorePanel.Current == null, 2f);
            yield return Shot("b_10_lore_list");
            MeasureCard("lore_list", BondLorePanel.Current != null ? BondLorePanel.Current.Card : null);
            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Shot("b_11_lore_list_fil");
            MeasureCard("lore_list_fil", BondLorePanel.Current != null ? BondLorePanel.Current.Card : null);
            UserPrefs.ChooseLanguage(Language.English);

            // Re-view from the list: the first open dialogue plays again.
            for (int i = 0; i < BondLore.All.Count && BondLorePanel.Current != null; i++)
            {
                if (game.IsLoreUnlocked(BondLore.All[i].BondId))
                {
                    BondLorePanel.Current.Listen(i);
                    break;
                }
            }

            yield return Shot("b_12_lore_replay", 2.5f);
            LorePlayer.Current?.Panel.Finish();
            yield return Wait(0.3f);
            BondLorePanel.Current?.Close();
            yield return Wait(0.3f);
        }

        /// <summary>Shoots every bond card in turn, and the lore dialogue the first rank C opens.</summary>
        private IEnumerator ShootBondCards()
        {
            bool heardLore = false;
            for (int shown = 0; BondRankCard.Current != null && shown < 6; shown++)
            {
                BondRankCard card = BondRankCard.Current;
                string pair = card.Showing != null ? card.Showing.Pair.Id : "?";
                yield return Shot("b_06_bond_rank_" + shown + "_" + pair, 1.6f);
                MeasureCard("bond_rank_" + shown, card.Card);

                if (!heardLore && card.Showing != null && card.Showing.UnlockedLore)
                {
                    UserPrefs.ChooseLanguage(Language.Filipino);
                    yield return Shot("b_06_bond_rank_" + shown + "_fil", 0.5f);
                    UserPrefs.ChooseLanguage(Language.English);

                    heardLore = true;
                    card.Listen();
                    yield return WaitWhile(() => LorePlayer.Current == null, 2f);
                    yield return Shot("b_07_lore_dialogue", 3f);
                    LorePlayer.Current?.Panel.Advance();
                    LorePlayer.Current?.Panel.Advance();
                    UserPrefs.ChooseLanguage(Language.Filipino);
                    yield return Shot("b_08_lore_dialogue_fil", 3f);
                    UserPrefs.ChooseLanguage(Language.English);
                    LorePlayer.Current?.Panel.Finish();
                    yield return WaitWhile(() => LorePlayer.Current != null, 2f);
                }

                BondRankCard.Current?.Continue();
                BondRankCard.Current?.Continue();
                yield return Wait(0.4f);
            }

            if (!heardLore)
            {
                Note("bonds", "no bond card opened a lore dialogue");
            }
        }

        /// <summary>Records which bonded pairs the auto-deployment put side by side.</summary>
        private void MeasureBondAdjacency(MetaGame game, BattlePlaytest battle)
        {
            KapatiranResolver rule = KapatiranResolver.CreateEmpty();
            foreach (BondPair pair in BondCatalog.Pairs)
            {
                OwnedUnit a = FirstUnit(game, pair.ArchetypeA);
                OwnedUnit b = FirstUnit(game, pair.ArchetypeB);
                GridCoord at;
                GridCoord bt;
                if (a == null || b == null || !battle.TryGetPlacement(a.id, out at) || !battle.TryGetPlacement(b.id, out bt))
                {
                    continue;
                }

                Line("bonds: " + pair.Id + " deployed at " + at + " and " + bt
                    + (rule.IsInProximity(at, bt) ? ", side by side" : ", apart"));
            }
        }

        /// <summary>Takes the first command a right answer offers, so older routes run on past it.</summary>
        private IEnumerator TakeAnyCommand()
        {
            yield return Wait(0.2f);
            TacticianCommandCard card = TacticianCommandCard.Current;
            if (card == null)
            {
                yield break;
            }

            for (int i = 0; i < TacticianCommandCard.Commands.Length; i++)
            {
                if (card.IsAllowed(i))
                {
                    yield return Wait(0.1f);
                    card.Pick(i);
                    break;
                }
            }

            yield return WaitWhile(() => TacticianCommandCard.Current != null, 3f);
        }

        private static OwnedUnit FirstUnit(MetaGame game, string archetype)
        {
            IReadOnlyList<OwnedUnit> units = game.Units;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].archetype == archetype)
                {
                    return units[i];
                }
            }

            return null;
        }

        private static string RankSummary(MetaGame game)
        {
            var parts = new List<string>();
            foreach (BondPair pair in BondCatalog.Pairs)
            {
                parts.Add(pair.Id + " " + BondCatalog.Label(game.BondRankOf(pair.Id)) + " (" + game.BondSupport(pair.Id) + ")");
            }

            return string.Join(", ", parts);
        }

        /// <summary>An informational audit line; not a problem.</summary>
        private void Line(string text)
        {
            audit.AppendLine("  " + text);
        }
    }
}
