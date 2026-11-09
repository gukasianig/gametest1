using System.Collections.Generic;
using UnityEngine;

// Короткие эффекты врагов из примитивов: взрыв, брызги, пыль, пятна на земле, ударное кольцо.
// Материалы непрозрачные Unlit (кэш по цвету): «затухание» сделано уменьшением размера и сменой цвета,
// поэтому не нужны прозрачные варианты шейдера и всё остаётся в стиле low-poly.
public static class EnemyFx
{
    static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
    static Shader unlit;

    public static Material Mat(Color color)
    {
        Color32 c = color;
        int key = c.r | c.g << 8 | c.b << 16;
        if (materials.TryGetValue(key, out Material m) && m != null) return m;
        if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit");
        m = new Material(unlit) { color = color, name = "Fx " + key.ToString("X6") };
        materials[key] = m;
        return m;
    }

    // Ступени цвета от a к b (для смены цвета по жизни частицы без новых материалов).
    public static Material[] Ramp(params Color[] keys)
    {
        var list = new Material[keys.Length];
        for (int i = 0; i < keys.Length; i++) list[i] = Mat(keys[i]);
        return list;
    }

    public static GameObject Prim(PrimitiveType type, Vector3 position, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = "Fx";
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; Object.Destroy(collider); }
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = Mat(color);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        go.transform.position = position;
        return go;
    }

    public static FxPiece Piece(PrimitiveType type, Vector3 position, float size, float life, Material[] ramp)
    {
        GameObject go = Prim(type, position, Color.white);
        go.GetComponent<Renderer>().sharedMaterial = ramp[0];
        FxPiece piece = go.AddComponent<FxPiece>();
        piece.life = life;
        piece.size = size;
        piece.ramp = ramp;
        go.transform.localScale = Vector3.one * size * 0.01f;
        return piece;
    }

    // Брызги/осколки: шарики разлетаются по дуге, падают и исчезают.
    public static void Blobs(Vector3 position, Color color, int count, float speed, float size, float life, float groundY)
    {
        Material[] ramp = Ramp(color, color * 0.75f);
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.4f;
            FxPiece p = Piece(PrimitiveType.Sphere, position, size * Random.Range(0.6f, 1.3f), life * Random.Range(0.7f, 1.2f), ramp);
            p.velocity = dir.normalized * speed * Random.Range(0.5f, 1.1f);
            p.gravity = 16f;
            p.groundY = groundY;
            p.grow = 0.08f;
        }
    }

    // Облачко пыли/пара: растёт, поднимается и тает.
    public static void Puff(Vector3 position, Color color, float size, float life, Vector3 velocity)
    {
        FxPiece p = Piece(PrimitiveType.Sphere, position, size, life, Ramp(color, Color.Lerp(color, Color.white, 0.25f)));
        p.velocity = velocity;
        p.drag = 3f;
        p.gravity = -1.2f;
        p.grow = 0.35f;
    }

    // Плоское пятно на земле (лужа, ожог): быстро растекается и медленно сжимается.
    public static void GroundSplat(Vector3 position, float radius, Color color, float life)
    {
        GameObject go = Prim(PrimitiveType.Cylinder, new Vector3(position.x, position.y + 0.02f, position.z), color);
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        FxPiece p = go.AddComponent<FxPiece>();
        p.life = life;
        p.size = radius * 2f;
        p.flat = true;
        p.grow = 0.06f;
        p.ramp = new[] { Mat(color) };
        go.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
    }

    // Расходящееся кольцо по земле.
    public static void Ring(Vector3 position, float radius, Color color, float life, float width = 0.35f)
    {
        var go = new GameObject("Fx ring");
        go.transform.position = position;
        FxRing ring = go.AddComponent<FxRing>();
        ring.Init(radius, color, life, width);
    }

    public static void LightFlash(Vector3 position, Color color, float range, float intensity, float life)
    {
        var go = new GameObject("Fx light");
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        FxLight fade = go.AddComponent<FxLight>();
        fade.life = life;
        fade.start = intensity;
    }

    static readonly Color Hot = new Color(1f, 0.97f, 0.75f);
    static readonly Color Yellow = new Color(1f, 0.85f, 0.25f);
    static readonly Color Orange = new Color(1f, 0.5f, 0.12f);
    static readonly Color Red = new Color(0.8f, 0.2f, 0.08f);
    static readonly Color Soot = new Color(0.22f, 0.18f, 0.18f);
    static readonly Color Smoke = new Color(0.42f, 0.4f, 0.42f);

    // Взрыв: вспышка, огненные шары, переходящие в дым, искры, обломки, ударное кольцо, ожог и свет.
    // power 0..1 масштабирует количество частиц и тряску камеры.
    public static void Explosion(Vector3 position, float radius, float power, bool shake = true)
    {
        float groundY = position.y;
        Vector3 center = position + Vector3.up * 0.5f;

        FxPiece flash = Piece(PrimitiveType.Sphere, center, radius * 0.8f, 0.22f, Ramp(Color.white, Hot, Yellow));
        flash.grow = 0.3f;

        Material[] fire = Ramp(Hot, Yellow, Orange, Orange, Red, Soot, Smoke);
        int fireballs = Mathf.RoundToInt(5 + 9 * power);
        for (int i = 0; i < fireballs; i++)
        {
            Vector3 offset = Random.insideUnitSphere * radius * 0.35f;
            offset.y = Mathf.Abs(offset.y);
            FxPiece p = Piece(PrimitiveType.Sphere, center + offset, radius * Random.Range(0.18f, 0.32f), Random.Range(0.5f, 0.85f), fire);
            Vector3 outward = offset.sqrMagnitude > 0.001f ? offset.normalized : Random.onUnitSphere;
            p.velocity = outward * radius * Random.Range(1.2f, 2.2f) + Vector3.up * Random.Range(1.5f, 3.5f);
            p.drag = 4f;
            p.gravity = -4f;
            p.grow = 0.18f;
        }

        Material[] smoke = Ramp(Soot, Smoke, Smoke, new Color(0.55f, 0.53f, 0.55f));
        for (int i = 0; i < 3 + 4 * power; i++)
        {
            Vector3 offset = Random.insideUnitSphere * radius * 0.4f;
            offset.y = Mathf.Abs(offset.y) + 0.4f;
            FxPiece p = Piece(PrimitiveType.Sphere, center + offset, radius * Random.Range(0.2f, 0.3f), Random.Range(1.1f, 1.6f), smoke);
            p.velocity = new Vector3(offset.x, 2.5f, offset.z);
            p.drag = 1.5f;
            p.gravity = -1.5f;
            p.grow = 0.45f;
        }

        Material[] spark = Ramp(Color.white, Yellow, Orange);
        for (int i = 0; i < 6 + 10 * power; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.7f + 0.2f;
            FxPiece p = Piece(PrimitiveType.Cube, center, Random.Range(0.08f, 0.14f), Random.Range(0.25f, 0.45f), spark);
            p.velocity = dir.normalized * Random.Range(9f, 16f);
            p.gravity = 10f;
            p.drag = 1.5f;
            p.stretch = true;
        }

        Material[] debris = Ramp(new Color(0.3f, 0.22f, 0.2f), Red, Soot);
        for (int i = 0; i < 4 + 8 * power; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) + 0.5f;
            FxPiece p = Piece(PrimitiveType.Cube, center, Random.Range(0.12f, 0.24f), Random.Range(0.9f, 1.4f), debris);
            p.velocity = dir.normalized * Random.Range(5f, 10f);
            p.gravity = 22f;
            p.groundY = groundY + 0.08f;
            p.spin = Random.Range(360f, 900f);
            p.grow = 0.02f;
        }

        Ring(new Vector3(position.x, groundY + 0.1f, position.z), radius * 1.15f, Hot, 0.35f, 0.5f);
        Ring(new Vector3(position.x, groundY + 0.1f, position.z), radius * 0.8f, Orange, 0.5f, 0.3f);
        GroundSplat(new Vector3(position.x, groundY, position.z), radius * 0.42f, new Color(0.1f, 0.08f, 0.08f), 3f);
        LightFlash(center + Vector3.up, new Color(1f, 0.6f, 0.25f), radius * 5f, 6f + 6f * power, 0.4f);
        if (shake) ScreenShake.AddAt(position, 0.35f + 0.45f * power);
    }
}

