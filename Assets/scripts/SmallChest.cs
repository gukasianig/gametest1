using UnityEngine;

// Малый сундук (10.11.2026): с шансом DropChance падает с элиты. Герой подходит — сундук открывается
// и предлагает три особых улучшения (то же окно, что у события «Защити сундук»). Лежит Life секунд.
public class SmallChest : MonoBehaviour
{
    public const float DropChance = 0.25f, PickupRadius = 1.6f, Life = 60f;
    private static readonly Color Wood = new Color(0.42f, 0.24f, 0.11f), Gold = new Color(1f, 0.78f, 0.22f), Glow = new Color(0.85f, 0.5f, 1f);

    private Transform visual;
    private Light glow;
    private float age, sparkle;

    public static SmallChest Spawn(Vector3 position)
    {
        Vector3 ground = new Vector3(position.x, GroundHeight.At(position), position.z);
        GameObject root = new GameObject("Small chest");
        root.transform.position = ground;
        SmallChest chest = root.AddComponent<SmallChest>();
        GameObject body = new GameObject("Visual");
        body.transform.SetParent(root.transform, false);
        chest.visual = body.transform;
        Part(chest.visual, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.5f, 0.6f), Wood);
        Part(chest.visual, new Vector3(0f, 0.63f, 0f), new Vector3(0.9f, 0.2f, 0.6f), Wood);
        Part(chest.visual, new Vector3(0f, 0.53f, 0f), new Vector3(0.94f, 0.08f, 0.64f), Gold);
        Part(chest.visual, new Vector3(0f, 0.45f, -0.32f), new Vector3(0.16f, 0.2f, 0.05f), Gold);
        // Модель сундука из Blender (tools/blender/make_items.py) вместо кубов, если она установлена.
        ModelSkins.ApplyEnemy(body, "Small chest");
        GameObject lightObject = new GameObject("Chest glow");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = Vector3.up * 1.2f;
        chest.glow = lightObject.AddComponent<Light>();
        chest.glow.type = LightType.Point;
        chest.glow.color = Glow;
        chest.glow.range = 5f;
        chest.glow.intensity = 2f;
        chest.glow.shadows = LightShadows.None;
        EnemyFx.Ring(ground, 2.5f, Glow, 0.7f, 0.3f);
        EnemyFx.LightFlash(ground + Vector3.up * 1.5f, Glow, 8f, 3f, 0.6f);
        GameSession session = GameSession.Instance;
        if (session != null) session.Announce(GameText.Source("smallchest.302"), 2f);
        return chest;
    }

    private static void Part(Transform parent, Vector3 local, Vector3 scale, Color colour)
    {
        GameObject part = EnemyFx.Prim(PrimitiveType.Cube, parent.position, colour);
        part.name = "Chest part";
        part.transform.SetParent(parent, false);
        part.transform.localPosition = local;
        part.transform.localScale = scale;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;
        age += Time.deltaTime;
        visual.localPosition = Vector3.up * (0.15f + 0.12f * Mathf.Sin(age * 3f));
        visual.localRotation = Quaternion.Euler(0f, age * 60f, 0f);
        glow.intensity = 1.6f + 0.8f * Mathf.Sin(age * 5f);
        sparkle -= Time.deltaTime;
        if (sparkle <= 0f)
        {
            sparkle = 0.25f;
            EnemyFx.Puff(transform.position + new Vector3(Random.Range(-0.4f, 0.4f), 0.9f, Random.Range(-0.3f, 0.3f)), Glow, 0.2f, 0.8f, Vector3.up * 1.2f);
        }
        if (age >= Life)
        {
            EnemyFx.Puff(transform.position + Vector3.up * 0.6f, Glow, 1.2f, 0.6f, Vector3.up);
            Destroy(gameObject);
            return;
        }
        GameSession session = GameSession.Instance;
        PlayerProgression hero = PlayerProgression.Instance;
        if (hero == null || hero.Choosing || (session != null && session.State != GameRunState.Playing)) return;
        if (WeaponTargets.Flat(transform.position, hero.transform.position) > PickupRadius) return;
        if (!hero.OfferChestReward()) return;
        EnemyFx.Blobs(transform.position + Vector3.up * 0.7f, Gold, 18, 7f, 0.18f, 0.9f, transform.position.y);
        EnemyFx.LightFlash(transform.position + Vector3.up * 1.5f, Gold, 10f, 4f, 0.8f);
        GameAudio.Play(Sfx.Chest, 0.9f, 0f);
        Destroy(gameObject);
    }
}
