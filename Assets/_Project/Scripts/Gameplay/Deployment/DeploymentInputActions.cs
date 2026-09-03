using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BinakayanRising.Gameplay.Deployment
{
    /// <summary>
    /// A thin wrapper over the new Input System exposing exactly the control scheme the capstone
    /// document specifies, plus the input lock the combat phase needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The document's control scheme, verbatim in intent:
    /// </para>
    /// <list type="table">
    ///   <item>
    ///     <term>Left-click</term>
    ///     <description>Selects a unit, interacts with an encampment facility, confirms a
    ///     drag-and-drop placement, selects a quiz answer. Exposed as <see cref="LeftClicked"/>.</description>
    ///   </item>
    ///   <item>
    ///     <term>Right-click</term>
    ///     <description>Cancels an action, deselects, closes a pop-up. Exposed as
    ///     <see cref="RightClicked"/>.</description>
    ///   </item>
    ///   <item>
    ///     <term>Scroll wheel</term>
    ///     <description>Zooms the camera. Exposed as <see cref="Zoomed"/>, and optionally applied
    ///     to <see cref="zoomCamera"/> here.</description>
    ///   </item>
    ///   <item>
    ///     <term>Escape</term>
    ///     <description>Opens pause, settings and the exit prompt. Exposed as
    ///     <see cref="EscapePressed"/>.</description>
    ///   </item>
    /// </list>
    /// <para>
    /// Devices are polled directly through <c>UnityEngine.InputSystem.Mouse</c> and
    /// <c>Keyboard</c>, never through legacy <c>UnityEngine.Input</c>. Polling rather than an
    /// <c>.inputactions</c> asset is deliberate: the four controls above are fixed by the document
    /// and an asset would be one more binary file to author in the Editor and keep in sync.
    /// </para>
    /// <para>
    /// <b>Input lock.</b> The document requires that "all player input is locked for the entire
    /// combat phase" once the formation is confirmed. <see cref="SetInputLocked"/> is that switch:
    /// while it is on, no pointer or scroll event is raised at all.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DeploymentInputActions : MonoBehaviour
    {
        [Header("Lock")]
        [Tooltip("Start with player input locked. The combat phase sets this for its whole duration.")]
        [SerializeField] private bool startLocked = false;

        [Tooltip("Let Escape through even while input is locked, so pause and the exit prompt stay " +
                 "reachable during combat. TODO(design): the document locks 'all player input' " +
                 "during combat but also binds Escape to pause; this resolves the contradiction.")]
        [SerializeField] private bool escapeIgnoresInputLock = true;

        [Header("Camera Zoom")]
        [Tooltip("Camera the scroll wheel zooms. Leave empty to only raise the Zoomed event.")]
        [SerializeField] private Camera zoomCamera;

        [Tooltip("Orthographic size removed per scroll notch. TODO(design): not specified in capstone document.")]
        [SerializeField] private float zoomUnitsPerNotch = 0.5f;

        [Tooltip("Closest the camera may zoom in. TODO(design): not specified in capstone document.")]
        [Min(0.01f)]
        [SerializeField] private float minOrthographicSize = 2f;

        [Tooltip("Furthest the camera may zoom out. TODO(design): not specified in capstone document.")]
        [Min(0.01f)]
        [SerializeField] private float maxOrthographicSize = 12f;

        private bool inputLocked;

        /// <summary>Left mouse button pressed. Carries the screen-space pointer position.</summary>
        public event Action<Vector2> LeftClicked;

        /// <summary>Left mouse button released. Carries the screen-space pointer position.</summary>
        public event Action<Vector2> LeftReleased;

        /// <summary>Right mouse button pressed. Carries the screen-space pointer position.</summary>
        public event Action<Vector2> RightClicked;

        /// <summary>
        /// Scroll wheel moved. Carries the vertical scroll delta in notches: positive scrolls up,
        /// which zooms in.
        /// </summary>
        public event Action<float> Zoomed;

        /// <summary>Escape pressed: open pause, settings, or the exit prompt.</summary>
        public event Action EscapePressed;

        /// <summary>Raised whenever <see cref="IsInputLocked"/> changes.</summary>
        public event Action<bool> InputLockChanged;

        /// <summary>True while player input is suppressed, as it is for the whole combat phase.</summary>
        public bool IsInputLocked
        {
            get { return inputLocked; }
        }

        /// <summary>
        /// The pointer position in screen pixels, or <see cref="Vector2.zero"/> when no pointer
        /// device is connected. Readable even while input is locked.
        /// </summary>
        public Vector2 PointerPosition
        {
            get
            {
                Pointer pointer = Pointer.current;
                return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
            }
        }

        /// <summary>
        /// Locks or unlocks all player input. The combat phase locks it on entry and unlocks it when
        /// control returns to the Encampment.
        /// </summary>
        /// <param name="locked">True to suppress input.</param>
        public void SetInputLocked(bool locked)
        {
            if (inputLocked == locked)
            {
                return;
            }

            inputLocked = locked;

            Action<bool> handler = InputLockChanged;

            if (handler != null)
            {
                handler(locked);
            }
        }

        /// <summary>Assigns the camera the scroll wheel zooms.</summary>
        /// <param name="value">The camera. Null disables camera zooming; the event still fires.</param>
        public void SetZoomCamera(Camera value)
        {
            zoomCamera = value;
        }

        private void Awake()
        {
            inputLocked = startLocked;

            if (zoomCamera == null)
            {
                zoomCamera = Camera.main;
            }
        }

        private void Update()
        {
            PollKeyboard();

            if (inputLocked)
            {
                return;
            }

            PollMouse();
        }

        private void PollKeyboard()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            if (inputLocked && !escapeIgnoresInputLock)
            {
                return;
            }

            Action handler = EscapePressed;

            if (handler != null)
            {
                handler();
            }
        }

        private void PollMouse()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                Raise(LeftClicked, position);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                Raise(LeftReleased, position);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                Raise(RightClicked, position);
            }

            // The Input System reports scroll in raw device units; 120 is one notch on a standard
            // wheel, so dividing normalises platforms that report per-pixel deltas.
            float scroll = mouse.scroll.ReadValue().y;

            if (!Mathf.Approximately(scroll, 0f))
            {
                float notches = Mathf.Abs(scroll) >= 1f ? scroll / 120f : scroll;

                if (Mathf.Approximately(notches, 0f))
                {
                    notches = Mathf.Sign(scroll) * 0.01f;
                }

                ApplyCameraZoom(notches);

                Action<float> handler = Zoomed;

                if (handler != null)
                {
                    handler(notches);
                }
            }
        }

        private void ApplyCameraZoom(float notches)
        {
            if (zoomCamera == null || !zoomCamera.orthographic)
            {
                return;
            }

            float size = zoomCamera.orthographicSize - (notches * zoomUnitsPerNotch);
            zoomCamera.orthographicSize = Mathf.Clamp(size, minOrthographicSize, maxOrthographicSize);
        }

        private static void Raise(Action<Vector2> handler, Vector2 position)
        {
            if (handler != null)
            {
                handler(position);
            }
        }
    }
}
