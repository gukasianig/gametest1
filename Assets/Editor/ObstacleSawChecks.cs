using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 19.11.2026: проверка двух жалоб владельца на GreenArenaPlay (batch, без -quit):
//  1) пила-бумеранг на холме не уходит под землю и меньше прежних 1,4 м;
//  2) враги, поставленные за камнями/деревьями (с дальней от героя стороны), не стоят на месте.
// Результат: Logs/obstacle-saw-check.txt. Запуск: -executeMethod ObstacleSawChecks.RunBatch
[InitializeOnLoad]
public static class ObstacleSawChecks
{
    const string Marker = "Logs/obstacle-saw.request", Report = "Logs/obstacle-saw-check.txt";
    const int SawWeapon = 4;
    static double started, nextStep;
    static int step, sawFrames, errors, levelUps;
    static double midStep;
    static readonly List<Vector3> midPos = new List<Vector3>();
    static float sawMinLift = 99f, sawMaxSize;
    static string sawMinNote = "";
    static readonly StringBuilder log = new StringBuilder();
    static readonly List<EnemyAI> probes = new List<EnemyAI>();
    static readonly List<float> startDistance = new List<float>();
    static int lastTraceFrame;
    static readonly Dictionary<int, Queue<string>> trace = new Dictionary<int, Queue<string>>();

