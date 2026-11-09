using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Play Mode-проверка «герой не застревает у камней» в GreenArenaPlay (batch, без -quit). Волны выключены, герой ходит через PlayMovement.CheckInput.
//  1. inside: герой поставлен на 0,3 м внутрь коробки камня (так его вдавливает враг физикой) — должен выйти за 1 с при вводе к камню, вдоль и без ввода.
//  2. slide: герой идёт по диагонали в бок камня — должен скользить вдоль (сдвиг по касательной).
//  3. piles: к каждой из первых куч подходит с 8 сторон (2 с к центру), затем уходит обратно — должен отойти ≥ 3 м.
// Результат: Logs/green-rock-walk.txt. Запуск: -executeMethod GreenArenaRockWalkCheck.RunBatch
[InitializeOnLoad]
public static class GreenArenaRockWalkCheck
{
    const string Marker = "Logs/green-rock-walk.request";
    static double started, waitUntil;
    static IEnumerator script;
    static readonly StringBuilder log = new StringBuilder();
    static int fails, upgrades;

    static GreenArenaRockWalkCheck()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker))
            {
                File.Delete(Marker);
                started = EditorApplication.timeSinceStartup;
                waitUntil = 0.0;
                script = Run();
                EditorApplication.update += Tick;
            }
        };
    }

    public static void RunBatch()
    {
        File.WriteAllText(Marker, "1");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup - started > 500) { Finish("FAIL timeout"); return; }
        // Забег идёт: окно улучшения (timeScale 0) закрываем выбором первой карты, ожидания считаем в игровом времени — пауза их не портит.
        if (GameSession.Instance != null && GameSession.Instance.State == GameRunState.Upgrade)
        {
            PlayerProgression pp = Object.FindObjectOfType<PlayerProgression>();
            if (pp == null || !pp.SelectOfferedUpgrade(0)) GameSession.Instance.LeaveUpgrade();
            if (GameSession.Instance.State == GameRunState.Upgrade) GameSession.Instance.LeaveUpgrade();
            upgrades++;
            return;
        }
        double t = Time.timeAsDouble;
        if (t < waitUntil) return;
        bool more;
        try { more = script.MoveNext(); }
        catch (System.Exception e) { Finish("FAIL exception " + e); return; }
        if (!more) { Finish((fails == 0 ? "OK" : "FAIL " + fails) + " (окон улучшения закрыто: " + upgrades + ")"); return; }
        waitUntil = Time.timeAsDouble + (script.Current is float f ? f : 0f);
    }

    static void Finish(string result)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText("Logs/green-rock-walk.txt", result + "\n" + log);
        EditorApplication.Exit(result.StartsWith("OK") ? 0 : 1);
    }

    static GameObject hero;
    static PlayMovement move;
    static Rigidbody body;

    static void Place(Vector3 p)
    {
        p.y = GroundHeight.At(p) + 1f;
        body.velocity = Vector3.zero;
        body.position = p;
        hero.transform.position = p;
    }

    static float Depth(Vector3 p)
    {
        float worst = -99f;
        foreach (Bounds b in ArenaLayout.Instance.Obstacles)
        {
            float d = Mathf.Min(Mathf.Min(p.x - (b.min.x - 0.5f), b.max.x + 0.5f - p.x), Mathf.Min(p.z - (b.min.z - 0.5f), b.max.z + 0.5f - p.z));
            worst = Mathf.Max(worst, d);
        }
        return worst;
    }

    static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

    static IEnumerator Run()
    {
        yield return null;   // кадр на Start() сцены (меню может держать timeScale 0 — ждать в игровом времени здесь нельзя)
        hero = GameObject.Find("Capsule");
        move = hero.GetComponent<PlayMovement>();
        body = hero.GetComponent<Rigidbody>();
        hero.GetComponent<StartMenu>().SendMessage("BeginGame", 1);
        foreach (WaveManager w in Object.FindObjectsOfType<WaveManager>()) w.enabled = false;
        foreach (EnemyAI e in new List<EnemyAI>(EnemyAI.All)) Object.Destroy(e.gameObject);
        log.AppendLine("state=" + GameSession.Instance.State + " obstacles=" + ArenaLayout.Instance.Obstacles.Count);
        yield return 0.5f;

        // Камни-препятствия (крупнее 1,5 м, не на краю арены).
        var rockBoxes = new List<Bounds>();
        foreach (Bounds b in ArenaLayout.Instance.Obstacles)
            if (b.size.x > 1.5f && b.size.z > 1.5f && b.size.x < 8f && Mathf.Abs(b.center.x) < 150f && Mathf.Abs(b.center.z) < 150f) rockBoxes.Add(b);
        log.AppendLine("candidate boxes=" + rockBoxes.Count);

        // 1. inside
        int insideOk = 0, insideTotal = 0;
        Vector2[] inputs = { new Vector2(1f, 0f), new Vector2(0f, 1f), Vector2.zero };   // к камню (камень справа), вдоль, без ввода
        string[] inputNames = { "toward", "along", "idle" };
        for (int k = 0; k < 6 && k < rockBoxes.Count; k++)
        {
            Bounds b = rockBoxes[k * rockBoxes.Count / 6];
            for (int j = 0; j < inputs.Length; j++)
            {
                Place(new Vector3(b.min.x - 0.5f + 0.3f, 0f, b.center.z));
                if (Depth(body.position) <= 0f) continue;   // точка не внутри (соседний камень перекрыл расчёт) — пропуск
                move.CheckInput = inputs[j];
                yield return 1.0f;
                insideTotal++;
                float depth = Depth(body.position);
                bool ok = depth <= 0.02f;
                if (ok) insideOk++; else { fails++; log.AppendLine("  inside FAIL box " + b.center + " input=" + inputNames[j] + " depth=" + depth.ToString("0.00")); }
            }
        }
        move.CheckInput = null;
        log.AppendLine("inside: escaped " + insideOk + "/" + insideTotal + " (герой на 0,3 м внутри коробки; ввод к камню / вдоль / без ввода, 1 с)");

        // 2. slide: старт в 2 м слева от коробки, ввод по диагонали вправо-вверх (в камень и вдоль).
        int slideOk = 0, slideTotal = 0;
        for (int k = 0; k < 6 && k < rockBoxes.Count; k++)
        {
            Bounds b = rockBoxes[(k * rockBoxes.Count / 6 + 3) % rockBoxes.Count];
            Vector3 start = new Vector3(b.min.x - 0.5f - 1.5f, 0f, b.center.z - b.extents.z * 0.5f);
            Place(start);
            if (Depth(body.position) > 0f) continue;
            move.CheckInput = new Vector2(0.7071f, 0.7071f);
            yield return 1.5f;
            slideTotal++;
            float tangential = body.position.z - start.z;
            bool ok = tangential > 2.5f && Depth(body.position) <= 0.02f;
            if (ok) slideOk++; else { fails++; log.AppendLine("  slide FAIL box " + b.center + " moved along=" + tangential.ToString("0.00") + " depth=" + Depth(body.position).ToString("0.00")); }
        }
        move.CheckInput = null;
        log.AppendLine("slide: " + slideOk + "/" + slideTotal + " (диагональ в бок камня 1,5 с, сдвиг вдоль > 2,5 м)");

        // 3. piles: подход с 8 сторон, уход обратно.
        var piles = new Dictionary<string, List<Transform>>();
        GameObject rocksRoot = GameObject.Find("Rocks");
        foreach (Transform r in rocksRoot.transform)
            if (r.name.StartsWith("RockPile "))
            {
                string key = r.name.Substring(0, 11);
                if (!piles.ContainsKey(key)) piles[key] = new List<Transform>();
                piles[key].Add(r);
            }
        int pileRuns = 0, pileOk = 0, pileChecked = 0, worstInside = 0;
        float minAway = 99f;
        foreach (var pile in piles)
        {
            if (pileChecked >= 10) break;
            Vector3 c = Vector3.zero;
            foreach (Transform r in pile.Value) c += r.position;
            c /= pile.Value.Count;
            if (Mathf.Abs(c.x) > 150f || Mathf.Abs(c.z) > 150f) continue;
            pileChecked++;
            for (int a = 0; a < 8; a++)
            {
                Vector3 dir = new Vector3(Mathf.Cos(a * Mathf.PI / 4f), 0f, Mathf.Sin(a * Mathf.PI / 4f));
                Vector3 start = Flat(c) + dir * 7f;
                Place(start);
                if (Depth(body.position) > 0f) continue;
                move.CheckInput = new Vector2(-dir.x, -dir.z);
                yield return 2.0f;
                if (Depth(body.position) > 0.02f) worstInside++;
                Vector3 near = Flat(body.position);
                move.CheckInput = new Vector2(dir.x, dir.z);
                yield return 2.0f;
                pileRuns++;
                float away = Vector3.Distance(Flat(body.position), near);
                minAway = Mathf.Min(minAway, away);
                if (away >= 3f && Depth(body.position) <= 0.02f) pileOk++;
                else { fails++; log.AppendLine("  pile FAIL " + pile.Key + " dir=" + a + " away=" + away.ToString("0.00") + " depth=" + Depth(body.position).ToString("0.00") + " state=" + GameSession.Instance.State + " timeScale=" + Time.timeScale + " t=" + Time.time.ToString("0") + " v=" + body.velocity + " kin=" + body.isKinematic + " moveOn=" + move.enabled); }
            }
        }
        move.CheckInput = null;
        log.AppendLine("piles: " + pileChecked + " куч, проходов " + pileOk + "/" + pileRuns + ", минимальный отход " + minAway.ToString("0.00") + " м, оказался внутри коробки при подходе: " + worstInside);
        if (worstInside > 0) fails++;
    }
}
