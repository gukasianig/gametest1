using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayMovement : MonoBehaviour
{
    
    public float speed = 5f;
    //public Transform cameraTransform;
    public float rotationSpeed = 10f;
    public Transform cameraTransform;

    Rigidbody rb;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Debug.Log("Game Started");
    }

    void FixedUpdate()
    {   
        if (cameraTransform == null) return;
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * z + right * x;

        Vector3 velocity = move * speed;
        rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
        //transform.Translate(move * speed * Time.deltaTime, Space.World);

            if (move != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move);
                rb.rotation =Quaternion.Lerp(
                    rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }


        
    }

   
}
