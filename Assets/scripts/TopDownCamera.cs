using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    public Transform target;

    public Vector3 offset = new Vector3(0, 40, -5);

    public float smoothSpeed = 5f;

    public float zoomSpeed = 50f;
    public float minHeight = 10f;
    public float maxHeight = 60f;

    void Start()
    {
        if (target == null) return;
        transform.position = target.position + offset;
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

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            1f - Mathf.Exp(-Mathf.Max(0f, smoothSpeed) * Time.deltaTime)
        );

        // Keep the viewing direction independent of follow lag and movement direction.
        transform.rotation = Quaternion.LookRotation(-offset, Vector3.up);
    }
}
