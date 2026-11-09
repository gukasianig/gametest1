using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Печёт иконки оружия из ТЕХ ЖЕ 3D-моделей, что герой держит в бою (Resources/ModelSkinSet.asset, ключи Weapon* и Kind_*).
// Результат: Assets/Resources/UI/Weapons/<ключ>.png (прозрачный фон). Экран выбора оружия берёт их по ModelSkins.WeaponKey.
// Меню: Tools/Bake Weapon Icons. Batch: -batchmode -executeMethod WeaponIconBaker.RunBatch (без -quit).
public static class WeaponIconBaker
{
    const int Size = 384;
    const string OutDir = "Assets/Resources/UI/Weapons";

    [MenuItem("Tools/Bake Weapon Icons")]
    public static void Bake()
    {
        ModelSkinSet set = Resources.Load<ModelSkinSet>("ModelSkinSet");
        if (set == null) { Debug.LogError("WeaponIconBaker: ModelSkinSet not found"); return; }
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        int done = 0;
        foreach (ModelSkinSet.Entry e in set.entries)
        {
            if (e == null || e.model == null) continue;
            if (!e.key.StartsWith("Weapon") && !e.key.StartsWith("Kind_")) continue;
            Texture2D tex = Render(e, Size);
            if (tex == null) continue;
            File.WriteAllBytes(OutDir + "/" + e.key + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            done++;
        }
        AssetDatabase.Refresh();
        Debug.Log("WeaponIconBaker: baked " + done + " icons into " + OutDir);
    }

    public static void RunBatch()
    {
        Bake();
        EditorApplication.Exit(0);
    }

    static Texture2D Render(ModelSkinSet.Entry e, int size)
    {
        Vector3 origin = new Vector3(5000f, 5000f, 5000f);
        var root = new GameObject("IconRoot").transform;
        root.position = origin;
        GameObject model = Object.Instantiate(e.model, root);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(e.eulerOffset);
        model.transform.localScale = Vector3.one;
        foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

        Renderer[] rs = model.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) { Object.DestroyImmediate(root.gameObject); return null; }
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);

        // Ствол смотрит вправо, лёгкий вид сверху-сзади — так оружие читается как «оружие».
        Vector3 dir = new Vector3(1f, 0.55f, -0.5f).normalized;
        var camGo = new GameObject("IconCam");
        camGo.transform.position = b.center + dir * 20f;
        camGo.transform.LookAt(b.center, Vector3.up);
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 60f;
        cam.allowHDR = false;
        cam.allowMSAA = false;

        float halfW = 0.01f, halfH = 0.01f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 rel = camGo.transform.InverseTransformPoint(corner);
            halfW = Mathf.Max(halfW, Mathf.Abs(rel.x));
            halfH = Mathf.Max(halfH, Mathf.Abs(rel.y));
        }
        // Поля вокруг модели; кадр квадратный.
        cam.orthographicSize = Mathf.Max(halfW, halfH) * 1.12f;

        // Свет: тёплый ключевой, холодный контровой, мягкий общий.
        MakeLight(new Vector3(40f, -60f, 0f), new Color(1f, 0.95f, 0.88f), 1.25f);
        MakeLight(new Vector3(20f, 130f, 0f), new Color(0.55f, 0.75f, 1f), 0.7f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);

        var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);

        foreach (Light l in Object.FindObjectsOfType<Light>()) Object.DestroyImmediate(l.gameObject);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(root.gameObject);
        return tex;
    }

    static void MakeLight(Vector3 euler, Color color, float intensity)
    {
        var go = new GameObject("IconLight");
        go.transform.rotation = Quaternion.Euler(euler);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = color;
        l.intensity = intensity;
    }
}
