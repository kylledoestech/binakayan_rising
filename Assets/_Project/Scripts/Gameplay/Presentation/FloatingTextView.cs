using System.Collections.Generic;
using UnityEngine;

namespace BinakayanRising.Gameplay.Presentation
{
    /// <summary>
    /// A single damage, heal or miss number that rises from a unit and fades out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instances are owned by a <see cref="FloatingTextPool"/> and recycled. A busy turn in a
    /// fourteen-unit battle can pop a dozen of these, and instantiating a GameObject per hit is the
    /// classic way to make a 2D battle stutter on the low-end laptops this project targets.
    /// </para>
    /// <para>
    /// The label is a <see cref="TextMesh"/> rather than a Canvas element: it lives in world space
    /// above the unit and needs to sort against the isometric sprites around it, which a world-space
    /// Canvas makes needlessly expensive.
    /// </para>
    /// <para>
    /// TODO(design): not specified in capstone document. Rise distance, lifetime, easing and colours
    /// are all presentation choices; every one is a serialized field.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FloatingTextView : MonoBehaviour
    {
        [Header("Label")]
        [Tooltip("World-space text mesh that renders the number.")]
        [SerializeField] private TextMesh label;

        [Tooltip("Renderer of the text mesh, used to set the sorting order. Usually on the same object as the label.")]
        [SerializeField] private MeshRenderer labelRenderer;

        [Header("Motion")]
        [Tooltip("Seconds the number stays on screen before it is recycled.")]
        [Min(0.05f)]
        [SerializeField] private float lifetime = 0.85f;

        [Tooltip("World units the number rises over its lifetime.")]
        [SerializeField] private float riseDistance = 0.55f;

        [Tooltip("World units the number drifts sideways, to separate two hits in the same frame.")]
        [SerializeField] private float horizontalDrift = 0.12f;

        [Tooltip("Vertical position over the lifetime, sampled 0..1. Ease out reads best.")]
        [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Alpha over the lifetime, sampled 0..1.")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Tooltip("Scale punch at the moment the number appears, sampled 0..1.")]
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        private FloatingTextPool owner;
        private Vector3 origin;
        private Vector3 drift;
        private Vector3 baseScale = Vector3.one;
        private float elapsed;
        private bool playing;
        private Color tint = Color.white;

        /// <summary>True while the number is on screen and has not been recycled.</summary>
        public bool IsPlaying
        {
            get { return playing; }
        }

        private void Awake()
        {
            baseScale = transform.localScale;

            if (label != null && labelRenderer == null)
            {
                labelRenderer = label.GetComponent<MeshRenderer>();
            }
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lifetime);

            transform.position = origin + (drift * t) + (Vector3.up * (riseCurve.Evaluate(t) * riseDistance));
            transform.localScale = baseScale * scaleCurve.Evaluate(t);

            if (label != null)
            {
                Color c = tint;
                c.a = tint.a * fadeCurve.Evaluate(t);
                label.color = c;
            }

            if (elapsed >= lifetime)
            {
                Recycle();
            }
        }

        /// <summary>
        /// Starts the rise-and-fade. Called by <see cref="FloatingTextPool"/>; call the pool rather
        /// than this method directly.
        /// </summary>
        /// <param name="pool">Pool to return to when the animation ends. May be null for a one-off.</param>
        /// <param name="text">Text to show, e.g. a damage number, "MISS" or "DODGE".</param>
        /// <param name="color">Tint applied to the label.</param>
        /// <param name="worldPosition">World-space point the number rises from.</param>
        /// <param name="sortingOrder">Sorting order within the label renderer's sorting layer.</param>
        /// <param name="scaleMultiplier">Extra scale, used to make critical hits read bigger.</param>
        public void Play(
            FloatingTextPool pool,
            string text,
            Color color,
            Vector3 worldPosition,
            int sortingOrder,
            float scaleMultiplier = 1f)
        {
            owner = pool;
            origin = worldPosition;
            tint = color;
            elapsed = 0f;
            playing = true;

            float side = Random.value < 0.5f ? -1f : 1f;
            drift = new Vector3(horizontalDrift * side, 0f, 0f);

            transform.position = worldPosition;
            transform.localScale = baseScale * scaleMultiplier;

            if (label != null)
            {
                label.text = text ?? string.Empty;
                label.color = color;
            }

            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = sortingOrder;
            }

            gameObject.SetActive(true);
        }

