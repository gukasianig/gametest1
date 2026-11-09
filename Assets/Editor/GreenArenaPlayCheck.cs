using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Короткая проверка сцены GreenArenaPlay в Play Mode (batch, без -quit): герой на земле, бой стартует, враги идут по рельефу.
// Результат: Logs/green-play-check.txt. Запуск: -executeMethod GreenArenaPlayCheck.RunBatch
[InitializeOnLoad]
public static class GreenArenaPlayCheck
{
    const string Marker = "Logs/green-play-check.request";
    static double started, nextStep;
    static int step, modelBullets;
    static StringBuilder log = new StringBuilder();

    static GreenArenaPlayCheck()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker)) { File.Delete(Marker); started = EditorApplication.timeSinceStartup; step = 0; EditorApplication.update += Tick; }
        };
    }

    public static void RunBatch()
    {
        File.WriteAllText(Marker, "1");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText("Logs/green-play-check.txt", result + "\n" + log);
        EditorApplication.Exit(result.StartsWith("OK") ? 0 : 1);
    }

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - started;
        foreach (Bullet bl in Object.FindObjectsOfType<Bullet>()) if (bl.transform.Find("ProjectileModel") != null) modelBullets++;
        GameObject hero = GameObject.Find("Capsule");
        if (hero == null) { Finish("FAIL no hero"); return; }
        if (step == 0 && t > 1.5)
        {
            log.AppendLine("hero start y=" + hero.transform.position.y.ToString("0.00") + " ground=" + GroundHeight.At(hero.transform.position).ToString("0.00") +
                " layout=" + (ArenaLayout.Instance != null) + " terrains=" + Object.FindObjectsOfType<Terrain>().Length);
            hero.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
            log.AppendLine("state=" + GameSession.Instance.State + " timeScale=" + Time.timeScale);
            { var h = new GameObject("tip test"); h.transform.rotation = Quaternion.LookRotation(Vector3.right); h.transform.position = new Vector3(0f, 50f, 0f); ProjectileModels.Skin(h, "bullet", 0.7f);
              MeshFilter mfT = h.GetComponentInChildren<MeshFilter>(); Vector3 tip = Vector3.zero; float best = 99f;
              foreach (Vector3 v in mfT.sharedMesh.vertices) { Vector3 w = mfT.transform.TransformPoint(v); Vector3 rel = w - h.transform.position; float radial = Vector3.Cross(rel, Vector3.right).magnitude; if (radial < best) { best = radial; tip = rel; } }
              log.AppendLine("bullet model tip direction dot flight=" + Vector3.Dot(tip.normalized, Vector3.right).ToString("0.00") + " size=" + tip.magnitude.ToString("0.00")); Object.Destroy(h); }
            var arena = ArenaLayout.Instance; int blocked = 0, tested = 0;
            foreach (Bounds b in arena.Obstacles) { tested++; Vector3 a = new Vector3(b.min.x - 3f, 0f, b.center.z), c = new Vector3(b.max.x + 3f, 0f, b.center.z); if (!arena.PathClear(a, c, 0.5f)) blocked++; if (tested >= 100) break; }
            var sw = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 1000; i++) arena.NextWaypoint(new Vector3(-90f + i * 0.05f, 0f, -60f), new Vector3(80f, 0f, 70f), 0.5f); sw.Stop();
            log.AppendLine("obstacles=" + arena.Obstacles.Count + " pathBlockedThroughObstacle=" + blocked + "/" + tested + " 1000 NextWaypoint ms=" + sw.ElapsedMilliseconds);
            Vector3 hp = new Vector3(110f, 0f, 20f); hp.y = GroundHeight.At(hp) + 1f; hero.GetComponent<Rigidbody>().position = hp; hero.transform.position = hp;
            log.AppendLine("teleport to hill y=" + hp.y.ToString("0.00"));
            step = 1; nextStep = t + 12;
        }
        else if (step == 1 && t > nextStep)
        {
            float maxDiff = 0f, minDiff = 99f; int n = 0;
            foreach (EnemyAI e in EnemyAI.All) { n++; float d = e.transform.position.y - GroundHeight.At(e.transform.position); maxDiff = Mathf.Max(maxDiff, d); minDiff = Mathf.Min(minDiff, d); }
            log.AppendLine("after 12s: state=" + GameSession.Instance.State + " enemies=" + n + " enemyHeightAboveGround min/max=" + minDiff.ToString("0.00") + "/" + maxDiff.ToString("0.00") +
                " hero=" + hero.transform.position + " groundUnderHero=" + GroundHeight.At(hero.transform.position).ToString("0.00"));
            int n0 = n; float minDiff0 = minDiff;
            log.AppendLine("bullet-frames with model=" + modelBullets);
            bool ok = GameSession.Instance.State == GameRunState.Playing && Mathf.Abs(hero.transform.position.y - GroundHeight.At(hero.transform.position) - 1f) < 0.3f && (n0 == 0 || minDiff0 > 0f);
            Finish(ok ? "OK" : "FAIL");
        }
    }
}
