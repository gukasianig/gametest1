using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 19.11.2026: турель-миниган (batch, без -quit): модель и части Head/Barrels/TurretMuzzle, размер, раскрутка стволов,
// доворот на цель, урон, очередь трассеров. Крупные кадры Logs/turret-minigun.png, отчёт Logs/turret-minigun-check.txt.
// Запуск: -executeMethod TurretMinigunChecks.RunBatch
[InitializeOnLoad]
public static class TurretMinigunChecks
{
    const string Marker = "Logs/turret-minigun.request", Report = "Logs/turret-minigun-check.txt";
    const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    static double started, mark;
    static int step, errors, failures, tracerFrames;
    static float maxSpin;
    static readonly StringBuilder log = new StringBuilder();
    static readonly List<Texture2D> frames = new List<Texture2D>();
    static GameObject hero;
    static WeaponTurret turret;
    static Health dummy;
    static int dummyStart;
    static Quaternion barrelsStart;

    static TurretMinigunChecks()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker))
            {
                File.Delete(Marker); started = EditorApplication.timeSinceStartup; step = 0;
                Application.logMessageReceived += (m, st, type) => { if (type == LogType.Exception || type == LogType.Error) { errors++; log.AppendLine("ERROR " + m + "\n" + st); } };
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

    static void Check(bool ok, string what) { if (!ok) failures++; log.AppendLine((ok ? "PASS " : "FAIL ") + what); }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        if (frames.Count > 0)
        {
            var sheet = new Texture2D(640 * frames.Count, 480, TextureFormat.RGB24, false);
            for (int i = 0; i < frames.Count; i++) sheet.SetPixels(i * 640, 0, 640, 480, frames[i].GetPixels());
            sheet.Apply();
            File.WriteAllBytes("Logs/turret-minigun.png", sheet.EncodeToPNG());
        }
        bool ok = failures == 0 && errors == 0;
        log.AppendLine("failures=" + failures + " runtime errors=" + errors);
        File.WriteAllText(Report, (ok ? "OK" : "FAIL") + "\n" + log);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    // Крупный кадр турели отдельной камерой (три четверти, сверху сбоку).
    static void Capture(Vector3 focus, Vector3 from)
    {
        var go = new GameObject("Turret check camera");
        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 35f;
        camera.transform.position = focus + from;
        camera.transform.LookAt(focus);
        var target = new RenderTexture(640, 480, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(640, 480, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        camera.targetTexture = null;
        target.Release();
        Object.Destroy(go);
        frames.Add(image);
    }

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - started;
        if (t > 90) { log.AppendLine("timeout at step " + step); failures++; Finish(); return; }
        if (hero == null) hero = GameObject.Find("Capsule");
        if (hero == null) return;
        var health = hero.GetComponent<PlayerHealth>();
        if (step >= 1 && health != null && !health.IsDead) { health.GrantInvulnerability(1f); health.Heal(1000); }
        if (GameSession.Instance != null && GameSession.Instance.State == GameRunState.Upgrade)
        {
            GameObject levelUp = GameObject.Find("Level Up");
            if (levelUp != null)
                foreach (UnityEngine.UI.Button card in levelUp.GetComponentsInChildren<UnityEngine.UI.Button>())
                    if (card.interactable) { card.onClick.Invoke(); break; }
        }
        if (turret != null)
        {
            maxSpin = Mathf.Max(maxSpin, turret.Spin);
            if (turret.TracersLeft > 0) tracerFrames++;
        }
        double since = t - mark;
        if (step == 0 && t > 1.5)
        {
            MetaProgress.UseMemory();
            hero.GetComponent<StartMenu>().SendMessage("BeginGame", (object)0);
            step = 1; mark = t;
        }
        else if (step == 1 && since > 2.5)
        {
            // Слот вспомогательной турели появляется после выдачи «карточки» турели.
            hero.GetComponent<AutoShoter>().enabled = false;
            hero.GetComponent<AutoShoter>().SetSupportUnits(0, 1, 0, 1f, 1f);
            step = 10; mark = t;
        }
        else if (step == 10 && since > 0.3)
        {
            foreach (WaveManager waves in Object.FindObjectsOfType<WaveManager>()) waves.enabled = false;
            EnemyAI template = null;
            foreach (EnemyAI e in EnemyAI.All) if (e != null) { template = e; break; }
            if (template == null) { if (since > 20) { Check(false, "no enemy to copy"); Finish(); } return; }
            var all = new List<EnemyAI>(EnemyAI.All);
            // Турель из карточки: слот вспомогательного оружия героя.
            SpecialWeapons slot = null;
            foreach (SpecialWeapons s in hero.GetComponents<SpecialWeapons>()) if (s.support && s.Weapon != null && s.Weapon.kind == WeaponKind.Turret) slot = s;
            if (slot == null) { Check(false, "turret support slot not found"); Finish(); return; }
            Vector3 at = hero.transform.position + hero.transform.right * 4f;
            WeaponTurret.Create(slot, at);
            float best = float.MaxValue;
            foreach (WeaponTurret candidate in Object.FindObjectsOfType<WeaponTurret>())
            {
                float d = (candidate.transform.position - at).sqrMagnitude;
                if (d < best) { best = d; turret = candidate; }
            }
            Transform head = ItemModelParts.Find(turret.transform, "Head"), barrels = ItemModelParts.Find(turret.transform, "Barrels"),
                muzzle = ItemModelParts.Find(turret.transform, "TurretMuzzle");
            Check(head != null && barrels != null && muzzle != null, "model parts Head/Barrels/TurretMuzzle found");
            barrelsStart = barrels != null ? barrels.localRotation : Quaternion.identity;
            // Манекен в 5 м от турели; остальные враги убраны.
            GameObject copy = Object.Instantiate(template.gameObject, at + new Vector3(3f, 0f, 4f), Quaternion.identity);
            copy.name = "Turret target";
            copy.GetComponent<EnemyAI>().MovementLocked = true;
            dummy = copy.GetComponent<Health>();
            dummy.SetHealth(100000);
            dummyStart = dummy.CurrentHealth;
            foreach (EnemyAI e in all) if (e != null) Object.Destroy(e.gameObject);
            step = 2; mark = t;
        }
        else if (step == 2 && since > 0.45)
        {
            Renderer[] renderers = turret.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(turret.transform.position, Vector3.zero);
            foreach (Renderer r in renderers) if (r.enabled && r.transform.name != "Turret" && r.transform.name != "Turret barrel") bounds.Encapsulate(r.bounds);
            float size = Mathf.Max(bounds.size.x, bounds.size.z);
            Check(size > 1.2f && size < 2.2f, "turret model size " + size.ToString("0.00") + " m (was ~1.04)");
            Capture(turret.transform.position + Vector3.up * 0.5f, new Vector3(-2.6f, 2.2f, -2.6f));
            step = 3; mark = t;
        }
        else if (step == 3 && since > 3)
        {
            Transform barrels = ItemModelParts.Find(turret.transform, "Barrels");
            Check(maxSpin > 1000f, "barrels spin up (max " + maxSpin.ToString("0") + " °/s)");
            Check(barrels != null && Quaternion.Angle(barrels.localRotation, barrelsStart) > 1f, "barrels rotated");
            Check(dummy != null && dummy.CurrentHealth < dummyStart, "turret damaged target: " + (dummy != null ? dummyStart - dummy.CurrentHealth : 0));
            Check(tracerFrames > 0, "tracer bursts fired (frames " + tracerFrames + ")");
            Transform head = ItemModelParts.Find(turret.transform, "Head"), muzzle = ItemModelParts.Find(turret.transform, "TurretMuzzle");
            Vector3 facing = muzzle.position - head.position, wanted = dummy.transform.position - head.position;
            facing.y = wanted.y = 0f;
            float angle = Vector3.Angle(facing, wanted);
            Check(angle < 15f, "head aims at target (" + angle.ToString("0.0") + "°)");
            Vector3 focus = turret.transform.position + Vector3.up * 0.5f;
            Capture(focus, new Vector3(2.4f, 1.8f, -2.4f));
            Capture((focus + dummy.transform.position) * 0.5f, new Vector3(0f, 7f, -5f));
            Finish();
        }
    }
}
