using System.Collections.Generic;
using UnityEngine;

public enum PickupKind { Health, Magnet }

// Items that enemies sometimes leave behind: a repair kit (heals) and a magnet (pulls every crystal on the arena).
public class DropPickup : MonoBehaviour
{
    public const float Lifetime = 25f;
    public const float HealthChance = 0.04f, MagnetChance = 0.012f;
    private static readonly List<DropPickup> active = new List<DropPickup>();
    public static int ActiveCount { get { return active.Count; } }

    public PickupKind Kind { get; private set; }
    private float born, baseHeight;

    // Normal enemies rarely drop something; elites always drop a repair kit.
    public static void TryDrop(Vector3 position, bool elite)
    {
        GameSession session = GameSession.Instance;
        if (session != null && session.State != GameRunState.Playing) return;
        if (elite) { Spawn(position, PickupKind.Health); return; }
        float roll = Random.value / (1f + Mathf.Max(0f, CombatStats.DropLuck));
        if (roll < HealthChance) Spawn(position, PickupKind.Health);
        else if (roll < HealthChance + MagnetChance) Spawn(position, PickupKind.Magnet);
    }

    public static DropPickup Spawn(Vector3 position, PickupKind kind)
    {
        GameObject item = kind == PickupKind.Health ? Kit() : MagnetShape();
        item.name = kind == PickupKind.Health ? "Repair kit" : "Magnet";
        item.transform.position = new Vector3(position.x, 0.8f, position.z);
        ModelSkins.ApplyEnemy(item, kind == PickupKind.Health ? "Repair kit" : "Magnet");
        DropPickup pickup = item.AddComponent<DropPickup>();
        pickup.Kind = kind;
        pickup.born = Time.time;
        pickup.baseHeight = 0.8f;
        active.Add(pickup);
        return pickup;
    }

    private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 local, Vector3 scale, Color colour)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = local;
        part.transform.localScale = scale;
        Renderer renderer = part.GetComponent<Renderer>();
        if (ArenaLayout.Instance != null) renderer.sharedMaterial = ArenaLayout.Instance.Material(colour);
        return part;
    }

    private static GameObject Kit()
    {
        GameObject root = new GameObject("Repair kit");
        Part(PrimitiveType.Cube, "Box", root.transform, Vector3.zero, new Vector3(0.7f, 0.7f, 0.7f), new Color(0.9f, 0.92f, 0.9f));
        Part(PrimitiveType.Cube, "Cross H", root.transform, new Vector3(0f, 0f, 0.36f), new Vector3(0.45f, 0.15f, 0.05f), new Color(0.85f, 0.15f, 0.15f));
        Part(PrimitiveType.Cube, "Cross V", root.transform, new Vector3(0f, 0f, 0.36f), new Vector3(0.15f, 0.45f, 0.05f), new Color(0.85f, 0.15f, 0.15f));
        Part(PrimitiveType.Cube, "Cross H back", root.transform, new Vector3(0f, 0f, -0.36f), new Vector3(0.45f, 0.15f, 0.05f), new Color(0.85f, 0.15f, 0.15f));
        Part(PrimitiveType.Cube, "Cross V back", root.transform, new Vector3(0f, 0f, -0.36f), new Vector3(0.15f, 0.45f, 0.05f), new Color(0.85f, 0.15f, 0.15f));
        return root;
    }

    private static GameObject MagnetShape()
    {
        GameObject root = new GameObject("Magnet");
        Color blue = new Color(0.25f, 0.45f, 1f), red = new Color(0.95f, 0.2f, 0.2f);
        Part(PrimitiveType.Cube, "Back", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.75f, 0.25f, 0.25f), blue);
        Part(PrimitiveType.Cube, "Left arm", root.transform, new Vector3(-0.25f, -0.05f, 0f), new Vector3(0.25f, 0.4f, 0.25f), blue);
        Part(PrimitiveType.Cube, "Right arm", root.transform, new Vector3(0.25f, -0.05f, 0f), new Vector3(0.25f, 0.4f, 0.25f), blue);
        Part(PrimitiveType.Cube, "Left tip", root.transform, new Vector3(-0.25f, -0.3f, 0f), new Vector3(0.25f, 0.15f, 0.25f), red);
        Part(PrimitiveType.Cube, "Right tip", root.transform, new Vector3(0.25f, -0.3f, 0f), new Vector3(0.25f, 0.15f, 0.25f), red);
        return root;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        float age = Time.time - born;
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        transform.position = new Vector3(transform.position.x, baseHeight + Mathf.Sin(age * 3f) * 0.12f, transform.position.z);
        if (age > Lifetime - 5f)
        {
            // С моделью мигает только она (исходные части-примитивы остаются скрытыми).
            Transform skin = transform.Find("SkinModel");
            foreach (Renderer renderer in (skin != null ? skin : transform).GetComponentsInChildren<Renderer>()) renderer.enabled = Mathf.FloorToInt(age * 6f) % 2 == 0;
        }
        if (age >= Lifetime) { Destroy(gameObject); return; }
        PlayerProgression player = PlayerProgression.Instance;
        if (player == null) return;
        float reach = Mathf.Max(1.3f, player.CollectionRadius);
        if (WeaponTargets.Flat(transform.position, player.transform.position) > reach) return;
        Collect(player);
    }

    private void Collect(PlayerProgression player)
    {
        GameSession session = GameSession.Instance;
        if (Kind == PickupKind.Health)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null) return;
            // A full-health player leaves the kit for later (it still expires).
            if (health.CurrentHP >= health.maxHP) return;
            int restored = health.Heal(Mathf.Max(15, Mathf.RoundToInt(health.maxHP * 0.25f)));
            if (session != null) session.Announce("Ремонт  +" + restored + " HP", 1.5f);
        }
        else
        {
            ExperiencePickup.ActivateMagnet(3f);
            if (session != null) session.Announce("МАГНИТ — весь опыт летит к вам", 2f);
        }
        Destroy(gameObject);
    }

    private void OnDestroy() { active.Remove(this); }

    public static void Clear()
    {
        for (int i = active.Count - 1; i >= 0; i--) if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();
    }
}
