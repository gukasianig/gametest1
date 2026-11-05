using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum WeaponType
{
    Single, //обичная пуля
    Shotgun,
    //Rocket
}

// Standard = ordinary bullets from AutoShoter. The others are handled by SpecialWeapons.
public enum WeaponKind { Standard, Boomerang, Mines, Rocket, Arc, Flame, Ice, Rail, Turret, Drone }

// Which enemy the automatic aim chooses.
public enum TargetRule { Nearest, Strongest, Densest, Weakest }

[System.Serializable]
public class WeaponData //: MonoBehaviour
{


   [Header ("general")]
   public string weaponName;
   public WeaponType WeaponType;

   [Header ("Shooting")]

    public GameObject bulletPrefab;
    public float fireRate = 0.5f;
    public int damage = 20;
    public float range = 20f;


   [Header("Shotgun")]

    public int pelletCount = 1;
    public float spreadAngle = 0f;

    [Header("Spread")]
    public float bulletSpeed = 20f;
    public float bulletLifeTime = 3f;


   [Header ("Grenade")]

    public bool userGrenade = false;
    public GameObject grenadePrefab;
    public float grenadeCooldown = 3f;
    public int grenadeDamage = 30;
    public float grenadeRadius = 3f;

    [Header("Menu card (empty = built-in text)")]
    public string title;
    public string role;
    public string description;

    // Special weapons. fireRate is the time between uses (for turrets: between placements, for flame: between ticks).
    // damage and range work as for bullets; the fields below are the per-kind knobs - tune them freely.
    [Header("Special weapon")]
    public WeaponKind kind = WeaponKind.Standard;
    public TargetRule targeting = TargetRule.Nearest;
    [Tooltip("Rail: beam half-width. Boomerang: saw radius.")]
    public float beamWidth = 0.7f;
    [Tooltip("Mines, rocket, ice: blast radius.")]
    public float explosionRadius = 3.2f;
    [Tooltip("Arc: extra targets after the first.")]
    public int chainJumps = 4;
    public float chainRadius = 6f;
    [Tooltip("Arc: damage multiplier per jump.")]
    public float chainFalloff = 0.75f;
    [Tooltip("Mines, turret, boomerang, drones: how many can exist at once.")]
    public int maxActive = 2;
    [Tooltip("Mines, turret: seconds before a placed object disappears.")]
    public float lifetime = 18f;
    [Tooltip("Flame: burn time. Ice: slow time.")]
    public float effectDuration = 3f;
    [Tooltip("Ice: enemy speed multiplier while slowed (0.5 = half speed).")]
    public float slowFactor = 0.5f;
    [Tooltip("Flame: burn damage per second.")]
    public float burnPerSecond = 6f;
    [Tooltip("Flame: full cone angle in degrees.")]
    public float coneAngle = 55f;
    [Tooltip("Turret and drones: seconds between their own shots.")]
    public float placedFireRate = 0.4f;

    public bool IsUsable { get { return kind != WeaponKind.Standard || bulletPrefab != null; } }
}
