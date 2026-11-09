using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Opt-in import setup and bind/animation validation. Does not save scenes.
public static class OperatorFbxImport
{
    private const string Folder = "Assets/Art/Characters/OperatorBlender";
    internal const string Path = Folder + "/Operator.fbx";
    private static readonly string[] ClipNames = { "Idle", "Run", "Dash", "Throw", "Hit", "Death" };

    public static void RunBatch()
    {
        try
        {
            ImportAndCheck();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Tools/Art/Import Operator FBX")]
    public static void ImportAndCheck()
    {
        var importer = AssetImporter.GetAtPath(Path) as ModelImporter;
        Require(importer != null, "Operator.fbx is missing");
        importer.animationType = ModelImporterAnimationType.Legacy;
        importer.importAnimation = true;
        importer.bakeAxisConversion = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.SaveAndReimport();

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            string name = clip.takeName.Split('|').Last();
            Require(ClipNames.Contains(name), "Unexpected animation stack: " + clip.takeName);
            clip.name = name;
            bool loop = name == "Idle" || name == "Run";
            clip.loopTime = loop;
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
        }
        Require(clips.Length == ClipNames.Length, "Expected six FBX animation takes, got " + clips.Length);
        importer.clipAnimations = ClipNames.Select(name => clips.Single(clip => clip.name == name)).ToArray();

        RemapMaterial(importer, "Graphite Suit", "282C38", .07f, .38f);
        RemapMaterial(importer, "Titanium Armor", "BEC0CC", .35f, .62f);
        RemapMaterial(importer, "Blue Steel", "535865", .37f, .57f);
        RemapMaterial(importer, "Joint Rubber", "171B23", .02f, .30f);
        RemapMaterial(importer, "Cyan Optics", "04D6FA", .09f, .70f, true);
        RemapMaterial(importer, "Brass Webbing", "A38C52", .23f, .51f);
        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var instance = Object.Instantiate(model);
        try { CheckModel(instance); }
        finally { Object.DestroyImmediate(instance); }
    }

    internal static bool InstallInScene(Scene scene)
    {
        var controller = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<HeroLocomotion>(true)).Single();
        var serialized = new SerializedObject(controller);
        var property = serialized.FindProperty("heroAnimation");
        var oldAnimation = (Animation)property.objectReferenceValue;
        Require(oldAnimation != null, scene.name + ": missing player model");
        var oldSkin = oldAnimation.GetComponentInChildren<SkinnedMeshRenderer>();
        if (oldSkin != null && AssetDatabase.GetAssetPath(oldSkin.sharedMesh) == Path)
            return false;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var oldVisual = oldAnimation.transform;
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, oldVisual.parent);
        visual.name = oldVisual.name;
        visual.transform.SetLocalPositionAndRotation(oldVisual.localPosition, oldVisual.localRotation);
        visual.transform.localScale = oldVisual.localScale;
        visual.transform.SetSiblingIndex(oldVisual.GetSiblingIndex());
        var animation = visual.GetComponent<Animation>();
        animation.clip = animation.GetClip("Idle");
        animation.playAutomatically = true;
        animation.cullingType = AnimationCullingType.AlwaysAnimate;
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animation);
        property.objectReferenceValue = animation;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Object.DestroyImmediate(oldVisual.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static void RemapMaterial(ModelImporter importer, string name, string colorHex, float metallic, float smoothness, bool emission = false)
    {
        string folder = Folder + "/Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Folder, "Materials");
        string path = folder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        ColorUtility.TryParseHtmlString("#" + colorHex, out Color color);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.25f);
        }
        EditorUtility.SetDirty(material);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
    }

    private static void CheckModel(GameObject instance)
    {
        var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
        var animation = instance.GetComponent<Animation>();
        Transform weapon = Find(instance, "WeaponSocket");
        Transform grenade = Find(instance, "LeftHandGrip");
        Transform back = Find(instance, "BackSocket");
        Debug.Log($"Operator FBX hierarchy: {string.Join(", ", instance.GetComponentsInChildren<Transform>().Select(t => t.name))}\n"
            + $"Weapon: {SocketDescription(weapon)}; Left: {SocketDescription(grenade)}; Back: {SocketDescription(back)}");
        Require(skin.bones.Length == 16 && skin.sharedMaterials.Length == 6, "Rig/material counts changed on import");
        Require(skin.sharedMaterials.All(mat => mat != null && mat.shader.name == "Universal Render Pipeline/Lit"), "URP material remap failed");
        Require(weapon != null && weapon.parent.name == "HandR" && weapon.position.x > 0, "Weapon socket must follow the right hand");
        Require(grenade != null && grenade.parent.name == "HandL" && grenade.position.x < 0, "Grenade socket must follow the left hand");
        Require(back != null && back.parent.name == "Chest", "Back socket must follow the chest");
        Require(weapon.forward.z > .97f && weapon.up.y > .97f, $"Weapon axes: forward {weapon.forward}, up {weapon.up}");
        Require(weapon.position.z > .04f && back.position.z < -.10f, "The character must face +Z");
        Require(animation != null, "Legacy Animation component is missing on FBX root");
        Require(animation.transform.Find("OperatorRig/Hips/Spine/Chest") != null, "FBX path is incompatible with HeroLocomotion");

        var mesh = new Mesh();
        var results = new List<string>();
        try
        {
            Vector3[] rest = skin.sharedMesh.vertices;
            Matrix4x4 restToRoot = instance.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            float minY = rest.Min(vertex => restToRoot.MultiplyPoint3x4(vertex).y);
            float maxY = rest.Max(vertex => restToRoot.MultiplyPoint3x4(vertex).y);
            Require(Mathf.Abs(minY) < .002f && Mathf.Abs(maxY - 2f) < .002f, $"Expected Y=0..2, got {minY:F4}..{maxY:F4}");
            Vector3 throwStart = Vector3.zero;
            foreach (string name in ClipNames)
            {
                AnimationClip clip = animation.GetClip(name);
                Require(clip != null && clip.legacy && clip.length > .15f, "Missing Legacy clip: " + name);
                clip.SampleAnimation(instance, 0);
                Vector3[] start = Bake(skin, mesh);
                if (name == "Throw") throwStart = grenade.position;
                clip.SampleAnimation(instance, clip.length * (name == "Idle" ? .25f : .47f));
                Vector3[] sample = Bake(skin, mesh);
                float movement = Enumerable.Range(0, start.Length).Max(i => Vector3.Distance(start[i], sample[i]));
                Require(movement > (name == "Idle" ? .005f : .12f), $"Clip does not deform the skin: {name}, {movement:F3} m");
                if (name == "Throw") Require(Vector3.Distance(grenade.position, throwStart) > .35f, "Grenade hand did not travel during throw");
                results.Add($"{name} {clip.length:F3}s / {movement:F3}m movement");
            }
            long triangles = Enumerable.Range(0, skin.sharedMesh.subMeshCount).Sum(i => (long)skin.sharedMesh.GetIndexCount(i)) / 3;
            string report = $"OPERATOR FBX PASS\nY={minY:F4}..{maxY:F4}m, {skin.bones.Length} bones, {triangles} triangles, {skin.sharedMaterials.Length} URP materials\n"
                + string.Join("\n", results);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/operator-fbx-results.txt", report + "\n");
            Debug.Log(report);
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    private static Vector3[] Bake(SkinnedMeshRenderer skin, Mesh mesh)
    {
        skin.BakeMesh(mesh);
        return mesh.vertices;
    }

    private static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == name);

    private static string SocketDescription(Transform socket) => socket == null ? "missing" :
        $"parent={socket.parent.name}, position={socket.position}, forward={socket.forward}, up={socket.up}";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