// Одна частица эффекта: полёт с гравитацией и сопротивлением, отскок от земли, рост, затем сжатие до нуля.
public sealed class FxPiece : MonoBehaviour
{
    public Vector3 velocity;
    public float gravity, drag, spin, life = 1f, size = 1f, grow = 0.15f;
    public float groundY = float.NegativeInfinity;
    public bool flat, stretch;
    public Material[] ramp;
    float age;
    int stage;
    Vector3 spinAxis;
    Renderer cached;

    void Start()
    {
        spinAxis = Random.onUnitSphere;
        cached = GetComponent<Renderer>();
        transform.rotation = flat ? transform.rotation : Random.rotation;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        age += dt;
        float t = age / life;
        if (t >= 1f) { Destroy(gameObject); return; }

        velocity += Vector3.down * gravity * dt;
        if (drag > 0f) velocity /= 1f + drag * dt;
        Vector3 p = transform.position + velocity * dt;
        if (p.y < groundY)
        {
            p.y = groundY;
            velocity = new Vector3(velocity.x * 0.55f, -velocity.y * 0.3f, velocity.z * 0.55f);
            spin *= 0.6f;
        }
        transform.position = p;
        if (spin != 0f) transform.Rotate(spinAxis, spin * dt, Space.World);

        float k = t < grow ? 1f - (1f - t / grow) * (1f - t / grow) : 1f - Mathf.Pow((t - grow) / (1f - grow), 2.2f);
        float s = Mathf.Max(0.001f, size * k);
        if (flat) transform.localScale = new Vector3(s, 0.02f, s);
        else if (stretch && velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(velocity);
            transform.localScale = new Vector3(s * 0.5f, s * 0.5f, s * (1f + velocity.magnitude * 0.12f));
        }
        else transform.localScale = Vector3.one * s;

        if (ramp != null && ramp.Length > 1 && cached != null)
        {
            int next = Mathf.Min(ramp.Length - 1, (int)(t * ramp.Length));
            if (next != stage) { stage = next; cached.sharedMaterial = ramp[stage]; }
        }
    }
}

