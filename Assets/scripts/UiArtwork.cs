using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UiArtwork
{
    [Serializable] private class Atlas { public int width; public int height; public Area[] icons; }
    [Serializable] private class Area { public int x; public int y; public int width; public int height; }
    private static Texture2D texture;
    private static Atlas atlas;
    private static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
    private static Sprite rounded;
    private static Texture2D roundedTexture;

    public static Sprite Icon(int index)
    {
        if (sprites.TryGetValue(index, out Sprite cached)) return cached;
        if (texture == null) texture = Resources.Load<Texture2D>("UI/ArenaIcons");
        if (atlas == null)
        {
            TextAsset data = Resources.Load<TextAsset>("UI/ArenaIconRegions");
            if (data != null) atlas = JsonUtility.FromJson<Atlas>(data.text);
        }
        if (texture == null || atlas == null || atlas.icons == null || index < 0 || index >= atlas.icons.Length) return null;
        Area area = atlas.icons[index];
        float sx = (float)texture.width / atlas.width, sy = (float)texture.height / atlas.height;
        Rect rect = new Rect(area.x * sx, (atlas.height - area.y - area.height) * sy, area.width * sx, area.height * sy);
        Sprite sprite = Sprite.Create(texture, rect, Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
        sprite.name = "Arena icon " + index;
        sprites[index] = sprite;
        return sprite;
    }

    // Atlas slots 9+ hold the weapon icons, so upgrades added later draw a vector icon instead.
    public static Sprite Upgrade(UpgradeEffect effect) { return (int)effect <= (int)UpgradeEffect.OrbitCount ? Icon((int)effect) : null; }
    public static Sprite Weapon(int index) { return Icon(9 + Mathf.Clamp(index, 0, 3)); }
    public static Sprite Heart(bool critical) { return Icon(critical ? 7 : 13); }

    public static Color WeaponAccent(WeaponKind kind)
    {
        switch (kind)
        {
            case WeaponKind.Boomerang: return new Color(0.95f, 0.6f, 0.2f);
            case WeaponKind.Mines: return new Color(0.95f, 0.4f, 0.25f);
            case WeaponKind.Rocket: return new Color(1f, 0.7f, 0.25f);
            case WeaponKind.Arc: return new Color(0.65f, 0.8f, 1f);
            case WeaponKind.Flame: return new Color(1f, 0.5f, 0.15f);
            case WeaponKind.Ice: return new Color(0.55f, 0.88f, 1f);
            case WeaponKind.Rail: return new Color(0.6f, 0.95f, 1f);
            case WeaponKind.Turret: return new Color(0.5f, 0.75f, 0.55f);
            case WeaponKind.Drone: return new Color(0.4f, 0.95f, 0.85f);
            default: return GameUi.Accent;
        }
    }

    private static readonly Dictionary<WeaponKind, Sprite> glyphs = new Dictionary<WeaponKind, Sprite>();

    // Simple white pictogram per special weapon (tinted by the card); a placeholder until real artwork exists.
    public static Sprite WeaponGlyph(WeaponKind kind)
    {
        if (glyphs.TryGetValue(kind, out Sprite cached) && cached != null) return cached;
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Weapon glyph " + kind;
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;
        Vector2 c = new Vector2(48f, 48f);
        switch (kind)
        {
            case WeaponKind.Rail:
                Line(pixels, size, new Vector2(8, 48), new Vector2(84, 48), 5f);
                Line(pixels, size, new Vector2(70, 36), new Vector2(86, 48), 4f);
                Line(pixels, size, new Vector2(70, 60), new Vector2(86, 48), 4f);
                for (int x = 24; x <= 56; x += 16) Line(pixels, size, new Vector2(x, 36), new Vector2(x, 60), 3f);
                break;
            case WeaponKind.Boomerang:
                Ring(pixels, size, c, 28f, 20f);
                for (int a = 0; a < 8; a++)
                {
                    float r = a * Mathf.PI / 4f;
                    Line(pixels, size, c + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 26f, c + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 40f, 5f);
                }
                Disc(pixels, size, c, 7f);
                break;
            case WeaponKind.Mines:
                Disc(pixels, size, c, 24f);
                for (int a = 0; a < 6; a++)
                {
                    float r = a * Mathf.PI / 3f;
                    Line(pixels, size, c + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 22f, c + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 40f, 5f);
                }
                Ring(pixels, size, c, 12f, 6f, true);
                break;
            case WeaponKind.Rocket:
                Line(pixels, size, new Vector2(20, 48), new Vector2(66, 48), 14f);
                Line(pixels, size, new Vector2(60, 36), new Vector2(86, 48), 5f);
                Line(pixels, size, new Vector2(60, 60), new Vector2(86, 48), 5f);
                Line(pixels, size, new Vector2(8, 38), new Vector2(18, 44), 4f);
                Line(pixels, size, new Vector2(8, 58), new Vector2(18, 52), 4f);
                break;
            case WeaponKind.Arc:
                Vector2[] bolt = { new Vector2(60, 90), new Vector2(38, 52), new Vector2(54, 52), new Vector2(32, 6) };
                for (int i = 0; i < bolt.Length - 1; i++) Line(pixels, size, bolt[i], bolt[i + 1], 8f);
                break;
            case WeaponKind.Flame:
                Disc(pixels, size, new Vector2(30, 48), 14f);
                Line(pixels, size, new Vector2(30, 48), new Vector2(86, 22), 10f);
                Line(pixels, size, new Vector2(30, 48), new Vector2(86, 48), 10f);
                Line(pixels, size, new Vector2(30, 48), new Vector2(86, 74), 10f);
                break;
            case WeaponKind.Ice:
                for (int a = 0; a < 3; a++)
                {
                    float r = a * Mathf.PI / 3f;
                    Vector2 d = new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 40f;
                    Line(pixels, size, c - d, c + d, 5f);
                }
                Disc(pixels, size, c, 8f);
                break;
            case WeaponKind.Turret:
                Line(pixels, size, new Vector2(26, 22), new Vector2(70, 22), 16f);
                Disc(pixels, size, new Vector2(48, 42), 18f);
                Line(pixels, size, new Vector2(52, 44), new Vector2(86, 62), 9f);
                break;
            case WeaponKind.Drone:
                Disc(pixels, size, c, 15f);
                Line(pixels, size, new Vector2(10, 62), new Vector2(40, 54), 5f);
                Line(pixels, size, new Vector2(86, 62), new Vector2(56, 54), 5f);
                Line(pixels, size, new Vector2(48, 36), new Vector2(48, 14), 5f);
                break;
            default:
                Disc(pixels, size, c, 20f);
                break;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
        sprite.name = "Weapon glyph " + kind;
        glyphs[kind] = sprite;
        return sprite;
    }

    private static void Plot(Color[] pixels, int size, int x, int y, float alpha, bool erase = false)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return;
        Color existing = pixels[y * size + x];
        float a = Mathf.Clamp01(alpha);
        pixels[y * size + x] = erase ? new Color(1f, 1f, 1f, Mathf.Min(existing.a, 1f - a)) : new Color(1f, 1f, 1f, Mathf.Max(existing.a, a));
    }

    private static void Disc(Color[] pixels, int size, Vector2 centre, float radius)
    {
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            Plot(pixels, size, x, y, radius + 0.5f - Vector2.Distance(new Vector2(x, y), centre));
    }

    private static void Ring(Color[] pixels, int size, Vector2 centre, float outer, float inner, bool erase = false)
    {
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), centre);
            float a = Mathf.Min(outer + 0.5f - d, d - inner + 0.5f);
            if (a > 0f) Plot(pixels, size, x, y, a, erase);
        }
    }

    private static void Line(Color[] pixels, int size, Vector2 from, Vector2 to, float width)
    {
        Vector2 delta = to - from;
        float length = Mathf.Max(0.001f, delta.sqrMagnitude);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x, y);
            float t = Mathf.Clamp01(Vector2.Dot(p - from, delta) / length);
            Plot(pixels, size, x, y, width * 0.5f + 0.5f - Vector2.Distance(p, from + delta * t));
        }
    }

    public static Image Picture(Transform parent, Sprite sprite, Vector2 min, Vector2 max)
    {
        Image image = GameUi.Box(parent, "Artwork", min, max, Color.white).GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        return image;
    }

    public static void Round(Image image)
    {
        if (rounded == null)
        {
            roundedTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            roundedTexture.name = "UI rounded panel";
            roundedTexture.filterMode = FilterMode.Bilinear;
            roundedTexture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                Vector2 q = new Vector2(Mathf.Abs(x - 31.5f) - 20f, Mathf.Abs(y - 31.5f) - 20f);
                float distance = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude +
                    Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 12f;
                pixels[y * 64 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance));
            }
            roundedTexture.SetPixels(pixels);
            roundedTexture.Apply(false, true);
            rounded = Sprite.Create(roundedTexture, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 100f,
                0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
        }
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Clear()
    {
        foreach (Sprite sprite in sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
        sprites.Clear();
        foreach (Sprite glyph in glyphs.Values) if (glyph != null) { if (glyph.texture != null) UnityEngine.Object.Destroy(glyph.texture); UnityEngine.Object.Destroy(glyph); }
        glyphs.Clear();
        if (rounded != null) UnityEngine.Object.Destroy(rounded);
        if (roundedTexture != null) UnityEngine.Object.Destroy(roundedTexture);
        rounded = null;
        roundedTexture = null;
        texture = null;
        atlas = null;
    }
}
