using BinakayanRising.Core.Grid;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BinakayanRising.Gameplay.Presentation
{
    /// <summary>
    /// Scroll-wheel zoom and pan for the orthographic camera that looks at the battlefield and the
    /// encampment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capstone document specifies scroll-wheel zoom on both the battlefield and the encampment,
    /// so one controller serves both: point it at whichever camera is live and give it the world
    /// bounds it may travel inside.
    /// </para>
    /// <para>
    /// Input goes through <c>UnityEngine.InputSystem</c> rather than the legacy <c>Input</c> class.
    /// Every device is read defensively — <see cref="Mouse.current"/> is null on a machine with no
    /// mouse, and on a touch-only build that is not an error — so a missing device disables that
    /// input path instead of throwing every frame.
    /// </para>
    /// <para>
    /// Zoom is applied around the cursor when <see cref="ZoomTowardCursor"/> is on, which is what
    /// makes a wheel zoom feel like it is examining a spot rather than the screen centre.
    /// </para>
    /// <para>
    /// TODO(design): not specified in capstone document. Zoom range, pan speed, edge-pan margin and
    /// the smoothing rates are all feel settings the document does not state; every one is a
    /// serialized field.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleCameraController : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Orthographic camera to drive. Defaults to the camera on this object, then Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Header("Zoom")]
        [Tooltip("Closest the camera may zoom in, as orthographic size in world units.")]
        [Min(0.01f)]
        [SerializeField] private float minOrthographicSize = 2.5f;

        [Tooltip("Furthest the camera may zoom out, as orthographic size in world units.")]
        [Min(0.02f)]
        [SerializeField] private float maxOrthographicSize = 12f;

        [Tooltip("Orthographic size change per notch of the scroll wheel.")]
        [Min(0f)]
        [SerializeField] private float zoomStep = 0.85f;

        [Tooltip("How fast the camera settles onto the requested zoom. Higher is snappier.")]
        [Min(0.01f)]
        [SerializeField] private float zoomSmoothing = 12f;

        [Tooltip("Zoom toward the point under the cursor rather than the screen centre.")]
        [SerializeField] private bool zoomTowardCursor = true;

        [Header("Drag Pan")]
        [Tooltip("Allow dragging the battlefield with a mouse button.")]
        [SerializeField] private bool enableDragPan = true;

        [Tooltip("Which mouse button drags. Middle keeps left free for deployment drag-and-drop.")]
        [SerializeField] private DragPanButton dragPanButton = DragPanButton.Middle;

        [Tooltip("Invert the drag direction, so the camera moves with the cursor instead of the world.")]
        [SerializeField] private bool invertDragPan = false;

        [Header("Edge Pan")]
        [Tooltip("Pan when the cursor rests near the edge of the screen.")]
        [SerializeField] private bool enableEdgePan = false;

        [Tooltip("Distance from a screen edge, in pixels, that starts an edge pan.")]
        [Min(1f)]
        [SerializeField] private float edgePanMargin = 24f;

        [Tooltip("World units per second an edge pan travels at an orthographic size of 1.")]
        [Min(0f)]
        [SerializeField] private float edgePanSpeed = 6f;

        [Header("Keyboard Pan")]
        [Tooltip("Pan with WASD and the arrow keys.")]
        [SerializeField] private bool enableKeyboardPan = true;

        [Tooltip("World units per second a keyboard pan travels at an orthographic size of 1.")]
        [Min(0f)]
        [SerializeField] private float keyboardPanSpeed = 8f;

        [Header("Bounds")]
        [Tooltip("Keep the camera inside a world-space rectangle.")]
        [SerializeField] private bool clampToBounds = true;

        [Tooltip("World-space rectangle the camera centre may travel inside. Set by FrameGrid at runtime.")]
        [SerializeField] private Rect panBounds = new Rect(-12f, -6f, 24f, 12f);

        [Tooltip("Extra world units of slack added around the grid by FrameGrid.")]
        [Min(0f)]
        [SerializeField] private float framePadding = 1.5f;

        [Header("Motion")]
        [Tooltip("How fast the camera settles onto the requested position. Higher is snappier.")]
        [Min(0.01f)]
        [SerializeField] private float panSmoothing = 14f;

        [Tooltip("Ignore all input. Set while a modal overlay such as the Quiz is up.")]
        [SerializeField] private bool inputLocked = false;

        private float targetSize;
        private Vector3 targetPosition;
        private bool dragging;
        private Vector3 dragWorldAnchor;

        /// <summary>Which mouse button drags the view.</summary>
        public enum DragPanButton
        {
            /// <summary>Left button. Conflicts with deployment drag-and-drop.</summary>
            Left = 0,

            /// <summary>Middle button. The default, because it never conflicts.</summary>
            Middle = 1,

            /// <summary>Right button.</summary>
            Right = 2
        }

        /// <summary>The camera being driven.</summary>
        public Camera TargetCamera
        {
            get { return targetCamera; }
        }

        /// <summary>Orthographic size the camera is settling toward.</summary>
        public float TargetOrthographicSize
        {
            get { return targetSize; }
        }

        /// <summary>Whether zoom follows the cursor rather than the screen centre.</summary>
        public bool ZoomTowardCursor
        {
            get { return zoomTowardCursor; }
        }

        /// <summary>Whether input is currently ignored.</summary>
        public bool InputLocked
        {
            get { return inputLocked; }
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                Debug.LogError(
                    "BattleCameraController on '" + name + "' found no camera to drive. Assign one "
                        + "in the Inspector.");
                enabled = false;
                return;
            }

            if (!targetCamera.orthographic)
            {
                Debug.LogWarning(
                    "BattleCameraController drives orthographic size, but the camera on '"
                        + targetCamera.name + "' is perspective. Zoom will have no effect.");
            }

            targetSize = Mathf.Clamp(targetCamera.orthographicSize, minOrthographicSize, maxOrthographicSize);
            targetPosition = targetCamera.transform.position;
        }

        private void Update()
        {
            if (targetCamera == null)
            {
                return;
            }

            if (!inputLocked)
            {
                ReadZoom();
                ReadDragPan();
                ReadEdgePan();
                ReadKeyboardPan();
            }

            ApplyMotion();
        }

        /// <summary>Ignores or accepts input, for use while a modal overlay is up.</summary>
        /// <param name="locked">True to ignore input.</param>
        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;

            if (locked)
            {
                dragging = false;
            }
        }

        /// <summary>
        /// Centres the camera on a grid and sets the pan bounds to that grid's extent.
        /// </summary>
        /// <param name="grid">Grid to frame. Must not be null.</param>
        /// <param name="worldSpace">
        /// Cell-to-world projection. Null falls back to Core's default isometric layout.
        /// </param>
        /// <param name="snap">Jump there immediately instead of gliding.</param>
        public void FrameGrid(IBattleGrid grid, IBattleWorldSpace worldSpace, bool snap = true)
        {
            if (grid == null || grid.Width <= 0 || grid.Height <= 0)
            {
                return;
            }

            IBattleWorldSpace space = worldSpace ?? new IsoLayoutWorldSpace();

            Vector3 first = space.CellToWorld(new GridCoord(0, 0));
            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;

            // The four corners of a square grid are the four corners of the drawn diamond, so
            // sampling them is enough to bound the whole board.
            SampleCorner(space, new GridCoord(grid.Width - 1, 0), ref minX, ref maxX, ref minY, ref maxY);
            SampleCorner(space, new GridCoord(0, grid.Height - 1), ref minX, ref maxX, ref minY, ref maxY);
            SampleCorner(space, new GridCoord(grid.Width - 1, grid.Height - 1), ref minX, ref maxX, ref minY, ref maxY);

            panBounds = Rect.MinMaxRect(
                minX - framePadding, minY - framePadding, maxX + framePadding, maxY + framePadding);

            Vector3 centre = new Vector3(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f,
                targetCamera != null ? targetCamera.transform.position.z : 0f);

            MoveTo(centre, snap);
        }

        /// <summary>Sets the world-space rectangle the camera centre may travel inside.</summary>
        /// <param name="bounds">Rectangle in world units.</param>
        public void SetPanBounds(Rect bounds)
        {
            panBounds = bounds;
        }

        /// <summary>Moves the camera to a world position.</summary>
        /// <param name="worldPosition">Destination. Z is preserved from the current camera position.</param>
        /// <param name="snap">Jump there immediately instead of gliding.</param>
        public void MoveTo(Vector3 worldPosition, bool snap = false)
        {
            worldPosition.z = targetCamera != null ? targetCamera.transform.position.z : worldPosition.z;
            targetPosition = ClampToBounds(worldPosition);

            if (snap && targetCamera != null)
            {
                targetCamera.transform.position = targetPosition;
            }
        }

        /// <summary>Sets the zoom level.</summary>
        /// <param name="orthographicSize">Requested size, clamped to the configured range.</param>
        /// <param name="snap">Jump there immediately instead of gliding.</param>
        public void SetZoom(float orthographicSize, bool snap = false)
        {
            targetSize = Mathf.Clamp(orthographicSize, minOrthographicSize, maxOrthographicSize);

            if (snap && targetCamera != null)
            {
                targetCamera.orthographicSize = targetSize;
            }
        }

        private void ReadZoom()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;

            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            // The Input System reports raw wheel deltas, which are 120 per notch on Windows and
            // roughly 1 per notch elsewhere. Normalising to a sign keeps one notch feeling the same
            // on every platform.
            float direction = Mathf.Sign(scroll);
            float previousSize = targetSize;
            float newSize = Mathf.Clamp(
                targetSize - (direction * zoomStep), minOrthographicSize, maxOrthographicSize);

            if (Mathf.Approximately(newSize, previousSize))
            {
                return;
            }

            if (zoomTowardCursor && targetCamera != null)
            {
                Vector3 cursorBefore = ScreenToWorld(mouse.position.ReadValue());
                targetSize = newSize;
                targetCamera.orthographicSize = newSize;
                Vector3 cursorAfter = ScreenToWorld(mouse.position.ReadValue());
                MoveTo(targetPosition + (cursorBefore - cursorAfter));
            }
            else
            {
                targetSize = newSize;
            }
        }

        private void ReadDragPan()
        {
            if (!enableDragPan)
            {
                dragging = false;
                return;
            }

            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                dragging = false;
                return;
            }

            ButtonControl button = ResolveDragButton(mouse);

            if (button == null)
            {
                dragging = false;
                return;
            }

            if (button.wasPressedThisFrame)
            {
                dragging = true;
                dragWorldAnchor = ScreenToWorld(mouse.position.ReadValue());
                return;
            }

            if (button.wasReleasedThisFrame || !button.isPressed)
            {
                dragging = false;
                return;
            }

            if (!dragging)
            {
                return;
            }

            Vector3 cursorNow = ScreenToWorld(mouse.position.ReadValue());
            Vector3 delta = dragWorldAnchor - cursorNow;

            if (invertDragPan)
            {
                delta = -delta;
            }

            MoveTo(targetPosition + delta);
        }

        private void ReadEdgePan()
        {
            if (!enableEdgePan)
            {
                return;
            }

            Mouse mouse = Mouse.current;

            if (mouse == null || dragging)
            {
                return;
            }

            Vector2 cursor = mouse.position.ReadValue();

            if (cursor.x < 0f || cursor.y < 0f || cursor.x > Screen.width || cursor.y > Screen.height)
            {
                return;
            }

            Vector2 direction = Vector2.zero;

            if (cursor.x <= edgePanMargin)
            {
                direction.x = -1f;
            }
            else if (cursor.x >= Screen.width - edgePanMargin)
            {
                direction.x = 1f;
            }

            if (cursor.y <= edgePanMargin)
            {
                direction.y = -1f;
            }
            else if (cursor.y >= Screen.height - edgePanMargin)
            {
                direction.y = 1f;
            }

            if (direction == Vector2.zero)
            {
                return;
            }

            ApplyPanVelocity(direction, edgePanSpeed);
        }

        private void ReadKeyboardPan()
        {
            if (!enableKeyboardPan)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            Vector2 direction = Vector2.zero;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                direction.x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                direction.x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                direction.y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                direction.y += 1f;
            }

            if (direction == Vector2.zero)
            {
                return;
            }

            ApplyPanVelocity(direction.normalized, keyboardPanSpeed);
        }

        private void ApplyPanVelocity(Vector2 direction, float speed)
        {
            // Scaling by the zoom level keeps the pan feeling the same at every magnification:
            // zoomed out, one second of panning should still cross a similar fraction of the screen.
            float scaled = speed * targetSize * Time.unscaledDeltaTime;
            MoveTo(targetPosition + new Vector3(direction.x * scaled, direction.y * scaled, 0f));
        }

        private void ApplyMotion()
        {
            Transform cameraTransform = targetCamera.transform;

            targetCamera.orthographicSize = Mathf.Lerp(
                targetCamera.orthographicSize,
                targetSize,
                1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime));

            targetPosition = ClampToBounds(targetPosition);

            cameraTransform.position = Vector3.Lerp(
                cameraTransform.position,
                targetPosition,
                1f - Mathf.Exp(-panSmoothing * Time.unscaledDeltaTime));
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            if (!clampToBounds)
            {
                return position;
            }

            position.x = Mathf.Clamp(position.x, panBounds.xMin, panBounds.xMax);
            position.y = Mathf.Clamp(position.y, panBounds.yMin, panBounds.yMax);
            return position;
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            Vector3 point = new Vector3(
                screenPosition.x, screenPosition.y, -targetCamera.transform.position.z);

            Vector3 world = targetCamera.ScreenToWorldPoint(point);
            world.z = targetCamera.transform.position.z;
            return world;
        }

        private ButtonControl ResolveDragButton(Mouse mouse)
        {
            switch (dragPanButton)
            {
                case DragPanButton.Left: return mouse.leftButton;
                case DragPanButton.Right: return mouse.rightButton;
                case DragPanButton.Middle: return mouse.middleButton;
                default: return mouse.middleButton;
            }
        }

        private static void SampleCorner(
            IBattleWorldSpace space,
            GridCoord cell,
            ref float minX,
            ref float maxX,
            ref float minY,
            ref float maxY)
        {
            Vector3 point = space.CellToWorld(cell);

            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }

        private void OnValidate()
        {
            if (maxOrthographicSize < minOrthographicSize)
            {
                maxOrthographicSize = minOrthographicSize;
            }
        }
    }
}
