using BinakayanRising.Gameplay;
using BinakayanRising.UI.Kit;
using UnityEngine;

namespace BinakayanRising.UI.Screens
{
    public sealed partial class BattleHud
    {
        /// <summary>Seconds for the side panel and field report to slide fully in or out.</summary>
        private const float PanelSlideSeconds = 0.28f;

        private CanvasGroup sideGroup;
        private CanvasGroup logGroup;
        private Vector2 sideRest;
        private Vector2 logRest;

        /// <summary>The player's choice for the replay; every battle opens with the panels away.</summary>
        private bool combatPanelsHidden = true;

        /// <summary>How far in the panels are: 1 at rest on screen, 0 slid off and switched off.</summary>
        private float panelsIn = 1f;

        /// <summary>
        /// True when the side panel and field report are meant to be on screen. They slide away
        /// once the assault begins, so the replay has the screen; Tab brings them back.
        /// </summary>
        public bool PanelsWanted
        {
            get
            {
                if (battle == null || battle.CurrentPhase != BattlePlaytest.Phase.Combat)
                {
                    // Deploying needs the roster; the result needs the order of battle behind it.
                    return true;
                }

                // A tutorial card on screen may be pointing at the field report.
                if (tutorial != null && tutorial.IsRunning && !tutorial.IsWaitingOffscreen)
                {
                    return true;
                }

                return !combatPanelsHidden;
            }
        }

        /// <summary>True once the panels have finished sliding off.</summary>
        public bool PanelsHidden => panelsIn <= 0f;

        /// <summary>Shows or hides the side panel and field report during the replay (Tab).</summary>
        /// <returns>False outside the replay, where the panels always stay.</returns>
        public bool ToggleCombatPanels()
        {
            if (battle == null || battle.CurrentPhase != BattlePlaytest.Phase.Combat)
            {
                return false;
            }

            combatPanelsHidden = !combatPanelsHidden;
            UiSfx.Play(UiSfx.Cue.Toggle);
            Activated("panels");
            return true;
        }

        /// <summary>Remembers where the panels rest, so sliding them never drifts.</summary>
        private void BindPanelSlide()
        {
            sideGroup = UiKit.Group(sidePanel.gameObject);
            logGroup = UiKit.Group(logPanel.gameObject);
            sideRest = sidePanel.anchoredPosition;
            logRest = logPanel.anchoredPosition;
        }

        /// <summary>Every new replay starts with the panels out of the way.</summary>
        private void ResetPanelsForPhase(BattlePlaytest.Phase phase)
        {
            if (phase == BattlePlaytest.Phase.Combat)
            {
                combatPanelsHidden = true;
            }
        }

        /// <summary>Moves the panels one frame toward where they are wanted.</summary>
        /// <remarks>
        /// Stepped here rather than by a coroutine, so a Tab pressed mid-slide simply turns the
        /// slide around instead of racing a second tween. Unscaled time: the panels still move
        /// while the replay is paused under a question card.
        /// </remarks>
        private void StepPanels()
        {
            float target = PanelsWanted ? 1f : 0f;
            if (Mathf.Approximately(panelsIn, target) && sidePanel.gameObject.activeSelf == (target > 0f))
            {
                return;
            }

            panelsIn = Mathf.MoveTowards(panelsIn, target, Time.unscaledDeltaTime / PanelSlideSeconds);
            ApplyPanels(panelsIn);
        }

        private void ApplyPanels(float amount)
        {
            float t = 1f - amount;
            float eased = 1f - (t * t * t);
            bool visible = amount > 0f;
            bool settled = amount >= 1f;

            SetPanel(sidePanel, sideGroup, visible, settled, eased,
                sideRest + new Vector2(-(SidePanelWidth + (2f * Theme.Space.Base)) * (1f - eased), 0f));
            SetPanel(logPanel, logGroup, visible, settled, eased,
                logRest + new Vector2(0f, -(LogHeight + (2f * Theme.Space.Base)) * (1f - eased)));
        }

        private static void SetPanel(RectTransform panel, CanvasGroup group, bool visible, bool settled, float alpha, Vector2 position)
        {
            // Switched off once gone, not only faded: an invisible panel would still catch clicks
            // meant for the board, and would count as covering the screen.
            if (panel.gameObject.activeSelf != visible)
            {
                panel.gameObject.SetActive(visible);
            }

            panel.anchoredPosition = position;
            group.alpha = alpha;
            group.blocksRaycasts = settled;
            group.interactable = settled;
        }
    }
}
