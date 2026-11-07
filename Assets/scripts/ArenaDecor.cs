using System;
using System.Collections.Generic;
using UnityEngine;

// Необязательный декор арены из ModelSkinSet.decor. Без набора ничего не делает; удалить — убрать вызов в ArenaLayout.Build.
// Если на сцене есть заполненный ArenaPropsRoot, случайный декор не создаётся (см. ArenaProp.cs).
public static class ArenaDecor
{
    public static void Populate(ArenaLayout arena)
    {
        ModelSkinSet set = ModelSkins.CurrentSet;
        if (set == null || set.decor == null || arena == null) return;
        var root = new GameObject("Arena decor").transform;
        root.SetParent(arena.transform, false);
        Generate(arena, set, root, d => UnityEngine.Object.Instantiate(d.model, root), true);
    }

    // Расставляет декор по правилам набора. create создаёт экземпляр (в редакторе — как префаб-инстанс).
    public static void Generate(ArenaLayout arena, ModelSkinSet set, Transform root, Func<ModelSkinSet.Decor, GameObject> create, bool stripColliders)
    {
        var rng = new System.Random(7321);
        var spots = new List<Vector3>();
        foreach (ModelSkinSet.Decor d in set.decor)
        {
            if (d == null || d.model == null) continue;
            int placed = 0, guard = 0;
            while (placed < d.count && guard++ < d.count * 40)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(d.minRadius, d.maxRadius, (float)rng.NextDouble());
                Vector3 centre = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!FreeSpot(arena, centre, spots)) continue;
                spots.Add(centre);
                for (int i = 0; i < Mathf.Max(1, d.cluster); i++)
                {
                    Vector3 p = centre + new Vector3((float)(rng.NextDouble() - 0.5) * 2.2f * (d.cluster > 1 ? 1f : 0f), 0f,
                        (float)(rng.NextDouble() - 0.5) * 2.2f * (d.cluster > 1 ? 1f : 0f));
                    Place(create(d), d, p, (float)rng.NextDouble() * 360f, 0.9f + (float)rng.NextDouble() * 0.25f, stripColliders);
                }
                placed++;
            }
        }
    }

    static bool FreeSpot(ArenaLayout arena, Vector3 p, List<Vector3> taken)
    {
        if (Mathf.Abs(p.x) > arena.halfSize - 4f || Mathf.Abs(p.z) > arena.halfSize - 4f) return false;
        foreach (Bounds b in arena.Obstacles)
        {
            Bounds grown = b; grown.Expand(new Vector3(3f, 20f, 3f));
            if (grown.Contains(new Vector3(p.x, b.center.y, p.z))) return false;
        }
        foreach (Vector3 t in taken) if ((t - p).sqrMagnitude < 9f) return false;
        return true;
    }

    static void Place(GameObject go, ModelSkinSet.Decor d, Vector3 position, float yaw, float scale, bool stripColliders)
    {
        go.name = "Decor_" + d.label;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (stripColliders) foreach (Collider c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go); return; }
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        float max = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (max > 0.0001f) go.transform.localScale = Vector3.one * (d.targetSize * scale / max);
        b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        go.transform.position += new Vector3(position.x - b.center.x, -b.min.y, position.z - b.center.z);
    }
}
