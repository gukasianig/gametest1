using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Навигация клавиатурой/геймпадом по меню (6Б, 21.11.2026): начальный фокус экранов и заметная рамка
// у кнопки, выбранной стрелками/стиком. Выбор мышью рамку не включает — у мыши своя подсветка наведения.
public static class UiFocus
{
    // Ставит фокус EventSystem на кнопку (если она активна); клики мышью это не перехватывает.
    public static void Set(Selectable target)
    {
        EventSystem system = EventSystem.current;
        if (system == null || target == null || !target.isActiveAndEnabled || !target.IsInteractable()) return;
        if (system.alreadySelecting) return;
        system.SetSelectedGameObject(null);
        system.SetSelectedGameObject(target.gameObject);
    }

    // Снимает фокус при входе в бой: иначе Пробел (рывок) = Submit нажимал бы последнюю выбранную кнопку.
    public static void Clear()
    {
        EventSystem system = EventSystem.current;
        if (system != null && !system.alreadySelecting) system.SetSelectedGameObject(null);
    }
}
