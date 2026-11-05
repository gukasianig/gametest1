using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off helper that creates enemy prefabs by copying Enemy_Shooter and registers them in the scene's WaveManager.
// Safe to re-run: existing prefabs and registered entries are skipped. Batch: -executeMethod NewEnemyBuilder.RunBatch (no -quit).
public static class NewEnemyBuilder
{
    private class Def
    {
        public string prefab, typeName;
        public EnemyRole role;
        public int health, damage, experience, cost, weight, minWave;
        public float speed, scale, minSeconds;
        public bool usesWeakAsMinion;
    }

    private static readonly Def[] Defs =
    {
        new Def { prefab = "Enemy_Summoner", typeName = "Summoner", role = EnemyRole.Summoner, health = 110, damage = 8, experience = 14,
            speed = 2.0f, scale = 1.1f, cost = 18, weight = 9, minWave = 9, minSeconds = 120f, usesWeakAsMinion = true },
        new Def { prefab = "Enemy_Sniper", typeName = "Sniper", role = EnemyRole.Sniper, health = 55, damage = 8, experience = 12,
            speed = 2.4f, scale = 1f, cost = 12, weight = 12, minWave = 10, minSeconds = 130f },
    };

    public static void RunBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    public static void Build()
    {
        foreach (Def def in Defs) CreatePrefab(def);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Register();
    }

    private static void CreatePrefab(Def def)
    {
        string path = "Assets/" + def.prefab + ".prefab";
        if (!File.Exists(path) && !AssetDatabase.CopyAsset("Assets/Enemy_Shooter.prefab", path))
            throw new System.Exception("Cannot copy Enemy_Shooter to " + path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = def.prefab;
            root.transform.localScale = Vector3.one * def.scale;
            EnemyAI ai = root.GetComponent<EnemyAI>();
            ai.speed = def.speed;
            ai.damage = def.damage;
            Health health = root.GetComponent<Health>();
            health.maxHealth = def.health;
            health.experienceReward = def.experience;
            EnemyAbility ability = root.GetComponent<EnemyAbility>();
            ability.role = def.role;
            ability.splitPrefab = def.usesWeakAsMinion ? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Enemy_Weak.prefab") : null;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Register()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        WaveManager waves = Object.FindObjectOfType<WaveManager>();
        if (waves == null) throw new System.Exception("No WaveManager in SampleScene.");
        SerializedObject serialized = new SerializedObject(waves);
        SerializedProperty list = serialized.FindProperty("enemies");
        foreach (Def def in Defs)
        {
            bool exists = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == def.typeName) exists = true;
            if (exists) continue;
            int index = list.arraySize;
            list.arraySize++;
            SerializedProperty entry = list.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("name").stringValue = def.typeName;
            entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/" + def.prefab + ".prefab");
            entry.FindPropertyRelative("baseCost").intValue = def.cost;
            entry.FindPropertyRelative("weight").intValue = def.weight;
            entry.FindPropertyRelative("minWave").intValue = def.minWave;
            entry.FindPropertyRelative("minSurvivalSeconds").floatValue = def.minSeconds;
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(waves);
        EditorSceneManager.MarkSceneDirty(waves.gameObject.scene);
        EditorSceneManager.SaveScene(waves.gameObject.scene);
    }
}
