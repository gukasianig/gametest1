using System.Collections.Generic;
using UnityEngine;

// Планировки тем «Пещера» и «Ледник» (завод — в ArenaLayout.BuildFactory). Всё из примитивов.
// Число твёрдых препятствий держим ≲ 30: поиск пути (NextWaypoint) растёт с их количеством.
public partial class ArenaLayout
{
    struct ThemeSpec
    {
        public int seed;
        public Color wall, rockA, rockB, pillar, cap, glowA, glowB, pad, detail;
        public float smooth;
        public int boulders, pillars, crystals, details;
        public bool roundCaps;
    }

    static ThemeSpec SpecFor(ArenaTheme theme)
    {
        if (theme == ArenaTheme.Glacier)
            return new ThemeSpec
            {
                seed = 9137, wall = new Color(0.55f, 0.72f, 0.86f), rockA = new Color(0.62f, 0.8f, 0.94f), rockB = new Color(0.78f, 0.9f, 0.98f),
                pillar = new Color(0.5f, 0.74f, 0.92f), cap = new Color(0.92f, 0.97f, 1f), glowA = new Color(0.45f, 0.85f, 1f), glowB = new Color(0.7f, 0.95f, 1f),
                pad = new Color(0.85f, 0.94f, 1f), detail = new Color(0.9f, 0.95f, 1f), smooth = 0.75f,
                boulders = 14, pillars = 7, crystals = 70, details = 140, roundCaps = true
            };
        return new ThemeSpec
        {
            seed = 4242, wall = new Color(0.13f, 0.11f, 0.12f), rockA = new Color(0.22f, 0.19f, 0.18f), rockB = new Color(0.29f, 0.24f, 0.21f),
            pillar = new Color(0.25f, 0.21f, 0.2f), cap = new Color(0.18f, 0.15f, 0.15f), glowA = new Color(0.55f, 0.3f, 1f), glowB = new Color(0.2f, 0.9f, 0.9f),
            pad = new Color(0.24f, 0.19f, 0.27f), detail = new Color(0.2f, 0.17f, 0.16f), smooth = 0.12f,
            boulders = 14, pillars = 8, crystals = 80, details = 160, roundCaps = false
        };
    }

    private readonly Dictionary<string, Material> fxMaterials = new Dictionary<string, Material>();

    private Material MaterialFx(Color color, float smooth, Color emission)
    {
        string key = color.ToString() + smooth + emission;
        if (fxMaterials.TryGetValue(key, out Material m)) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = color;
        m.SetFloat("_Smoothness", smooth);
        if (emission.maxColorComponent > 0.001f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
        }
        fxMaterials[key] = m;
        return m;
    }

    private GameObject Detail(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        Collider c = go.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        return go;
    }

