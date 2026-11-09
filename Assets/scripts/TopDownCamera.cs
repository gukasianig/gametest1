using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    public Transform target;

    // Высота и наклон фиксированы (в сцене GreenArenaPlay — 18 м над героем, 6 м позади).
    // 09.11.2026 по просьбе владельца убрано приближение/отдаление колесом мыши.
    public Vector3 offset = new Vector3(0, 40, -5);

    public float smoothSpeed = 5f;

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
