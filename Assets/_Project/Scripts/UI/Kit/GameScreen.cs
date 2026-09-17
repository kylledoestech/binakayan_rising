using BinakayanRising.Gameplay.Flow;
using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// Base class for every full-screen view. Owns a root <see cref="RectTransform"/>, builds its
    /// content once, and fades itself in and out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named <c>GameScreen</c> rather than <c>Screen</c> because <see cref="UnityEngine.Screen"/>
    /// already occupies that name in every file that uses <c>UnityEngine</c>, and shadowing it
    /// would make <c>Screen.width</c> resolve to the wrong thing in confusing ways.
    /// </para>
    /// <para>
    /// Content is built lazily on the first <see cref="Show"/> rather than in <c>Awake</c>. Eleven
    /// screens exist and a session touches a handful of them, so building the encampment's roster
    /// grid before anyone has left the main menu is work spent on nothing.
    /// </para>
    /// </remarks>
    public abstract class GameScreen : MonoBehaviour
    {
        private CanvasGroup group;
        private Coroutine transition;
        private bool built;

        /// <summary>The state this screen is shown for.</summary>
        public abstract GameState State { get; }

        /// <summary>The state machine driving the session. Assigned by the router before first use.</summary>
        public GameStateMachine Machine { get; set; }

        /// <summary>This screen's root. Content should be parented here.</summary>
        public RectTransform Root => (RectTransform)transform;

        /// <summary>True while the screen is the visible one.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>
        /// Builds the screen's content. Called once, immediately before the first
        /// <see cref="Show"/>.
        /// </summary>
        protected abstract void Build();

        /// <summary>Refreshes displayed values. Called on every <see cref="Show"/>.</summary>
        protected virtual void Refresh()
        {
        }

        /// <summary>Called after the screen has finished fading out.</summary>
        protected virtual void OnHidden()
        {
        }

        /// <summary>Fades the screen in, building it first if this is its first appearance.</summary>
        public void Show()
        {
            if (IsVisible)
            {
                Refresh();
                return;
            }

            EnsureBuilt();
            IsVisible = true;
            gameObject.SetActive(true);
            Refresh();

            StopTransition();
            transition = CoroutineHost.Run(
                UiTween.Enter(Root, Group, new Vector2(0f, -24f), UiTween.PanelDuration));
            UiSfx.Play(UiSfx.Cue.Open);
        }

        /// <summary>Fades the screen out and deactivates it.</summary>
        public void Hide(bool immediate = false)
        {
            if (!IsVisible)
            {
                return;
            }

            IsVisible = false;
            StopTransition();

            if (immediate)
            {
                Group.alpha = 0f;
                Group.interactable = false;
                Group.blocksRaycasts = false;
                gameObject.SetActive(false);
                OnHidden();
                return;
            }

            transition = CoroutineHost.Run(HideRoutine());
        }

        private System.Collections.IEnumerator HideRoutine()
        {
            yield return UiTween.Fade(Group, 0f, UiTween.PanelDuration * 0.75f);
            gameObject.SetActive(false);
            OnHidden();
        }

        /// <summary>The screen's <see cref="CanvasGroup"/>, created on first access.</summary>
        protected CanvasGroup Group
        {
            get
            {
                if (group == null)
                {
                    group = UiKit.Group(gameObject);
                }

                return group;
            }
        }

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;
            UiKit.Stretch(Root);
            Build();
        }

        private void StopTransition()
        {
            if (transition != null)
            {
                CoroutineHost.Stop(transition);
                transition = null;
            }
        }

        /// <summary>
        /// Attempts a state transition, reporting refusal as an error sound rather than silently.
        /// </summary>
        /// <remarks>
        /// <see cref="GameStateMachine"/> refuses transitions that are not edges in the capstone's
        /// Figure 2 diagram. A button wired to an illegal edge would otherwise do nothing at all,
        /// which is indistinguishable from a broken button.
        /// </remarks>
        protected void GoTo(GameState next)
        {
            if (Machine == null)
            {
                Debug.LogError($"{GetType().Name}: no state machine assigned; cannot leave {State}.");
                return;
            }

            if (Machine.TryTransitionTo(next))
            {
                UiSfx.Play(UiSfx.Cue.Confirm);
                return;
            }

            Machine.CanTransitionTo(next, out string reason);
            Debug.LogWarning($"{GetType().Name}: refused transition {State} -> {next}. {reason}");
            UiSfx.Play(UiSfx.Cue.Error);
        }
    }
}
