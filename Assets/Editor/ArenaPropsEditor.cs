using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Редактируемый декор арены «Завод»: меню Arena/..., запекание случайного декора в сцену, пометка препятствий.
public static class ArenaPropsEditor
{
    const string RootName = "Arena Props (edit me)";

    static ArenaPropsRoot FindOrCreateRoot()
    {
        ArenaPropsRoot root = Object.FindObjectOfType<ArenaPropsRoot>(true);
        if (root != null) return root;
        var go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Create arena props root");
        return go.AddComponent<ArenaPropsRoot>();
    }

    [MenuItem("Arena/Create empty props root")]
    public static void CreateRoot()
    {
        ArenaPropsRoot root = FindOrCreateRoot();
        Selection.activeGameObject = root.gameObject;
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }

    [MenuItem("Arena/Bake random decor into scene (editable)")]
    public static void BakeMenu()
    {
        ArenaPropsRoot existing = Object.FindObjectOfType<ArenaPropsRoot>(true);
        if (existing != null && existing.transform.childCount > 0)
        {
            EditorUtility.DisplayDialog("Arena", "Корень \"" + existing.name + "\" уже заполнен. Чтобы запечь заново, удалите его детей вручную.", "OK");
            return;
        }
        string result = Bake();
        EditorUtility.DisplayDialog("Arena", result, "OK");
    }

    // Возвращает текст-итог. Корень с детьми не трогает.
    public static string Bake()
    {
        ModelSkinSet set = Resources.Load<ModelSkinSet>("ModelSkinSet");
        if (set == null || set.decor == null || set.decor.Length == 0) return "Нет ModelSkinSet с декором.";
        ArenaPropsRoot root = FindOrCreateRoot();
        if (root.transform.childCount > 0) return "Корень уже заполнен — ничего не сделано.";

        var temp = new GameObject("TempArena").AddComponent<ArenaLayout>();
        try
        {
            temp.EditorCollectFactoryObstacles();
            ArenaDecor.Generate(temp, set, root.transform, d => (GameObject)PrefabUtility.InstantiatePrefab(d.model, root.transform), false);
        }
        finally { Object.DestroyImmediate(temp.gameObject); }
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "Расставлено объектов: " + root.transform.childCount + ". Двигайте, удаляйте, добавляйте модели из Assets/Art/Kenney внутрь \"" + root.name + "\". Не забудьте сохранить сцену (Ctrl+S).";
    }

    [MenuItem("Arena/Make selected props solid (obstacle)")]
    public static void MakeSolid()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            ArenaProp prop = go.GetComponent<ArenaProp>() ?? Undo.AddComponent<ArenaProp>(go);
            Undo.RecordObject(prop, "Solid prop");
            prop.solid = true;
        }
    }

    [MenuItem("Arena/Make selected props decorative (walk-through)")]
    public static void MakeDecorative()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            ArenaProp prop = go.GetComponent<ArenaProp>();
            if (prop == null) continue;
            Undo.RecordObject(prop, "Decor prop");
            prop.solid = false;
        }
    }

    // Batch: -executeMethod ArenaPropsEditor.BakeBatch -quit
    public static void BakeBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        string result = Bake();
        EditorSceneManager.SaveOpenScenes();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/arena-props-bake.txt", result);
        EditorApplication.Exit(0);
    }
}
