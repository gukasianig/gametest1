using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Короткая проверка подключения модели героя: GreenArenaPlay статически и в Play Mode (SampleScene удалена 09.11.2026).
// Модель — вложенный Operator.fbx под Capsule; подошвы должны стоять на земле, оружие — в сокете правой руки.
// Результат: Logs/hero-model-check.txt. Запуск: -executeMethod HeroModelSceneCheck.RunBatch (без -quit).
[InitializeOnLoad]
public static class HeroModelSceneCheck
{
    const string Marker = "Logs/hero-model-check.request";
    const string Report = "Logs/hero-model-check.txt";
    static double started;
    static int step;
    static bool staticOk;
    static readonly StringBuilder log = new StringBuilder();

    static HeroModelSceneCheck()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker))
            {
                staticOk = File.ReadAllText(Marker).StartsWith("1");
                if (File.Exists(Report)) log.Append(File.ReadAllText(Report));
                File.Delete(Marker);
                started = EditorApplication.timeSinceStartup;
                step = 0;
                EditorApplication.update += Tick;
            }
        };
    }

    public static void RunBatch()
    {
        log.Clear();
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        bool ok = Describe("GreenArenaPlay (edit)", false);
        File.WriteAllText(Report, log.ToString());
        File.WriteAllText(Marker, ok ? "1" : "0");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    // Проверяет Capsule → HeroOperator: источник prefab, сетка, видимость, отключённая капсула, подошвы на земле.
    static bool Describe(string label, bool playMode)
    {
        GameObject capsule = GameObject.Find("Capsule");
        if (capsule == null) { log.AppendLine(label + ": FAIL no Capsule"); return false; }
        Transform hero = capsule.transform.Find("HeroOperator");
        if (hero == null) { log.AppendLine(label + ": FAIL no HeroOperator child"); return false; }
        var smr = hero.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null) { log.AppendLine(label + ": FAIL no skinned mesh"); return false; }
        string srcPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
        uint indices = 0;
        for (int i = 0; i < smr.sharedMesh.subMeshCount; i++) indices += smr.sharedMesh.GetIndexCount(i);
        uint tris = indices / 3;
        var capsuleRenderer = capsule.GetComponent<MeshRenderer>();

        // Нижняя точка реальной (скиннутой) сетки в мире против высоты земли под героем.
        var baked = new Mesh();
        smr.BakeMesh(baked);
        float minY = float.MaxValue;
        Matrix4x4 m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
        foreach (Vector3 v in baked.vertices) minY = Mathf.Min(minY, m.MultiplyPoint3x4(v).y);
        Object.DestroyImmediate(baked);
        float ground = playMode ? GroundHeight.At(capsule.transform.position) : 0f;
        float gap = minY - ground;

        Transform socket = FindDeep(hero, "WeaponSocket");
        int weaponChildren = socket != null ? socket.childCount : -1;
        var anim = hero.GetComponentInChildren<Animation>();
        string clip = "none";
        if (anim != null) foreach (AnimationState st in anim) if (anim.IsPlaying(st.name)) clip = st.name;

        bool ok = smr.enabled && hero.gameObject.activeInHierarchy && srcPath == OperatorFbxImport.Path
            && (capsuleRenderer == null || !capsuleRenderer.enabled) && Mathf.Abs(gap) < 0.08f && socket != null
            && (!playMode || weaponChildren > 0);
        log.AppendLine(label + ": " + (ok ? "OK" : "FAIL") + " source=" + srcPath + " mesh=" + (smr != null && smr.sharedMesh != null ? smr.sharedMesh.name : "none")
            + " tris=" + tris + " materials=" + (smr != null ? smr.sharedMaterials.Length : 0) + " heroLocal=" + hero.localPosition + " scale=" + hero.lossyScale
            + " capsuleRendererOn=" + (capsuleRenderer != null && capsuleRenderer.enabled) + " soleY=" + minY.ToString("0.000") + " ground=" + ground.ToString("0.000")
            + " gap=" + gap.ToString("0.000") + " weaponInSocket=" + weaponChildren + (playMode ? " clip=" + clip : ""));
        return ok;
    }

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - started;
        GameObject capsule = GameObject.Find("Capsule");
        if (capsule == null) { Finish(false); return; }
        if (step == 0 && t > 1.5)
        {
            capsule.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
            step = 1;
        }
        else if (step == 1 && t > 3.5)
        {
            Finish(Describe("GreenArenaPlay (play)", true) && staticOk);
        }
    }

    static void Finish(bool ok)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText(Report, (ok ? "RESULT OK" : "RESULT FAIL") + "\n" + log);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { Transform f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }
}
