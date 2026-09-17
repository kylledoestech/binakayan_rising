using System;
using System.Collections;
using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// The small set of eased animations the UI needs, as coroutines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately not DOTween. The whole requirement is four curves over a
    /// <see cref="CanvasGroup"/> alpha and a <see cref="RectTransform"/> offset; adding a
    /// third-party animation package to a capstone build for that would be a dependency to
    /// justify at the defense for no gain.
    /// </para>
    /// <para>
    /// Everything here runs on unscaled time. Panels must still animate while the battlefield is
    /// frozen — <c>QuizController</c> zeroes <see cref="Time.timeScale"/> while a quiz is open, and
    /// a quiz panel that cannot slide in because of it would be a strange bug to chase.
    /// </para>
    /// </remarks>
    public static class UiTween
    {
        /// <summary>Duration of a panel appearing or dismissing.</summary>
        public const float PanelDuration = 0.22f;

        /// <summary>Delay between successive children in a staggered reveal.</summary>
        public const float StaggerStep = 0.04f;

        /// <summary>Fades a group's alpha, and toggles its interactivity to match.</summary>
        public static IEnumerator Fade(CanvasGroup group, float to, float duration)
        {
            if (group == null)
            {
                yield break;
            }

            float from = group.alpha;
            float elapsed = 0f;

            // A group fading out must stop accepting clicks immediately, not when the fade ends,
            // or a half-transparent screen keeps eating input for a fifth of a second.
            if (to < from)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            group.alpha = to;

            if (to > 0.99f)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }
        }

        /// <summary>Slides a rect from an offset back to its resting position while fading in.</summary>
        public static IEnumerator Enter(RectTransform rect, CanvasGroup group, Vector2 fromOffset, float duration)
        {
            if (rect == null)
            {
                yield break;
            }

            Vector2 target = rect.anchoredPosition;
            Vector2 start = target + fromOffset;
            float elapsed = 0f;

            rect.anchoredPosition = start;
            if (group != null)
            {
                group.alpha = 0f;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                rect.anchoredPosition = Vector2.LerpUnclamped(start, target, EaseOutBack(t));
                if (group != null)
                {
                    group.alpha = EaseOutCubic(t);
                }

                yield return null;
            }

            rect.anchoredPosition = target;
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
        }

        /// <summary>Reveals a container's children one after another.</summary>
        /// <remarks>
        /// The single highest-value flourish in the kit. A roster or a log that materialises row by
        /// row reads as deliberate; the same content appearing all at once reads as a data dump.
        /// </remarks>
        public static IEnumerator Stagger(Transform container, float step = StaggerStep)
        {
            if (container == null)
            {
                yield break;
            }

            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                CanvasGroup group = UiKit.Group(child.gameObject);
                group.alpha = 0f;
            }

            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                CanvasGroup group = child.GetComponent<CanvasGroup>();
                var rect = child as RectTransform;
                if (rect != null)
                {
                    CoroutineHost.Run(Enter(rect, group, new Vector2(0f, -12f), PanelDuration));
                }

                float waited = 0f;
                while (waited < step)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        /// <summary>Pulses a graphic's scale once, for drawing the eye to a change.</summary>
        public static IEnumerator Punch(Transform target, float strength = 0.12f, float duration = 0.22f)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 baseScale = target.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // One damped half-cycle: overshoot, then settle.
                float amount = Mathf.Sin(t * Mathf.PI) * strength * (1f - t);
                target.localScale = baseScale * (1f + amount);
                yield return null;
            }

            target.localScale = baseScale;
        }

        private static float EaseOutCubic(float t)
        {
            float inverted = 1f - t;
            return 1f - (inverted * inverted * inverted);
        }

        /// <summary>Overshoots slightly past the target before settling, which reads as weight.</summary>
        private static float EaseOutBack(float t)
        {
            const float Overshoot = 1.70158f;
            float inverted = t - 1f;
            return (inverted * inverted * (((Overshoot + 1f) * inverted) + Overshoot)) + 1f;
        }
    }

    /// <summary>
    /// Runs coroutines for callers that are not themselves MonoBehaviours.
    /// </summary>
    /// <remarks>
    /// <see cref="UiTween"/> is static so any code can start an animation, but Unity only runs
    /// coroutines on a live behaviour. This is the one hidden object that hosts them.
    /// </remarks>
    public sealed class CoroutineHost : MonoBehaviour
    {
        private static CoroutineHost instance;

        /// <summary>Starts a coroutine on the shared host, creating it on first use.</summary>
        public static Coroutine Run(IEnumerator routine)
        {
            if (routine == null)
            {
                return null;
            }

            if (instance == null)
            {
                var go = new GameObject("UI Coroutines")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CoroutineHost>();
            }

            return instance.StartCoroutine(routine);
        }

        /// <summary>Stops a coroutine previously started by <see cref="Run"/>.</summary>
        public static void Stop(Coroutine routine)
        {
            if (routine != null && instance != null)
            {
                instance.StopCoroutine(routine);
            }
        }

        /// <summary>Clears the cached host, which domain-reload-disabled play sessions require.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }
    }
}
