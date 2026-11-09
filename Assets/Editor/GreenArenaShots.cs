using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Снимки сцены GreenArena для оценки вида (batch: -executeMethod GreenArenaShots.Shoot -quit). Файлы: Logs/green-shot-*.png
// Высота камеры задаётся над землёй (по Terrain), а не абсолютно.
// Кадры камней (08.11): rocks-* — с игровой камеры (смещение 0,18,−6 от точки, как TopDownCamera в GreenArenaPlay) на средний камень,
// на кучу камней (RockPile, если есть) и вид сбоку с высоты 4 м.
public static class GreenArenaShots
{
    static readonly Vector3 GameOffset = new Vector3(0f, 18f, -6f);

    public static void Shoot()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArena.unity");
        Camera cam = Camera.main;
        Terrain terrain = Object.FindObjectOfType<Terrain>();
        Directory.CreateDirectory("Logs");
        string info = "";
        if (terrain != null)
        {
            TerrainData d = terrain.terrainData;
            int sum = 0;
            for (int i = 0; i < d.detailPrototypes.Length; i++)
                foreach (int v in d.GetDetailLayer(0, 0, d.detailWidth, d.detailHeight, i)) sum += v;
            info = "prototypes=" + d.detailPrototypes.Length + " details=" + sum + " drawFoliage=" + terrain.drawTreesAndFoliage +
                " distance=" + terrain.detailObjectDistance + " valid=" + (d.detailPrototypes.Length > 0 && d.detailPrototypes[0].Validate());
        }
        var views = new (string name, Vector3 pos, float above, Vector3 euler)[]
        {
            ("overview", new Vector3(0f, 0f, -170f), 80f, new Vector3(28f, 0f, 0f)),
            ("game", new Vector3(0f, 0f, -12f), 16f, new Vector3(52f, 0f, 0f)),
            ("meadow", new Vector3(-40f, 0f, -55f), 1.7f, new Vector3(6f, 30f, 0f)),
            ("pond", new Vector3(0f, 0f, 0f), 0f, Vector3.zero),
            ("close", new Vector3(-75f, 0f, -60f), 2.5f, new Vector3(6f, 40f, 0f)),
        };
        foreach (var v in views)
        {
            float ground = terrain != null ? terrain.SampleHeight(v.pos) + terrain.transform.position.y : 0f;
            cam.transform.position = new Vector3(v.pos.x, ground + v.above, v.pos.z);
            cam.transform.rotation = Quaternion.Euler(v.euler);
            GameObject water = GameObject.Find("Water");
            if (v.name == "pond" && water != null && water.transform.childCount > 0)
            {
                Vector3 target = water.transform.GetChild(1 % water.transform.childCount).position;
                Vector3 eye = target + new Vector3(-16f, 0f, -16f);
                eye.y = (terrain != null ? terrain.SampleHeight(eye) + terrain.transform.position.y : 0f) + 4f;
                cam.transform.position = eye;
                cam.transform.LookAt(target);
            }
            Render(cam, v.name);
        }

        // Камни: первый средний камень и первая куча в иерархии Rocks.
        Transform rocks = GameObject.Find("Rocks") != null ? GameObject.Find("Rocks").transform : null;
        Transform medium = null, pile = null;
        if (rocks != null)
            foreach (Transform t in rocks)
            {
                bool isPile = t.name.StartsWith("RockPile");
                if (pile == null && isPile) pile = t;
                if (medium == null && !isPile && (t.name.StartsWith("rock_medium") || t.name.StartsWith("rock_mossy_medium"))) medium = t;
            }
        info += "\nmedium=" + (medium != null ? medium.name + " " + medium.position : "none") + " pile=" + (pile != null ? pile.name + " " + pile.position : "none");
        foreach (var (name, t) in new[] { ("rocks-medium", medium), ("rocks-pile", pile) })
        {
            if (t == null) continue;
            cam.transform.position = t.position + GameOffset;
            cam.transform.rotation = Quaternion.LookRotation(-GameOffset, Vector3.up);
            Render(cam, name + "-game");
            Vector3 eye = t.position + new Vector3(-7f, 0f, -9f);
            eye.y = (terrain != null ? terrain.SampleHeight(eye) + terrain.transform.position.y : t.position.y) + 4f;
            cam.transform.position = eye;
            cam.transform.LookAt(t.position + Vector3.up * 1f);
            Render(cam, name + "-side");
        }
        File.WriteAllText("Logs/green-shot-info.txt", info);
        EditorApplication.Exit(0);
    }

    static void Render(Camera cam, string name)
    {
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        File.WriteAllBytes("Logs/green-shot-" + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
