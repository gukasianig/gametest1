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
        // Высота от земли: на аренах с рельефом кристаллы на y=0,55 были под землёй (видны не были, но собирались).
        crystal.transform.position = new Vector3(position.x, GroundHeight.At(position) + 0.55f, position.z);
        crystal.transform.localScale = new Vector3(0.25f, 0.4f, 0.25f);
        crystal.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Collider collider = crystal.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        crystal.GetComponent<Renderer>().sharedMaterial = material;
        // Модель кристалла из Blender (tools/blender/make_items.py) стоит прямо; куб без модели — наклонён, как раньше.
        ModelSkins.ApplyEnemy(crystal, "XP Crystal");
        ExperiencePickup pickup = crystal.AddComponent<ExperiencePickup>();
        pickup.amount = experience;
        pickup.skin = crystal.transform.Find("SkinModel");
        if (pickup.skin == null) crystal.transform.rotation = Quaternion.Euler(0f, 45f, 30f);
        else pickup.skinBase = pickup.skin.localPosition;
        pickup.phase = Random.Range(0f, Mathf.PI * 2f);
        active.Add(pickup);
    }

    // Кристалл медленно вращается и покачивается (только модель; сбор и притяжение двигают сам объект).
    private Transform skin;
    private Vector3 skinBase;
    private float phase;

    private void Update()
    {
        if (skin == null || Time.deltaTime <= 0f) return;
        transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        float scaleY = Mathf.Max(0.0001f, transform.localScale.y);
        skin.localPosition = skinBase + Vector3.up * (Mathf.Sin(Time.time * 2.6f + phase) * 0.06f / scaleY);
    }

    private static float magnetUntil;
    // While active every crystal on the arena flies to the player (the Magnet pickup).
    public static void ActivateMagnet(float seconds) { magnetUntil = Time.time + seconds; }

    public static void CollectNearby(PlayerProgression player, ProgressionSettings settings)
    {
        Vector3 target = player.transform.position;
        target.y = GroundHeight.At(target) + 0.55f;
        float radius = player.CollectionRadius;
        bool magnet = Time.time < magnetUntil;
        bool coop = CoopMode.Active;
        float attraction = magnet ? 100000f : player.AttractionRadius;
        float speed = Mathf.Max(0.1f, settings.attractionSpeed) * (1f + player.Stats.PickupSpeedBonus) * (magnet ? 4f : 1f);
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ExperiencePickup pickup = active[i];
            if (pickup == null) { active.RemoveAt(i); continue; }
            Vector3 position = pickup.transform.position;
            // Кооператив: кристалл летит к ближайшему живому герою, опыт получает он.
            if (coop && CoopMode.NearestAlive(position) != player.transform) continue;
            float distance = (position - target).sqrMagnitude;
            if (distance <= attraction * attraction)
                pickup.transform.position = Vector3.MoveTowards(position, target, speed * Time.deltaTime);
            if ((pickup.transform.position - target).sqrMagnitude > radius * radius) continue;
            int value = Mathf.Max(1, Mathf.RoundToInt(pickup.amount * (1f + player.Stats.ExperienceGain)));
            active.RemoveAt(i);
            Destroy(pickup.gameObject);
            PickupSound();
            player.AddExperience(value);
            if (Time.timeScale == 0f) break;
        }
    }

    // Звук кристалла: серия подряд звучит всё выше (до +60%), пауза 0,7 с сбрасывает серию.
    private static float lastPickup = -10f;
    private static int streak;

    private static void PickupSound()
    {
        streak = Time.time - lastPickup < 0.7f ? Mathf.Min(streak + 1, 12) : 0;
        lastPickup = Time.time;
        GameAudio.PlayAt(Sfx.Pickup, 0.5f, 1f + streak * 0.05f);
    }

    private void OnDestroy() { active.Remove(this); }

    public static void Clear()
    {
        magnetUntil = 0f;
        DropPickup.Clear();
        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();
        if (material != null) Destroy(material);
        material = null;
    }
}
