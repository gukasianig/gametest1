using UnityEngine;

// Необязательные 3D-модели снарядов (пуля, ракета, граната, ледяной осколок): Assets/Resources/Projectiles/*.fbx,
// собираются docs/art/Projectiles/make_projectiles.py. Если файла нет, снаряд остаётся прежним примитивом.
// Удалить эксперимент: удалить папку Assets/Resources/Projectiles (код ничего не потеряет).
public static class ProjectileModels
{
    // Из Blender (нос по +Y) нос приходит по локальной -Z (проверено по импортированной сетке), поэтому разворот на 180° вокруг Y.
    static readonly Quaternion NoseFix = Quaternion.Euler(0f, 180f, 0f);

    static GameObject Load(string name) { return Resources.Load<GameObject>("Projectiles/" + name); }

    // Модель вместо примитива: исходные рендереры хоста выключаются (коллайдеры и логика остаются).
    public static bool Skin(GameObject host, string name, float length)
    {
        GameObject prefab = Load(name);
        if (prefab == null || host == null) return false;
        foreach (Renderer r in host.GetComponentsInChildren<Renderer>()) r.enabled = false;
        GameObject model = Object.Instantiate(prefab, host.transform, false);
        model.name = "ProjectileModel";
        Fit(model, host.transform, length);
        return true;
    }

    // Отдельный объект только с моделью (для снарядов, у которых нет примитива-основы). Null, если модели нет.
    public static GameObject Create(string name, Vector3 position, Quaternion rotation, float length)
    {
        GameObject prefab = Load(name);
        if (prefab == null) return null;
        var host = new GameObject(name);
        host.transform.SetPositionAndRotation(position, rotation);
        GameObject model = Object.Instantiate(prefab, host.transform, false);
        model.name = "ProjectileModel";
        Fit(model, host.transform, length);
        return host;
    }

    static void Fit(GameObject model, Transform host, float length)
    {
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        Vector3 ls = host.lossyScale;
        Vector3 comp = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = NoseFix * model.transform.localRotation;
        model.transform.localScale = comp;
        Bounds b = BoundsOf(model);
        float max = Mathf.Max(b.size.x, b.size.y, b.size.z);
        if (max > 0.0001f) model.transform.localScale = comp * (length / max);
        b = BoundsOf(model);
        model.transform.position += host.position - b.center;
    }

    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
