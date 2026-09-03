using UnityEngine;

namespace BinakayanRising.Gameplay.Presentation
{
    /// <summary>
    /// A world-space health bar drawn from two sprites: a fixed background and a fill that scales
    /// horizontally as the unit takes damage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capstone document states that once combat begins the deployment interface fades out and
    /// <b>only the floating health bars above the sprites remain</b>. That makes this the one piece
    /// of persistent UI on the battlefield, so it is built from <see cref="SpriteRenderer"/>s rather
    /// than a Canvas: it lives in world space, it sorts with the unit it belongs to, and it costs
    /// nothing to have thirty of them on screen.
    /// </para>
    /// <para>
    /// The bar tweens toward its target rather than snapping, so a burst of damage in one replayed
    /// event still reads as a drain. Use <see cref="SetImmediate"/> when the bar must be correct on
    /// the very first frame, e.g. at deployment.
    /// </para>
    /// <para>
    /// TODO(design): not specified in capstone document. Bar size, colours, drain rate and the
    /// thresholds the colour shifts at are all presentation choices the document does not make.
    /// Every one of them is a serialized field.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HealthBarView : MonoBehaviour
    {
        [Header("Renderers")]
        [Tooltip("Sprite that scales horizontally to show remaining health. Pivot must be on its LEFT edge.")]
        [SerializeField] private SpriteRenderer fillRenderer;

        [Tooltip("Static sprite drawn behind the fill. Optional.")]
        [SerializeField] private SpriteRenderer backgroundRenderer;

        [Header("Drain")]
        [Tooltip("Fractions of the bar drained per second while catching up to the target. Higher is snappier.")]
        [Min(0.01f)]
        [SerializeField] private float drainSpeed = 1.5f;

        [Tooltip("Snap to the target instead of tweening when the difference is smaller than this fraction.")]
        [Min(0f)]
        [SerializeField] private float snapThreshold = 0.002f;

        [Header("Colour")]
        [Tooltip("Fill colour at full health.")]
        [SerializeField] private Color healthyColor = new Color(0.36f, 0.72f, 0.36f, 1f);

        [Tooltip("Fill colour at the wounded threshold.")]
        [SerializeField] private Color woundedColor = new Color(0.90f, 0.76f, 0.28f, 1f);

        [Tooltip("Fill colour at zero health.")]
        [SerializeField] private Color criticalColor = new Color(0.80f, 0.24f, 0.22f, 1f);

        [Tooltip("Fraction of health below which the bar reads as wounded. TODO(design): unspecified.")]
        [Range(0f, 1f)]
        [SerializeField] private float woundedThreshold = 0.55f;

        [Tooltip("Fraction of health below which the bar reads as critical. TODO(design): unspecified.")]
        [Range(0f, 1f)]
        [SerializeField] private float criticalThreshold = 0.25f;

        [Header("Visibility")]
        [Tooltip("Hide the bar while the unit is at full health, so an untouched line of units stays clean.")]
        [SerializeField] private bool hideWhenFull = false;

        [Tooltip("Hide the bar once the unit is dead.")]
        [SerializeField] private bool hideWhenEmpty = true;

        private float displayedFraction = 1f;
        private float targetFraction = 1f;
        private float currentHP;
        private float maxHP = 1f;
        private bool forcedHidden;
        private Vector3 fillBaseScale = Vector3.one;
        private bool cachedFillScale;

        /// <summary>Health the bar is currently showing, in HP.</summary>
        public float CurrentHP
        {
            get { return currentHP; }
        }

        /// <summary>Maximum health the bar scales against, in HP.</summary>
        public float MaxHP
        {
            get { return maxHP; }
        }

        /// <summary>Fraction of health the bar is animating toward, 0..1.</summary>
        public float TargetFraction
        {
            get { return targetFraction; }
        }

        /// <summary>True once the drain tween has caught up with the target.</summary>
        public bool IsSettled
        {
            get { return Mathf.Abs(displayedFraction - targetFraction) <= snapThreshold; }
        }

        private void Awake()
        {
            CacheFillScale();
        }

        private void LateUpdate()
        {
            if (Mathf.Abs(displayedFraction - targetFraction) <= snapThreshold)
            {
                displayedFraction = targetFraction;
            }
            else
            {
                displayedFraction = Mathf.MoveTowards(
                    displayedFraction, targetFraction, drainSpeed * Time.deltaTime);
            }

            ApplyFraction(displayedFraction);
        }

        /// <summary>
        /// Sets the health the bar animates toward.
        /// </summary>
        /// <param name="current">Current HP. Clamped to <c>0 .. max</c>.</param>
        /// <param name="max">Maximum HP. Values at or below zero are treated as 1 to avoid a divide by zero.</param>
        public void SetHealth(float current, float max)
        {
            maxHP = max > 0f ? max : 1f;
            currentHP = Mathf.Clamp(current, 0f, maxHP);
            targetFraction = currentHP / maxHP;
            UpdateVisibility();
        }

        /// <summary>
        /// Sets the health and skips the drain tween, so the bar is correct on this frame.
        /// </summary>
        /// <param name="current">Current HP.</param>
        /// <param name="max">Maximum HP.</param>
        public void SetImmediate(float current, float max)
        {
            SetHealth(current, max);
            displayedFraction = targetFraction;
            ApplyFraction(displayedFraction);
        }

        /// <summary>
        /// Shows or hides the whole bar regardless of health, for the deployment-to-combat
        /// transition.
        /// </summary>
        /// <param name="visible">Whether the bar may draw.</param>
        public void SetVisible(bool visible)
        {
            forcedHidden = !visible;
            UpdateVisibility();
        }

        /// <summary>
        /// Puts the bar's renderers on a sorting order, so it draws over its own unit but under
        /// units nearer the camera.
        /// </summary>
        /// <param name="order">Sorting order within the renderers' sorting layer.</param>
        public void SetSortingOrder(int order)
        {
            if (backgroundRenderer != null)
            {
                backgroundRenderer.sortingOrder = order;
            }

            if (fillRenderer != null)
            {
                fillRenderer.sortingOrder = order + 1;
            }
        }

        /// <summary>The fill colour for a given health fraction.</summary>
        /// <param name="fraction">Health fraction, 0..1.</param>
        public Color EvaluateColor(float fraction)
        {
            if (fraction <= criticalThreshold)
            {
                float t = criticalThreshold <= 0f ? 1f : Mathf.Clamp01(fraction / criticalThreshold);
                return Color.Lerp(criticalColor, woundedColor, t);
            }

            if (fraction <= woundedThreshold)
            {
                float span = woundedThreshold - criticalThreshold;
                float t = span <= 0f ? 1f : Mathf.Clamp01((fraction - criticalThreshold) / span);
                return Color.Lerp(woundedColor, healthyColor, t);
            }

            return healthyColor;
        }

        private void ApplyFraction(float fraction)
        {
            if (fillRenderer == null)
            {
                return;
            }

            CacheFillScale();

            Vector3 scale = fillBaseScale;
            scale.x = fillBaseScale.x * Mathf.Clamp01(fraction);
            fillRenderer.transform.localScale = scale;
            fillRenderer.color = EvaluateColor(fraction);
        }

        private void CacheFillScale()
        {
            if (cachedFillScale || fillRenderer == null)
            {
                return;
            }

            fillBaseScale = fillRenderer.transform.localScale;
            cachedFillScale = true;
        }

        private void UpdateVisibility()
        {
            bool visible = !forcedHidden;

            if (visible && hideWhenFull && targetFraction >= 1f)
            {
                visible = false;
            }

            if (visible && hideWhenEmpty && currentHP <= 0f)
            {
                visible = false;
            }

            if (fillRenderer != null)
            {
                fillRenderer.enabled = visible;
            }

            if (backgroundRenderer != null)
            {
                backgroundRenderer.enabled = visible;
            }
        }
    }
}
