using System.Collections;
using BinakayanRising.Core.Combat;
using BinakayanRising.Core.Grid;
using BinakayanRising.Gameplay.Adapters;
using UnityEngine;

namespace BinakayanRising.Gameplay.Presentation
{
    /// <summary>
    /// The on-screen presence of one unit: its sprite, its floating health bar, and the short
    /// animations the replayer drives — a step to an adjacent cell, a hit flash, a damage number,
    /// and a death fade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This class never talks to the simulation.</b> It is told what happened and plays it. All
    /// of its state comes from <see cref="Bind"/> and from the coroutines the
    /// <see cref="BattleReplayer"/> starts, which is what keeps the visuals a pure function of the
    /// event log — and therefore identical every time the same seed is replayed.
    /// </para>
    /// <para>
    /// <b>Isometric sorting.</b> The sprite's <c>sortingOrder</c> is derived from the cell's depth
    /// key <c>X + Y</c>, which <see cref="IsoGridLayout"/> documents as the diamond grid's depth
    /// axis: cells sharing a sum sit on the same screen row, and a higher sum is nearer the camera.
    /// The rule itself lives in <see cref="IsoVectorExtensions.ToSortingOrder"/> so that every
    /// renderer in the game agrees. While a unit is mid-step between two cells it takes the higher
    /// of the two keys, so it never briefly draws behind the tile it is walking in front of.
    /// </para>
    /// <para>
    /// <b>Deployment chrome.</b> The capstone document specifies that when combat starts the
    /// deployment interface fades out and only the floating health bars above the sprites remain.
    /// <see cref="SetDeploymentChromeVisible"/> is that switch: it hides the selection ring and the
    /// name plate and leaves the health bar alone.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnitView : MonoBehaviour
    {
        [Header("Renderers")]
        [Tooltip("Sprite renderer showing the unit. Required.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Floating health bar above the sprite. Survives the deployment-to-combat fade.")]
        [SerializeField] private HealthBarView healthBar;

        [Tooltip("Selection ring, deployment highlight and anything else that belongs to the deployment phase only.")]
        [SerializeField] private GameObject[] deploymentChrome = new GameObject[0];

        [Tooltip("Name plate shown under the sprite. Optional; part of the deployment chrome.")]
        [SerializeField] private TextMesh namePlate;

        [Header("Placement")]
        [Tooltip("World offset applied on top of the cell centre, to lift the sprite's feet onto the tile.")]
        [SerializeField] private Vector3 cellOffset = Vector3.zero;

        [Tooltip("Sorting orders reserved per isometric row. Must exceed the number of renderers stacked in one cell.")]
        [Min(1)]
        [SerializeField] private int sortingStep = 16;

        [Tooltip("Sorting offset of the sprite inside its cell's window.")]
        [SerializeField] private int spriteSortingOffset = 0;

        [Tooltip("Sorting offset of the health bar inside its cell's window. Must exceed the sprite's.")]
        [SerializeField] private int healthBarSortingOffset = 4;

        [Tooltip("Sorting offset of floating numbers inside the cell's window. Must exceed the health bar's.")]
        [SerializeField] private int floatingTextSortingOffset = 8;

        [Header("Motion")]
        [Tooltip("Position over a single step, sampled 0..1. Ease in-out reads as a stride.")]
        [SerializeField] private AnimationCurve stepCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Extra height at the middle of a step, in world units. Zero for a flat slide.")]
        [SerializeField] private float stepArcHeight = 0.08f;

        [Tooltip("Flip the sprite horizontally to face the direction of travel or the current target.")]
        [SerializeField] private bool flipToFaceTarget = true;

        [Header("Attack")]
        [Tooltip("World units the sprite lunges toward its target when it attacks.")]
        [SerializeField] private float attackLungeDistance = 0.18f;

        [Tooltip("Seconds the attack lunge takes, out and back.")]
        [Min(0.01f)]
        [SerializeField] private float attackLungeDuration = 0.18f;

        [Header("Hit Flash")]
        [Tooltip("Colour the sprite is tinted to on a hit.")]
        [SerializeField] private Color hitFlashColor = Color.white;

        [Tooltip("Seconds the hit flash lasts.")]
        [Min(0.01f)]
        [SerializeField] private float hitFlashDuration = 0.12f;

        [Tooltip("World units the sprite recoils away from the attacker on a hit.")]
        [SerializeField] private float hitRecoilDistance = 0.06f;

        [Header("Death")]
        [Tooltip("Seconds the death fade takes.")]
        [Min(0.01f)]
        [SerializeField] private float deathFadeDuration = 0.45f;

        [Tooltip("World units the sprite sinks as it fades out.")]
        [SerializeField] private float deathSinkDistance = 0.15f;

        [Tooltip("Deactivate the GameObject once the death fade finishes.")]
        [SerializeField] private bool disableOnDeath = false;

        [Header("Floating Numbers")]
        [Tooltip("Colour of a normal damage number.")]
        [SerializeField] private Color damageColor = new Color(0.93f, 0.34f, 0.30f, 1f);

        [Tooltip("Colour of a critical damage number.")]
        [SerializeField] private Color criticalColor = new Color(1f, 0.82f, 0.25f, 1f);

        [Tooltip("Colour of a healing number.")]
        [SerializeField] private Color healColor = new Color(0.45f, 0.85f, 0.45f, 1f);

        [Tooltip("Colour of a MISS or DODGE label.")]
        [SerializeField] private Color missColor = new Color(0.75f, 0.75f, 0.78f, 1f);

        [Tooltip("Extra scale applied to a critical hit's number.")]
        [Min(1f)]
        [SerializeField] private float criticalTextScale = 1.35f;

        [Tooltip("World offset from the unit's position that numbers rise from.")]
        [SerializeField] private Vector3 floatingTextOffset = new Vector3(0f, 0.6f, 0f);

        private IBattleWorldSpace world;
        private FloatingTextPool textPool;
        private GridCoord cell;
        private int unitId = -1;
        private Team team = Team.Katipunan;
        private string displayName = string.Empty;
        private float currentHP;
        private float maxHP = 1f;
        private bool isAlive = true;
        private Color baseColor = Color.white;
        private Coroutine flashRoutine;

        /// <summary>Runtime unit id, as it appears in the event log. Negative until bound.</summary>
        public int UnitId
        {
            get { return unitId; }
        }

        /// <summary>Side the unit fights for.</summary>
        public Team Team
        {
            get { return team; }
        }

        /// <summary>Name shown on the name plate and in tooltips.</summary>
        public string DisplayName
        {
            get { return displayName; }
        }

        /// <summary>Cell the unit currently occupies.</summary>
        public GridCoord Cell
        {
            get { return cell; }
        }

        /// <summary>Health the view is currently showing, in HP.</summary>
        public float CurrentHP
        {
            get { return currentHP; }
        }

        /// <summary>Maximum health the view scales its bar against, in HP.</summary>
        public float MaxHP
        {
            get { return maxHP; }
        }

        /// <summary>False once the death fade has been played.</summary>
        public bool IsAlive
        {
            get { return isAlive; }
        }

        /// <summary>World position the unit's sprite currently sits at.</summary>
        public Vector3 WorldPosition
        {
            get { return transform.position; }
        }

        /// <summary>
        /// Configures the view for one unit and snaps it to its deployment cell.
        /// </summary>
        /// <param name="entry">Identity, sprite, starting cell and Max HP, snapshotted before the battle ran.</param>
        /// <param name="worldSpace">Cell-to-world projection. Must not be null.</param>
        /// <param name="pool">Pool that supplies floating numbers. May be null, in which case none appear.</param>
        public void Bind(UnitReplayEntry entry, IBattleWorldSpace worldSpace, FloatingTextPool pool)
        {
            world = worldSpace;
            textPool = pool;
            unitId = entry.UnitId;
            team = entry.Team;
            displayName = entry.DisplayName;
            maxHP = entry.MaxHP > 0f ? entry.MaxHP : 1f;
            currentHP = maxHP;
            isAlive = true;

            if (spriteRenderer != null)
            {
                if (entry.Sprite != null)
                {
                    spriteRenderer.sprite = entry.Sprite;
                }

                baseColor = spriteRenderer.color;
                baseColor.a = 1f;
                spriteRenderer.color = baseColor;
                spriteRenderer.enabled = true;
            }

            if (namePlate != null)
            {
                namePlate.text = displayName;
            }

            name = "Unit_" + entry.UnitId.ToString() + "_" + displayName;

            SnapToCell(entry.StartCell);

            if (healthBar != null)
            {
                healthBar.SetImmediate(currentHP, maxHP);
            }
        }

        /// <summary>Moves the sprite to a cell instantly and re-sorts it.</summary>
        /// <param name="destination">Cell to occupy.</param>
        public void SnapToCell(GridCoord destination)
        {
            cell = destination;
            transform.position = ResolveWorldPosition(destination);
            ApplySortingOrder(destination.DepthKey());
        }

        /// <summary>
        /// Slides the sprite from its current cell to an adjacent one over a fixed duration.
        /// </summary>
        /// <param name="destination">Cell to move to.</param>
        /// <param name="duration">Seconds the step takes. Values at or below zero snap.</param>
        /// <returns>A coroutine the replayer yields on.</returns>
        public IEnumerator MoveToCell(GridCoord destination, float duration)
        {
            GridCoord from = cell;
            Vector3 start = transform.position;
            Vector3 end = ResolveWorldPosition(destination);

            FaceTowards(end);

            // Take the nearer of the two depth keys for the whole step, so the sprite never
            // flickers behind scenery it is already walking in front of.
            int depth = Mathf.Max(from.DepthKey(), destination.DepthKey());
            ApplySortingOrder(depth);

            if (duration <= 0f)
            {
                cell = destination;
                transform.position = end;
                ApplySortingOrder(destination.DepthKey());
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = stepCurve.Evaluate(t);

                Vector3 position = Vector3.LerpUnclamped(start, end, eased);
                position.y += Mathf.Sin(t * Mathf.PI) * stepArcHeight;
                transform.position = position;

                yield return null;
            }

            cell = destination;
            transform.position = end;
            ApplySortingOrder(destination.DepthKey());
        }

        /// <summary>
        /// Plays the short lunge that reads as "this unit swung at that one".
        /// </summary>
        /// <param name="targetWorldPosition">Where the target stands, used for direction and facing.</param>
        /// <returns>A coroutine the replayer yields on.</returns>
        public IEnumerator PlayAttack(Vector3 targetWorldPosition)
        {
            FaceTowards(targetWorldPosition);

            Vector3 home = ResolveWorldPosition(cell);
            Vector3 direction = targetWorldPosition - home;

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
            }
            else
            {
                direction = Vector3.zero;
            }

            Vector3 lunged = home + (direction * attackLungeDistance);
            float half = attackLungeDuration * 0.5f;

            yield return MoveWorld(home, lunged, half);
            yield return MoveWorld(lunged, home, half);

            transform.position = home;
        }

