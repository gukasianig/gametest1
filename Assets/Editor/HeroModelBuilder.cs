using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Opt-in asset authoring. Does not modify the gameplay scene or player physics.
public static class HeroModelBuilder
{
    private const string Folder = "Assets/Art/Characters/HeroOperator";
    private const string Preview = "docs/art/HeroOperator";
    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Vector2> Uvs = new List<Vector2>();
    private static readonly List<BoneWeight> Weights = new List<BoneWeight>();
    private static readonly List<List<int>> Faces = new List<List<int>>();
    private static readonly List<Transform> Bones = new List<Transform>();
    private static readonly Dictionary<string, int> BoneIds = new Dictionary<string, int>();
    private static Transform root;
    private static readonly string[] MaterialNames = { "Graphite", "Titanium", "Rubber", "Steel", "TealLight", "Scarf", "Brass", "DeepTeal" };
    private static readonly string[] Colors = { "34454E", "C5CEC8", "18262D", "71868B", "37E6CF", "A99C76", "BA9055", "196B72" };

    public static void RunBatch()
    {
        try { Build(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Art/Build Hero Operator")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(Preview);
        AssetDatabase.Refresh();
        Vertices.Clear(); Uvs.Clear(); Weights.Clear(); Faces.Clear(); Bones.Clear(); BoneIds.Clear();
        for (int i = 0; i < MaterialNames.Length; i++) Faces.Add(new List<int>());
        GameObject hero = new GameObject("HeroOperator");
        root = hero.transform;
        Bone("Hips", null, new Vector3(0, .96f, 0));
        Bone("Spine", "Hips", new Vector3(0, 1.12f, 0));
        Bone("Chest", "Spine", new Vector3(0, 1.40f, 0));
        Bone("Head", "Chest", new Vector3(0, 1.70f, 0));
        foreach (int s in new[] { -1, 1 })
        {
            string side = s < 0 ? "L" : "R";
            Bone("UpperArm" + side, "Chest", new Vector3(s * .36f, 1.48f, 0));
            Bone("Forearm" + side, "UpperArm" + side, new Vector3(s * .43f, 1.16f, .025f));
            Bone("Hand" + side, "Forearm" + side, new Vector3(s * .44f, .94f, .055f));
            Bone("Thigh" + side, "Hips", new Vector3(s * .16f, .94f, 0));
            Bone("Shin" + side, "Thigh" + side, new Vector3(s * .18f, .53f, .015f));
            Bone("Foot" + side, "Shin" + side, new Vector3(s * .18f, .14f, .025f));
        }
        // Broad upper silhouette, narrow waist; front is +Z, sole is Y=0.
        Box("Hips", new Vector3(0, .97f, 0), new Vector3(.43f, .26f, .29f), .055f, 0);
        Box("Spine", new Vector3(0, 1.20f, -.01f), new Vector3(.39f, .27f, .27f), .06f, 2);
        Box("Chest", new Vector3(0, 1.40f, 0), new Vector3(.58f, .35f, .34f), .07f, 0);
        Box("Chest", new Vector3(0, 1.44f, .17f), new Vector3(.48f, .23f, .09f), .04f, 1);
        Box("Chest", new Vector3(0, 1.34f, .216f), new Vector3(.10f, .075f, .018f), .012f, 4);
        for (int i = 0; i < 3; i++) Box("Spine", new Vector3(0, 1.12f + i * .067f, .139f), new Vector3(.31f, .042f, .045f), .014f, 3);
        // Collar and short scarf stay clear of the back light.
        Box("Chest", new Vector3(0, 1.605f, .015f), new Vector3(.34f, .13f, .30f), .055f, 5);
        Box("Chest", new Vector3(-.22f, 1.39f, .235f), new Vector3(.12f, .31f, .045f), .02f, 5);
        // Helmet: faceted shell, framed visor and lower respirator.
        Rounded("Head", new Vector3(0, 1.785f, .005f), new Vector3(.215f, .215f, .205f), 0);
        Box("Head", new Vector3(0, 1.89f, .095f), new Vector3(.35f, .085f, .27f), .036f, 1);
        Box("Head", new Vector3(0, 1.79f, .184f), new Vector3(.355f, .118f, .075f), .032f, 2);
        Box("Head", new Vector3(0, 1.802f, .225f), new Vector3(.30f, .062f, .022f), .014f, 4);
        Box("Head", new Vector3(0, 1.70f, .18f), new Vector3(.235f, .10f, .12f), .025f, 3);
        for (int i = -1; i <= 1; i++) Box("Head", new Vector3(i * .052f, 1.70f, .243f), new Vector3(.018f, .047f, .01f), .003f, 2);
        foreach (int s in new[] { -1, 1 })
        {
            string side = s < 0 ? "L" : "R";
            Box("Head", new Vector3(s * .205f, 1.785f, -.005f), new Vector3(.065f, .19f, .18f), .025f, 1);
            Box("Head", new Vector3(s * .243f, 1.80f, .015f), new Vector3(.012f, .035f, .08f), .005f, 7);
            Box("UpperArm" + side, new Vector3(s * .365f, 1.475f, 0), new Vector3(.26f, .23f, .36f), .055f, 1);
            Box("UpperArm" + side, new Vector3(s * .43f, 1.28f, .01f), new Vector3(.185f, .27f, .22f), .035f, 0);
            Rounded("Forearm" + side, new Vector3(s * .43f, 1.16f, .025f), new Vector3(.105f, .09f, .105f), 2);
            Box("Forearm" + side, new Vector3(s * .44f, 1.055f, .035f), new Vector3(.205f, .21f, .25f), .04f, 3);
            Box("Forearm" + side, new Vector3(s * .44f, 1.07f, .17f), new Vector3(.13f, .11f, .035f), .018f, s < 0 ? 4 : 1);
            Box("Hand" + side, new Vector3(s * .44f, .89f, .07f), new Vector3(.15f, .16f, .18f), .035f, 2);
            Box("Hand" + side, new Vector3(s * .44f, .91f, .16f), new Vector3(.12f, .07f, .04f), .015f, 1);
            Box("Thigh" + side, new Vector3(s * .17f, .75f, 0), new Vector3(.235f, .37f, .27f), .045f, 0);
            Box("Thigh" + side, new Vector3(s * .17f, .76f, .13f), new Vector3(.18f, .25f, .065f), .03f, 3);
            Rounded("Shin" + side, new Vector3(s * .18f, .53f, .015f), new Vector3(.12f, .115f, .12f), 2);
            Box("Shin" + side, new Vector3(s * .18f, .53f, .14f), new Vector3(.20f, .19f, .10f), .035f, 1);
            Box("Shin" + side, new Vector3(s * .18f, .335f, 0), new Vector3(.22f, .30f, .245f), .04f, 0);
            Box("Shin" + side, new Vector3(s * .18f, .35f, .125f), new Vector3(.15f, .22f, .055f), .025f, 1);
            Box("Foot" + side, new Vector3(s * .18f, .115f, .07f), new Vector3(.25f, .20f, .40f), .045f, 2);
            Box("Foot" + side, new Vector3(s * .18f, .035f, .07f), new Vector3(.26f, .07f, .41f), .018f, 0);
            Box("Foot" + side, new Vector3(s * .18f, .155f, .20f), new Vector3(.22f, .09f, .14f), .025f, 3);
            Box("Hips", new Vector3(s * .245f, 1.005f, .06f), new Vector3(.13f, .19f, .23f), .025f, 5);
            Box("Hips", new Vector3(s * .25f, 1.02f, .184f), new Vector3(.07f, .04f, .025f), .008f, 6);
        }
        Box("Hips", new Vector3(0, 1.075f, .02f), new Vector3(.49f, .07f, .35f), .025f, 2);
        Box("Hips", new Vector3(0, 1.075f, .206f), new Vector3(.10f, .065f, .035f), .012f, 6);
        Box("Chest", new Vector3(0, 1.385f, -.235f), new Vector3(.40f, .43f, .22f), .045f, 0);
        Box("Chest", new Vector3(0, 1.40f, -.355f), new Vector3(.29f, .31f, .055f), .025f, 3);
        Box("Chest", new Vector3(0, 1.41f, -.389f), new Vector3(.065f, .25f, .018f), .012f, 4);
        for (int i = 0; i < 3; i++)
            Box("Chest", new Vector3(.10f, 1.32f + i * .065f, -.387f), new Vector3(.055f, .022f, .018f), .005f, 2);
        Socket("WeaponSocket", "HandR", new Vector3(.44f, .89f, .18f));
        Socket("LeftHandGrip", "HandL", new Vector3(-.44f, .89f, .18f));
        Socket("BackSocket", "Chest", new Vector3(0, 1.40f, -.40f));
        Mesh mesh = new Mesh { name = "HeroOperatorMesh", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(Vertices); mesh.SetUVs(0, Uvs); mesh.boneWeights = Weights.ToArray();
        mesh.subMeshCount = Faces.Count;
        for (int i = 0; i < Faces.Count; i++) mesh.SetTriangles(Faces[i], i);
        Matrix4x4[] bind = new Matrix4x4[Bones.Count];
        for (int i = 0; i < Bones.Count; i++) bind[i] = Bones[i].worldToLocalMatrix * root.localToWorldMatrix;
        mesh.bindposes = bind; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = Save(mesh, Folder + "/HeroOperatorMesh.asset");
        Material[] materials = new Material[MaterialNames.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            ColorUtility.TryParseHtmlString("#" + Colors[i], out Color color);
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = MaterialNames[i] };
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", i == 1 || i == 3 ? .35f : .05f);
            mat.SetFloat("_Smoothness", i == 4 ? .7f : .34f);
            if (i == 4) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * 1.3f); }
            materials[i] = Save(mat, Folder + "/" + MaterialNames[i] + ".mat");
        }
        SkinnedMeshRenderer skin = hero.AddComponent<SkinnedMeshRenderer>();
        skin.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/HeroOperatorMesh.asset");
        skin.bones = Bones.ToArray(); skin.rootBone = Bones[0]; skin.sharedMaterials = materials;
        skin.localBounds = new Bounds(new Vector3(0, 1, 0), new Vector3(2.4f, 2.6f, 2.6f));
        Animation anim = hero.AddComponent<Animation>();
        AnimationClip idle = Clip(false); AnimationClip run = Clip(true);
        anim.AddClip(idle, "Idle"); anim.AddClip(run, "Run"); anim.clip = idle;
        PrefabUtility.SaveAsPrefabAsset(hero, Folder + "/HeroOperator.prefab");
        ExportObj(mesh);
        // Check the actual prefab after serializing; samples use real skin deformation.
        GameObject loaded = PrefabUtility.LoadPrefabContents(Folder + "/HeroOperator.prefab");
        SkinnedMeshRenderer loadedSkin = loaded.GetComponent<SkinnedMeshRenderer>();
        Mesh baked = new Mesh(); loadedSkin.BakeMesh(baked);
        Vector3[] restVertices = baked.vertices;
        if (Mathf.Abs(baked.bounds.min.y) > .001f || baked.bounds.size.y > 2.05f) throw new Exception("Hero scale/sole check failed");
        if (loadedSkin.bones.Length != 16 || loadedSkin.sharedMaterials.Length != 8) throw new Exception("Rig/material serialization check failed");
        run.SampleAnimation(loaded, .125f); loadedSkin.BakeMesh(baked);
        float maxMovement = 0;
        Vector3[] runningVertices = baked.vertices;
        for (int i = 0; i < runningVertices.Length; i++) maxMovement = Mathf.Max(maxMovement, Vector3.Distance(restVertices[i], runningVertices[i]));
        if (maxMovement < .10f) throw new Exception("Run animation does not deform the mesh");
        if (baked.bounds.size.y > 2.3f || baked.bounds.size.z > 2.5f) throw new Exception("Animation bounds failed");
        PrefabUtility.UnloadPrefabContents(loaded); UnityEngine.Object.DestroyImmediate(baked);
        RenderPreviews(hero, idle, run);
        string report = "HeroOperator asset checks PASS\n" +
            "Triangles: " + mesh.triangles.Length / 3 + "\nVertices: " + mesh.vertexCount + "\nBones: " + Bones.Count +
            "\nMaterials: 8\nHeight: " + mesh.bounds.size.y.ToString("F3", CultureInfo.InvariantCulture) +
            " m\nFront +Z; soles Y=0; sockets serialized; Idle/Run sampled.\nSeparate prefab only; gameplay scene untouched. No device/gameplay/performance test.\n";
        File.WriteAllText(Preview + "/VERIFICATION.txt", report);
        UnityEngine.Object.DestroyImmediate(hero);
        AssetDatabase.SaveAssets(); Debug.Log(report);
    }

