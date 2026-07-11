using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// Left-button pointer drag with capture: reports panel-space deltas to a callback.
    /// Shared by the titlebar drag region and the three resize handles.
    /// </summary>
    public sealed class PointerDragManipulator : PointerManipulator
    {
        private readonly Action<Vector2> dragged;
        private Vector3 lastPointerPosition;
        private bool dragging;

        public PointerDragManipulator(Action<Vector2> dragged)
        {
            this.dragged = dragged ?? throw new ArgumentNullException(nameof(dragged));
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }

        private void OnPointerDown(PointerDownEvent pointerEvent)
        {
            if (!CanStartManipulation(pointerEvent))
            {
                return;
            }

            dragging = true;
            lastPointerPosition = pointerEvent.position;
            target.CapturePointer(pointerEvent.pointerId);
            pointerEvent.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent pointerEvent)
        {
            if (!dragging || !target.HasPointerCapture(pointerEvent.pointerId))
            {
                return;
            }

            var delta = pointerEvent.position - lastPointerPosition;
            lastPointerPosition = pointerEvent.position;
            dragged(new Vector2(delta.x, delta.y));
            pointerEvent.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent pointerEvent)
        {
            if (!dragging || !CanStopManipulation(pointerEvent))
            {
                return;
            }

            dragging = false;
            target.ReleasePointer(pointerEvent.pointerId);
            pointerEvent.StopPropagation();
        }
    }
}
