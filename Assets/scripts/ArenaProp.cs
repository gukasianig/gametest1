using UnityEngine;

// Объект-корень редактируемого декора арены «Завод». Перетащите сюда модели из Assets/Art/Kenney (или любые префабы) —
// они появятся в бою на этом месте. Пока на сцене есть корень с детьми, случайный декор не создаётся.
// Объект с компонентом ArenaProp и галочкой solid становится препятствием (враги и герой его обходят).
public class ArenaPropsRoot : MonoBehaviour
{
}

public class ArenaProp : MonoBehaviour
{
    [Tooltip("Препятствие: блокирует движение и учитывается в поиске пути. Всего препятствий на арене лучше держать не больше ~36.")]
    public bool solid;

    private void OnDrawGizmosSelected()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        Gizmos.color = solid ? new Color(1f, 0.4f, 0.2f) : new Color(0.4f, 0.8f, 1f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}

public static class ArenaProps
{
    // Возвращает true, если на сцене есть заполненный корень редактируемого декора (тогда случайный декор не нужен).
    public static bool Apply(ArenaLayout arena, bool visible)
    {
        ArenaPropsRoot root = Object.FindObjectOfType<ArenaPropsRoot>(true);
        if (root == null) return false;
        root.gameObject.SetActive(visible);
        if (!visible || root.transform.childCount == 0) return root.transform.childCount > 0;
        foreach (ArenaProp prop in root.GetComponentsInChildren<ArenaProp>())
        {
            if (!prop.solid) continue;
            Renderer[] rs = prop.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            arena.RegisterSolid(b);
        }
        if (arena.Obstacles.Count > 36) Debug.LogWarning("Arena: " + arena.Obstacles.Count + " obstacles; more than ~36 slows enemy pathfinding.");
        return true;
    }
}
