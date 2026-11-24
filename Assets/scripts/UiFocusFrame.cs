using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Рамка фокуса клавиатуры/геймпада (6Б, 21.11.2026); см. UiFocus.
public class UiFocusFrame : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    public Color color = new Color(1f, 0.82f, 0.35f, 0.95f);
    private Outline frame;

    public void OnSelect(BaseEventData eventData)
    {
        if (eventData is PointerEventData) return;
        if (frame == null)
        {
            frame = gameObject.AddComponent<Outline>();
            frame.effectDistance = new Vector2(3f, -3f);
        }
        frame.effectColor = color;
        frame.enabled = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (frame != null) frame.enabled = false;
    }

    private void OnDisable()
    {
        if (frame != null) frame.enabled = false;
    }
}
