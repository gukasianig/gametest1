using UnityEngine;
using UnityEngine.EventSystems;

// Сообщает о выборе элемента стрелками/геймпадом или из кода (не кликом мыши) — 21.11.2026.
// Плитки оружия по нему показывают подробности, не меняя поведения мыши (первый клик выбирает, второй — в бой).
public class SelectRelay : MonoBehaviour, ISelectHandler
{
    public System.Action onKeyboardSelect;

    public void OnSelect(BaseEventData eventData)
    {
        if (eventData is PointerEventData) return;
        if (onKeyboardSelect != null) onKeyboardSelect();
    }
}
