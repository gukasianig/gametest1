using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Animates the GroundFireKit on a level surface. The caller owns damage and lifetime.</summary>
public sealed class GroundFireVisual : MonoBehaviour
{
    [SerializeField] private GameObject kit;
    [SerializeField, Range(1f, 6f)] private float radius = 2.5f;
    [SerializeField] private int seed = 506;
    [SerializeField] private Vector2 wind = new Vector2(1f, 0.25f);

    private const float MaxRadius = 6f;
    private const float RiseTime = 0.3f;
    private const float ScorchTime = 2.5f;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly string[] FlameNames =
    {
        "Fire_Flame_Large_A", "Fire_Flame_Large_B",
        "Fire_Flame_Mid_A", "Fire_Flame_Mid_B", "Fire_Flame_Mid_C",
        "Fire_Flame_Small_A", "Fire_Flame_Small_B"
    };

    private sealed class Site
    {
        public Vector2 Position;
        public float Phase;
        public float Frequency;
        public float Strength;
        public int Variant = -1;
        public MeshFilter Flame;
        public MeshRenderer Coals;
    }

    private sealed class Spark
    {
        public MeshRenderer Renderer;
        public Vector3 Origin;
        public Vector2 Drift;
        public float Age;
        public float Life;
        public float Size;
        public bool Ash;
    }

    private readonly List<Site> sites = new List<Site>();
    private readonly List<Site> litSites = new List<Site>();
    private readonly List<Transform> rim = new List<Transform>();
    private readonly List<Spark> sparks = new List<Spark>();
    private readonly Dictionary<string, MeshFilter> parts = new Dictionary<string, MeshFilter>();
    private System.Random random;
    private MaterialPropertyBlock glow;
    private Transform visualRoot;
    private Transform scorch;
    private Mesh rimMesh;
    private Vector3[] rimVertices;
    private Vector2[] rimPolar;
    private float displayedRadius;
    private float elapsed;
    private float extinguishedFor;
    private bool burning = true;

    public void SetRadius(float metres)
    {
        radius = Mathf.Clamp(metres, 1f, MaxRadius);
    }

    public void SetBurning(bool value)
    {
        if (burning == value)
            return;

        burning = value;
        extinguishedFor = 0f;
        if (burning && visualRoot != null)
            visualRoot.gameObject.SetActive(true);
    }

    private void Awake()
    {
        if (kit != null)
        {
            foreach (MeshFilter part in kit.GetComponentsInChildren<MeshFilter>(true))
                parts.Add(part.name, part);
        }
        if (!parts.TryGetValue("Fire_Rim_Segment", out MeshFilter source) || !source.sharedMesh.isReadable)
        {
            Debug.LogError("GroundFireVisual requires the readable GroundFireKit FBX.", this);
            enabled = false;
            return;
        }

        random = new System.Random(seed);
        glow = new MaterialPropertyBlock();
        visualRoot = new GameObject("Fire VFX").transform;
        visualRoot.SetParent(transform, false);
        scorch = CreatePiece("Fire_Base").transform;

        rimMesh = Instantiate(source.sharedMesh);
        rimMesh.MarkDynamic();
        rimVertices = rimMesh.vertices;
        rimPolar = new Vector2[rimVertices.Length];
        // FBX import can mirror the horizontal axis. Normalize the arc to +X.
        float direction = rimMesh.bounds.center.x < 0f ? 1f : -1f;
        for (int i = 0; i < rimVertices.Length; i++)
        {
            Vector3 vertex = rimVertices[i];
            Vector2 fromCentre = new Vector2(1f + vertex.x * direction, vertex.z * direction);
            rimPolar[i] = new Vector2(Mathf.Atan2(fromCentre.y, fromCentre.x), fromCentre.magnitude - 1f);
        }

        // A jittered triangular grid keeps the spacing and position stable during expansion.
        for (int row = -7; row <= 7; row++)
        {
            for (int column = -7; column <= 7; column++)
            {
                Vector2 position = new Vector2(1.025f * (column + 0.5f * (row & 1)), 0.888f * row);
                position += new Vector2(Range(-0.24f, 0.24f), Range(-0.24f, 0.24f));
                if (position.sqrMagnitude < MaxRadius * MaxRadius)
                {
                    sites.Add(new Site
                    {
                        Position = position,
                        Phase = Range(0f, 20f),
                        Frequency = Range(6f, 10f)
                    });
                }
            }
        }
        ResizeScorch(Mathf.Clamp(radius, 1f, MaxRadius));
    }

