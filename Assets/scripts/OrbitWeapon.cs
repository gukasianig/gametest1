using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbitWeapon : MonoBehaviour
{
    public Transform player;
    public float orbitRadius = 2f;
    public float orbitSpeed = 180f;

    public int damage = 10;
    public int maxHits = 2;
    private int currentHits = 0;

    public float resspawnTime = 3f;


    // Update is called once per frame
    void Update()
    {
        
    }
}
