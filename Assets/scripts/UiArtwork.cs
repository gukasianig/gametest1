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

    public static Sprite Upgrade(UpgradeEffect effect) { return Icon((int)effect); }
    public static Sprite Weapon(int index) { return Icon(9 + Mathf.Clamp(index, 0, 3)); }
    public static Sprite Heart(bool critical) { return Icon(critical ? 7 : 13); }

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
        if (rounded != null) UnityEngine.Object.Destroy(rounded);
        if (roundedTexture != null) UnityEngine.Object.Destroy(roundedTexture);
        rounded = null;
        roundedTexture = null;
        texture = null;
        atlas = null;
    }
}