    static ObstacleSawChecks()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker))
            {
                File.Delete(Marker); started = EditorApplication.timeSinceStartup; step = 0;
                Application.logMessageReceived += (m, st, type) => { if (type == LogType.Exception || type == LogType.Error) { errors++; log.AppendLine("ERROR " + m); } };
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

    static void Finish(bool ok)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText(Report, (ok ? "OK" : "FAIL") + "\n" + log);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static string Dump(EnemyAI e)
    {
        var sb = new StringBuilder();
        foreach (string f in new[] { "waypoint", "velocity", "detour", "detourUntil", "nextPathTime", "bodyRadius", "heroImpulseUntil" })
        {
            var fi = typeof(EnemyAI).GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fi != null) sb.Append(f + "=" + fi.GetValue(e) + " ");
        }
        var arena = ArenaLayout.Instance;
        Vector3 pos = e.transform.position;
        Vector3 toHero = e.target.position - pos; toHero.y = 0f; toHero.Normalize();
        Vector3 res = arena.ResolveMovement(pos, pos + toHero * 0.06f, 0.7071068f);
        var deep = typeof(ArenaLayout).GetMethod("DeepestPenetration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        sb.Append("resolveStep=" + Flat(res, pos).ToString("0.000") + " walk707=" + arena.IsWalkable(pos, 0.7071068f) + " deepest=" + (deep != null ? deep.Invoke(arena, new object[] { pos, 0.7071068f }) : "n/a") + " ");
        int near = 0; foreach (Bounds b in arena.Obstacles) if (Flat(b.center, pos) < 4f) { near++; sb.Append("[obs " + b.center.ToString("0.0") + " ext " + b.extents.ToString("0.0") + "] "); }
        sb.Append("preferredRange=" + e.PreferredRange + " abilities=" + string.Join(",", System.Array.ConvertAll(e.GetComponents<MonoBehaviour>(), m => m.GetType().Name)) + " target=" + (e.target != null ? e.target.name : "null") + " heightKnown=" + typeof(EnemyAI).GetField("heightKnown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(e) + " time=" + Time.time.ToString("0.0") + " slow=" + SlowEffect.MovementFactor(e).ToString("0.00") + " speed=" + e.speed);
        return sb.ToString();
    }

    static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - started;
        GameObject hero = GameObject.Find("Capsule");
        if (hero == null) { log.AppendLine("no hero"); Finish(false); return; }
        if (t > 90) { log.AppendLine("timeout at step " + step); Finish(false); return; }
        // Проверяем движение врагов и пилу, а не выживание: герой не должен погибнуть (поражение ставит игру на паузу).
        var heroHealth = hero.GetComponent<PlayerHealth>();
        if (heroHealth != null && step >= 1) { heroHealth.GrantInvulnerability(1f); heroHealth.Heal(1000); }
        // Пила убивает врагов → уровень → карточки ставят игру на паузу; выбираем первую карточку, чтобы время шло.
        if (GameSession.Instance != null && GameSession.Instance.State == GameRunState.Upgrade)
        {
            GameObject levelUp = GameObject.Find("Level Up");
            if (levelUp != null)
                foreach (UnityEngine.UI.Button card in levelUp.GetComponentsInChildren<UnityEngine.UI.Button>())
                    if (card.interactable) { card.onClick.Invoke(); levelUps++; break; }
        }
        foreach (WeaponSaw saw in Object.FindObjectsOfType<WeaponSaw>())
        {
            Vector3 p = saw.transform.position;
            sawFrames++;
            float liftNow = p.y - GroundHeight.At(p);
            if (liftNow < sawMinLift) { sawMinLift = liftNow; sawMinNote = "saw " + p + " ground=" + GroundHeight.At(p).ToString("0.00") + " hero=" + hero.transform.position + " t=" + t.ToString("0.0") + " scale=" + saw.transform.localScale.x.ToString("0.00"); }
            Renderer[] rs = saw.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0) { Bounds b = rs[0].bounds; foreach (Renderer r in rs) if (r.enabled) b.Encapsulate(r.bounds); sawMaxSize = Mathf.Max(sawMaxSize, Mathf.Max(b.size.x, b.size.z)); }
        }
        if (step == 2 && Time.frameCount != lastTraceFrame)
            for (int i = 0; i < probes.Count; i++)
            {
                if (probes[i] == null) continue;
                if (!trace.TryGetValue(i, out var q)) trace[i] = q = new Queue<string>();
                var f = typeof(EnemyAI).GetField("velocity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var w = typeof(EnemyAI).GetField("waypoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                q.Enqueue("f" + Time.frameCount + " t" + Time.time.ToString("0.00") + " p" + probes[i].transform.position.ToString("0.00") + " v" + ((Vector3)f.GetValue(probes[i])).ToString("0.0") + " w" + ((Vector3)w.GetValue(probes[i])).ToString("0.0") + " clock" + typeof(EnemyAI).GetField("stuckClock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(probes[i]) + " active" + probes[i].isActiveAndEnabled + " ts" + Time.timeScale + " state" + GameSession.Instance.State);
                while (q.Count > 25) q.Dequeue();
                lastTraceFrame = Time.frameCount;
            }
        if (step == 0 && t > 1.5)
        {
            MetaProgress.UseMemory();
            MetaProgress.Data.currency = 99999;
            MetaProgress.TryUnlock(MetaProgress.WeaponKey(SawWeapon));
            hero.GetComponent<StartMenu>().SendMessage("BeginGame", SawWeapon);
            // Холм из GreenArenaPlayCheck: рельеф вокруг заметно неровный.
            Vector3 hp = new Vector3(110f, 0f, 20f); hp.y = GroundHeight.At(hp) + 1f;
            hero.GetComponent<Rigidbody>().position = hp; hero.transform.position = hp;
            log.AppendLine("state=" + GameSession.Instance.State + " weapon=" + hero.GetComponent<AutoShoter>().WeaponIndex);
            step = 1;
        }
        else if (step == 1 && t > 4)
        {
            EnemyAI template = null;
            foreach (EnemyAI e in EnemyAI.All) { template = e; break; }
            if (template == null) { if (t > 20) { log.AppendLine("no enemies spawned"); Finish(false); } return; }
            // Препятствия на 7–30 м от героя: враг ставится вплотную за ними, на линии герой → препятствие.
            Vector3 heroPos = hero.transform.position;
            var candidates = new List<Bounds>();
            foreach (Bounds b in ArenaLayout.Instance.Obstacles)
            {
                float d = Flat(b.center, heroPos);
                if (d > 7f && d < 45f && Mathf.Max(b.size.x, b.size.z) > 0.9f) candidates.Add(b);
            }
            candidates.Sort((a, b) => Flat(a.center, heroPos).CompareTo(Flat(b.center, heroPos)));
            float offset = template.transform.position.y - GroundHeight.At(template.transform.position);
            foreach (Bounds b in candidates)
            {
                if (probes.Count >= 24) break;
                Vector3 away = b.center - heroPos; away.y = 0f; away.Normalize();
                Vector3 spot = b.center + away * (Mathf.Max(b.extents.x, b.extents.z) + 1.1f);
                if (!ArenaLayout.Instance.IsWalkable(spot, 0.6f)) continue;
                spot.y = offset;   // EnemyAI считает высоту от плоскости 0 и сам кладёт врага на рельеф
                EnemyAI clone = Object.Instantiate(template.gameObject, spot, Quaternion.identity).GetComponent<EnemyAI>();
                clone.target = hero.transform;
                probes.Add(clone);
                startDistance.Add(Flat(spot, heroPos));
            }
            log.AppendLine("obstacle candidates=" + candidates.Count + " probes=" + probes.Count + " template=" + template.name);
            step = 2; nextStep = t + 16; midStep = t + 11;
        }
        else if (step == 2 && midPos.Count == 0 && t > midStep)
        {
            foreach (EnemyAI p in probes) midPos.Add(p != null ? p.transform.position : Vector3.zero);
        }
        else if (step == 2 && t > nextStep)
        {
            int stuck = 0, alive = 0;
            Vector3 heroPos = hero.transform.position;
            for (int i = 0; i < probes.Count; i++)
            {
                if (probes[i] == null || !probes[i].gameObject.activeInHierarchy) continue;   // убит пилой — значит дошёл
                alive++;
                float now = Flat(probes[i].transform.position, heroPos);
                bool progressed = now < 4.5f || now < startDistance[i] * 0.55f;
                if (!progressed && probes[i].GetComponent<Health>() != null && probes[i].GetComponent<Health>().isDead) { log.AppendLine("dead probe " + i); continue; }
                if (!progressed && trace.TryGetValue(i, out var tq)) { int k = 0; foreach (string line in tq) if (k++ % 8 == 0) log.AppendLine("   " + line); }
                if (!progressed) { stuck++; log.AppendLine("stuck probe " + i + " start=" + startDistance[i].ToString("0.0") + " now=" + now.ToString("0.0") + " at " + probes[i].transform.position + " moved last 5s=" + (midPos.Count > i ? Flat(midPos[i], probes[i].transform.position).ToString("0.0") : "?") + " dead=" + (probes[i].GetComponent<Health>() != null && probes[i].GetComponent<Health>().isDead) + " walkable=" + ArenaLayout.Instance.IsWalkable(probes[i].transform.position, 0.5f) + " locked=" + probes[i].MovementLocked + " enabled=" + probes[i].enabled + " " + Dump(probes[i])); }
            }
            log.AppendLine("probes alive=" + alive + " stuck=" + stuck + " / " + probes.Count);
            log.AppendLine("saw frames=" + sawFrames + " min lift above ground=" + sawMinLift.ToString("0.00") + " max visual size=" + sawMaxSize.ToString("0.00"));
            log.AppendLine(sawMinNote);
            log.AppendLine("state=" + GameSession.Instance.State + " runtime errors=" + errors + " level-up cards picked=" + levelUps);
            bool ok = GameSession.Instance.State == GameRunState.Playing && probes.Count >= 6 && stuck <= probes.Count / 8 && sawFrames > 30 && sawMinLift > 0.1f && sawMaxSize < 1.3f && errors == 0;
            Finish(ok);
        }
    }
}
