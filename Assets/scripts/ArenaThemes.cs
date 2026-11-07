using UnityEngine;

public enum ArenaTheme { Factory = 0, Cave = 1, Glacier = 2 }

// Темы арены: пол, свет, туман и (в ArenaLayout.BuildThemed) планировка. Выбор хранится в PlayerPrefs, применяется при старте забега.
public static class ArenaThemes
{
    const string Key = "arena_theme";
    public static readonly string[] Names = { "Завод", "Пещера", "Ледник" };
    public static readonly string[] Hints =
    {
        "Стальные укрытия и здания, ровный пол — классика",
        "Тёмные скалы, каменные столбы и светящиеся кристаллы",
        "Ледяные глыбы и колонны, холодный свет и дымка"
    };

    static bool loaded;
    static ArenaTheme current;

    // Исходные настройки сцены (завод) запоминаются при первом применении темы.
    static bool baselineSaved;
    static bool baseFog;
    static Color baseFogColor, baseAmbient, baseSunColor;
    static float baseFogDensity, baseSunIntensity;
    static UnityEngine.Rendering.AmbientMode baseAmbientMode;
    static FogMode baseFogMode;

    public static ArenaTheme Current
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                try { current = (ArenaTheme)Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, Names.Length - 1); } catch { current = ArenaTheme.Factory; }
            }
            return current;
        }
    }

    public static void Set(ArenaTheme theme, bool save = true)
    {
        loaded = true;
        current = theme;
        if (!save) return;
        try { PlayerPrefs.SetInt(Key, (int)theme); PlayerPrefs.Save(); } catch { }
    }

    public static ArenaTheme Next(ArenaTheme theme) { return (ArenaTheme)(((int)theme + 1) % Names.Length); }

    // Пересобирает арену, пол и окружение под текущую тему (пока забег не идёт).
    public static void Apply()
    {
        ApplyEnvironment(Current);
        GridDrawer floor = Object.FindObjectOfType<GridDrawer>();
        if (floor != null) floor.Apply(Current);
        if (ArenaLayout.Instance != null) ArenaLayout.Instance.Rebuild();
    }

    public static void ApplyEnvironment(ArenaTheme theme)
    {
        SaveBaseline();
        Light sun = FindSun();
        switch (theme)
        {
            case ArenaTheme.Cave:
                Env(true, new Color(0.10f, 0.07f, 0.13f), 0.011f, new Color(0.30f, 0.27f, 0.34f), sun, new Color(0.9f, 0.8f, 0.9f), 1.05f);
                break;
            case ArenaTheme.Glacier:
                Env(true, new Color(0.62f, 0.76f, 0.88f), 0.009f, new Color(0.40f, 0.48f, 0.58f), sun, new Color(0.85f, 0.93f, 1f), 1.15f);
                break;
            default:
                RenderSettings.fog = baseFog;
                RenderSettings.fogColor = baseFogColor;
                RenderSettings.fogDensity = baseFogDensity;
                RenderSettings.fogMode = baseFogMode;
                RenderSettings.ambientMode = baseAmbientMode;
                RenderSettings.ambientLight = baseAmbient;
                if (sun != null) { sun.color = baseSunColor; sun.intensity = baseSunIntensity; }
                break;
        }
    }

    static void Env(bool fog, Color fogColor, float density, Color ambient, Light sun, Color sunColor, float sunIntensity)
    {
        RenderSettings.fog = fog;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = density;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        if (sun != null) { sun.color = sunColor; sun.intensity = sunIntensity; }
    }

    static void SaveBaseline()
    {
        if (baselineSaved) return;
        baselineSaved = true;
        baseFog = RenderSettings.fog; baseFogColor = RenderSettings.fogColor; baseFogDensity = RenderSettings.fogDensity; baseFogMode = RenderSettings.fogMode;
        baseAmbientMode = RenderSettings.ambientMode; baseAmbient = RenderSettings.ambientLight;
        Light sun = FindSun();
        if (sun != null) { baseSunColor = sun.color; baseSunIntensity = sun.intensity; }
    }

    static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        foreach (Light l in Object.FindObjectsOfType<Light>()) if (l.type == LightType.Directional) return l;
        return null;
    }

    // Бесшовная текстура пола 512×512 (повторяется каждые 10 м).
    public static Texture2D MakeFloor(ArenaTheme theme)
    {
        const int N = 512;
        var tex = new Texture2D(N, N, TextureFormat.RGB24, true);
        tex.name = "Arena floor " + theme;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                px[y * N + x] = theme == ArenaTheme.Cave ? CaveTexel(x, y, N) : theme == ArenaTheme.Glacier ? IceTexel(x, y, N) : FactoryTexel(x, y, N);
        tex.SetPixels(px);
        tex.Apply(true, true);
        return tex;
    }

    static Color FactoryTexel(int x, int y, int n)
    {
        int tileX = x * 5 / n, tileY = y * 5 / n;
        float grain = ((x * 17 + y * 31) % 23 - 11) * 0.001f;
        float variation = ((tileX + tileY) % 2) * 0.018f;
        Color color = new Color(0.23f + grain + variation, 0.28f + grain + variation, 0.26f + grain + variation);
        bool seam = x * 5 % n < 5 || y * 5 % n < 5;
        bool major = x < 3 || y < 3;
        bool marker = (Mathf.Abs(x - n / 2) < 2 && Mathf.Abs(y - n / 2) < 12) || (Mathf.Abs(y - n / 2) < 2 && Mathf.Abs(x - n / 2) < 12);
        if (seam) color *= 0.78f;
        if (major) color = new Color(0.34f, 0.39f, 0.35f);
        if (marker) color = new Color(0.55f, 0.52f, 0.36f);
        return color;
    }

    // Периодический «шум» из синусов (целые частоты → шов не виден).
    static float Wave(int x, int y, int n, float seed)
    {
        float u = x * Mathf.PI * 2f / n, v = y * Mathf.PI * 2f / n;
        return 0.5f + 0.18f * Mathf.Sin(3f * u + seed + 1.7f * Mathf.Sin(2f * v)) + 0.14f * Mathf.Sin(5f * v + seed * 2f + Mathf.Sin(3f * u)) + 0.1f * Mathf.Sin(9f * u + 7f * v + seed);
    }

    // Булыжник/плитка: ячейки 8×8 со своим оттенком и тёмными швами.
    static float Cell(int x, int y, int n, int cells, out bool seam)
    {
        int cs = n / cells;
        int cx = x / cs, cy = y / cs;
        int h = (cx * 73856093) ^ (cy * 19349663);
        h = (h ^ (h >> 13)) * 1274126177;
        float shade = ((h >> 8) & 255) / 255f;
        int lx = x % cs, ly = y % cs;
        seam = lx < 3 || ly < 3;
        return shade;
    }

    static Color CaveTexel(int x, int y, int n)
    {
        float w = Wave(x, y, n, 1.3f);
        float shade = Cell(x, y, n, 8, out bool seam);
        float g = 0.21f + 0.08f * w + 0.05f * shade;
        var c = new Color(g * 0.98f, g * 0.95f, g * 0.95f);
        if (seam) c *= 0.55f;
        float vein = Mathf.Abs(Wave(x, y, n, 4.1f) - 0.5f);
        if (vein < 0.012f) c = Color.Lerp(c, new Color(0.34f, 0.2f, 0.44f), 0.55f);   // фиолетовые прожилки
        return c;
    }

    static Color IceTexel(int x, int y, int n)
    {
        float w = Wave(x, y, n, 2.2f);
        float shade = Cell(x, y, n, 6, out bool seam);
        float t = 0.52f + 0.16f * w + 0.08f * shade;
        var c = new Color(t * 0.78f, t * 0.9f, Mathf.Min(1f, t * 1.05f));
        if (seam) c = Color.Lerp(c, new Color(0.9f, 0.97f, 1f), 0.6f);
        float crack = Mathf.Abs(Wave(x, y, n, 5.5f) - 0.5f);
        if (crack < 0.01f) c = Color.Lerp(c, new Color(0.32f, 0.5f, 0.68f), 0.6f);
        return c;
    }
}
