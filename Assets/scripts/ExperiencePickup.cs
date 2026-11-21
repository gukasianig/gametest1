using System.Collections.Generic;
using UnityEngine;

// Updated centrally by the player: no physics triggers or scene searches per crystal.
public class ExperiencePickup : MonoBehaviour
{
    private static readonly List<ExperiencePickup> active = new List<ExperiencePickup>();
    private static Material material;
    private int amount;

    // H08 (20.11.2026): новый кристалл рядом с лежащим (не летящим к игроку) не создаётся — опыт добавляется к лежащему.
    private const float MergeRadius = 1.8f;
    private const float MaxWeightScale = 1.6f;
    private static readonly Vector3 BaseScale = new Vector3(0.25f, 0.4f, 0.25f);
    // Оттенок по суммарному опыту слитого кристалла: 0 — обычный голубой, 1 — фиолетовый, 2 — золотой.
    private static readonly int[] TierAmount = { 0, 40, 120 };
    private static readonly float[] TierHue = { 0f, 0.78f, 0.12f };
    private static readonly float[] TierGlow = { 0f, 0.55f, 0.75f };
    private static readonly Dictionary<Material, Material>[] tierMaterials =
        { null, new Dictionary<Material, Material>(), new Dictionary<Material, Material>() };
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private int pieces = 1, tier;
    private bool attracted;
    private Renderer[] tintRenderers;
    private Material[][] originalMaterials;

