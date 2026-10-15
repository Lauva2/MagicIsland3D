using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicIsland.UI.Views
{
    // Panel controllers own these callbacks; dragging never moves state directly.
    public sealed class InventorySlotInput : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler,
        IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        public Func<PointerEventData, bool> Begin;
        public Action<PointerEventData> Drag, End, Drop;
        public Action Split, Cancel;
        public Action Quick;
        public Func<bool> ShiftHeld;
        public bool QuickModifier => ShiftHeld?.Invoke() ?? (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        private bool active;
        private int pointerId;
        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = true; }
        public void OnBeginDrag(PointerEventData data)
        {
            data.eligibleForClick = false;
            if (data.button != PointerEventData.InputButton.Left) return;
            if (active) return;
            active = Begin?.Invoke(data) ?? false;
            if (active) pointerId = data.pointerId;
        }
        public void OnDrag(PointerEventData data) { if (active && data.pointerId == pointerId) Drag?.Invoke(data); }
        public void OnEndDrag(PointerEventData data) { if (!active || data.pointerId != pointerId) return; active = false; End?.Invoke(data); }
        public void OnDrop(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Drop?.Invoke(data); }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Right) Split?.Invoke();
            else if (data.button == PointerEventData.InputButton.Left && Quick != null && QuickModifier) Quick();
        }
        private void OnDisable() { if (!active) return; active = false; Cancel?.Invoke(); }
        private void OnApplicationFocus(bool focused) { if (!focused) OnDisable(); }
        public void Unbind() { active = false; Begin = null; Drag = End = Drop = null; Split = Cancel = Quick = null; ShiftHeld = null; }
    }
}
