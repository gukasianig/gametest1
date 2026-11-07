using System.Collections.Generic;
using UnityEngine;

public class OrbitManager : MonoBehaviour
{
    public GameObject orbitPrefab;
    public bool enableDebugUpgrades = false;

    public int orbitCount = 1;
    public float orbitRadius = 2f;
    public float orbitSpeed = 180f;
    public int orbitDamage = 10;
    public int orbitMaxHits = 2;
    public float orbitRespawnTime = 2f;

    private List<OrbitWeapon> orbits = new List<OrbitWeapon>();
    private float damageMultiplier = 1f;
    public int CurrentDamage => Mathf.RoundToInt(orbitDamage * damageMultiplier);

    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = Mathf.Max(1f, multiplier);
        foreach (OrbitWeapon orbit in orbits) if (orbit != null) orbit.damage = CurrentDamage;
    }

    void Start()
    {
        CreateOrbits();
    }

    void Update()
    {
        if (!enableDebugUpgrades || Time.timeScale == 0f) return;

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

        if (orbitPrefab == null) return;

        for (int i = 0; i < orbitCount; i++)
        {
            GameObject obj = Instantiate(orbitPrefab, transform.position, Quaternion.identity);

            OrbitWeapon ow = obj.GetComponent<OrbitWeapon>();
            if (ow != null)
            {
                ow.player = transform;
                ow.orbitRadius = orbitRadius;
                ow.orbitSpeed = orbitSpeed;
                ow.damage = CurrentDamage;
                ow.maxHits = orbitMaxHits;
                ow.respawnTime = orbitRespawnTime;
                ow.startAngle = (360f / orbitCount) * i;

                orbits.Add(ow);
                if (ArenaLayout.Instance != null) ArenaLayout.Instance.Tint(obj, GameUi.Accent);
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
                orbit.damage = CurrentDamage;
            }
        }
    }

    private float baseOrbitSpeed = -1f;

    // Upgrade: rotation speed as a share of the original (0.2 = +20%), applied to existing satellites too.
    public void SetSpeedBonus(float bonus)
    {
        if (baseOrbitSpeed < 0f) baseOrbitSpeed = orbitSpeed;
        orbitSpeed = baseOrbitSpeed * (1f + Mathf.Max(0f, bonus));
        foreach (OrbitWeapon orbit in orbits)
            if (orbit != null) orbit.orbitSpeed = orbitSpeed;
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
