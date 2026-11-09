using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Электрическая дуга: рваная молния с мерцанием, ветвями, мягким свечением, вспышкой и искрами в точках попадания.
// Слои: широкое голубое свечение + тонкая белая жила (аддитивные LineRenderer с мягкой текстурой). Форма пересчитывается
// каждые ~0.035 с (мерцание), толщина и яркость спадают к концу жизни.
public class LightningBolt : MonoBehaviour
{
    private class Strand
    {
        public LineRenderer glow, core;
        public float widthScale = 1f, fadeSpeed = 1f;
    }

    private static Material glowMaterial, coreMaterial;
    private static Texture2D beamTexture, dotTexture;
    private static Material dotMaterial;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static MaterialPropertyBlock block;

    private readonly List<Strand> strands = new List<Strand>();
    private Vector3 from, to;
    private Color color;
    private float width, life, age, nextShape;
    private System.Random random;

    // ---------- создание ----------

    public static LightningBolt Create(Vector3 from, Vector3 to, Color color, float width = 0.4f, float life = 0.24f, int branches = 2)
    {
        EnsureResources();
        var item = new GameObject("Lightning");
        var bolt = item.AddComponent<LightningBolt>();
        bolt.from = from; bolt.to = to; bolt.color = color; bolt.width = width; bolt.life = Mathf.Max(0.05f, life);
        bolt.random = new System.Random(Random.Range(0, int.MaxValue));
        bolt.strands.Add(bolt.NewStrand(item.transform, 1f, 1f));
        float length = Vector3.Distance(from, to);
        for (int i = 0; i < branches && length > 1.5f; i++) bolt.strands.Add(bolt.NewStrand(item.transform, 0.5f, 1.8f));
        bolt.Reshape();
        return bolt;
    }

    // Вспышка + искры + короткий свет в точке попадания; с дугами по телу цели (shock) и без.
    public static void Impact(Vector3 point, Color color, float scale = 1f, bool shock = true)
    {
        EnsureResources();
        var item = new GameObject("Lightning impact");
        item.transform.position = point;
        // Вспышка.
        ParticleSystem flash = MakeParticles(item.transform, "Flash", 1, 0f, 0f, 0.16f, 1.5f * scale, color * 1.2f, 0f);
        // Искры.
        ParticleSystem sparks = MakeParticles(item.transform, "Sparks", Mathf.RoundToInt(14 * scale), 3f * scale, 7f * scale, 0.4f, 0.22f * scale, Color.Lerp(color, Color.white, 0.5f), 0f);
        flash.Play(); sparks.Play();
        var lightObject = new GameObject("Light");
        lightObject.transform.SetParent(item.transform, false);
        lightObject.transform.localPosition = Vector3.up * 0.5f;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = Color.Lerp(color, Color.white, 0.3f); light.range = 5f * scale; light.intensity = 3f; light.shadows = LightShadows.None;
        item.AddComponent<ImpactFade>().flash = light;
        if (shock)
        {
            // 2-3 коротких дуги по цели.
            int arcs = Random.Range(2, 4);
            for (int i = 0; i < arcs; i++)
            {
                Vector2 a = Random.insideUnitCircle.normalized * Random.Range(0.5f, 0.9f);
                Vector3 start = point + new Vector3(a.x, Random.Range(-0.3f, 0.5f), a.y);
                Vector3 end = point + new Vector3(-a.x * Random.Range(0.2f, 1f), Random.Range(-0.2f, 0.6f), -a.y * Random.Range(0.2f, 1f));
                Create(start, end, color, 0.16f * scale, Random.Range(0.14f, 0.26f), 0);
            }
        }
    }

    // ---------- ресурсы ----------

