using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Создаёт Assets/Resources/ModelSkinSet.asset с бластерами Kenney для 4 пресетов оружия и снимает контрольные кадры героя.
public static class KenneySkinSetup
{
    const string Models = "Assets/Art/Kenney/BlasterKit/Models/";
    const string AssetPath = "Assets/Resources/ModelSkinSet.asset";

    // Декорация арены: модель из другого набора, без поворота, растягивается по габариту куба.
    static ModelSkinSet.Entry A(string key, string kit, string fbx, bool stretch = true)
    {
        return new ModelSkinSet.Entry
        {
            key = key,
            model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Kenney/" + kit + "/Models/" + fbx + ".fbx"),
            stretch = stretch
        };
    }

    static ModelSkinSet.Decor D(string kit, string fbx, float size, int count, int cluster = 1, float minR = 14f, float maxR = 100f)
    {
        return new ModelSkinSet.Decor
        {
            label = fbx,
            model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Kenney/" + kit + "/Models/" + fbx + ".fbx"),
            targetSize = size, count = count, cluster = cluster, minRadius = minR, maxRadius = maxR
        };
    }

    static ModelSkinSet.Entry G(string key, string fbx, float yaw = 180f)
    {
        return new ModelSkinSet.Entry { key = key, model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Generated/" + fbx), eulerOffset = new Vector3(0f, yaw, 0f) };
    }

    static ModelSkinSet.Entry E(string key, string fbx, float yaw = 180f)
    {
        // Большинство бластеров Kenney смотрят в -Z, поэтому по умолчанию поворот на 180° вокруг Y; blaster-e (снайперка) смотрит в +Z.
        return new ModelSkinSet.Entry { key = key, model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + fbx), eulerOffset = new Vector3(0f, yaw, 0f), centerOnHand = true };
    }

    public static void Create()
    {
        ModelSkinSet set = AssetDatabase.LoadAssetAtPath<ModelSkinSet>(AssetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<ModelSkinSet>();
            AssetDatabase.CreateAsset(set, AssetPath);
        }
        set.enabled = true;
        set.entries = new[]
        {
            E("Weapon0", "blaster-b.fbx"), // пистолет
            E("Weapon1", "blaster-h.fbx"), // автомат
            E("Weapon2", "blaster-d.fbx"), // дробовик
            E("Weapon3", "blaster-e.fbx"), // тяжёлая винтовка
            // Специальные оружия (в руке героя; по виду оружия, не по индексу)
            E("Kind_Boomerang", "blaster-a.fbx"),
            E("Kind_Mines", "grenade-b.fbx"),
            E("Kind_Rocket", "blaster-f.fbx"),
            E("Kind_Arc", "blaster-g.fbx"),
            E("Kind_Flame", "blaster-l.fbx"),
            E("Kind_Ice", "blaster-o.fbx"),
            E("Kind_Rail", "blaster-p.fbx"),
            E("Kind_Turret", "blaster-j.fbx"),
            E("Kind_Drone", "blaster-i.fbx"),
            // Арена: укрытия вместо кубов (коллайдеры не меняются)
            A("Cargo", "SurvivalKit", "box-large"),
            A("North generator", "ModularSpaceKit", "room-small", false),
            A("Eastern relay", "ModularSpaceKit", "room-small-variation", false),
            A("Control tower", "ModularSpaceKit", "room-large", false),
            A("Cooling station", "ModularSpaceKit", "room-wide", false),
            A("Radar base", "ModularSpaceKit", "room-small-variation", false),
        };
        var withEnemies = new System.Collections.Generic.List<ModelSkinSet.Entry>(set.entries);
        EnemyModels.AddEntries(withEnemies);
        set.entries = withEnemies.ToArray();
        set.decor = new[]
        {
            D("SurvivalKit", "barrel", 1.2f, 22, 2),
            D("SurvivalKit", "barrel-open", 1.2f, 8),
            D("SurvivalKit", "box", 1.0f, 20, 3),
            D("SurvivalKit", "box-open", 1.1f, 8, 2),
            D("SurvivalKit", "box-large-open", 1.7f, 8),
            D("SurvivalKit", "chest", 1.1f, 8),
            D("SurvivalKit", "workbench", 2.0f, 6),
            D("SurvivalKit", "workbench-anvil", 1.5f, 4),
            D("SurvivalKit", "fence-fortified", 3.2f, 14),
            D("SurvivalKit", "metal-panel", 2.6f, 14),
            D("SurvivalKit", "metal-panel-screws", 2.6f, 8),
            D("SurvivalKit", "structure-metal-wall", 3.2f, 8),
            D("ModularSpaceKit", "cables", 2.2f, 18),
            D("ModularSpaceKit", "gate-lasers", 5.0f, 4),
            D("ModularSpaceKit", "template-wall-half", 3.6f, 12),
            D("ModularSpaceKit", "template-floor-detail", 4.0f, 16, 1, 10f, 100f),
            D("ModularSpaceKit", "stairs", 3.2f, 3),
        };
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log("KenneySkinSetup: entries=" + set.entries.Length);
    }

    public static void Preview()
    {
        Create();
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity", OpenSceneMode.Single);
        Directory.CreateDirectory("docs/art");
        var cam = new GameObject("PreviewCam").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.22f, 0.26f);
        cam.fieldOfView = 35f;
        var fill = new GameObject("Fill").AddComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = 1.2f;

        var shooter = Object.FindObjectOfType<AutoShoter>();
        Transform hero = shooter != null ? shooter.transform : null;
        if (hero == null) { Debug.Log("KenneySkinSetup: hero not found"); return; }
        for (int w = 0; w < 4; w++)
        {
            ModelSkins.ApplyHeroWeapon(hero, w);
            if (ModelSkins.TryGetMuzzle(out Vector3 mz))
            {
                var mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mark.name = "MuzzleMark";
                mark.transform.position = mz;
                mark.transform.localScale = Vector3.one * 0.07f;
                Object.DestroyImmediate(mark.GetComponent<Collider>());
                mark.GetComponent<Renderer>().sharedMaterial.color = Color.red;
                Debug.Log("KenneySkinSetup: muzzle" + w + " heroLocal=" + hero.InverseTransformPoint(mz).ToString("F2"));
            }
            Vector3 p = hero.position + Vector3.up * 1.0f;
            fill.transform.rotation = Quaternion.LookRotation(-(hero.right * 2f + hero.forward * 1f + Vector3.up * 2f));
            cam.transform.position = p + hero.right * 4.5f + hero.forward * 1.5f + Vector3.up * 0.8f;
            cam.transform.LookAt(p + hero.forward * 0.3f);
            Shot(cam, "docs/art/KenneyWeapon" + w + ".png");
            var old = GameObject.Find("MuzzleMark"); if (old != null) Object.DestroyImmediate(old);
        }
        Debug.Log("KenneySkinSetup: previews done");
    }

    static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(rt);
    }
}
