using UnityEngine;

// Всплывающее окно подробностей Мастерской (18.11, вместо правой панели в треть экрана).
// Наведение на узел показывает его; клик закрепляет окно. Закреплённое окно закрывается кликом
// по пустому месту дерева или Esc. Курсор внутри окна закрепляет показанный узел, чтобы кнопки
// «Улучшить»/«Установить» всегда работали с тем, что видно.
public partial class WorkshopTree
{
    private const float PopupWidth = 290f, PopupGap = 6f, PopupMargin = 6f;

    private bool IsPicked(Node node) { return pinned && node == selected; }

    private void Pin(Node node, bool reveal = true)
    {
        selected = node;
        pinned = true;
        ShowPopup(node);
        Refresh();
        if (reveal) Reveal(node, true);
    }

    private void ShowPopup(Node node)
    {
        if (shown == node) return;
        shown = node;
        if (detailGroup != null)
        {
            bool visible = node != null;
            detailGroup.blocksRaycasts = detailGroup.interactable = visible;
            if (!visible) detailGroup.alpha = 0f;
        }
        if (node != null) RefreshDetails();
    }

    private Camera UiCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private bool PointerOver(RectTransform rect)
    {
        return rect != null && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, UiCamera());
    }

    private void UpdatePopup()
    {
        if (detailPanel == null) return;
        Node over = null;
        foreach (Node node in nodes) if (node.diamond != null && node.diamond.Hovered) { over = node; break; }
        bool overPanel = shown != null && PointerOver(detailPanel);
        if (over != null) lastHoverTime = Time.unscaledTime;

        if (Input.GetKeyDown(KeyCode.Escape) && pinned) { pinned = false; ShowPopup(null); Refresh(); return; }
        if (Input.GetMouseButtonDown(0) && pinned && over == null && !overPanel && PointerOver(Scroll.viewport))
        {
            pinned = false; ShowPopup(null); Refresh(); return;
        }

        if (overPanel)
        {
            // Курсор дошёл до окна: показанный узел становится выбранным, кнопки окна действуют на него.
            if (!pinned || selected != shown) { selected = shown; pinned = true; Refresh(); }
            return;
        }
        Node target;
        if (over != null) target = over;
        else if (shown != null && (Time.unscaledTime - lastHoverTime < HoverGrace || PointerOver(Frame(shown)))) target = shown;
        else target = pinned ? selected : null;
        if (target != shown) { ShowPopup(target); RefreshBranchActivity(); }
    }

    private RectTransform Frame(Node node) { return node.frame != null ? node.frame : node.rect; }

    private void PlacePopup()
    {
        if (detailPanel == null || shown == null || shown.rect == null) return;
        bool equipment = shown.weapon >= 0;
        float height = equipment ? (WeaponElements.IsLocked(shown.weapon) ? 450f * EquipmentShortShare : 450f) : shown.project != null ? 360f : 320f;
        if (!Mathf.Approximately(detailPanel.sizeDelta.y, height)) detailPanel.sizeDelta = new Vector2(PopupWidth, height);

        RectTransform root = (RectTransform)transform;
        var corners = new Vector3[4];
        Frame(shown).GetWorldCorners(corners);
        Vector2 min = root.InverseTransformPoint(corners[0]), max = root.InverseTransformPoint(corners[2]);
        Rect area = root.rect;
        // Справа от карточки; если не помещается — слева; если нигде — прижать к краю экрана.
        float x = max.x + PopupGap;
        if (x + PopupWidth > area.xMax - PopupMargin) x = min.x - PopupGap - PopupWidth;
        x = Mathf.Clamp(x, area.xMin + PopupMargin, area.xMax - PopupMargin - PopupWidth);
        float top = Mathf.Clamp(max.y, area.yMin + PopupMargin + height, area.yMax - area.height * .07f);
        detailPanel.anchoredPosition = new Vector2(x, top) - area.center;
    }
}
