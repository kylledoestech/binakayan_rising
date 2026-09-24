using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BinakayanRising.Gameplay
{
    /// <summary>
    /// Answers "is this screen point covered by interface?" for world input — board clicks,
    /// camera zoom and drag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <see cref="EventSystem.RaycastAll"/> against a synthetic pointer event rather than
    /// <c>EventSystem.IsPointerOverGameObject()</c>, for the reason documented on
    /// <c>GridRaycaster.IsPointerOverUI</c>: the latter is tied to pointer ids the input module has
    /// seen and can report false over a panel. The raycast form also respects
    /// <see cref="ICanvasRaycastFilter"/>, which is how the tutorial spotlight lets clicks fall
    /// through its hole onto the board.
    /// </para>
    /// <para>
    /// A missing event system means nothing covers the board, not an error: the offline harness
    /// runs the battle with no interface at all.
    /// </para>
    /// </remarks>
    public static class UiPointer
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static PointerEventData pointer;
        private static EventSystem pointerOwner;
        private static int rightClickClaimedFrame = -1;

        /// <summary>True when any raycast-target graphic sits under the screen point.</summary>
        /// <param name="screenPosition">Screen position in pixels.</param>
        public static bool IsOverUi(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            // Rebuilt when the event system changes: play mode runs without a domain reload, so a
            // cached event from the previous session would point at a destroyed system.
            if (pointer == null || pointerOwner != eventSystem)
            {
                pointer = new PointerEventData(eventSystem);
                pointerOwner = eventSystem;
            }

            pointer.Reset();
            pointer.position = screenPosition;

            Hits.Clear();
            eventSystem.RaycastAll(pointer, Hits);
            bool hit = Hits.Count > 0;
            Hits.Clear();
            return hit;
        }

        /// <summary>
        /// The sorting order of the topmost canvas with a raycast target under the screen point,
        /// or <see cref="int.MinValue"/> when nothing is there.
        /// </summary>
        public static int TopSortingOrder(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return int.MinValue;
            }

            if (pointer == null || pointerOwner != eventSystem)
            {
                pointer = new PointerEventData(eventSystem);
                pointerOwner = eventSystem;
            }

            pointer.Reset();
            pointer.position = screenPosition;

            Hits.Clear();
            eventSystem.RaycastAll(pointer, Hits);

            // RaycastAll sorts front to back, so the first hit is the one drawn on top.
            int order = int.MinValue;
            if (Hits.Count > 0 && Hits[0].gameObject != null)
            {
                Canvas canvas = Hits[0].gameObject.GetComponentInParent<Canvas>();
                order = canvas != null ? canvas.rootCanvas.sortingOrder : 0;
            }

            Hits.Clear();
            return order;
        }

        /// <summary>
        /// Claims this frame's right-click for a "cancel / close" action, at most once per frame.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Right-click means "back out of whatever is on top". Several components listen for it —
        /// the board, the How-to-Play deck, camp panels, Settings, the Library — and their
        /// <c>Update</c> order is not defined, so without a claim one press could close a panel
        /// and the one beneath it in the same frame. The first claimant wins; the rest see false.
        /// </para>
        /// <para>
        /// <paramref name="maxLayer"/> is the caller's canvas sorting order: the claim fails when a
        /// canvas drawn above it sits under the pointer (a quiz card, a confirm dialog), so a
        /// right-click never reaches through a modal to close what is behind it.
        /// </para>
        /// </remarks>
        /// <returns>True when the right button went down this frame and the caller now owns it.</returns>
        public static bool TryClaimRightClick(int maxLayer = int.MaxValue)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame || rightClickClaimedFrame == Time.frameCount)
            {
                return false;
            }

            if (maxLayer != int.MaxValue && TopSortingOrder(mouse.position.ReadValue()) > maxLayer)
            {
                return false;
            }

            rightClickClaimedFrame = Time.frameCount;
            return true;
        }
    }
}
