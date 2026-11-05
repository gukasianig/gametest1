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
    private bool isRespawning;
    public bool IsRespawning => isRespawning;
    private readonly HashSet<Collider> contacts = new HashSet<Collider>();

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
        if (isRespawning || Time.timeScale == 0f) return;
        Health target = other.GetComponentInParent<Health>();
        if (target == null || !target.CompareTag("Enemy") || target.isDead) return;
        foreach (Collider contact in contacts)
            if (contact != null && contact.GetComponentInParent<Health>() == target) { contacts.Add(other); return; }
        contacts.Add(other);

        IDamagable dmg = target;

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
        // Deactivating this GameObject would stop its respawn coroutine.
        isRespawning = true;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Collider[] colliders = GetComponentsInChildren<Collider>();
        bool[] rendererStates = new bool[renderers.Length];
        bool[] colliderStates = new bool[colliders.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            rendererStates[i] = renderers[i].enabled;
            renderers[i].enabled = false;
        }
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }
        yield return new WaitForSeconds(Mathf.Max(0f, respawnTime));
        currentHits = 0;
        contacts.Clear();
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = rendererStates[i];
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
        isRespawning = false;
    }

    void OnTriggerExit(Collider other) { contacts.Remove(other); }
}
