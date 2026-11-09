using System.Collections.Generic;
using UnityEngine;

// Зелёная арена: делает деревья (стволы), камни, здания, брёвна и реквизит непроходимыми.
//  - для героя и врагов: препятствия ArenaLayout (плоскость XZ, с сеткой поиска);
//  - для пуль героя: BoxCollider-блокеры, зарегистрированные в ArenaLayout (bullet.cs гасит пулю о блокер);
//  - вражеские выстрелы проверяют те же препятствия сами.
// Трава, цветы, кусты, мелкая галька и плоские плиты не блокируют.
public class GreenArenaObstacles : MonoBehaviour
{
    private const float treeTrunk = 1.0f;       // сторона ствола (дерево блокирует только ствол, не крону)
    private const float minRockSize = 1.0f;     // камни меньше (по большей стороне) не блокируют
    private const float minHeight = 0.5f;       // и плоские (ниже этого) тоже
    private const float rockFill = 0.85f;       // доля точной коробки камня (по вершинам), которая держит

    // Точные коробки камней по вершинам меша (считает GreenArenaPlayBuilder в редакторе через FitRocks).
    // renderer.bounds у повёрнутого, наклонённого, вытянутого камня заметно шире самого камня: невидимые стены и тесные «карманы» в кучах.
    // Во время игры меш не перечитать: камни статично батчатся (sharedMesh становится общим), а в сборке меш не читаем.
    [SerializeField, HideInInspector] private List<Transform> fittedRocks = new List<Transform>();
    [SerializeField, HideInInspector] private List<Bounds> fittedBounds = new List<Bounds>();

#if UNITY_EDITOR
    public int FitRocks()
    {
        fittedRocks.Clear();
        fittedBounds.Clear();
        foreach (Transform rock in Children("Rocks"))
        {
            bool any = false;
            Bounds b = default;
            foreach (MeshFilter mf in rock.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 w = mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    Vector3 p = w.MultiplyPoint3x4(v);
                    if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
                }
            }
            if (!any) continue;
            fittedRocks.Add(rock);
            fittedBounds.Add(b);
        }
        return fittedRocks.Count;
    }
#endif

    private void Start()
    {
        ArenaLayout arena = ArenaLayout.Instance;
        if (arena == null) return;
        var root = new GameObject("Green Arena Colliders").transform;
        int trees = 0, rocks = 0, buildings = 0, props = 0;
        foreach (Transform child in Children("Plants"))
        {
            string n = child.name;
            if (n.StartsWith("tree_")) { if (AddTrunk(arena, root, child, n.Contains("sapling") ? treeTrunk * 0.5f : treeTrunk)) trees++; }
            else if (n.StartsWith("stump_big") || n.StartsWith("log_fallen")) { if (AddBox(arena, root, child, 0.8f, 0f)) props++; }
        }
        var fitted = new Dictionary<Transform, Bounds>();
        for (int i = 0; i < fittedRocks.Count && i < fittedBounds.Count; i++)
            if (fittedRocks[i] != null) fitted[fittedRocks[i]] = fittedBounds[i];
        foreach (Transform child in Children("Rocks"))
        {
            bool ok = fitted.TryGetValue(child, out Bounds exact) ? AddBounds(arena, root, child.name, exact, rockFill, minRockSize)
                : AddBox(arena, root, child, 0.8f, minRockSize);
            if (ok) rocks++;
        }
        foreach (Transform child in Children("Buildings")) if (AddBox(arena, root, child, 0.9f, 1.5f)) buildings++;
        foreach (Transform child in Children("Props")) if (AddBox(arena, root, child, 0.9f, 0.8f)) props++;
        Debug.Log("GreenArenaObstacles: trees=" + trees + " rocks=" + rocks + " buildings=" + buildings + " props=" + props);
    }

    private static IEnumerable<Transform> Children(string rootName)
    {
        GameObject group = GameObject.Find(rootName);
        if (group == null) yield break;
        foreach (Transform child in group.transform) yield return child;
    }

    private static bool TryBounds(Transform t, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;
        bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return true;
    }

    private bool AddTrunk(ArenaLayout arena, Transform root, Transform tree, float side)
    {
        if (!TryBounds(tree, out Bounds b)) return false;
        Register(arena, root, tree.name, new Bounds(new Vector3(tree.position.x, b.center.y, tree.position.z), new Vector3(side, b.size.y, side)));
        return true;
    }

    private bool AddBox(ArenaLayout arena, Transform root, Transform t, float fill, float minSize)
    {
        return TryBounds(t, out Bounds b) && AddBounds(arena, root, t.name, b, fill, minSize);
    }

    private bool AddBounds(ArenaLayout arena, Transform root, string name, Bounds b, float fill, float minSize)
    {
        if (Mathf.Max(b.size.x, b.size.z) < minSize || b.size.y < minHeight) return false;
        b.size = new Vector3(b.size.x * fill, b.size.y, b.size.z * fill);   // чуть меньше габарита: не цепляться за свесы
        Register(arena, root, name, b);
        return true;
    }

    private static void Register(ArenaLayout arena, Transform root, string name, Bounds bounds)
    {
        var go = new GameObject("Block " + name);
        go.transform.SetParent(root, false);
        go.transform.position = bounds.center;
        var box = go.AddComponent<BoxCollider>();
        box.size = bounds.size;
        arena.AddBlocker(box);
        arena.AddObstacle(bounds);
    }
}
