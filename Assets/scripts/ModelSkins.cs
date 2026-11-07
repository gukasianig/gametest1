using UnityEngine;

// Подставляет внешние модели вместо примитивов. Если набор отсутствует или выключен — ничего не делает.
// Логику и коллайдеры не трогает: исходные рендереры только выключаются.
public static class ModelSkins
{
    const string SkinName = "SkinModel";
    static ModelSkinSet set;
    static bool loaded;

    public static ModelSkinSet CurrentSet { get { return Set; } }

    static ModelSkinSet Set
    {
        get
        {
            if (!loaded) { set = Resources.Load<ModelSkinSet>("ModelSkinSet"); loaded = true; }
            return set != null && set.enabled ? set : null;
        }
    }

    public static void ApplyEnemy(GameObject enemy, string prefabName)
    {
        ModelSkinSet s = Set;
        if (s == null || enemy == null) return;
        ModelSkinSet.Entry entry = s.Find(prefabName);
        if (entry == null || enemy.transform.Find(SkinName) != null) return;

        Renderer[] original = enemy.GetComponentsInChildren<Renderer>();
        if (original.Length == 0) return;
        Bounds target = original[0].bounds;
        foreach (Renderer r in original) target.Encapsulate(r.bounds);

        GameObject model = Object.Instantiate(entry.model, enemy.transform);
        model.name = SkinName;
        // Сохраняем поворот корня из FBX (у моделей Blender/Codex он 270° по X) и поверх добавляем поворот записи.
        model.transform.localRotation = Quaternion.Euler(entry.eulerOffset) * entry.model.transform.localRotation;
        // Родитель может быть сплюснут (пила, мина, кристалл) — компенсируем, чтобы модель не искажалась.
        Vector3 ls = enemy.transform.localScale;
        model.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.y)), 1f / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);

        Bounds mb = BoundsOf(model);
        float modelMax = Mathf.Max(mb.size.x, mb.size.y, mb.size.z);
        float targetMax = Mathf.Max(target.size.x, target.size.y, target.size.z);
        if (modelMax > 0.0001f) model.transform.localScale *= targetMax / modelMax * entry.sizeMultiplier;

        mb = BoundsOf(model);
        model.transform.position += new Vector3(target.center.x - mb.center.x, target.min.y - mb.min.y, target.center.z - mb.center.z);

        foreach (Renderer r in original) r.enabled = false;
        // Модели с частями Leg* получают процедурную анимацию шага (по реальной скорости врага).
        if (ModelWalkAnimator.CanAnimate(model.transform)) model.AddComponent<ModelWalkAnimator>();
    }

    // Часть модели-скина: сам SkinModel или любой его потомок.
    public static bool IsSkinPart(Transform t)
    {
        for (; t != null; t = t.parent)
            if (t.name == SkinName || t.name.StartsWith(SkinName + "_")) return true;
        return false;
    }

    // Мировые объекты спецоружия (пила, мина, турель, дрон): примитив заменяется моделью по имени.
    // Дополнительные части (ствол турели, винты дрона) скрываются, если модель их уже содержит.
    public static void ApplySpecial(GameObject item, string name)
    {
        ModelSkinSet s = Set;
        if (s == null || item == null) return;
        if (s.Find(name) != null) { ApplyEnemy(item, name); return; }
        string parent = name == "Turret barrel" ? "Turret" : name == "Drone rotor" ? "Drone" : null;
        if (parent != null && s.Find(parent) != null)
            foreach (Renderer r in item.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    // Ключ записи оружия: обычное — "Weapon<индекс>", специальное — "Kind_<вид>".
    public static string WeaponKey(WeaponData weapon, int index)
    {
        return weapon != null && weapon.kind != WeaponKind.Standard ? "Kind_" + weapon.kind : "Weapon" + index;
    }

    // Декорация арены: куб-укрытие скрывается (коллайдер остаётся), модель растягивается до его габарита.
    // Ключ — имя объекта целиком или его первое слово ("Cargo A" → "Cargo").
    public static void ApplyProp(GameObject prop, string name)
    {
        ModelSkinSet s = Set;
        if (s == null || prop == null || !Application.isPlaying) return;
        ModelSkinSet.Entry entry = s.Find(name) ?? s.Find(name.Split(' ')[0]);
        Renderer cube = prop.GetComponent<Renderer>();
        if (entry == null || cube == null) return;

        Bounds target = cube.bounds;
        Transform parent = prop.transform.parent;
        GameObject model = Object.Instantiate(entry.model, parent);
        model.name = SkinName + "_" + name;
        model.transform.rotation = Quaternion.Euler(entry.eulerOffset);
        model.transform.localScale = Vector3.one;
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);

        Bounds mb = BoundsOf(model);
        Vector3 size = mb.size;
        if (size.x < 0.0001f || size.y < 0.0001f || size.z < 0.0001f) { Object.Destroy(model); return; }
        Vector3 factor;
        if (entry.stretch) factor = new Vector3(target.size.x / size.x, target.size.y / size.y, target.size.z / size.z);
        else factor = Vector3.one * Mathf.Min(target.size.x / size.x, target.size.y / size.y, target.size.z / size.z);
        model.transform.localScale = Vector3.Scale(model.transform.localScale, factor) * entry.sizeMultiplier;

        mb = BoundsOf(model);
        model.transform.position += new Vector3(target.center.x - mb.center.x, target.min.y - mb.min.y, target.center.z - mb.center.z);
        cube.enabled = false;
    }

    // Модель оружия в сокете руки героя. index — номер пресета AutoShoter.weapons (0–3).
    public static void ApplyHeroWeapon(Transform hero, int index) { ApplyHeroWeapon(hero, "Weapon" + index); }

    public static void ApplyHeroWeapon(Transform hero, string key)
    {
        ModelSkinSet s = Set;
        if (s == null || hero == null) return;
        Transform socket = FindDeep(hero, "WeaponSocket");
        if (socket == null) return;

        Transform old = socket.Find(SkinName);
        currentMuzzle = null;
        if (old != null)
        {
            old.name = SkinName + "Old";
            old.gameObject.SetActive(false);
            Object.Destroy(old.gameObject);
        }

        ModelSkinSet.Entry entry = s.Find(key);
        if (entry == null) return;

        GameObject model = Object.Instantiate(entry.model, socket);
        model.name = SkinName;
        model.transform.localPosition = entry.localPosition;
        model.transform.localRotation = Quaternion.Euler(entry.eulerOffset);
        model.transform.localScale = Vector3.one * entry.sizeMultiplier;
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        if (entry.centerOnHand && LocalBounds(model.transform, hero, out Vector3 bmin, out Vector3 bmax))
        {
            // Центр модели по длине — чуть впереди кисти (у некоторых моделей начало координат на конце ствола).
            float handZ = hero.InverseTransformPoint(socket.position).z;
            float shift = handZ + 0.1f - (bmin.z + bmax.z) * 0.5f;
            model.transform.position += hero.forward * shift;
        }
        CreateMuzzle(model.transform, hero, entry.muzzleOffset);
    }

    // Точка выстрела на конце ствола: самая передняя точка модели вдоль «вперёд» героя, по центру и чуть выше середины.
    static void CreateMuzzle(Transform skin, Transform hero, Vector3 offset)
    {
        if (!LocalBounds(skin, hero, out Vector3 min, out Vector3 max)) return;
        Vector3 local = new Vector3((min.x + max.x) * 0.5f, min.y + (max.y - min.y) * 0.65f, max.z) + offset;
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(skin, true);
        muzzle.position = hero.TransformPoint(local);
        currentMuzzle = muzzle;
    }

    // Габарит модели в системе героя (по границам мешей, без чтения вершин).
    static bool LocalBounds(Transform skin, Transform hero, out Vector3 min, out Vector3 max)
    {
        bool any = false;
        min = Vector3.zero; max = Vector3.zero;
        foreach (MeshFilter mf in skin.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = hero.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!any) { min = max = p; any = true; }
                else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            }
        }
        return any;
    }

    static Transform currentMuzzle;

    // Мировая позиция дула модели оружия или false, если модели нет (тогда стреляем из firePoint).
    public static bool TryGetMuzzle(out Vector3 position)
    {
        if (currentMuzzle != null && currentMuzzle.gameObject.activeInHierarchy) { position = currentMuzzle.position; return true; }
        position = Vector3.zero;
        return false;
    }

    static Bounds BoundsOf(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform t = FindDeep(root.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }
}
