using UnityEngine;

// Свободная камера для тестовой сцены рельефа: WASD — движение, QE — вниз/вверх, Shift — быстрее, правая кнопка мыши — обзор.
public class FlyCamera : MonoBehaviour
{
    public float speed = 20f;
    public float fastMultiplier = 4f;
    public float lookSensitivity = 2.5f;
    private float yaw, pitch;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x > 180f ? angles.x - 360f : angles.x;
    }

    private void Update()
    {
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (Input.GetKey(KeyCode.E)) move.y += 1f;
        if (Input.GetKey(KeyCode.Q)) move.y -= 1f;
        float factor = Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f;
        transform.position += transform.TransformDirection(move) * speed * factor * Time.unscaledDeltaTime;
    }
}
