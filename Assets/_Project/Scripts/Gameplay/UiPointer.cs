using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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
    }
}
