using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Кадры электродуги (batch, нужен графический режим): Logs/lightning-sheet.png. -executeMethod LightningShots.Run -quit
public static class LightningShots
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50f, -30f, 0f); light.intensity = 0.9f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.35f, 0.4f, 0.35f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = Vector3.one * 5f;
        ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.22f, 0.33f, 0.2f) };
        Vector3[] enemies = { new Vector3(7f, 0f, 3f), new Vector3(11f, 0f, -2f), new Vector3(15f, 0f, 2f), new Vector3(19f, 0f, -1f) };
        foreach (Vector3 e in enemies)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            c.transform.position = e + Vector3.up; c.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.6f, 0.25f, 0.2f) };
        }
        string[] views = { "game", "near" };
        for (int v = 0; v < 2; v++)
        {
            // Чистим прошлый показ.
            foreach (var b in Object.FindObjectsOfType<LightningBolt>()) Object.DestroyImmediate(b.gameObject);
            foreach (var go in GameObject.FindObjectsOfType<GameObject>()) if (go != null && go.name == "Lightning impact") Object.DestroyImmediate(go);
            Random.InitState(7);
            Vector3 from = new Vector3(0f, 0.9f, 0f), start = from;
            for (int j = 0; j < enemies.Length; j++)
            {
                Vector3 point = enemies[j] + Vector3.up * 0.9f;
                var color = new Color(0.35f, 0.6f, 1f);
                LightningBolt.Create(start, point, color, j == 0 ? 0.42f : 0.34f, 0.26f, j == 0 ? 3 : 2);
                if (j == 0) LightningBolt.Impact(start, color, 0.45f, false);
                LightningBolt.Impact(point, color, 1f - 0.12f * j);
                start = point;
            }
            var camObject = new GameObject("Cam"); var cam = camObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.08f, 0.1f, 0.08f);
            if (v == 0) { cam.transform.position = new Vector3(9f, 40f, -5f); cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -40f, 5f)); cam.fieldOfView = 25f; }
            else { cam.transform.position = new Vector3(8f, 10f, -10f); cam.transform.LookAt(new Vector3(8f, 0.8f, 1f)); cam.fieldOfView = 40f; }
            const int frames = 4, W = 640, H = 360;
            var sheet = new Texture2D(W * 2, H * 2, TextureFormat.RGB24, false);
            float[] times = { 0.01f, 0.07f, 0.12f, 0.2f };
            float last = 0f;
            for (int i = 0; i < frames; i++)
            {
                float dt = times[i] - last; last = times[i];
                foreach (var b in Object.FindObjectsOfType<LightningBolt>()) b.Step(dt);
                foreach (var ps in Object.FindObjectsOfType<ParticleSystem>()) ps.Simulate(dt, true, false, false);
                var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var tile = new Texture2D(W, H, TextureFormat.RGB24, false); tile.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                sheet.SetPixels((i % 2) * W, (1 - i / 2) * H, W, H, tile.GetPixels());
                RenderTexture.active = null; cam.targetTexture = null;
                Object.DestroyImmediate(tile); Object.DestroyImmediate(rt);
            }
            File.WriteAllBytes("Logs/lightning-" + views[v] + ".png", sheet.EncodeToPNG());
            Object.DestroyImmediate(camObject);
        }
        EditorApplication.Exit(0);
    }
}