    private GameObject Solid(string name, PrimitiveType shape, Vector3 centre, Vector3 scale, Material material, Vector3 boundsSize)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.position = centre;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        blockers.Add(go.GetComponent<Collider>());
        obstacles.Add(new Bounds(centre, boundsSize));
        return go;
    }

    private static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    private bool PickSpot(System.Random rng, List<Vector3> taken, float minR, float limit, float gap, out Vector3 spot)
    {
        for (int i = 0; i < 80; i++)
        {
            var p = new Vector3(R(rng, -limit, limit), 0f, R(rng, -limit, limit));
            if (p.magnitude < minR) continue;
            bool ok = true;
            foreach (Vector3 t in taken) if ((t - p).sqrMagnitude < gap * gap) { ok = false; break; }
            if (!ok) continue;
            taken.Add(p);
            spot = p;
            return true;
        }
        spot = Vector3.zero;
        return false;
    }

    private void BuildThemed(ArenaTheme theme)
    {
        ThemeSpec s = SpecFor(theme);
        float areaScale = Mathf.Pow(halfSize / 110f, 2f);   // декор рассчитан на арену 220×220: на большой — пропорционально больше
        s.boulders = Mathf.RoundToInt(s.boulders * areaScale); s.pillars = Mathf.RoundToInt(s.pillars * areaScale);
        s.crystals = Mathf.RoundToInt(s.crystals * areaScale); s.details = Mathf.RoundToInt(s.details * areaScale);
        bool ice = theme == ArenaTheme.Glacier;
        var rng = new System.Random(s.seed);
        float h = halfSize;
        Material wallM = MaterialFx(s.wall, s.smooth, Color.clear);
        Material rockA = MaterialFx(s.rockA, s.smooth, Color.clear), rockB = MaterialFx(s.rockB, s.smooth, Color.clear);
        Material pillarM = MaterialFx(s.pillar, s.smooth, Color.clear), capM = MaterialFx(s.cap, s.smooth, Color.clear);
        Material detailM = MaterialFx(s.detail, s.smooth * 0.6f, Color.clear);
        Material glowA = MaterialFx(s.glowA, 0.6f, s.glowA * 1.6f), glowB = MaterialFx(s.glowB, 0.6f, s.glowB * 1.6f);

        // Границы: сплошные стены + «зубья» сверху (скалы/сосульки), чтобы край не был ровной линией.
        Solid("North perimeter", PrimitiveType.Cube, new Vector3(0, 2f, h), new Vector3(h * 2, 4, 2), wallM, Vector3.zero);
        Solid("South perimeter", PrimitiveType.Cube, new Vector3(0, 2f, -h), new Vector3(h * 2, 4, 2), wallM, Vector3.zero);
        Solid("East perimeter", PrimitiveType.Cube, new Vector3(h, 2f, 0), new Vector3(2, 4, h * 2), wallM, Vector3.zero);
        Solid("West perimeter", PrimitiveType.Cube, new Vector3(-h, 2f, 0), new Vector3(2, 4, h * 2), wallM, Vector3.zero);
        obstacles.Clear();   // стены обрабатываются границей арены (halfSize), в список препятствий не входят
        for (float t = -h + 3f; t < h - 2f; t += 5f)
            for (int side = 0; side < 4; side++)
            {
                float j = R(rng, -1.2f, 1.2f), hh = R(rng, 1.6f, 4.2f);
                Vector3 pos = side == 0 ? new Vector3(t + j, 4f + hh * 0.35f, h - 0.4f) : side == 1 ? new Vector3(t + j, 4f + hh * 0.35f, -h + 0.4f)
                    : side == 2 ? new Vector3(h - 0.4f, 4f + hh * 0.35f, t + j) : new Vector3(-h + 0.4f, 4f + hh * 0.35f, t + j);
                Detail("Wall spike", ice ? PrimitiveType.Cube : PrimitiveType.Cube, pos, new Vector3(R(rng, 1.4f, 2.6f), hh, R(rng, 1.4f, 2.6f)),
                    Quaternion.Euler(R(rng, -8f, 8f), R(rng, 0f, 90f), R(rng, -8f, 8f)), rng.NextDouble() < 0.5 ? wallM : rockB);
            }

        // Площадка в центре: тёмный диск и светящееся кольцо.
        Detail("Landing pad", PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(10f, 0.015f, 10f), Quaternion.identity, MaterialFx(s.pad, s.smooth, Color.clear));
        Detail("Pad ring", PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0), new Vector3(10.4f, 0.01f, 10.4f), Quaternion.identity, MaterialFx(s.glowA, 0.4f, s.glowA * 0.7f));
        Detail("Pad inner", PrimitiveType.Cylinder, new Vector3(0, 0.04f, 0), new Vector3(10.0f, 0.01f, 10.0f), Quaternion.identity, MaterialFx(s.pad, s.smooth, Color.clear));

        var taken = new List<Vector3>();
        float limit = h - 12f;
        // Глыбы: одно сплошное тело (AABB для коллизий) + округлая «шапка».
        for (int i = 0; i < s.boulders; i++)
        {
            if (!PickSpot(rng, taken, 20f, limit, 20f, out Vector3 p)) continue;
            Vector3 size = new Vector3(R(rng, 4f, 8f), R(rng, 2f, 3.6f), R(rng, 4f, 8f));
            Vector3 centre = new Vector3(p.x, size.y * 0.5f, p.z);
            Solid(ice ? "Ice block" : "Rock mass", PrimitiveType.Cube, centre, size, i % 2 == 0 ? rockA : rockB, size);
            Detail("Rock top", PrimitiveType.Sphere, new Vector3(p.x, size.y * 0.92f, p.z), new Vector3(size.x * 0.82f, size.y * 0.6f, size.z * 0.82f),
                Quaternion.Euler(0, R(rng, 0, 180f), 0), capM);
            Detail("Rubble", PrimitiveType.Sphere, new Vector3(p.x + R(rng, -size.x, size.x) * 0.7f, 0.35f, p.z + size.z * 0.62f),
                new Vector3(R(rng, 0.8f, 1.6f), R(rng, 0.5f, 1f), R(rng, 0.8f, 1.6f)), Quaternion.identity, detailM);
        }
        // Колонны: стебель + расширенная шапка/сосулька сверху.
        for (int i = 0; i < s.pillars; i++)
        {
            if (!PickSpot(rng, taken, 18f, limit, 16f, out Vector3 p)) continue;
            float r = R(rng, 1.2f, 2.0f), height = R(rng, 6f, 10f);
            Solid(ice ? "Ice pillar" : "Stalagmite", PrimitiveType.Cylinder, new Vector3(p.x, height * 0.5f, p.z), new Vector3(r * 2f, height * 0.5f, r * 2f), pillarM, new Vector3(r * 2f, height, r * 2f));
            Detail("Pillar cap", s.roundCaps ? PrimitiveType.Sphere : PrimitiveType.Cylinder, new Vector3(p.x, height + 0.2f, p.z),
                s.roundCaps ? new Vector3(r * 2.2f, r * 1.6f, r * 2.2f) : new Vector3(r * 2.6f, 0.4f, r * 2.6f), Quaternion.identity, capM);
            Detail("Glow band", PrimitiveType.Cylinder, new Vector3(p.x, height * 0.45f, p.z), new Vector3(r * 2.05f, 0.12f, r * 2.05f), Quaternion.identity, i % 2 == 0 ? glowA : glowB);
        }
        // Угловые «арки»: два столба и перемычка (перемычка высоко, не твёрдая).
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 c = new Vector3(sx * (h - 22f), 0f, sz * (h - 22f));
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 pc = c + new Vector3(k * 5f, 0f, 0f);
                    Solid(ice ? "Ice gate" : "Rock gate", PrimitiveType.Cylinder, new Vector3(pc.x, 4.5f, pc.z), new Vector3(3f, 4.5f, 3f), pillarM, new Vector3(3f, 9f, 3f));
                }
                Detail("Arch beam", PrimitiveType.Cube, new Vector3(c.x, 9.6f, c.z), new Vector3(13f, 1.6f, 3.2f), Quaternion.identity, capM);
                Detail("Arch glow", PrimitiveType.Cube, new Vector3(c.x, 8.6f, c.z), new Vector3(7f, 0.25f, 0.4f), Quaternion.identity, glowB);
            }
        // Светящиеся кристаллы: кучки по 1–3 штуки.
        for (int i = 0; i < s.crystals / 2; i++)
        {
            Vector3 p = new Vector3(R(rng, -h + 6f, h - 6f), 0f, R(rng, -h + 6f, h - 6f));
            if (p.magnitude < 12f || !IsWalkable(p, 1.5f)) continue;
            int n = rng.Next(1, 4);
            for (int k = 0; k < n; k++)
            {
                float hgt = R(rng, 1.2f, 3.2f);
                Detail("Crystal", PrimitiveType.Cube, new Vector3(p.x + R(rng, -0.8f, 0.8f), hgt * 0.45f, p.z + R(rng, -0.8f, 0.8f)),
                    new Vector3(R(rng, 0.4f, 0.8f), hgt, R(rng, 0.4f, 0.8f)), Quaternion.Euler(R(rng, -18f, 18f), R(rng, 0, 90f), R(rng, -18f, 18f)),
                    rng.NextDouble() < 0.5 ? glowA : glowB);
            }
        }
        // Мелкие камни/ледышки и пятна на полу (без коллизий).
        for (int i = 0; i < s.details; i++)
        {
            Vector3 p = new Vector3(R(rng, -h + 4f, h - 4f), 0f, R(rng, -h + 4f, h - 4f));
            if (p.magnitude < 11f || !IsWalkable(p, 0.8f)) continue;
            float w = R(rng, 0.5f, 1.8f);
            Detail("Pebble", PrimitiveType.Sphere, new Vector3(p.x, w * 0.18f, p.z), new Vector3(w, w * 0.45f, w * R(rng, 0.7f, 1.3f)),
                Quaternion.Euler(0, R(rng, 0, 180f), 0), detailM);
        }
    }
}
