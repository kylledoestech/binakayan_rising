using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Localization;
using BinakayanRising.Data;
using BinakayanRising.Gameplay;
using BinakayanRising.Gameplay.Flow;
using BinakayanRising.UI.Kit;
using UnityEngine;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// Where a campaign battle's quiz pays out (#43): the Reales announcement, and the Tactician's
    /// Command the player picks, applied to the battle on the board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Reales themselves are credited by <see cref="Core.Meta.MetaGame.RecordQuizAnswer"/>,
    /// the Core rule the economy tests cover, the moment the answer is recorded. So
    /// <see cref="AwardReales"/> does not credit them a second time: it is the payout the player
    /// sees and hears, the coin and the toast.
    /// </para>
    /// <para>
    /// <see cref="ApplyRewardEffect"/> maps Table 4's effect onto the Core command of the same
    /// value and hands it to <see cref="BattlePlaytest.ApplyTacticianCommand(TacticianCommand, float, int)"/>,
    /// which re-fights the rest of the battle with it.
    /// </para>
    /// </remarks>
    public sealed class CampaignQuizRewards : IQuizRewardReceiver
    {
        private readonly BattlePlaytest battle;

        /// <param name="battle">The battle the commands land in.</param>
        public CampaignQuizRewards(BattlePlaytest battle)
        {
            this.battle = battle;
        }

        /// <summary>True when the last command given was applied to the battle.</summary>
        public bool LastApplied { get; private set; }

        /// <summary>Announces Reales already credited by the answer: the coin cue and a toast.</summary>
        public void AwardReales(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            UiSfx.Play(UiSfx.Cue.Coin);
            UiControls.Toast(Loc.Format(TextKey.QuizReales, amount));
        }

        /// <summary>Applies one Table 4 effect to the battle, re-simulating what follows.</summary>
        public void ApplyRewardEffect(QuizRewardEffect effect, float magnitude, int durationTurns)
        {
            LastApplied = false;
            TacticianCommand command = ToCommand(effect);
            if (battle == null || command == TacticianCommand.None)
            {
                return;
            }

            LastApplied = battle.ApplyTacticianCommand(command, magnitude, durationTurns);
            if (!LastApplied)
            {
                Debug.LogWarning("[CampaignQuizRewards] " + command + " could not be applied.");
            }
        }

        /// <summary>Table 4's effect as the Core command; the two enums share their values.</summary>
        public static TacticianCommand ToCommand(QuizRewardEffect effect)
        {
            switch (effect)
            {
                case QuizRewardEffect.MapWideHeal:
                    return TacticianCommand.MapWideHeal;
                case QuizRewardEffect.AttackBuff:
                    return TacticianCommand.AttackBuff;
                case QuizRewardEffect.ResetEnemyPositions:
                    return TacticianCommand.ResetEnemyPositions;
                case QuizRewardEffect.ReviveFallenUnit:
                    return TacticianCommand.ReviveFallenUnit;
                default:
                    return TacticianCommand.None;
            }
        }

        /// <summary>The Core command as Table 4's effect.</summary>
        public static QuizRewardEffect ToEffect(TacticianCommand command)
        {
            switch (command)
            {
                case TacticianCommand.MapWideHeal:
                    return QuizRewardEffect.MapWideHeal;
                case TacticianCommand.AttackBuff:
                    return QuizRewardEffect.AttackBuff;
                case TacticianCommand.ResetEnemyPositions:
                    return QuizRewardEffect.ResetEnemyPositions;
                case TacticianCommand.ReviveFallenUnit:
                    return QuizRewardEffect.ReviveFallenUnit;
                default:
                    return QuizRewardEffect.None;
            }
        }
    }
}
