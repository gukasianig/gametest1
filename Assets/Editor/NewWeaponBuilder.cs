using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off helper: appends the special weapons to AutoShoter.weapons in SampleScene (indices 4..12).
// Safe to re-run: weapons whose weaponName already exists are skipped.
// Batch: -executeMethod NewWeaponBuilder.RunBatch (without -quit). Tune numbers afterwards in the Inspector.
public static class NewWeaponBuilder
{
    private class Def
    {
        public string name, title, role, description;
        public WeaponKind kind;
        public TargetRule targeting = TargetRule.Nearest;
        public float fireRate, range, bulletSpeed = 20f, bulletLifeTime = 3f, beamWidth = 0.7f, explosionRadius = 3.2f,
            chainRadius = 6f, chainFalloff = 0.75f, lifetime = 18f, effectDuration = 3f, slowFactor = 0.5f,
            burnPerSecond = 6f, coneAngle = 55f, placedFireRate = 0.4f;
        public int damage, chainJumps = 4, maxActive = 2;
    }

    private static readonly Def[] Defs =
    {
        new Def { name = "Saw", title = "Пила-бумеранг", role = "ВОЗВРАТНЫЙ УДАР", kind = WeaponKind.Boomerang, fireRate = 1.4f, damage = 22, range = 14f,
            bulletSpeed = 15f, beamWidth = 1.1f, maxActive = 1, description = "Диск бьёт врагов туда и обратно" },
        new Def { name = "Mines", title = "Сапёрные мины", role = "ЛОВУШКИ", kind = WeaponKind.Mines, fireRate = 1.0f, damage = 40, range = 14f,
            explosionRadius = 3.2f, maxActive = 6, lifetime = 25f, description = "Мины под ногами подрывают преследователей" },
        new Def { name = "Rocket", title = "Ракетница", role = "ПО ПЛОЩАДИ", kind = WeaponKind.Rocket, targeting = TargetRule.Densest, fireRate = 1.8f,
            damage = 55, range = 20f, bulletSpeed = 9f, bulletLifeTime = 3f, explosionRadius = 3.5f, description = "Взрыв по скоплению врагов" },
        new Def { name = "Arc", title = "Электродуга", role = "ЦЕПНАЯ МОЛНИЯ", kind = WeaponKind.Arc, fireRate = 0.7f, damage = 14, range = 12f,
            chainJumps = 4, chainRadius = 6f, chainFalloff = 0.75f, description = "Молния перескакивает между врагами" },
        new Def { name = "Flame", title = "Огнемёт", role = "ВБЛИЗИ", kind = WeaponKind.Flame, fireRate = 0.15f, damage = 4, range = 7f,
            coneAngle = 55f, burnPerSecond = 6f, effectDuration = 2.5f, description = "Конус огня и горение" },
        new Def { name = "Ice", title = "Ледяная пушка", role = "ЗАМЕДЛЕНИЕ", kind = WeaponKind.Ice, fireRate = 1.0f, damage = 12, range = 16f,
            bulletSpeed = 14f, explosionRadius = 2.5f, slowFactor = 0.5f, effectDuration = 3f, description = "Замедляет врагов в зоне попадания" },
        new Def { name = "Rail", title = "Рельсовая пушка", role = "ПРОБИВАНИЕ", kind = WeaponKind.Rail, targeting = TargetRule.Strongest, fireRate = 1.6f,
            damage = 60, range = 28f, beamWidth = 0.7f, description = "Луч пробивает всю линию врагов" },
        new Def { name = "Turret", title = "Турель", role = "ОБОРОНА", kind = WeaponKind.Turret, fireRate = 7f, damage = 9, range = 14f,
            maxActive = 2, lifetime = 18f, placedFireRate = 0.4f, description = "Ставит до двух стреляющих турелей" },
        new Def { name = "Drones", title = "Дроны-стрелки", role = "ВТОРОЙ СТВОЛ", kind = WeaponKind.Drone, fireRate = 0.5f, damage = 9, range = 16f,
            maxActive = 2, placedFireRate = 0.5f, description = "Два дрона стреляют по разным целям" },
    };

    public static void RunBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    public static void Build()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        AutoShoter shooter = Object.FindObjectOfType<AutoShoter>();
        if (shooter == null) throw new System.Exception("No AutoShoter in SampleScene.");
        SerializedObject serialized = new SerializedObject(shooter);
        SerializedProperty list = serialized.FindProperty("weapons");
        foreach (Def def in Defs)
        {
            bool exists = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("weaponName").stringValue == def.name) exists = true;
            if (exists) continue;
            int index = list.arraySize;
            list.arraySize++;
            SerializedProperty e = list.GetArrayElementAtIndex(index);
            e.FindPropertyRelative("weaponName").stringValue = def.name;
            e.FindPropertyRelative("title").stringValue = def.title;
            e.FindPropertyRelative("role").stringValue = def.role;
            e.FindPropertyRelative("description").stringValue = def.description;
            e.FindPropertyRelative("kind").enumValueIndex = (int)def.kind;
            e.FindPropertyRelative("targeting").enumValueIndex = (int)def.targeting;
            e.FindPropertyRelative("bulletPrefab").objectReferenceValue = null;
            e.FindPropertyRelative("fireRate").floatValue = def.fireRate;
            e.FindPropertyRelative("damage").intValue = def.damage;
            e.FindPropertyRelative("range").floatValue = def.range;
            e.FindPropertyRelative("pelletCount").intValue = 1;
            e.FindPropertyRelative("spreadAngle").floatValue = 0f;
            e.FindPropertyRelative("bulletSpeed").floatValue = def.bulletSpeed;
            e.FindPropertyRelative("bulletLifeTime").floatValue = def.bulletLifeTime;
            e.FindPropertyRelative("beamWidth").floatValue = def.beamWidth;
            e.FindPropertyRelative("explosionRadius").floatValue = def.explosionRadius;
            e.FindPropertyRelative("chainJumps").intValue = def.chainJumps;
            e.FindPropertyRelative("chainRadius").floatValue = def.chainRadius;
            e.FindPropertyRelative("chainFalloff").floatValue = def.chainFalloff;
            e.FindPropertyRelative("maxActive").intValue = def.maxActive;
            e.FindPropertyRelative("lifetime").floatValue = def.lifetime;
            e.FindPropertyRelative("effectDuration").floatValue = def.effectDuration;
            e.FindPropertyRelative("slowFactor").floatValue = def.slowFactor;
            e.FindPropertyRelative("burnPerSecond").floatValue = def.burnPerSecond;
            e.FindPropertyRelative("coneAngle").floatValue = def.coneAngle;
            e.FindPropertyRelative("placedFireRate").floatValue = def.placedFireRate;
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(shooter);
        EditorSceneManager.MarkSceneDirty(shooter.gameObject.scene);
        EditorSceneManager.SaveScene(shooter.gameObject.scene);
    }
}