        /// <summary>
        /// Tints the sprite for a moment and nudges it away from the attacker.
        /// </summary>
        /// <param name="fromWorldPosition">Where the blow came from. Pass the unit's own position for no recoil.</param>
        public void PlayHitFlash(Vector3 fromWorldPosition)
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
                RestoreBaseColor();
            }

            if (isActiveAndEnabled)
            {
                flashRoutine = StartCoroutine(HitFlashRoutine(fromWorldPosition));
            }
        }

        /// <summary>
        /// Updates the health bar. Does not itself flash or pop a number; the replayer decides which
        /// of those an event deserves.
        /// </summary>
        /// <param name="current">Current HP.</param>
        /// <param name="max">Maximum HP. Pass a value at or below zero to keep the bound maximum.</param>
        public void SetHealth(float current, float max = -1f)
        {
            if (max > 0f)
            {
                maxHP = max;
            }

            currentHP = Mathf.Clamp(current, 0f, maxHP);

            if (healthBar != null)
            {
                healthBar.SetHealth(currentHP, maxHP);
            }
        }

        /// <summary>Pops a damage number above the unit.</summary>
        /// <param name="amount">Health removed. Rendered rounded to the nearest whole number.</param>
        /// <param name="wasCrit">Whether the hit was critical, which changes colour and size.</param>
        public void ShowDamage(float amount, bool wasCrit)
        {
            string text = "-" + Mathf.Max(1, Mathf.RoundToInt(amount)).ToString();
            ShowFloatingText(
                wasCrit ? text + "!" : text,
                wasCrit ? criticalColor : damageColor,
                wasCrit ? criticalTextScale : 1f);
        }

        /// <summary>Pops a healing number above the unit.</summary>
        /// <param name="amount">Health restored. Rendered rounded to the nearest whole number.</param>
        public void ShowHeal(float amount)
        {
            ShowFloatingText("+" + Mathf.Max(1, Mathf.RoundToInt(amount)).ToString(), healColor, 1f);
        }

        /// <summary>Pops a "MISS" or "DODGE" label above the unit.</summary>
        /// <param name="text">Label to show.</param>
        public void ShowMiss(string text)
        {
            ShowFloatingText(text, missColor, 1f);
        }

        /// <summary>Pops arbitrary text above the unit, e.g. a modifier name.</summary>
        /// <param name="text">Text to show.</param>
        /// <param name="color">Tint applied to the label.</param>
        /// <param name="scaleMultiplier">Extra scale.</param>
        public void ShowFloatingText(string text, Color color, float scaleMultiplier = 1f)
        {
            if (textPool == null)
            {
                return;
            }

            textPool.Spawn(
                text,
                color,
                transform.position + floatingTextOffset,
                cell.ToSortingOrder(sortingStep, floatingTextSortingOffset),
                scaleMultiplier);
        }

        /// <summary>
        /// Fades the sprite out and sinks it, then hides the health bar.
        /// </summary>
        /// <returns>A coroutine the replayer yields on.</returns>
        public IEnumerator PlayDeathFade()
        {
            isAlive = false;
            currentHP = 0f;

            if (healthBar != null)
            {
                healthBar.SetHealth(0f, maxHP);
            }

            SetDeploymentChromeVisible(false);

            Vector3 start = transform.position;
            Vector3 end = start + (Vector3.down * deathSinkDistance);
            float elapsed = 0f;

            while (elapsed < deathFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / deathFadeDuration);

                transform.position = Vector3.Lerp(start, end, t);

                if (spriteRenderer != null)
                {
                    Color c = baseColor;
                    c.a = Mathf.Lerp(baseColor.a, 0f, t);
                    spriteRenderer.color = c;
                }

                yield return null;
            }

            if (spriteRenderer != null)
            {
                Color c = baseColor;
                c.a = 0f;
                spriteRenderer.color = c;
            }

            if (healthBar != null)
            {
                healthBar.SetVisible(false);
            }

            if (disableOnDeath)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Shows or hides everything that belongs to the deployment phase, leaving the health bar
        /// alone.
        /// </summary>
        /// <param name="visible">Whether the deployment chrome may draw.</param>
        /// <remarks>
        /// The capstone document specifies that when combat begins the deployment interface fades
        /// out and only the floating health bars above the sprites remain. The replayer calls this
        /// with <c>false</c> on the first turn.
        /// </remarks>
        public void SetDeploymentChromeVisible(bool visible)
        {
            for (int i = 0; i < deploymentChrome.Length; i++)
            {
                if (deploymentChrome[i] != null)
                {
                    deploymentChrome[i].SetActive(visible);
                }
            }

            if (namePlate != null)
            {
                namePlate.gameObject.SetActive(visible);
            }
        }

        /// <summary>Shows or hides the floating health bar.</summary>
        /// <param name="visible">Whether the bar may draw.</param>
        public void SetHealthBarVisible(bool visible)
        {
            if (healthBar != null)
            {
                healthBar.SetVisible(visible);
            }
        }

        /// <summary>Flips the sprite to face a world position.</summary>
        /// <param name="targetWorldPosition">Point to face.</param>
        public void FaceTowards(Vector3 targetWorldPosition)
        {
            if (!flipToFaceTarget || spriteRenderer == null)
            {
                return;
            }

            float dx = targetWorldPosition.x - transform.position.x;

            if (Mathf.Abs(dx) > 0.0001f)
            {
                spriteRenderer.flipX = dx < 0f;
            }
        }

        private IEnumerator HitFlashRoutine(Vector3 fromWorldPosition)
        {
            Vector3 home = ResolveWorldPosition(cell);
            Vector3 away = home - fromWorldPosition;

            if (away.sqrMagnitude > 0.0001f)
            {
                away.Normalize();
            }
            else
            {
                away = Vector3.zero;
            }

            Vector3 recoiled = home + (away * hitRecoilDistance);
            float elapsed = 0f;

            while (elapsed < hitFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hitFlashDuration);

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.Lerp(hitFlashColor, baseColor, t);
                }

                transform.position = Vector3.Lerp(recoiled, home, t);
                yield return null;
            }

            RestoreBaseColor();
            transform.position = home;
            flashRoutine = null;
        }

        private IEnumerator MoveWorld(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                transform.position = to;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            transform.position = to;
        }

        private void RestoreBaseColor()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }
        }

        private Vector3 ResolveWorldPosition(GridCoord target)
        {
            Vector3 basePosition = world != null
                ? world.CellToWorld(target)
                : IsoGridLayout.Default.CellToWorldPosition(target);

            return basePosition + cellOffset;
        }

        private void ApplySortingOrder(int depthKey)
        {
            int cellOrder = depthKey * sortingStep;

            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = cellOrder + spriteSortingOffset;
            }

            if (healthBar != null)
            {
                healthBar.SetSortingOrder(cellOrder + healthBarSortingOffset);
            }
        }
    }
}
