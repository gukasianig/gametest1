using System.Collections.Generic;
using UnityEngine;

public class OrbitManager : MonoBehaviour
{
    public GameObject orbitPrefab;

    public int orbitCount = 1;
    public float orbitRadius = 2f;
    public float orbitSpeed = 180f;
    public int orbitDamage = 10;
    public int orbitMaxHits = 2;
    public float orbitRespawnTime = 2f;

    private List<OrbitWeapon> orbits = new List<OrbitWeapon>();

    void Start()
    {
        CreateOrbits();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            UpgradeOrbitCount(1);
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            UpgradeDamage(5);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            UpgradeSpeed(50f);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            UpgradeRadius(0.5f);
        }
    }

    void CreateOrbits()
    {
        foreach (OrbitWeapon orbit in orbits)
        {
            if (orbit != null)
            {
                Destroy(orbit.gameObject);
            }
        }

        orbits.Clear();

        for (int i = 0; i < orbitCount; i++)
        {
            GameObject obj = Instantiate(orbitPrefab, transform.position, Quaternion.identity);

            OrbitWeapon ow = obj.GetComponent<OrbitWeapon>();
            if (ow != null)
            {
                ow.player = transform;
                ow.orbitRadius = orbitRadius;
                ow.orbitSpeed = orbitSpeed;
                ow.damage = orbitDamage;
                ow.maxHits = orbitMaxHits;
                ow.respawnTime = orbitRespawnTime;
                ow.startAngle = (360f / orbitCount) * i;

                orbits.Add(ow);
            }
        }
    }

    public void UpgradeOrbitCount(int amount)
    {
        orbitCount += amount;
        CreateOrbits();
    }

    public void UpgradeDamage(int amount)
    {
        orbitDamage += amount;

        foreach (OrbitWeapon orbit in orbits)
        {
            if (orbit != null)
            {
                orbit.damage = orbitDamage;
            }
        }
    }

    public void UpgradeSpeed(float amount)
    {
        orbitSpeed += amount;

        foreach (OrbitWeapon orbit in orbits)
        {
            if (orbit != null)
            {
                orbit.orbitSpeed = orbitSpeed;
            }
        }
    }

    public void UpgradeRadius(float amount)
    {
        orbitRadius += amount;

        foreach (OrbitWeapon orbit in orbits)
        {
            if (orbit != null)
            {
                orbit.orbitRadius = orbitRadius;
            }
        }
    }
}
