using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Decorative scale stays separate from the layout and never blocks an action.
public sealed class WorkshopButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Button button;
    private bool hovered, pressed;
    private float scale = 1f;
    private void Awake() { button = GetComponent<Button>(); }
    public void OnPointerEnter(PointerEventData data) { hovered = true; }
    public void OnPointerExit(PointerEventData data) { hovered = pressed = false; }
    public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData data) { pressed = false; }
    private void Update()
    {
        float target = button != null && button.IsInteractable() ? pressed ? .975f : hovered ? 1.025f : 1f : 1f;
        scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        transform.localScale = Vector3.one * scale;
    }
    private void OnDisable() { hovered = pressed = false; scale = 1f; transform.localScale = Vector3.one; }
}
