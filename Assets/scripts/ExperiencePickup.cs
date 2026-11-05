using System.Collections.Generic;
using UnityEngine;

// Updated centrally by the player: no physics triggers or scene searches per crystal.
public class ExperiencePickup : MonoBehaviour
{
    private static readonly List<ExperiencePickup> active = new List<ExperiencePickup>();
    private static Material material;
    private int amount;

    public static void Spawn(Vector3 position, int experience)
    {
        if (experience <= 0 || PlayerProgression.Instance == null) return;
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            material = new Material(shader);
            material.SetColor("_BaseColor", new Color(0.1f, 0.9f, 1f));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.05f, 0.45f, 0.6f));
        }
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.name = "Experience Crystal";
        crystal.transform.position = new Vector3(position.x, 0.55f, position.z);
        crystal.transform.localScale = new Vector3(0.25f, 0.4f, 0.25f);
        crystal.transform.rotation = Quaternion.Euler(0f, 45f, 30f);
        Collider collider = crystal.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        crystal.GetComponent<Renderer>().sharedMaterial = material;
        ExperiencePickup pickup = crystal.AddComponent<ExperiencePickup>();
        pickup.amount = experience;
        active.Add(pickup);
    }

    public static void CollectNearby(PlayerProgression player, ProgressionSettings settings)
    {
        Vector3 target = player.transform.position;
        target.y = 0.55f;
        float radius = player.CollectionRadius;
        float attraction = player.AttractionRadius;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ExperiencePickup pickup = active[i];
            if (pickup == null) { active.RemoveAt(i); continue; }
            Vector3 position = pickup.transform.position;
            float distance = (position - target).sqrMagnitude;
            if (distance <= attraction * attraction)
                pickup.transform.position = Vector3.MoveTowards(position, target,
                    Mathf.Max(0.1f, settings.attractionSpeed) * Time.deltaTime);
            if ((pickup.transform.position - target).sqrMagnitude > radius * radius) continue;
            int value = pickup.amount;
            active.RemoveAt(i);
            Destroy(pickup.gameObject);
            player.AddExperience(value);
            if (Time.timeScale == 0f) break;
        }
    }

    private void OnDestroy() { active.Remove(this); }

    public static void Clear()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();
        if (material != null) Destroy(material);
        material = null;
    }
}
