using System.Collections.Generic;
using UnityEngine;

public class ArenaLayout : MonoBehaviour
{
    public static ArenaLayout Instance { get; private set; }
    public float halfSize = 72f;
    private readonly List<Bounds> obstacles = new List<Bounds>();
    private readonly HashSet<Collider> blockers = new HashSet<Collider>();
    private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    public IReadOnlyList<Bounds> Obstacles => obstacles;

    private void Awake()
    {
        Instance = this;
        Build();
    }

    public Material Material(Color color)
    {
        if (materials.TryGetValue(color, out Material material)) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        material.SetFloat("_Smoothness", 0.15f);
        materials.Add(color, material);
        return material;
    }

    public void Tint(GameObject actor, Color color)
    {
        foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = Material(color);
    }

    private GameObject Prop(string name, PrimitiveType primitive, Vector3 position, Vector3 size, Color color, bool solid)
    {
        GameObject prop = GameObject.CreatePrimitive(primitive);
        prop.name = name;
        prop.transform.SetParent(transform, false);
        prop.transform.position = position;
        prop.transform.localScale = size;
        prop.GetComponent<Renderer>().sharedMaterial = Material(color);
        Collider collider = prop.GetComponent<Collider>();
        if (!solid) { collider.enabled = false; Destroy(collider); }
        else blockers.Add(collider);
        return prop;
    }

    private void Cover(string name, Vector3 centre, Vector3 size, Color color)
    {
        Prop(name, PrimitiveType.Cube, centre, size, color, true);
        obstacles.Add(new Bounds(centre, size));
        Prop("Safety stripe", PrimitiveType.Cube, centre + Vector3.up * (size.y * 0.5f + 0.025f),
            new Vector3(size.x, 0.05f, size.z * 0.3f), new Color(0.85f, 0.63f, 0.18f), false);
    }

