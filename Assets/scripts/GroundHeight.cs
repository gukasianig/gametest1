using UnityEngine;

// Высота земли под точкой. Есть Unity Terrain (сцена GreenArenaPlay) - берёт его высоту, иначе 0 (плоские арены).
public static class GroundHeight
{
    private static Terrain terrain;

    public static bool HasTerrain { get { if (terrain == null) terrain = Terrain.activeTerrain; return terrain != null; } }

    public static float At(Vector3 point)
    {
        if (!HasTerrain) return 0f;
        return terrain.SampleHeight(point) + terrain.transform.position.y;
    }
}