    private static T Save<T>(T value, string path) where T : UnityEngine.Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
        EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); return existing;
    }

    private static void Bone(string name, string parent, Vector3 world)
    {
        Transform bone = new GameObject(name).transform;
        bone.SetParent(parent == null ? root : Bones[BoneIds[parent]], false); bone.position = world;
        BoneIds.Add(name, Bones.Count); Bones.Add(bone);
    }

    private static void Socket(string name, string bone, Vector3 world)
    {
        Transform socket = new GameObject(name).transform;
        socket.SetParent(Bones[BoneIds[bone]], false); socket.position = world;
    }

    private static void Polygon(List<Vector3> points, Vector3 center, int mat, int bone)
    {
        if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), points[0] - center) < 0) points.Reverse();
        int start = Vertices.Count;
        for (int i = 0; i < points.Count; i++)
        {
            Vertices.Add(points[i]); Uvs.Add(new Vector2((i == 1 || i == 2) ? 1 : 0, i >= 2 ? 1 : 0));
            Weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 });
        }
        for (int i = 1; i < points.Count - 1; i++) { Faces[mat].Add(start); Faces[mat].Add(start + i); Faces[mat].Add(start + i + 1); }
    }

    private static void Box(string bone, Vector3 c, Vector3 size, float bevel, int mat)
    {
        float x = size.x / 2, y = size.y / 2, z = size.z / 2;
        float b = Mathf.Min(bevel, Mathf.Min(x, Mathf.Min(y, z)) * .8f);
        Vector3[][] rings = new Vector3[4][];
        for (int r = 0; r < 4; r++)
        {
            float inset = (r == 0 || r == 3) ? b * .55f : 0;
            float yy = r == 0 ? -y : r == 1 ? -y + b : r == 2 ? y - b : y;
            float xx = x - inset, zz = z - inset, cut = b * .6f;
            rings[r] = new[] { new Vector3(-xx + cut, yy, -zz), new Vector3(xx - cut, yy, -zz), new Vector3(xx, yy, -zz + cut), new Vector3(xx, yy, zz - cut), new Vector3(xx - cut, yy, zz), new Vector3(-xx + cut, yy, zz), new Vector3(-xx, yy, zz - cut), new Vector3(-xx, yy, -zz + cut) };
            for (int i = 0; i < 8; i++) rings[r][i] += c;
        }
        int id = BoneIds[bone];
        Polygon(new List<Vector3>(rings[0]), c, mat, id); Polygon(new List<Vector3>(rings[3]), c, mat, id);
        for (int r = 0; r < 3; r++) for (int i = 0; i < 8; i++)
            Polygon(new List<Vector3> { rings[r][i], rings[r][(i + 1) % 8], rings[r + 1][(i + 1) % 8], rings[r + 1][i] }, c, mat, id);
    }

    private static void Rounded(string bone, Vector3 c, Vector3 radius, int mat)
    {
        const int sides = 12, rows = 6;
        for (int row = 0; row < rows; row++) for (int i = 0; i < sides; i++)
        {
            var points = new List<Vector3>();
            foreach (Vector2 uv in new[] { new Vector2(i, row), new Vector2(i + 1, row), new Vector2(i + 1, row + 1), new Vector2(i, row + 1) })
            {
                float a = uv.x * Mathf.PI * 2 / sides, p = -.5f * Mathf.PI + uv.y * Mathf.PI / rows;
                points.Add(c + Vector3.Scale(radius, new Vector3(Mathf.Cos(p) * Mathf.Cos(a), Mathf.Sin(p), Mathf.Cos(p) * Mathf.Sin(a))));
            }
            // At the poles remove coincident vertices to avoid degenerate triangles.
            for (int j = points.Count - 1; j >= 0; j--) if ((points[j] - points[(j + 1) % points.Count]).sqrMagnitude < 1e-10f) points.RemoveAt(j);
            Polygon(points, c, mat, BoneIds[bone]);
        }
    }

    private static AnimationClip Clip(bool running)
    {
        float length = running ? .64f : 2.4f;
        AnimationClip clip = new AnimationClip { name = running ? "Run" : "Idle", legacy = true, frameRate = 30, wrapMode = WrapMode.Loop };
        foreach (Transform bone in Bones)
        {
            string path = AnimationUtility.CalculateTransformPath(bone, root);
            var rx = new AnimationCurve(); var ry = new AnimationCurve(); var rz = new AnimationCurve(); var rw = new AnimationCurve();
            for (int k = 0; k <= 32; k++)
            {
                float t = length * k / 32, wave = Mathf.Sin(k * Mathf.PI * 2 / 32);
                float angle = 0;
                if (running)
                {
                    if (bone.name.StartsWith("Thigh")) angle = 30 * wave * (bone.name.EndsWith("L") ? 1 : -1);
                    if (bone.name.StartsWith("Shin")) angle = 55 * Mathf.Max(0, wave * (bone.name.EndsWith("L") ? -1 : 1));
                    if (bone.name.StartsWith("UpperArm")) angle = 24 * wave * (bone.name.EndsWith("L") ? -1 : 1) - 10;
                    if (bone.name.StartsWith("Forearm")) angle = -25;
                    if (bone.name == "Chest") angle = 8;
                }
                else if (bone.name == "Chest") angle = wave * 1.6f;
                Quaternion q = Quaternion.Euler(angle, 0, 0);
                rx.AddKey(t, q.x); ry.AddKey(t, q.y); rz.AddKey(t, q.z); rw.AddKey(t, q.w);
            }
            clip.SetCurve(path, typeof(Transform), "localRotation.x", rx); clip.SetCurve(path, typeof(Transform), "localRotation.y", ry);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", rz); clip.SetCurve(path, typeof(Transform), "localRotation.w", rw);
        }
        clip.EnsureQuaternionContinuity(); return Save(clip, Folder + "/" + clip.name + ".anim");
    }

    private static void ExportObj(Mesh mesh)
    {
        var obj = new StringBuilder("# Hero Operator; meters; Y up; +Z forward; static rest pose\nmtllib HeroOperator.mtl\n");
        foreach (Vector3 v in mesh.vertices) obj.AppendFormat(CultureInfo.InvariantCulture, "v {0} {1} {2}\n", v.x, v.y, v.z);
        foreach (Vector2 uv in mesh.uv) obj.AppendFormat(CultureInfo.InvariantCulture, "vt {0} {1}\n", uv.x, uv.y);
        foreach (Vector3 n in mesh.normals) obj.AppendFormat(CultureInfo.InvariantCulture, "vn {0} {1} {2}\n", n.x, n.y, n.z);
        var mtl = new StringBuilder();
        for (int m = 0; m < MaterialNames.Length; m++)
        {
            obj.Append("usemtl " + MaterialNames[m] + "\n");
            int[] tris = mesh.GetTriangles(m);
            for (int i = 0; i < tris.Length; i += 3)
            {
                obj.Append("f");
                for (int j = 0; j < 3; j++) { int v = tris[i + j] + 1; obj.AppendFormat(" {0}/{0}/{0}", v); }
                obj.Append('\n');
            }
            ColorUtility.TryParseHtmlString("#" + Colors[m], out Color c);
            mtl.AppendFormat(CultureInfo.InvariantCulture, "newmtl {0}\nKd {1} {2} {3}\nNs 40\n\n", MaterialNames[m], c.r, c.g, c.b);
        }
        File.WriteAllText(Preview + "/HeroOperator.obj", obj.ToString()); File.WriteAllText(Preview + "/HeroOperator.mtl", mtl.ToString());
    }

    private static void RenderPreviews(GameObject hero, AnimationClip idle, AnimationClip run)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var copy = UnityEngine.Object.Instantiate(hero); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, scene);
        SkinnedMeshRenderer previewSkin = copy.GetComponent<SkinnedMeshRenderer>();
        Mesh previewMesh = new Mesh();
        MeshFilter previewFilter = copy.AddComponent<MeshFilter>(); previewFilter.sharedMesh = previewMesh;
        MeshRenderer previewRenderer = copy.AddComponent<MeshRenderer>(); previewRenderer.sharedMaterials = previewSkin.sharedMaterials;
        previewSkin.enabled = false;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
        Material groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); groundMat.color = new Color(.13f, .17f, .19f); ground.GetComponent<Renderer>().sharedMaterial = groundMat;
        var lightGo = new GameObject("Key", typeof(Light)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, scene);
        Light light = lightGo.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; light.shadows = LightShadows.Soft; lightGo.transform.rotation = Quaternion.Euler(45, -35, 0);
        var fill = new GameObject("Fill", typeof(Light)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fill, scene);
        fill.GetComponent<Light>().type = LightType.Directional; fill.GetComponent<Light>().intensity = .65f; fill.transform.rotation = Quaternion.Euler(20, 140, 0);
        var cameraGo = new GameObject("PreviewCamera", typeof(Camera)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo, scene);
        Camera camera = cameraGo.GetComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .105f, .13f); camera.orthographic = true; camera.orthographicSize = 1.3f; camera.nearClipPlane = .05f; camera.farClipPlane = 50;
        RenderTexture rt = new RenderTexture(900, 1000, 24); camera.targetTexture = rt;
        Vector3 target = new Vector3(0, 1, 0);
        string[] names = { "front", "back", "top" };
        Vector3[] views = { new Vector3(3, 2.5f, 5), new Vector3(-3, 2.5f, -5), new Vector3(0, 8, -2) };
        for (int i = 0; i < names.Length; i++)
        {
            idle.SampleAnimation(copy, 0); previewSkin.BakeMesh(previewMesh); camera.transform.position = target + views[i]; camera.transform.LookAt(target);
            Capture(camera, rt, Preview + "/" + names[i] + ".png");
        }
        camera.orthographicSize = 1.34f; camera.transform.position = target + views[0]; camera.transform.LookAt(target);
        Directory.CreateDirectory(Preview + "/run-frames");
        for (int i = 0; i < 24; i++) { run.SampleAnimation(copy, run.length * i / 24); previewSkin.BakeMesh(previewMesh); Capture(camera, rt, Preview + "/run-frames/" + i.ToString("D2") + ".png"); }
        camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(groundMat); EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(previewMesh);
    }

    private static void Capture(Camera camera, RenderTexture rt, string path)
    {
        RenderTexture old = RenderTexture.active;
        camera.Render(); RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex); RenderTexture.active = old;
    }
}
