using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bilboard : MonoBehaviour
{
    public Transform cameraTransform;
    void LateUpdate()
    {
        transform.LookAt(cameraTransform);
        transform.Rotate(0, 180, 0);
    }
}