    private void Build()
    {
        Color steel = new Color(0.13f, 0.2f, 0.22f);
        Prop("North perimeter", PrimitiveType.Cube, new Vector3(0, 1.5f, halfSize), new Vector3(halfSize * 2, 3, 1), steel, true);
        Prop("South perimeter", PrimitiveType.Cube, new Vector3(0, 1.5f, -halfSize), new Vector3(halfSize * 2, 3, 1), steel, true);
        Prop("East perimeter", PrimitiveType.Cube, new Vector3(halfSize, 1.5f, 0), new Vector3(1, 3, halfSize * 2), steel, true);
        Prop("West perimeter", PrimitiveType.Cube, new Vector3(-halfSize, 1.5f, 0), new Vector3(1, 3, halfSize * 2), steel, true);
        Cover("Cargo A", new Vector3(-15, 1, 13), new Vector3(6, 2, 3), steel);
        Cover("Cargo B", new Vector3(16, 1, -14), new Vector3(3, 2, 6), steel);
        Cover("Cargo C", new Vector3(31, 1, 22), new Vector3(6, 2, 3), steel);
        Cover("Cargo D", new Vector3(-30, 1, -23), new Vector3(3, 2, 6), steel);
        Cover("North generator", new Vector3(0, 2, 48), new Vector3(8, 4, 5), new Color(0.18f, 0.43f, 0.39f));
        for (int i = -1; i <= 1; i++)
            Prop("Generator cooling fin", PrimitiveType.Cube, new Vector3(i * 2.5f, 4.7f, 48),
                new Vector3(0.7f, 1.4f, 3), steel, false);
        Cover("Western tanks foundation", new Vector3(-48, 0.3f, 0), new Vector3(7, 0.6f, 8), steel);
        for (int i = -1; i <= 1; i += 2)
            Prop("Western tank", PrimitiveType.Cylinder, new Vector3(-48, 2.3f, i * 2), new Vector3(2.7f, 2, 2.7f),
                new Color(0.63f, 0.39f, 0.18f), false);
        Cover("Eastern relay", new Vector3(48, 2, 0), new Vector3(5, 4, 5), new Color(0.24f, 0.35f, 0.51f));
        Prop("Relay antenna", PrimitiveType.Cylinder, new Vector3(48, 6, 0), new Vector3(0.25f, 3, 0.25f), steel, false);
        Prop("Landing pad", PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0), new Vector3(7, 0.015f, 7),
            new Color(0.18f, 0.31f, 0.3f), false);
        for (int side = -1; side <= 1; side += 2)
        for (int i = -3; i <= 3; i++)
        {
            Prop("Perimeter marker", PrimitiveType.Cube, new Vector3(i * 18, 0.03f, side * (halfSize - 2)),
                new Vector3(4, 0.05f, 0.35f), new Color(0.8f, 0.57f, 0.15f), false);
            Prop("Perimeter marker", PrimitiveType.Cube, new Vector3(side * (halfSize - 2), 0.03f, i * 18),
                new Vector3(0.35f, 0.05f, 4), new Color(0.8f, 0.57f, 0.15f), false);
        }
    }

    public bool IsBlocker(Collider collider) { return blockers.Contains(collider); }

    public bool IsWalkable(Vector3 point, float radius)
    {
        if (Mathf.Abs(point.x) + radius >= halfSize - 0.6f || Mathf.Abs(point.z) + radius >= halfSize - 0.6f) return false;
        foreach (Bounds bounds in obstacles)
            if (Mathf.Abs(point.x - bounds.center.x) < bounds.extents.x + radius &&
                Mathf.Abs(point.z - bounds.center.z) < bounds.extents.z + radius) return false;
        return true;
    }

    private static bool Crosses(Vector3 start, Vector3 end, Bounds obstacle, float radius)
    {
        Vector3 min = obstacle.min - Vector3.one * radius;
        Vector3 max = obstacle.max + Vector3.one * radius;
        Vector3 delta = end - start;
        float enter = 0f, exit = 1f;
        for (int axis = 0; axis < 2; axis++)
        {
            float a = axis == 0 ? start.x : start.z;
            float d = axis == 0 ? delta.x : delta.z;
            float low = axis == 0 ? min.x : min.z;
            float high = axis == 0 ? max.x : max.z;
            if (Mathf.Abs(d) < 0.0001f) { if (a <= low || a >= high) return false; continue; }
            float first = (low - a) / d, last = (high - a) / d;
            if (first > last) { float swap = first; first = last; last = swap; }
            enter = Mathf.Max(enter, first);
            exit = Mathf.Min(exit, last);
            if (enter >= exit - 0.0001f) return false;
        }
        return exit > 0.0001f && enter < 0.9999f;
    }

    public bool PathClear(Vector3 start, Vector3 end, float radius)
    {
        foreach (Bounds bounds in obstacles) if (Crosses(start, end, bounds, radius)) return false;
        return true;
    }

    public Vector3 ResolveMovement(Vector3 start, Vector3 desired, float radius)
    {
        float edge = halfSize - radius - 0.65f;
        desired.x = Mathf.Clamp(desired.x, -edge, edge);
        desired.z = Mathf.Clamp(desired.z, -edge, edge);
        if (PathClear(start, desired, radius)) return desired;
        Vector3 xOnly = new Vector3(desired.x, desired.y, start.z);
        Vector3 zOnly = new Vector3(start.x, desired.y, desired.z);
        bool x = PathClear(start, xOnly, radius), z = PathClear(start, zOnly, radius);
        if (x && z) return (xOnly - start).sqrMagnitude > (zOnly - start).sqrMagnitude ? xOnly : zOnly;
        if (x) return xOnly;
        return z ? zOnly : start;
    }

    public Vector3 NextWaypoint(Vector3 start, Vector3 goal, float radius)
    {
        if (PathClear(start, goal, radius)) return goal;
        var nodes = new List<Vector3> { start, goal };
        foreach (Bounds bounds in obstacles)
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = new Vector3(bounds.center.x + x * (bounds.extents.x + radius + 0.2f), start.y,
                bounds.center.z + z * (bounds.extents.z + radius + 0.2f));
            if (IsWalkable(corner, radius)) nodes.Add(corner);
        }
        float[] distances = new float[nodes.Count];
        int[] previous = new int[nodes.Count];
        bool[] used = new bool[nodes.Count];
        for (int i = 0; i < nodes.Count; i++) { distances[i] = float.PositiveInfinity; previous[i] = -1; }
        distances[0] = 0f;
        for (int step = 0; step < nodes.Count; step++)
        {
            int current = -1;
            for (int i = 0; i < nodes.Count; i++)
                if (!used[i] && (current < 0 || distances[i] < distances[current])) current = i;
            if (current < 0 || float.IsPositiveInfinity(distances[current])) break;
            if (current == 1) break;
            used[current] = true;
            for (int i = 1; i < nodes.Count; i++)
            {
                if (used[i] || !PathClear(nodes[current], nodes[i], radius)) continue;
                float cost = distances[current] + Vector3.Distance(nodes[current], nodes[i]);
                if (cost >= distances[i]) continue;
                distances[i] = cost;
                previous[i] = current;
            }
        }
        int next = 1;
        if (previous[next] < 0) return start;
        while (previous[next] > 0) next = previous[next];
        return nodes[next];
    }

    public bool TryBossSpawn(Camera camera, Transform player, out Vector3 position)
    {
        return OffscreenSpawn.TryFindPosition(camera, player, 3.5f, 1.6f, out position);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (Material material in materials.Values) if (material != null) Destroy(material);
    }
}