    // merge = false — для наградных россыпей (события, Громила, сундук): там кристаллы раскиданы нарочно.
    public static void Spawn(Vector3 position, int experience, bool merge = true)
    {
        if (experience <= 0 || PlayerProgression.Instance == null) return;
        if (merge)
        {
            ExperiencePickup into = MergeTarget(position);
            if (into != null) { into.Absorb(experience); return; }
        }
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
        crystal.transform.localScale = BaseScale;
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

    // Ближайший лежащий кристалл в радиусе слияния (по горизонтали).
    private static ExperiencePickup MergeTarget(Vector3 position)
    {
        ExperiencePickup best = null;
        float bestDistance = MergeRadius * MergeRadius;
        for (int i = 0; i < active.Count; i++)
        {
            ExperiencePickup pickup = active[i];
            if (pickup == null || pickup.attracted) continue;
            Vector3 offset = pickup.transform.position - position;
            float distance = offset.x * offset.x + offset.z * offset.z;
            if (distance <= bestDistance) { bestDistance = distance; best = pickup; }
        }
        return best;
    }

    private void Absorb(int experience)
    {
        amount += experience;
        pieces++;
        // «Вес»: +12% размера за каждое удвоение числа слитых кристаллов, не больше ×1,6 (не закрывать врагов).
        transform.localScale = BaseScale * Mathf.Min(MaxWeightScale, 1f + 0.12f * Mathf.Log(pieces, 2f));
        int newTier = 0;
        for (int i = TierAmount.Length - 1; i > 0; i--)
            if (amount >= TierAmount[i]) { newTier = i; break; }
        if (newTier != tier) ApplyTier(newTier);
        // Небольшой повторный «пружинистый» толчок, чтобы слияние было заметно.
        age = Mathf.Min(age, 0.15f);
    }

    private void ApplyTier(int newTier)
    {
        if (tintRenderers == null)
        {
            // Пока оттенок обычный, у рендереров стоят исходные материалы — запоминаем их один раз.
            tintRenderers = GetComponentsInChildren<Renderer>(true);
            originalMaterials = new Material[tintRenderers.Length][];
            for (int i = 0; i < tintRenderers.Length; i++) originalMaterials[i] = tintRenderers[i].sharedMaterials;
        }
        tier = newTier;
        for (int i = 0; i < tintRenderers.Length; i++)
        {
            if (tintRenderers[i] == null) continue;
            Material[] source = originalMaterials[i];
            Material[] tinted = new Material[source.Length];
            for (int m = 0; m < source.Length; m++) tinted[m] = TierMaterial(source[m], tier);
            tintRenderers[i].sharedMaterials = tinted;
        }
    }

    // Материалы оттенков общие для всех кристаллов (кэш по исходному материалу) — батчинг не ломается.
    private static Material TierMaterial(Material source, int tier)
    {
        if (source == null || tier == 0) return source;
        Dictionary<Material, Material> cache = tierMaterials[tier];
        if (cache.TryGetValue(source, out Material tinted) && tinted != null) return tinted;
        tinted = new Material(source) { name = source.name + " (XP tier " + tier + ")" };
        float hue = TierHue[tier];
        if (tinted.HasProperty(BaseColorId))
        {
            Color color = tinted.GetColor(BaseColorId);
            Color.RGBToHSV(color, out _, out float saturation, out float value);
            Color shifted = Color.HSVToRGB(hue, Mathf.Max(saturation, 0.55f), value);
            shifted.a = color.a;
            tinted.SetColor(BaseColorId, shifted);
        }
        if (tinted.HasProperty(EmissionColorId))
        {
            tinted.EnableKeyword("_EMISSION");
            tinted.SetColor(EmissionColorId, Color.HSVToRGB(hue, 0.85f, 1f) * TierGlow[tier]);
        }
        cache[source] = tinted;
        return tinted;
    }

    // Кристалл медленно вращается и покачивается (только модель; сбор и притяжение двигают сам объект).
    private Transform skin;
    private Vector3 skinBase, skinScale;
    private float phase, age;

    private void Update()
    {
        if (skin == null || Time.deltaTime <= 0f) return;
        age += Time.deltaTime;
        transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
        float scaleY = Mathf.Max(0.0001f, transform.localScale.y);
        // 19.11.2026: кристалл выскакивает из врага (подскок и пружинистый масштаб), потом покачивается.
        if (skinScale == Vector3.zero) skinScale = skin.localScale;
        float pop = Mathf.Clamp01(age / 0.3f);
        float spring = 1f + 2.2f * Mathf.Pow(pop - 1f, 3f) + 1.2f * Mathf.Pow(pop - 1f, 2f);
        skin.localScale = skinScale * Mathf.Max(0.01f, spring);
        float hop = pop < 1f ? Mathf.Sin(pop * Mathf.PI) * 0.45f : 0f;
        skin.localPosition = skinBase + Vector3.up * ((hop + Mathf.Sin(Time.time * 2.6f + phase) * 0.06f) / scaleY);
    }

    // Искра при сборе: не чаще раза в 0,05 с (кристаллов бывает очень много).
    private static float nextSparkle;
    private static void Sparkle(Vector3 at)
    {
        if (Time.time < nextSparkle) return;
        nextSparkle = Time.time + 0.05f;
        EnemyFx.Puff(at + Vector3.up * 0.3f, new Color(0.4f, 0.95f, 1f), 0.13f, 0.28f, Vector3.up * 1.6f);
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
            // Летящий к игроку кристалл не принимает слияний (иначе опыт «догонял» бы игрока с задержкой).
            pickup.attracted = distance <= attraction * attraction;
            if (pickup.attracted)
                pickup.transform.position = Vector3.MoveTowards(position, target, speed * Time.deltaTime);
            if ((pickup.transform.position - target).sqrMagnitude > radius * radius) continue;
            // Слитый кристалл хранит точную сумму опыта; бонус к опыту применяется к сумме, минимум 1 за каждый слитый.
            int value = Mathf.Max(pickup.pieces, Mathf.RoundToInt(pickup.amount * (1f + player.Stats.ExperienceGain)));
            active.RemoveAt(i);
            Sparkle(pickup.transform.position);
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
        for (int t = 1; t < tierMaterials.Length; t++)
        {
            foreach (Material tinted in tierMaterials[t].Values) if (tinted != null) Destroy(tinted);
            tierMaterials[t].Clear();
        }
        if (material != null) Destroy(material);
        material = null;
    }
}
