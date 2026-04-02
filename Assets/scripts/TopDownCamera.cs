using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 10, 0);
    //public float height = 10f;
    public float smoothSpeed = 5f;
    void LateUpdate()
    {
        Vector3 desiredPosition = target.position + offset;
    
        transform.position = Vector3.Lerp(
        transform.position,
        desiredPosition,
        smoothSpeed * Time.deltaTime
    );

        transform.LookAt(target);
    }

}
