using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BinakayanRising.UI.Screens
{
    /// <summary>
    /// One portrait in the deployment roster strip. Passes presses and drags on to the HUD, which
    /// owns what they mean; this only turns uGUI's pointer events into screen positions.
    /// </summary>
    /// <remarks>
    /// Implementing the drag handlers here also stops the strip's scroll view from taking the
    /// drag: a portrait pulled upward is being carried to the board, not scrolling the strip.
    /// The wheel still scrolls a strip too long for its space.
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class RosterStripCell : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>The roster slot this portrait stands for.</summary>
        public int Slot;

        /// <summary>The button went down on the portrait, with the slot.</summary>
        public Action<int> Pressed;

        /// <summary>The pointer pulled away far enough to count as a drag: slot and screen point.</summary>
        public Action<int, Vector2> DragBegan;

        /// <summary>The carried portrait moved to a screen point.</summary>
        public Action<Vector2> Dragged;

        /// <summary>The button came up at a screen point, ending the drag.</summary>
        public Action<Vector2> DragEnded;

        /// <inheritdoc/>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Pressed?.Invoke(Slot);
            }
        }

        /// <inheritdoc/>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                DragBegan?.Invoke(Slot, eventData.position);
            }
        }

        /// <inheritdoc/>
        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Dragged?.Invoke(eventData.position);
            }
        }

        /// <inheritdoc/>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                DragEnded?.Invoke(eventData.position);
            }
        }
    }
}