    private static void EnsureResources()
    {
        if (glowMaterial != null) return;
        beamTexture = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++)
        {
            float v = (y + 0.5f) / 64f * 2f - 1f;
            float a = Mathf.Exp(-v * v * 4.5f);
            for (int x = 0; x < 4; x++) beamTexture.SetPixel(x, y, new Color(a, a, a, a));
        }
        beamTexture.Apply();
        dotTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a);
                dotTexture.SetPixel(x, y, new Color(a, a, a, a));
            }
        dotTexture.Apply();
        glowMaterial = Additive(beamTexture);
        coreMaterial = Additive(beamTexture);
        dotMaterial = Additive(dotTexture);
        block = new MaterialPropertyBlock();
    }

    private static Material Additive(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(shader);
        m.SetTexture("_BaseMap", texture);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent + 5;
        return m;
    }

    private static ParticleSystem MakeParticles(Transform parent, string name, int count, float speedMin, float speedMax, float lifetime, float size, Color color, float gravity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.1f; main.loop = false; main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, Mathf.Max(speedMin, speedMax));
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });
        var shape = ps.shape;
        shape.enabled = speedMax > 0f;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;
        var over = ps.sizeOverLifetime;
        over.enabled = true;
        over.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dotMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        // Искры чуть вытягиваем по скорости - выглядят как штрихи.
        if (speedMax > 0f) { renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = 0.06f; renderer.lengthScale = 1.5f; }
        return ps;
    }

    // ---------- молния ----------

    private Strand NewStrand(Transform parent, float widthScale, float fadeSpeed)
    {
        var s = new Strand { widthScale = widthScale, fadeSpeed = fadeSpeed };
        s.glow = NewLine(parent, "Glow", glowMaterial);
        s.core = NewLine(parent, "Core", coreMaterial);
        return s;
    }

    private static LineRenderer NewLine(Transform parent, string name, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = material;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 3;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(0.1f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.2f));   // тоньше на концах: без «квадратиков» на торцах
        return line;
    }

    private float Rand() { return (float)random.NextDouble() * 2f - 1f; }

    // Рваная ломаная from→to: середины смещаются перпендикулярно, амплитуда растёт к середине и падает к концам.
    private List<Vector3> Jagged(Vector3 a, Vector3 b, float amplitudeScale)
    {
        float length = Vector3.Distance(a, b);
        int segments = Mathf.Clamp(Mathf.RoundToInt(length / 0.55f), 4, 28);
        Vector3 dir = (b - a).normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        side.Normalize();
        Vector3 up = Vector3.Cross(side, dir).normalized;
        var points = new List<Vector3> { a };
        float amplitude = Mathf.Min(1.0f, 0.07f + length * 0.07f) * amplitudeScale;
        float carry = 0f;
        for (int i = 1; i < segments; i++)
        {
            float t = i / (float)segments;
            float envelope = Mathf.Sin(t * Mathf.PI);
            carry = Mathf.Lerp(carry, Rand(), 0.65f);   // соседние точки связаны: излом, а не белый шум
            Vector3 p = Vector3.Lerp(a, b, t) + side * (carry * amplitude * envelope) + up * (Rand() * amplitude * 0.35f * envelope);
            points.Add(p);
        }
        points.Add(b);
        return points;
    }

    private void Reshape()
    {
        float length = Vector3.Distance(from, to);
        List<Vector3> main = Jagged(from, to, 1f);
        Apply(strands[0], main);
        for (int i = 1; i < strands.Count; i++)
        {
            int index = random.Next(2, Mathf.Max(3, main.Count - 2));
            Vector3 origin = main[Mathf.Min(index, main.Count - 1)];
            Vector3 dir = (to - from).normalized;
            Quaternion turn = Quaternion.AngleAxis((25f + (float)random.NextDouble() * 35f) * (random.Next(2) == 0 ? -1f : 1f), Vector3.up);
            Vector3 tilt = new Vector3(0f, Rand() * 0.35f, 0f);
            Vector3 end = origin + (turn * dir + tilt).normalized * Mathf.Min(3f, length * (0.18f + (float)random.NextDouble() * 0.15f));
            Apply(strands[i], Jagged(origin, end, 0.8f));
        }
    }

    private static void Apply(Strand s, List<Vector3> points)
    {
        s.glow.positionCount = points.Count;
        s.core.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) { s.glow.SetPosition(i, points[i]); s.core.SetPosition(i, points[i]); }
    }

    private void Update() { Step(Time.deltaTime); }

    // Публичный шаг нужен проверкам в редакторе (там Update не идёт).
    public void Step(float dt)
    {
        age += dt;
        if (age >= life) { Kill(gameObject); return; }
        nextShape -= dt;
        if (nextShape <= 0f) { nextShape = 0.035f; Reshape(); }
        float t = age / life;
        float fade = 1f - t * t;
        float flicker = 0.75f + 0.25f * Mathf.Sin(age * 140f + width * 10f);
        foreach (Strand s in strands)
        {
            float f = Mathf.Clamp01(1f - t * s.fadeSpeed) * fade * flicker;
            float w = width * s.widthScale * (1f - t * 0.6f);
            s.glow.widthMultiplier = w * 2.6f;
            s.core.widthMultiplier = w * 0.8f;
            SetColor(s.glow, color * (1.9f * f));
            SetColor(s.core, Color.Lerp(color, Color.white, 0.8f) * (2.2f * f));
        }
    }

    private static void SetColor(LineRenderer line, Color c)
    {
        line.GetPropertyBlock(block);
        block.SetColor(BaseColor, c);
        line.SetPropertyBlock(block);
    }

    public static void Kill(GameObject go)
    {
        if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
    }

    // Удаление вспышки/искр/света после показа.
    private class ImpactFade : MonoBehaviour
    {
        public Light flash;
        private float age;
        private void Update() { Step(Time.deltaTime); }
        public void Step(float dt)
        {
            age += dt;
            if (flash != null) flash.intensity = Mathf.Max(0f, 3f * (1f - age / 0.18f));
            if (age > 0.7f) Kill(gameObject);
        }
    }
}