    private MeshFilter CreatePiece(string partName)
    {
        MeshFilter source = parts[partName];
        GameObject piece = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
        piece.transform.SetParent(visualRoot, false);
        MeshFilter filter = piece.GetComponent<MeshFilter>();
        filter.sharedMesh = source.sharedMesh;
        MeshRenderer renderer = piece.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return filter;
    }

    private void ResizeScorch(float newRadius)
    {
        displayedRadius = newRadius;
        scorch.localScale = new Vector3(newRadius, 1f, newRadius);
        int count = Mathf.RoundToInt(12f * newRadius);
        while (rim.Count < count)
        {
            MeshFilter segment = CreatePiece("Fire_Rim_Segment");
            segment.sharedMesh = rimMesh;
            rim.Add(segment.transform);
        }

        // Recurve the source arc to the new circumference, keeping its radial width.
        for (int i = 0; i < rimVertices.Length; i++)
        {
            float angle = rimPolar[i].x * 12f / count;
            float distance = newRadius + rimPolar[i].y;
            rimVertices[i].x = Mathf.Cos(angle) * distance - newRadius;
            rimVertices[i].z = Mathf.Sin(angle) * distance;
        }
        rimMesh.vertices = rimVertices;
        rimMesh.RecalculateBounds();
        rimMesh.RecalculateNormals();
        for (int i = 0; i < rim.Count; i++)
        {
            Transform segment = rim[i];
            segment.gameObject.SetActive(i < count);
            if (i >= count)
                continue;
            Quaternion rotation = Quaternion.Euler(0f, 360f * i / count, 0f);
            segment.SetLocalPositionAndRotation(rotation * (Vector3.right * newRadius), rotation);
        }
    }

