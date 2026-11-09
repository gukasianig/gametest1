using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Собирает играбельную сцену Assets/Scenes/GreenArenaPlay.unity: игровая часть SampleScene (герой, камера, UI, волны)
// + ландшафт и объекты из GreenArena. SampleScene и GreenArena не меняются. Заводской пол (Grid) и декор Завода не берутся.
// Меню Arena → Build Green Arena Play Scene; batch: -executeMethod GreenArenaPlayBuilder.BuildBatch -quit
public static class GreenArenaPlayBuilder
{
    const string SamplePath = "Assets/Scenes/SampleScene.unity";
    const string GreenPath = "Assets/Scenes/GreenArena.unity";
    const string PlayPath = "Assets/Scenes/GreenArenaPlay.unity";

    [MenuItem("Arena/Build Green Arena Play Scene")]
    public static void Build()
    {
        // 1. Окружение GreenArena (туман, небо, свет окружения) нужно перенести вручную: оно хранится в самой сцене.
        EditorSceneManager.OpenScene(GreenPath, OpenSceneMode.Single);
        bool fog = RenderSettings.fog; FogMode fogMode = RenderSettings.fogMode; Color fogColor = RenderSettings.fogColor;
        float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
        AmbientMode ambientMode = RenderSettings.ambientMode;
        Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
        Material skybox = RenderSettings.skybox;

        // 2. Копия SampleScene как основа.
        Scene play = EditorSceneManager.OpenScene(SamplePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(play, PlayPath, false);
        foreach (GameObject root in play.GetRootGameObjects())
            if (root.name == "Grid" || root.name == "Arena Props (edit me)" || root.name == "Directional Light")
                Object.DestroyImmediate(root);

        // 3. Содержимое GreenArena (кроме его камеры).
        Scene green = EditorSceneManager.OpenScene(GreenPath, OpenSceneMode.Additive);
        foreach (GameObject root in green.GetRootGameObjects())
        {
            if (root.name == "Main Camera") { Object.DestroyImmediate(root); continue; }
            SceneManager.MoveGameObjectToScene(root, play);
        }
        EditorSceneManager.CloseScene(green, true);
        SceneManager.SetActiveScene(play);

        RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
        RenderSettings.skybox = skybox;
        foreach (Light l in Object.FindObjectsOfType<Light>())
            if (l.type == LightType.Directional) { RenderSettings.sun = l; break; }

        // 4. Правила открытого поля и камера.
        var rules = new GameObject("Green Arena Rules");
        rules.AddComponent<ArenaLayout>().openField = true;
        int fittedRocks = rules.AddComponent<GreenArenaObstacles>().FitRocks();   // точные коробки камней по вершинам
        Debug.Log("GreenArenaPlayBuilder: fitted rocks " + fittedRocks);
        Camera cam = Camera.main;
        if (cam != null) { cam.farClipPlane = 1500f; cam.clearFlags = CameraClearFlags.Skybox; }

        // 5. Герой стоит на земле.
        GameObject hero = GameObject.Find("Capsule");
        Terrain terrain = Object.FindObjectOfType<Terrain>();
        if (hero != null && terrain != null)
        {
            Vector3 p = hero.transform.position;
            float y = terrain.SampleHeight(new Vector3(p.x, 0f, p.z)) + terrain.transform.position.y;
            hero.transform.position = new Vector3(p.x, Mathf.Max(p.y, y + 1f), p.z);
        }

        EditorSceneManager.SaveScene(play);

        // Перезапуск забега грузит сцену по имени: она должна быть в Build Settings.
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == PlayPath)) scenes.Add(new EditorBuildSettingsScene(PlayPath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("GreenArenaPlayBuilder: ok " + PlayPath);
    }

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }
}
