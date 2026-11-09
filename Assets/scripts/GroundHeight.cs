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

    // Нормаль земли (на плоских аренах — вверх).
    public static Vector3 Normal(Vector3 point)
    {
        if (!HasTerrain) return Vector3.up;
        TerrainData data = terrain.terrainData;
        Vector3 local = point - terrain.transform.position;
        return data.GetInterpolatedNormal(Mathf.Clamp01(local.x / data.size.x), Mathf.Clamp01(local.z / data.size.z));
    }

    // Наклон плоского наземного эффекта (пятно, кольцо, лужа) по склону, с поворотом вокруг своей оси.
    public static Quaternion Tilt(Vector3 point, float yaw = 0f)
    {
        return Quaternion.FromToRotation(Vector3.up, Normal(point)) * Quaternion.Euler(0f, yaw, 0f);
    }

    // Насколько поднять наклонённый диск радиуса radius, чтобы выпуклый рельеф нигде не закрывал его край
    // (09.11.2026: пятна взрывов на склонах были видны наполовину).
    public static float Lift(Vector3 center, float radius)
    {
        if (!HasTerrain || radius <= 0f) return 0f;
        float h = At(center);
        Vector3 normal = Normal(center);
        float lift = 0f;
        for (int ring = 1; ring <= 2; ring++)
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + ring * 0.4f;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * ring * 0.5f;
                // Высота плоскости диска над точкой p: h - (n.x*dx + n.z*dz) / n.y.
                float plane = h - (normal.x * (p.x - center.x) + normal.z * (p.z - center.z)) / Mathf.Max(0.2f, normal.y);
                lift = Mathf.Max(lift, At(p) - plane);
            }
        return Mathf.Min(lift, 1.5f);
    }
}
