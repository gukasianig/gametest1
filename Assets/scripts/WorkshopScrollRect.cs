using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WorkshopScrollRect : ScrollRect
{
    public WorkshopTree Tree;
    public override void OnBeginDrag(PointerEventData eventData)
    {
        if (Tree != null) Tree.CancelNavigation();
        base.OnBeginDrag(eventData);
    }
    public override void OnScroll(PointerEventData eventData)
    {
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Tree != null) Tree.ZoomAt(eventData.scrollDelta.y, eventData.position);
            return;
        }
        if (Tree != null) Tree.CancelNavigation();
        base.OnScroll(eventData);
    }
}