    private void Update()
    {
        float delta = Time.deltaTime;
        if (!burning)
        {
            extinguishedFor += delta;
            if (extinguishedFor >= ScorchTime)
            {
                if (visualRoot.gameObject.activeSelf)
                    visualRoot.gameObject.SetActive(false);
                return;
            }
        }

        elapsed += delta;
        if (burning && !Mathf.Approximately(displayedRadius, radius))
            ResizeScorch(Mathf.MoveTowards(displayedRadius, radius, delta * 3f));

        Vector2 breeze = wind.normalized;
        litSites.Clear();
        foreach (Site site in sites)
        {
            float distance = site.Position.magnitude;
            bool inFire = burning && distance <= displayedRadius - 0.18f;
            if (inFire)
                litSites.Add(site);

            site.Strength = Mathf.MoveTowards(site.Strength, inFire ? 1f : 0f, delta / RiseTime);
            if (inFire && (site.Flame == null || !site.Flame.gameObject.activeSelf))
                LightSite(site, distance);
            if (site.Flame == null)
                continue;

            bool visible = site.Strength > 0f;
            site.Flame.gameObject.SetActive(visible);
            if (site.Coals != null)
            {
                site.Coals.gameObject.SetActive(visible || !burning && distance < displayedRadius - 0.18f);
                float heat = burning ? site.Strength : Mathf.Clamp01(1f - extinguishedFor / 1.7f);
                float pulse = 0.64f + 0.23f * Mathf.Sin(elapsed * 10f + site.Phase);
                SetGlow(site.Coals, heat * pulse, Mathf.Lerp(0.12f, 1f, heat));
            }
            if (!visible)
                continue;

            float phase = site.Phase;
            float flutter = Mathf.Sin(elapsed * site.Frequency * Mathf.PI * 2f + phase);
            float surge = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(elapsed * 4.1f + phase * 2f)), 5f);
            float height = (0.91f + 0.075f * flutter + 0.055f * Mathf.Sin(elapsed * 13f + phase)
                            + 0.17f * surge) * site.Strength;
            float tilt = 9f + 6f * Mathf.Sin(elapsed * 10f + phase);
            Quaternion lean = Quaternion.Euler(breeze.y * tilt, 0f, -breeze.x * tilt);
            Quaternion sway = Quaternion.Euler(4f * Mathf.Sin(elapsed * 8f + phase), 0f, 0f);
            site.Flame.transform.localRotation = lean * sway * Quaternion.Euler(0f, phase * 97f, 0f);
            float width = Mathf.Lerp(0.38f, 1.03f + 0.09f * surge, site.Strength);
            site.Flame.transform.localScale = new Vector3(width, height, width);
        }
        AnimateSparks(delta, breeze);
    }

    private void LightSite(Site site, float distance)
    {
        // Use the destination radius so incoming flames can fill the interior of a growing patch.
        float proportion = distance / Mathf.Max(radius, displayedRadius);
        int variant = proportion < 0.38f ? random.Next(0, 2)
                    : proportion < 0.75f ? random.Next(2, 5) : random.Next(5, 7);
        if (site.Flame == null)
        {
            site.Flame = CreatePiece(FlameNames[variant]);
            site.Flame.transform.localPosition = new Vector3(site.Position.x, 0.028f, site.Position.y);
            if (site.Phase < 8f)
            {
                site.Coals = CreatePiece("Fire_Coals_Patch_" + "ABC"[random.Next(3)]).GetComponent<MeshRenderer>();
                site.Coals.transform.localPosition = new Vector3(site.Position.x * 0.93f, 0f, site.Position.y * 0.93f);
                site.Coals.transform.localRotation = Quaternion.Euler(0f, site.Phase * 100f, 0f);
            }
        }
        else if (site.Variant != variant)
        {
            site.Flame.sharedMesh = parts[FlameNames[variant]].sharedMesh;
        }
        site.Variant = variant;
        site.Flame.name = FlameNames[variant];
    }

    private void AnimateSparks(float delta, Vector2 breeze)
    {
        int count = burning ? Mathf.Min(litSites.Count, Mathf.CeilToInt(1.7f * displayedRadius * displayedRadius / 3f)) : 0;
        while (sparks.Count < count)
        {
            bool ash = sparks.Count % 5 == 4;
            MeshRenderer renderer = CreatePiece(ash ? "Fire_Ember_Flake" : "Fire_Spark").GetComponent<MeshRenderer>();
            renderer.enabled = false;
            sparks.Add(new Spark { Renderer = renderer, Ash = ash, Age = -Range(0f, 1.4f) });
        }
        for (int i = 0; i < sparks.Count; i++)
        {
            Spark spark = sparks[i];
            spark.Age += delta;
            if (spark.Age >= spark.Life)
            {
                if (i >= count)
                {
                    spark.Renderer.enabled = false;
                    continue;
                }
                Site source = litSites[random.Next(litSites.Count)];
                spark.Origin = new Vector3(source.Position.x, Range(0.20f, 0.65f), source.Position.y);
                spark.Drift = breeze * Range(0.19f, 0.68f) + RandomDrift();
                spark.Size = Range(0.65f, 1.35f);
                spark.Life = Range(0.8f, 1.4f);
                spark.Age = 0f;
                spark.Renderer.enabled = true;
            }
            if (!spark.Renderer.enabled)
                continue;

            float t = spark.Age / spark.Life;
            Vector2 drift = spark.Drift * t * (0.7f + 0.3f * t);
            float rise = t * (1.8f - 0.58f * t);
            Transform particle = spark.Renderer.transform;
            particle.localPosition = spark.Origin + new Vector3(drift.x, rise, drift.y);
            particle.localRotation = Quaternion.Euler(205f * t + i * 13f, 180f * t, 110f * t);
            particle.localScale = Vector3.one * (spark.Size * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)));
            if (!spark.Ash)
                SetGlow(spark.Renderer, 1.6f * (1f - t), 1f - t);
        }
    }

    private Vector2 RandomDrift()
    {
        return new Vector2(Range(-0.21f, 0.21f), Range(-0.21f, 0.21f));
    }

    private void SetGlow(MeshRenderer renderer, float intensity, float brightness)
    {
        glow.SetColor(EmissionColor, new Color(intensity, intensity, intensity, 1f));
        glow.SetColor(BaseColor, new Color(brightness, brightness, brightness, 1f));
        renderer.SetPropertyBlock(glow);
    }

    private float Range(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private void OnDestroy()
    {
        if (rimMesh != null)
            Destroy(rimMesh);
        if (visualRoot != null)
            Destroy(visualRoot.gameObject);
    }
}
