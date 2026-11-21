using System.Collections.Generic;
using UnityEngine;

// Короткие эффекты врагов из примитивов: взрыв, брызги, пыль, пятна на земле, ударное кольцо.
// Материалы непрозрачные Unlit (кэш по цвету): «затухание» сделано уменьшением размера и сменой цвета,
// поэтому не нужны прозрачные варианты шейдера и всё остаётся в стиле low-poly. Исключение — пятна GroundSplat (FxSplat, прозрачные, 20.11).
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

    // Полупрозрачный Unlit (обычное альфа-смешение) — для кругов-предупреждений врагов. Как в LightningFx.Additive.
    public static Material SeeThrough(Color color, float alpha)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Fx see-through" };
        color.a = alpha;
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    // Заливка круга-предупреждения: плоский диск на земле растёт от центра до края ровно к моменту удара.
    public static TelegraphFill Fill(Vector3 center, float radius, float seconds, Material material)
    {
        // Диск строит сам TelegraphFill: сетка по высоте рельефа (на склонах и буграх не торчат треугольники).
        GameObject go = Prim(PrimitiveType.Cylinder, center, Color.white);
        go.name = "Telegraph fill";
        go.GetComponent<Renderer>().sharedMaterial = material;
        TelegraphFill fill = go.AddComponent<TelegraphFill>();
        fill.radius = radius;
        fill.seconds = Mathf.Max(0.05f, seconds);
        fill.Begin(center);
        return fill;
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

    // Пул частиц (20.11.2026): сферы и кубы FxPiece (брызги, пыль, взрывы) берутся из стека, а не CreatePrimitive/Destroy.
    // Потолок живых частиц — MaxPieces: при переполнении переиспользуется самая «догоревшая».
    public const int MaxPieces = 96;
    static readonly Stack<FxPiece>[] idlePieces = { new Stack<FxPiece>(), new Stack<FxPiece>() };
    static readonly List<FxPiece> livePieces = new List<FxPiece>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void InitPool()
    {
        ClearPool();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) ClearPool();
    }

    static void ClearPool()
    {
        foreach (Stack<FxPiece> stack in idlePieces) stack.Clear();
        livePieces.Clear();
    }

    public static int LivePieceCount => livePieces.Count;

    static int PoolSlot(PrimitiveType type) { return type == PrimitiveType.Sphere ? 0 : type == PrimitiveType.Cube ? 1 : -1; }

    public static FxPiece Piece(PrimitiveType type, Vector3 position, float size, float life, Material[] ramp)
    {
        int slot = PoolSlot(type);
        if (slot >= 0 && livePieces.Count >= MaxPieces) RecycleOldest();
        FxPiece piece = null;
        if (slot >= 0)
        {
            Stack<FxPiece> idle = idlePieces[slot];
            while (piece == null && idle.Count > 0) piece = idle.Pop();
        }
        if (piece == null)
        {
            GameObject go = Prim(type, position, Color.white);
            piece = go.AddComponent<FxPiece>();
            piece.poolSlot = slot;
        }
        else
        {
            piece.transform.SetPositionAndRotation(position, Quaternion.identity);
            piece.gameObject.SetActive(true);
        }
        piece.Restart(size, life, ramp);
        if (slot >= 0)
        {
            piece.liveIndex = livePieces.Count;
            livePieces.Add(piece);
        }
        return piece;
    }

    static void RecycleOldest()
    {
        FxPiece oldest = null;
        float best = -1f;
        for (int i = livePieces.Count - 1; i >= 0; i--)
        {
            FxPiece p = livePieces[i];
            if (p == null) { Untrack(i); continue; }
            if (p.Progress > best) { best = p.Progress; oldest = p; }
        }
        if (oldest != null) Release(oldest);
    }

    static void Untrack(int index)
    {
        int last = livePieces.Count - 1;
        if (index < last)
        {
            livePieces[index] = livePieces[last];
            if (livePieces[index] != null) livePieces[index].liveIndex = index;
        }
        livePieces.RemoveAt(last);
    }

    internal static void Forget(FxPiece piece)
    {
        int i = piece.liveIndex;
        piece.liveIndex = -1;
        if (i >= 0 && i < livePieces.Count && livePieces[i] == piece) Untrack(i);
    }

    // Частица догорела: в стек своего типа (или Destroy, если тип не пулится).
    internal static void Release(FxPiece piece)
    {
        Forget(piece);
        if (piece.poolSlot < 0) { Object.Destroy(piece.gameObject); return; }
        if (!piece.gameObject.activeSelf) return;
        piece.gameObject.SetActive(false);
        idlePieces[piece.poolSlot].Push(piece);
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
        // На склоне пятно ложится по наклону и чуть поднимается — иначе половина уходила под землю.
        float ground = Mathf.Max(position.y, GroundHeight.At(position)) + 0.03f + GroundHeight.Lift(position, radius);
        // Мягкое полупрозрачное пятно с рваным краем (не глухой чёрный диск): плоский квад, общая текстура, угасание к концу.
        var go = new GameObject("Fx splat");
        go.transform.SetPositionAndRotation(new Vector3(position.x, ground, position.z), GroundHeight.Tilt(position, Random.Range(0f, 360f)));
        go.AddComponent<MeshFilter>().sharedMesh = SplatQuad();
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = SplatMaterial(color);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        FxSplat splat = go.AddComponent<FxSplat>();
        splat.life = Mathf.Max(0.1f, life);
        splat.size = radius * 2f;
        go.transform.localScale = Vector3.one * 0.01f;
    }

    static Mesh splatQuad;
    static Texture2D splatTexture;
    static readonly Dictionary<int, Material> splatMaterials = new Dictionary<int, Material>();

    static Mesh SplatQuad()
    {
        if (splatQuad != null) return splatQuad;
        splatQuad = new Mesh { name = "Fx splat quad" };
        splatQuad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
        splatQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        splatQuad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        splatQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        splatQuad.RecalculateBounds();
        return splatQuad;
    }

    // Цвет — в материале, форма и альфа (≤0.7 в центре, мягкий рваный край) — в общей текстуре.
    static Material SplatMaterial(Color color)
    {
        Color32 c = color;
        int key = c.r | c.g << 8 | c.b << 16;
        if (splatMaterials.TryGetValue(key, out Material m) && m != null) return m;
        color.a = 1f;
        m = SeeThrough(color, 1f);
        m.name = "Fx splat";
        m.SetTexture("_BaseMap", SplatTexture());
        m.SetFloat("_Cull", 0f);
        splatMaterials[key] = m;
        return m;
    }

    static Texture2D SplatTexture()
    {
        if (splatTexture != null) return splatTexture;
        const int size = 128;
        splatTexture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Fx splat", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy), angle = Mathf.Atan2(dy, dx);
                // Неровный край: несколько синусоид по углу + мелкий шум.
                float edge = 0.78f + 0.07f * Mathf.Sin(angle * 5f + 1.3f) + 0.05f * Mathf.Sin(angle * 11f + 0.4f)
                    + 0.04f * (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f);
                float body = Mathf.Clamp01((edge - r) / 0.22f);
                float alpha = 0.7f * body * body * (0.75f + 0.25f * Mathf.PerlinNoise(x * 0.05f + 7f, y * 0.05f + 3f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
            }
        splatTexture.SetPixels32(pixels);
        splatTexture.Apply(true, true);
        return splatTexture;
    }

    // Расходящееся кольцо по земле.
    public static void Ring(Vector3 position, float radius, Color color, float life, float width = 0.35f)
    {
        var go = new GameObject("Fx ring");
        position.y = Mathf.Max(position.y, GroundHeight.At(position) + 0.08f) + GroundHeight.Lift(position, radius);
        go.transform.SetPositionAndRotation(position, GroundHeight.Tilt(position));
        FxRing ring = go.AddComponent<FxRing>();
        ring.Init(radius, color, life, width);
    }

    // Вспышка выстрела у дула: свет, дымок по направлению, искры. scale ~1 для обычного, больше для тяжёлого.
    public static void MuzzleFlash(Vector3 position, Vector3 direction, Color color, float scale = 1f)
    {
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        LightFlash(position, color, 3.5f * scale, 3f * scale, 0.12f);
        Puff(position, Color.Lerp(color, Color.white, 0.6f), 0.35f * scale, 0.2f, direction * 2.5f);
        Blobs(position, color, Mathf.RoundToInt(5 * scale), 3.5f, 0.07f, 0.25f, GroundHeight.At(position));
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
    bool begun;
    // Пул EnemyFx: тип (0 сфера, 1 куб, -1 не пулится) и место в списке живых частиц.
    internal int poolSlot = -1;
    internal int liveIndex = -1;

    internal float Progress => life > 0f ? age / life : 1f;

    // Полный сброс при выдаче из пула: все поля, которые выставляют Blobs/Puff/Explosion и другие вызовы Piece.
    internal void Restart(float pieceSize, float pieceLife, Material[] pieceRamp)
    {
        velocity = Vector3.zero;
        gravity = drag = spin = 0f;
        grow = 0.15f;
        groundY = float.NegativeInfinity;
        flat = stretch = false;
        life = pieceLife;
        size = pieceSize;
        ramp = pieceRamp;
        age = 0f;
        stage = 0;
        begun = false;
        if (cached == null) cached = GetComponent<Renderer>();
        if (cached != null && pieceRamp != null && pieceRamp.Length > 0) cached.sharedMaterial = pieceRamp[0];
        transform.localScale = Vector3.one * pieceSize * 0.01f;
    }

    // Как прежний Start: вызывается в первом кадре после выдачи, когда вызывающий уже выставил flat/spin.
    void Begin()
    {
        begun = true;
        spinAxis = Random.onUnitSphere;
        if (cached == null) cached = GetComponent<Renderer>();
        transform.rotation = flat ? transform.rotation : Random.rotation;
    }

    void Start()
    {
        if (!begun) Begin();
    }

    void OnDestroy() { EnemyFx.Forget(this); }

    void Update()
    {
        if (!begun) Begin();
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        age += dt;
        float t = age / life;
        if (t >= 1f) { EnemyFx.Release(this); return; }

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

// Растущая заливка круга-предупреждения (EnemyFx.Fill). Время идёт только в бою (пауза и окно улучшений её держат).
public sealed class TelegraphFill : MonoBehaviour
{
    public float radius, seconds;
    float age;
    Mesh mesh;
    Vector3 centre;
    const int Segments = 32, Rings = 6;
    const float Offset = 0.08f;   // подъём над рельефом: диск не мерцает и не режется землёй

    // Диск повторяет рельеф (каждая вершина берёт высоту земли), поэтому не торчит треугольниками и не уходит в склон.
    public void Begin(Vector3 centreGround)
    {
        centre = centreGround;
        mesh = new Mesh { name = "Telegraph fill" };
        var triangles = new int[Segments * (Rings * 2 - 1) * 3];
        int t = 0;
        for (int s = 0; s < Segments; s++)
        {
            int n = (s + 1) % Segments;
            triangles[t++] = 0; triangles[t++] = 1 + n; triangles[t++] = 1 + s;
            for (int r = 1; r < Rings; r++)
            {
                int a = 1 + (r - 1) * Segments, b = 1 + r * Segments;
                triangles[t++] = a + s; triangles[t++] = a + n; triangles[t++] = b + s;
                triangles[t++] = a + n; triangles[t++] = b + n; triangles[t++] = b + s;
            }
        }
        mesh.vertices = new Vector3[1 + Segments * Rings];
        mesh.triangles = triangles;
        GetComponent<MeshFilter>().sharedMesh = mesh;
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
        Rebuild(0.05f);
    }

    void Rebuild(float t)
    {
        float size = radius * Mathf.Lerp(0.05f, 1f, t);
        var v = mesh.vertices;
        v[0] = Ground(centre.x, centre.z);
        for (int r = 1; r <= Rings; r++)
            for (int s = 0; s < Segments; s++)
            {
                float a = s * Mathf.PI * 2f / Segments, d = size * r / Rings;
                v[1 + (r - 1) * Segments + s] = Ground(centre.x + Mathf.Cos(a) * d, centre.z + Mathf.Sin(a) * d);
            }
        mesh.vertices = v;
        mesh.RecalculateBounds();
    }

    static Vector3 Ground(float x, float z)
    {
        var p = new Vector3(x, 0f, z);
        p.y = GroundHeight.At(p) + Offset;
        return p;
    }

    void Update()
    {
        if (mesh == null || Time.deltaTime <= 0f) return;
        age += Time.deltaTime;
        Rebuild(Mathf.Clamp01(age / seconds));
    }

    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}

// Пятно на земле: быстро раскрывается (0,08 с), держится и плавно гаснет за последние 40% жизни.
public class FxSplat : MonoBehaviour
{
    public float life = 2f, size = 1f;
    float age;
    Renderer cached;
    MaterialPropertyBlock block;
    Color tint;

    void Start()
    {
        cached = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        tint = cached.sharedMaterial.GetColor("_BaseColor");
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float open = Mathf.Clamp01(age / 0.08f);
        float s = size * (0.6f + 0.4f * (1f - (1f - open) * (1f - open)));
        transform.localScale = new Vector3(s, 1f, s);
        float t = age / life;
        tint.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        block.SetColor("_BaseColor", tint);
        cached.SetPropertyBlock(block);
    }
}
