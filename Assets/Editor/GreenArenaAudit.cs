using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Проверка моделей ArenaGreen: размер, положение pivot, треугольники, материалы. batch: -executeMethod GreenArenaAudit.Run -quit
public static class GreenArenaAudit
{
    public static void Run()
    {
        var sb = new StringBuilder("path;renderers;sizeX;sizeY;sizeZ;minY;centerX;centerZ;tris;rootRot;materials\n");
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/ArenaGreen" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            Bounds b = new Bounds(); bool first = true; int rend = 0, tris = 0;
            var mats = new System.Collections.Generic.HashSet<string>();
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                rend++;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                foreach (Material m in r.sharedMaterials) mats.Add(m == null ? "NULL" : m.name);
            }
            foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>())
                if (f.sharedMesh != null) tris += f.sharedMesh.triangles.Length / 3;
            sb.AppendLine(path.Replace("Assets/Art/ArenaGreen/", "") + ";" + rend + ";" + b.size.x.ToString("0.##") + ";" + b.size.y.ToString("0.##") + ";" +
                b.size.z.ToString("0.##") + ";" + b.min.y.ToString("0.##") + ";" + b.center.x.ToString("0.##") + ";" + b.center.z.ToString("0.##") + ";" + tris + ";" +
                go.transform.localEulerAngles + ";" + string.Join("|", mats));
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/green-audit.csv", sb.ToString());
        EditorApplication.Exit(0);
    }
}
