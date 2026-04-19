using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum WeaponType
{
    Single, //обичная пуля
    Shotgun,
    //Rocket 
}

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
   
}
