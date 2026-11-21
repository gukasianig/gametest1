using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 19.11.2026: ракетница на GreenArenaPlay (batch, без -quit). Жалоба владельца: герой не смотрит туда, куда стреляет.
// Меряет угол между стволом ракетницы и направлением вылетевшей ракеты, высоту ракет над землёй на холме,
// снимает три кадра героя крупным планом (Logs/rocket-aim-*.png). Отчёт: Logs/rocket-aim-check.txt.
// Запуск: -executeMethod RocketAimChecks.RunBatch
[InitializeOnLoad]
public static class RocketAimChecks
{
    const string Marker = "Logs/rocket-aim.request", Report = "Logs/rocket-aim-check.txt";
    const int RocketWeapon = 6;
    static double started;
    static int step, rockets, errors, shots;
    static float minLift = 99f, maxAngle, sumAngle;
    static readonly HashSet<int> seen = new HashSet<int>();
    static readonly StringBuilder log = new StringBuilder();

    static RocketAimChecks()
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

    static Vector3 FlatDir(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero; }

    static void Shot(GameObject hero, string path)
    {
        var look = new GameObject("Rocket look").AddComponent<Camera>();
        Vector3 chest = hero.transform.position + Vector3.up * 0.4f;
        look.transform.position = chest - hero.transform.forward * 2.2f + hero.transform.right * 1.6f + Vector3.up * 1.1f;
        look.transform.LookAt(chest + hero.transform.forward * 1.2f);
        look.fieldOfView = 45f;
        var rt = RenderTexture.GetTemporary(960, 540, 24);
        look.targetTexture = rt;
        look.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        look.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(look.gameObject);
        Object.Destroy(tex);
    }

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - started;
        GameObject hero = GameObject.Find("Capsule");
        if (hero == null) { log.AppendLine("no hero"); Finish(false); return; }
        if (t > 70) { log.AppendLine("timeout at step " + step); Finish(false); return; }
        var health = hero.GetComponent<PlayerHealth>();
        if (health != null && step >= 1) { health.GrantInvulnerability(1f); health.Heal(1000); }
        if (GameSession.Instance != null && GameSession.Instance.State == GameRunState.Upgrade)
        {
            GameObject levelUp = GameObject.Find("Level Up");
            if (levelUp != null)
                foreach (UnityEngine.UI.Button card in levelUp.GetComponentsInChildren<UnityEngine.UI.Button>())
                    if (card.interactable) { card.onClick.Invoke(); break; }
        }
        Transform muzzle = ModelSkins.FindMuzzle(hero.transform);
        WeaponFireAnimation fx = muzzle != null ? muzzle.GetComponentInParent<WeaponFireAnimation>() : null;
        foreach (WeaponProjectile shot in Object.FindObjectsOfType<WeaponProjectile>())
        {
            Vector3 p = shot.transform.position;
            minLift = Mathf.Min(minLift, p.y - GroundHeight.At(p));
            if (!seen.Add(shot.GetInstanceID())) continue;
            rockets++;
            if (fx == null) continue;
            // Угол между стволом (в плоскости земли) и направлением полёта новой ракеты.
            float angle = Vector3.Angle(FlatDir(fx.BarrelForward), FlatDir(shot.transform.forward));
            maxAngle = Mathf.Max(maxAngle, angle);
            sumAngle += angle;
        }
        if (step == 0 && t > 1.5)
        {
            MetaProgress.UseMemory();
            MetaProgress.Data.currency = 99999;
            MetaProgress.TryUnlock(MetaProgress.WeaponKey(RocketWeapon));
            hero.GetComponent<StartMenu>().SendMessage("BeginGame", RocketWeapon);
            Vector3 hp = new Vector3(110f, 0f, 20f); hp.y = GroundHeight.At(hp) + 1f;
            hero.GetComponent<Rigidbody>().position = hp; hero.transform.position = hp;
            log.AppendLine("state=" + GameSession.Instance.State + " weapon=" + hero.GetComponent<AutoShoter>().WeaponIndex + " muzzle=" + (muzzle != null) + " fx=" + (fx != null));
            step = 1;
        }
        else if (step >= 1 && step <= 3 && t > 9 + step * 3)
        {
            Shot(hero, "Logs/rocket-aim-" + step + ".png");
            shots++;
            step++;
        }
        else if (step == 4 && t > 22)
        {
            log.AppendLine("rockets=" + rockets + " min lift above ground=" + minLift.ToString("0.00") +
                " barrel-vs-rocket angle max=" + maxAngle.ToString("0.0") + " mean=" + (rockets > 0 ? sumAngle / rockets : 0f).ToString("0.0"));
            log.AppendLine("pose=" + hero.GetComponent<HeroLocomotion>().CurrentPose + " shots=" + shots + " runtime errors=" + errors);
            bool ok = GameSession.Instance.State == GameRunState.Playing && rockets >= 3 && minLift > 0.2f && maxAngle < 25f && errors == 0;
            Finish(ok);
        }
    }
}
