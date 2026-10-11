using UnityEngine;
using UnityEngine.EventSystems;

namespace BiomechanicalCrafting
{
    /// <summary>
    /// 2x2 顶层覆盖层交互转发器 (Event Forwarder)
    /// 承载覆盖层全域点击、一笔画划入与拖拽事件，并精准无缝转发给根槽位 ShellSlotView
    /// </summary>
    public class Shell2x2OverlayView : MonoBehaviour,
        IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public ShellSlotView rootSlot;

        public void OnPointerDown(PointerEventData eventData) => rootSlot?.OnPointerDown(eventData);
        public void OnPointerEnter(PointerEventData eventData) => rootSlot?.OnPointerEnter(eventData);
        public void OnPointerExit(PointerEventData eventData) => rootSlot?.OnPointerExit(eventData);
        public void OnPointerUp(PointerEventData eventData) => rootSlot?.OnPointerUp(eventData);
        public void OnBeginDrag(PointerEventData eventData) => rootSlot?.OnBeginDrag(eventData);
        public void OnDrag(PointerEventData eventData) => rootSlot?.OnDrag(eventData);
        public void OnEndDrag(PointerEventData eventData) => rootSlot?.OnEndDrag(eventData);
        public void OnPointerClick(PointerEventData eventData) => rootSlot?.OnPointerClick(eventData);
    }
}
