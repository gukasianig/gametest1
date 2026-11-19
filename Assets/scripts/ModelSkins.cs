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
        // Родитель может быть сплюснут (пила, мина, турель, кристалл) — компенсируем, чтобы модель не искажалась.
        // Компенсация по осям самой модели: у FBX из Blender корень повёрнут на 90° по X, и его локальная Y смотрит
        // вдоль Z родителя (11.11.2026: раньше делили по осям родителя, и турель/мина/пила выходили сплющенными).
        Vector3 ls = enemy.transform.localScale;
        Quaternion turn = model.transform.localRotation;
        model.transform.localScale = new Vector3(1f / AxisScale(turn * Vector3.right, ls), 1f / AxisScale(turn * Vector3.up, ls),
            1f / AxisScale(turn * Vector3.forward, ls));
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

    // Масштаб родителя вдоль направления оси модели (для поворотов на 90° — ровно масштаб соответствующей оси родителя).
    static float AxisScale(Vector3 axis, Vector3 scale)
    {
        return Mathf.Max(0.0001f, Mathf.Abs(axis.x) * Mathf.Abs(scale.x) + Mathf.Abs(axis.y) * Mathf.Abs(scale.y) + Mathf.Abs(axis.z) * Mathf.Abs(scale.z));
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
        if (index == WeaponElements.RifleWeapon && weapon != null && weapon.kind == WeaponKind.Standard)
            return WeaponElements.RifleModelKey(WeaponElements.Equipped(index));
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
        SetOffhandWeapon(hero, false);

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

        GameObject model = CreateHeldWeapon(socket, entry);
        currentWeaponFx = model.GetComponent<WeaponFireAnimation>();
    }

    private static GameObject CreateHeldWeapon(Transform socket, ModelSkinSet.Entry entry)
    {
        GameObject model = Object.Instantiate(entry.model, socket);
        model.name = SkinName;
        model.transform.localPosition = entry.localPosition;
        model.transform.localRotation = Quaternion.Euler(entry.eulerOffset);
        model.transform.localScale = Vector3.one * entry.sizeMultiplier;
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        if (entry.centerOnHand && LocalBounds(model.transform, socket, out Vector3 bmin, out Vector3 bmax))
        {
            // Fit the rear grip in socket space so switching a weapon mid-animation keeps the same grip.
            Vector3 grip = new Vector3((bmin.x + bmax.x) * .5f, Mathf.Lerp(bmin.y, bmax.y, .26f), Mathf.Lerp(bmin.z, bmax.z, .28f));
            model.transform.localPosition += entry.localPosition - grip;
        }
        CreateMuzzle(model.transform, socket, entry.muzzleOffset);
        // Анимация выстрела (отскок, перезарядка боеголовки, дым) — запоминается после окончательной расстановки.
        WeaponFireAnimation fx = model.AddComponent<WeaponFireAnimation>();
        fx.Init(socket);
        return model;
    }

    public const string OffhandSocketName = "LeftWeaponSocket";

    public static void SetOffhandWeapon(Transform hero, bool equipped)
    {
        Transform grip = FindDeep(hero, "LeftHandGrip");
        if (grip == null) return;
        Transform socket = grip.Find(OffhandSocketName);
        if (!equipped)
        {
            if (socket != null)
            {
                socket.name = OffhandSocketName + "Old";
                socket.gameObject.SetActive(false);
                Object.Destroy(socket.gameObject);
            }
            return;
        }
        if (socket != null) return;
        ModelSkinSet.Entry entry = Set != null ? Set.Find("Weapon0") : null;
        if (entry == null) return;
        socket = new GameObject(OffhandSocketName).transform;
        socket.SetParent(grip, false);
        Transform primaryMuzzle = currentMuzzle;
        CreateHeldWeapon(socket, entry);
        currentMuzzle = primaryMuzzle;
    }

    public static Transform FindOffhandMuzzle(Transform hero)
    {
        Transform socket = FindDeep(hero, OffhandSocketName);
        Transform skin = socket != null ? socket.Find(SkinName) : null;
        return skin != null ? skin.Find(MuzzleName) : null;
    }

    static WeaponFireAnimation currentWeaponFx;

    // Анимация выстрела текущей модели оружия героя или null.
    public static WeaponFireAnimation CurrentWeaponFx
    {
        get { return currentWeaponFx != null && currentWeaponFx.gameObject.activeInHierarchy ? currentWeaponFx : null; }
    }

    // The muzzle is calculated in weapon coordinates and moves with the grip.
    static void CreateMuzzle(Transform skin, Transform socket, Vector3 offset)
    {
        if (!LocalBounds(skin, socket, out Vector3 min, out Vector3 max)) return;
        Vector3 local = new Vector3((min.x + max.x) * 0.5f, min.y + (max.y - min.y) * 0.65f, max.z) + offset;
        var muzzle = new GameObject(MuzzleName).transform;
        muzzle.SetParent(skin, true);
        muzzle.position = socket.TransformPoint(local);
        currentMuzzle = muzzle;
    }

    // Своё имя: в FBX оружия из Blender есть маркер «Muzzle», а точка выстрела считается здесь с поправкой записи.
    public const string MuzzleName = "SkinMuzzle";

    // Дуло модели в руке конкретного героя (кооператив: у каждого своё; общий currentMuzzle помнит только последнюю модель).
    public static Transform FindMuzzle(Transform hero)
    {
        if (hero == null) return null;
        Transform socket = FindDeep(hero, "WeaponSocket");
        Transform skin = socket != null ? socket.Find(SkinName) : null;
        return skin != null ? skin.Find(MuzzleName) : null;
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