        /// <summary>Stops immediately and returns to the pool.</summary>
        public void Recycle()
        {
            playing = false;
            gameObject.SetActive(false);
            transform.localScale = baseScale;

            if (owner != null)
            {
                FloatingTextPool pool = owner;
                owner = null;
                pool.Release(this);
            }
        }
    }

    /// <summary>
    /// A recycling pool of <see cref="FloatingTextView"/> instances.
    /// </summary>
    /// <remarks>
    /// Attach this to a single GameObject in the battle scene, assign a prefab, and hand it to
    /// <see cref="BattleReplayer"/>. The pool grows on demand up to <see cref="MaxInstances"/>, past
    /// which the oldest live number is recycled early rather than allocating without bound.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FloatingTextPool : MonoBehaviour
    {
        [Header("Pool")]
        [Tooltip("Prefab holding a FloatingTextView. Must be assigned for any number to appear.")]
        [SerializeField] private FloatingTextView prefab;

        [Tooltip("Instances created up front, before the first hit lands.")]
        [Min(0)]
        [SerializeField] private int prewarmCount = 12;

        [Tooltip("Hard ceiling on live instances. Past this the oldest number is recycled early.")]
        [Min(1)]
        [SerializeField] private int maxInstances = 48;

        [Tooltip("Parent for spawned instances. Defaults to this object.")]
        [SerializeField] private Transform spawnRoot;

        private readonly Stack<FloatingTextView> idle = new Stack<FloatingTextView>();
        private readonly List<FloatingTextView> live = new List<FloatingTextView>();
        private int createdCount;

        /// <summary>Hard ceiling on simultaneously live numbers.</summary>
        public int MaxInstances
        {
            get { return maxInstances; }
        }

        /// <summary>How many instances exist, live or idle.</summary>
        public int CreatedCount
        {
            get { return createdCount; }
        }

        private void Awake()
        {
            if (spawnRoot == null)
            {
                spawnRoot = transform;
            }

            for (int i = 0; i < prewarmCount; i++)
            {
                FloatingTextView instance = CreateInstance();

                if (instance == null)
                {
                    break;
                }

                instance.gameObject.SetActive(false);
                idle.Push(instance);
            }
        }

        /// <summary>
        /// Shows a number, taking an idle instance or creating one.
        /// </summary>
        /// <param name="text">Text to show.</param>
        /// <param name="color">Tint applied to the label.</param>
        /// <param name="worldPosition">World-space point the number rises from.</param>
        /// <param name="sortingOrder">Sorting order within the label renderer's sorting layer.</param>
        /// <param name="scaleMultiplier">Extra scale, used to make critical hits read bigger.</param>
        /// <returns>The instance, or null when no prefab is assigned.</returns>
        public FloatingTextView Spawn(
            string text,
            Color color,
            Vector3 worldPosition,
            int sortingOrder,
            float scaleMultiplier = 1f)
        {
            FloatingTextView instance = Take();

            if (instance == null)
            {
                return null;
            }

            live.Add(instance);
            instance.Play(this, text, color, worldPosition, sortingOrder, scaleMultiplier);
            return instance;
        }

        /// <summary>
        /// Returns an instance to the idle set. Called by <see cref="FloatingTextView.Recycle"/>.
        /// </summary>
        /// <param name="instance">Instance to recycle.</param>
        public void Release(FloatingTextView instance)
        {
            if (instance == null)
            {
                return;
            }

            live.Remove(instance);

            if (!idle.Contains(instance))
            {
                idle.Push(instance);
            }
        }

        /// <summary>Recycles every live number at once, e.g. when a replay is skipped to the end.</summary>
        public void RecycleAll()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                live[i].Recycle();
            }

            live.Clear();
        }

        private FloatingTextView Take()
        {
            if (idle.Count > 0)
            {
                return idle.Pop();
            }

            if (createdCount < maxInstances)
            {
                return CreateInstance();
            }

            if (live.Count > 0)
            {
                FloatingTextView oldest = live[0];
                oldest.Recycle();

                if (idle.Count > 0)
                {
                    return idle.Pop();
                }
            }

            return null;
        }

        private FloatingTextView CreateInstance()
        {
            if (prefab == null)
            {
                Debug.LogWarning(
                    "FloatingTextPool on '" + name + "' has no prefab assigned, so no damage "
                        + "numbers will appear. Assign one in the Inspector.");
                return null;
            }

            FloatingTextView instance = Instantiate(prefab, spawnRoot);
            instance.name = prefab.name + "_" + createdCount.ToString();
            createdCount++;
            return instance;
        }
    }
}
