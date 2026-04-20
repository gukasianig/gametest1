using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class OrbitWeapon : MonoBehaviour
{
    public Transform player;
    public float orbitRadius = 2f;
    public float orbitSpeed = 180f;
    public float startAngle = 0f;

    public int damage = 10;
    public int maxHits = 2;
    private int currentHits = 0;

    public float respawnTime = 3f;

    private float angle;

    void Start()
    {
        // if (player == null)
        // {
        //     GameObject obj = GameObject.FindGameObjectWithTag("Player");
        //     if (obj != null)
        //         player = obj.transform;
        // }
        if (player == null)
    {
        Debug.LogWarning("OrbitWeapon has no player assigned, destroying stray orbit.");
        Destroy(gameObject);
    }
    }

    void Update()
    {
        if (player == null) return;

        angle += orbitSpeed * Time.deltaTime;

        float radians = (angle + startAngle) * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(radians) * orbitRadius,
            0f,
            Mathf.Sin(radians) * orbitRadius
        );

        transform.position = player.position + offset;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy")) return;

        IDamagable dmg = other.GetComponentInParent<IDamagable>();

        if (dmg != null)
        {
            dmg.TakeDamage(damage);
            currentHits++;

            if (currentHits >= maxHits)
            {
                StartCoroutine(DisableAndRespawn());
            }
        }
    }

    IEnumerator DisableAndRespawn()
    {
        gameObject.SetActive(false);
        yield return new WaitForSeconds(respawnTime);
        currentHits = 0;
        gameObject.SetActive(true);
    }
}