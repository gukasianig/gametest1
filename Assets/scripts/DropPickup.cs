using System.Collections.Generic;
using UnityEngine;

public enum PickupKind { Health, Magnet }

// Items that enemies sometimes leave behind: a repair kit (heals) and a magnet (pulls every crystal on the arena).
public class DropPickup : MonoBehaviour
{
    public const float Lifetime = 25f;
    // Баланс 19.11.2026: аптечек было слишком много (4% с каждого врага + с каждой элиты).
    public const float HealthChance = 0.012f, MagnetChance = 0.01f, EliteHealthChance = 0.35f, EliteMagnetChance = 0.2f;
    private static readonly List<DropPickup> active = new List<DropPickup>();
    public static int ActiveCount { get { return active.Count; } }

    public PickupKind Kind { get; private set; }
    private float born, baseHeight;

    // Normal enemies rarely drop something; elites always drop a repair kit.
    public static void TryDrop(Vector3 position, bool elite)
    {
        GameSession session = GameSession.Instance;
        if (session != null && session.State != GameRunState.Playing) return;
        // Испытание «Без аптечек»: элита роняет магнит вместо аптечки, обычные враги — только магнит.
        bool noKits = RunModifiers.NoHealthKits || CombatStats.GambleActive;   // карточка «Азарт»
        // Элита иногда роняет малый сундук с особой наградой (10.11.2026).
        // Удача: малый сундук и магнит — в полную силу, аптечки — вполовину (чтобы лечение не захлёстывало).
        float luck = Mathf.Max(0f, CombatStats.DropLuck);
        float kitChance = (elite ? EliteHealthChance : HealthChance) * (1f + 0.5f * luck);
        float magnetChance = (elite ? EliteMagnetChance : MagnetChance) * (1f + luck);
        if (elite && Random.value < Mathf.Min(0.6f, SmallChest.DropChance * (1f + luck))) SmallChest.Spawn(position);
        float roll = Random.value;
        if (roll < kitChance) Spawn(position, noKits ? PickupKind.Magnet : PickupKind.Health);
        else if (roll < kitChance + magnetChance) Spawn(position, PickupKind.Magnet);
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

    private Color Accent => Kind == PickupKind.Health ? new Color(0.45f, 1f, 0.55f) : new Color(0.35f, 0.6f, 1f);
    private float nextRing;

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        float age = Time.time - born;
        transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        // 19.11.2026: появление с подскоком и пружинистым масштабом, затем мягкий пульс кольца на земле.
        float pop = Mathf.Clamp01(age / 0.35f);
        float spring = 1f + 2.2f * Mathf.Pow(pop - 1f, 3f) + 1.2f * Mathf.Pow(pop - 1f, 2f);
        transform.localScale = Vector3.one * Mathf.Max(0.01f, spring);
        float hop = age < 0.35f ? Mathf.Sin(pop * Mathf.PI) * 0.6f : 0f;
        float ground = GroundHeight.At(transform.position);
        transform.position = new Vector3(transform.position.x, ground + baseHeight + hop + Mathf.Sin(age * 3f) * 0.12f, transform.position.z);
        if (Time.time >= nextRing && age < Lifetime - 5f)
        {
            nextRing = Time.time + 1.4f;
            EnemyFx.Ring(new Vector3(transform.position.x, ground + 0.05f, transform.position.z), 0.9f, Accent, 0.6f, 0.07f);
        }
        if (age > Lifetime - 5f)
        {
            // С моделью мигает только она (исходные части-примитивы остаются скрытыми).
            Transform skin = transform.Find("SkinModel");
            foreach (Renderer renderer in (skin != null ? skin : transform).GetComponentsInChildren<Renderer>()) renderer.enabled = Mathf.FloorToInt(age * 6f) % 2 == 0;
        }
        if (age >= Lifetime) { Destroy(gameObject); return; }
        // Подбирает любой живой герой рядом (кооператив); в одиночной игре — единственный герой.
        foreach (PlayerHealth hero in CoopMode.Heroes)
        {
            if (hero == null || hero.IsDead) continue;
            PlayerProgression player = hero.GetComponent<PlayerProgression>();
            if (player == null) continue;
            float reach = Mathf.Max(1.3f, player.CollectionRadius);
            if (WeaponTargets.Flat(transform.position, player.transform.position) > reach) continue;
            if (Collect(player)) return;
        }
    }

    private bool Collect(PlayerProgression player)
    {
        GameSession session = GameSession.Instance;
        if (Kind == PickupKind.Health)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null) return false;
            // A full-health player leaves the kit for later (it still expires).
            if (health.CurrentHP >= health.maxHP) return false;
            CollectFx(player.transform.position, 2.2f);
            int restored = health.Heal(Mathf.Max(15, Mathf.RoundToInt(health.maxHP * 0.25f)));
            if (session != null) session.Announce(GameText.Source("droppickup.115") + restored + " HP", 1.5f);
        }
        else
        {
            ExperiencePickup.ActivateMagnet(3f);
            CollectFx(player.transform.position, 7f);
            if (session != null) session.Announce(GameText.Source("droppickup.116"), 2f);
        }
        Destroy(gameObject);
        return true;
    }

    // Подбор: вспышка, кольцо у ног героя (у магнита — широкая волна) и брызги цвета предмета.
    private void CollectFx(Vector3 hero, float ring)
    {
        Vector3 feet = new Vector3(hero.x, GroundHeight.At(hero) + 0.05f, hero.z);
        Color color = Accent;
        EnemyFx.Ring(feet, ring, color, 0.45f, 0.16f);
        EnemyFx.LightFlash(transform.position + Vector3.up * 0.4f, color, 5f, 2f, 0.3f);
        EnemyFx.Blobs(transform.position, color, 10, 4f, 0.12f, 0.6f, feet.y);
        for (int i = 0; i < 4; i++)
            EnemyFx.Puff(feet + new Vector3(Mathf.Cos(i * 1.57f) * 0.5f, 0.6f, Mathf.Sin(i * 1.57f) * 0.5f), color, 0.14f, 0.5f, Vector3.up * 1.8f);
    }

    private void OnDestroy() { active.Remove(this); }

    public static void Clear()
    {
        for (int i = active.Count - 1; i >= 0; i--) if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();
    }
}
