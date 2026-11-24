using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Звук кнопок (21.11.2026): наведение мышью или переход стрелками/стиком — тихий UiHover, нажатие мышью или Submit — UiClick.
// Начальный фокус экрана (UiFocus.Set) звука не даёт: озвучивается только навигация (AxisEventData).
public class UiSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISelectHandler, ISubmitHandler
{
    private Selectable selectable;

    private bool Active
    {
        get
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            return selectable == null || selectable.IsInteractable();
        }
    }

    public void OnPointerEnter(PointerEventData eventData) { if (Active) GameAudio.Play(Sfx.UiHover, 0.6f, 0.02f); }

    public void OnSelect(BaseEventData eventData) { if (eventData is AxisEventData && Active) GameAudio.Play(Sfx.UiHover, 0.6f, 0.02f); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && Active) GameAudio.Play(Sfx.UiClick, 0.8f, 0.02f);
    }

    public void OnSubmit(BaseEventData eventData) { if (Active) GameAudio.Play(Sfx.UiClick, 0.8f, 0.02f); }
}
