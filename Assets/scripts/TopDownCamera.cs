using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    public Transform target;

    public Vector3 offset = new Vector3(0, 40, -5);

    public float smoothSpeed = 5f;

    public float zoomSpeed = 50f;
    public float minHeight = 10f;
    public float maxHeight = 60f;

    // Позиция без тряски: следование считается от неё, смещение ScreenShake добавляется сверху.
    private Vector3 basePosition;
    private bool baseKnown;

    void Start()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        basePosition = transform.position;
        baseKnown = true;
        transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
    }

    void LateUpdate()
    {
        if (target == null || Time.timeScale <= 0f) return;

        // 🔥 ZOOM
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            offset.y -= scroll * zoomSpeed;
            offset.y = Mathf.Clamp(offset.y, minHeight, maxHeight);
        }

        // 🔥 FOLLOW
        Vector3 desiredPosition = target.position + offset;

        if (!baseKnown) { basePosition = transform.position; baseKnown = true; }
        basePosition = Vector3.Lerp(
            basePosition,
            desiredPosition,
            1f - Mathf.Exp(-Mathf.Max(0f, smoothSpeed) * Time.deltaTime)
        );
        transform.position = basePosition + ScreenShake.Offset(Time.deltaTime);

        // Keep the viewing direction independent of follow lag and movement direction.
        transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
    }
}
