using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageZone : MonoBehaviour
{
    
    public int damage = 10;
    void OnTriggerEnter(Collider other)
    {
        Debug.Log("triger works");
        Health health = other.GetComponent<Health>();
        if (health != null && !health.isDead)
        {
            Debug.Log("hit player");
            health.TakeDamage(damage);
        }
    }
}
