using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayMovement : MonoBehaviour
{

    public float speed = 5f;
    public float SpeedMultiplier { get; set; } = 1f;
    //public Transform cameraTransform;
    public float rotationSpeed = 10f;
    public Transform cameraTransform;

    Rigidbody rb;
    private Vector2 movementInput;
    private Vector3 aimDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        movementInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }

    public void SetAimDirection(Vector3 direction)
    {
        direction.y = 0f;
        aimDirection = direction;
    }

    void FixedUpdate()
    {
        if (cameraTransform == null) return;
        float x = movementInput.x;
        float z = movementInput.y;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 move = Vector3.ClampMagnitude(forward * z + right * x, 1f);

        Vector3 velocity = move * speed * SpeedMultiplier;
        if (ArenaLayout.Instance != null)
        {
            Vector3 desired = rb.position + velocity * Time.fixedDeltaTime;
            Vector3 allowed = ArenaLayout.Instance.ResolveMovement(rb.position, desired, 0.5f);
            velocity = (allowed - rb.position) / Time.fixedDeltaTime;
        }
        rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
        //transform.Translate(move * speed * Time.deltaTime, Space.World);

            Vector3 facing = aimDirection.sqrMagnitude > 0.001f ? aimDirection : move;
            if (facing.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(facing);
                rb.MoveRotation(Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-rotationSpeed * Time.fixedDeltaTime)
                ));
            }



    }


}