public sealed class FxRing : MonoBehaviour
{
    LineRenderer line;
    float radius, life, width, age;
    const int Segments = 40;

    public void Init(float radius, Color color, float life, float width)
    {
        this.radius = radius;
        this.life = life;
        this.width = width;
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.sharedMaterial = EnemyFx.Mat(color);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        Apply(0f);
    }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        Apply(age / life);
    }

    void Apply(float t)
    {
        float eased = 1f - (1f - t) * (1f - t) * (1f - t);
        float r = Mathf.Lerp(radius * 0.15f, radius, eased);
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
        }
        line.widthMultiplier = width * (1f - t);
    }
}

public sealed class FxLight : MonoBehaviour
{
    public float life = 0.3f, start = 5f;
    float age;
    Light cached;

    void Start() { cached = GetComponent<Light>(); }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float k = 1f - age / life;
        cached.intensity = start * k * k;
    }
}

// Лёгкая тряска камеры: «травма» накапливается от взрывов рядом с героем и быстро гаснет.
public static class ScreenShake
{
    const float MaxOffset = 0.45f;
    const float Falloff = 20f;   // метров, дальше взрыв камеру не трясёт
    static float trauma;
    static float seed = 13.7f;
    static Transform hero;

    public static void AddAt(Vector3 position, float amount)
    {
        if (!GameSettings.ScreenShakeEnabled) return;
        if (hero == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) hero = player.transform;
        }
        float distance = hero != null ? Vector3.Distance(hero.position, position) : 0f;
        float k = Mathf.Clamp01(1f - distance / Falloff);
        trauma = Mathf.Min(1f, trauma + amount * k);
    }

    public static Vector3 Offset(float dt)
    {
        if (trauma <= 0f) return Vector3.zero;
        trauma = Mathf.Max(0f, trauma - dt * 1.8f);
        seed += dt * 22f;
        float s = trauma * trauma * MaxOffset;
        return new Vector3(Mathf.PerlinNoise(seed, 0.3f) - 0.5f, 0f, Mathf.PerlinNoise(0.7f, seed) - 0.5f) * 2f * s;
    }
}
