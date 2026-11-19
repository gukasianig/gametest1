using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HeroOverhaulChecks
{
    private const string Request = "Logs/hero-overhaul.request";
    private const string Report = "Logs/hero-overhaul-results.txt";
    private static CheckRunner runner;
    private static double started;
    private static int errors;
    private static byte[] languageBackup;
    private static bool hadLanguageSettings;
    private static readonly List<string> results = new List<string>();
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    static HeroOverhaulChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request)) EditorApplication.delayCall += Begin;
        };
    }

    public static void RunBatch()
    {
        RunBatchFor("batch");
    }

    public static void RunHudBatch()
    {
        RunBatchFor("experience-hud");
    }

    public static void RunClockBatch()
    {
        RunBatchFor("clock-hud");
    }

    private static void RunBatchFor(string scope)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Request, scope);
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    private static void Begin()
    {
        string scope = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        results.Clear();
        started = EditorApplication.timeSinceStartup;
        errors = 0;
        hadLanguageSettings = File.Exists(GameText.SettingsPath);
        languageBackup = hadLanguageSettings ? File.ReadAllBytes(GameText.SettingsPath) : null;
        Application.logMessageReceived += OnLog;
        runner = new CheckRunner(scope == "clock-hud" ? ClockHudChecks() : scope == "experience-hud" ? ExperienceHudChecks() : Checks());
        EditorApplication.update += Tick;
    }

    private static void OnLog(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            results.Add("RUNTIME " + text);
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Hero overhaul checks timed out");
            if (!runner.MoveNext()) Finish(errors == 0, "Hero overhaul checks complete; runtime errors=" + errors);
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void Finish(bool ok, string detail)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        if (hadLanguageSettings) File.WriteAllBytes(GameText.SettingsPath, languageBackup);
        else if (File.Exists(GameText.SettingsPath)) File.Delete(GameText.SettingsPath);
        results.Add((ok ? "PASS " : "FAIL ") + detail);
        File.WriteAllLines(Report, results);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static void Require(bool ok, string detail) { if (!ok) throw new Exception(detail); }
    private static void Pass(string detail) { results.Add("PASS " + detail); File.WriteAllLines(Report, results); }
    private static IEnumerator Wait(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    private static IEnumerator ExperienceHudChecks()
    {
        yield return null;
        MetaProgress.UseMemory();
        var progression = PlayerProgression.Instance;
        progression.GetComponent<StartMenu>().BeginTestArena();
        yield return Wait(.2f);
        var canvas = (Canvas)typeof(PlayerProgression).GetField("canvas", PrivateInstance).GetValue(progression);
        var track = (RectTransform)canvas.transform.Find("Experience/Experience body/Experience track");
        var fill = (RectTransform)track.Find("Fill");
        var status = (Text)typeof(PlayerProgression).GetField("status", PrivateInstance).GetValue(progression);
        var level = (Text)typeof(PlayerProgression).GetField("levelText", PrivateInstance).GetValue(progression);
        Action validate = () =>
        {
            Canvas.ForceUpdateCanvases();
            float expected = (float)progression.Experience / progression.ExperienceRequired;
            Require(Mathf.Abs(fill.rect.width / track.rect.width - expected) < .002f, "XP fill differs from actual progression");
            Require(fill.gameObject.activeSelf == (progression.Experience > 0), "Zero XP leaves a visible fill");
            Require(status.text == progression.Experience + " / " + progression.ExperienceRequired && level.text == progression.Level.ToString(), "Displayed XP/level differs from actual progression");
            Require(status.preferredWidth <= status.rectTransform.rect.width && level.preferredWidth <= level.rectTransform.rect.width, "XP or level text clipped");
            foreach (Text text in track.parent.GetComponentsInChildren<Text>())
            {
                Bounds textBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(track, text.rectTransform);
                Require(textBounds.min.y >= track.rect.yMax, "XP caption overlaps the scale");
            }
        };
        validate();
        progression.AddExperience(progression.ExperienceRequired + 5, false);
        yield return Wait(.45f);
        GameText.SetLanguage(GameLanguage.Russian);
        validate();
        Capture("experience-segments-ru", progression.transform, false);
        Pass("Actual test arena, 0 XP and level 2 with 5 XP; RU captions and fill/label layout at 1280x720");
        progression.AddExperience(1, false);
        yield return Wait(.45f);
        GameText.SetLanguage(GameLanguage.English);
        WorkshopTreeChecks.Capture(canvas, "Logs/experience-segments-en-1024.png", 1024, 768, validate);
        Pass("Crystal gain settles to actual XP; English captions and unclipped layout at 1024x768");
        progression.AddExperience(progression.ExperienceRequired - progression.Experience - 1, false);
        yield return Wait(.45f);
        GameText.SetLanguage(GameLanguage.Russian);
        WorkshopTreeChecks.Capture(canvas, "Logs/experience-segments-ru-wide.png", 2560, 1080, validate);
        Pass("Near-full XP and unclipped layout at 2560x1080");
        progression.AddExperience(1, false);
        yield return null;
        validate();
        Require(progression.Level == 3 && progression.Experience == 0, "Level-up failed to reset XP");
        Pass("Actual level-up resets the scale immediately without a backwards fill animation");
    }

    private static IEnumerator ClockHudChecks()
    {
        yield return null;
        MetaProgress.UseMemory();
        GameSession.TestMode = false;
        GameSession.EndlessMode = false;
        var hero = PlayerProgression.Instance;
        var session = hero.GetComponent<GameSession>();
        var menu = hero.GetComponent<StartMenu>();
        menu.ShowWeapons();
        typeof(StartMenu).GetMethod("BeginGame", PrivateInstance).Invoke(menu, new object[] { 1 });
        hero.GetComponent<PlayerHealth>().GrantInvulnerability(100f);
        hero.GetComponent<ArenaEvents>().enabled = false;
        Object.FindObjectOfType<WaveManager>().StopSpawning();
        typeof(GameSession).GetField("miniSpawned", PrivateInstance).SetValue(session, true);
        session.survivalSeconds = 600f;
        var elapsed = typeof(GameSession).GetProperty("Elapsed");
        var plate = (RectTransform)typeof(GameSession).GetField("clockPlate", PrivateInstance).GetValue(session);
        var caption = (Text)typeof(GameSession).GetField("clockCaption", PrivateInstance).GetValue(session);
        var time = (Text)typeof(GameSession).GetField("clock", PrivateInstance).GetValue(session);
        var progress = (Image)typeof(GameSession).GetField("clockProgress", PrivateInstance).GetValue(session);
        var canvas = plate.GetComponentInParent<Canvas>();
        Action validate = () =>
        {
            Canvas.ForceUpdateCanvases();
            Require(caption.preferredWidth <= caption.rectTransform.rect.width + 1f && time.preferredWidth <= time.rectTransform.rect.width + 1f, "Timer labels clipped");
            Bounds label = RectTransformUtility.CalculateRelativeRectTransformBounds(plate, caption.rectTransform);
            Bounds digits = RectTransformUtility.CalculateRelativeRectTransformBounds(plate, time.rectTransform);
            Require(label.max.x <= digits.min.x + 1f, "Timer caption overlaps digits");
        };
        elapsed.SetValue(session, 120f);
        yield return Wait(.2f);
        Require(Mathf.Abs(progress.rectTransform.anchorMax.x - session.Elapsed / 600f) < .002f, "Boss approach differs from run time");
        session.TogglePause();
        float paused = session.Elapsed;
        yield return Realtime(.2f);
        Require(session.Elapsed == paused, "Timer advances while paused");
        session.TogglePause();
        Require(session.EnterUpgrade(), "Cannot open upgrade state");
        yield return Realtime(.2f);
        Require(!plate.gameObject.activeSelf, "Timer covers upgrade title");
        session.LeaveUpgrade();
        elapsed.SetValue(session, 580f);
        GameText.SetLanguage(GameLanguage.Russian);
        yield return Wait(.2f);
        validate();
        Require(caption.text == GameText.Source("hud.boss-near") && progress.color.g > .6f, "Near-boss warning missing");
        Capture("clock-near-ru", hero.transform, false);
        Pass("Real run progress, pause freeze, hidden during upgrades and amber warning at 20 seconds; RU 1280x720");
        elapsed.SetValue(session, 595f);
        GameText.SetLanguage(GameLanguage.English);
        yield return Wait(.2f);
        Require(session.BossWarning && progress.color.g < .5f, "Final boss warning missing");
        WorkshopTreeChecks.Capture(canvas, "Logs/clock-warning-en-1024.png", 1024, 768, validate);
        elapsed.SetValue(session, 600f);
        yield return Wait(.3f);
        Require(session.Boss != null && time.text == GameText.Translate(GameText.Source("hud.boss-fight")), "Boss fight state missing");
        validate();
        Pass("Final ten-second warning and actual boss spawn/fight label; EN 1024x768");
        GameSession.EndlessMode = true;
        // Match Begin's endless setup before exercising its round-complete callback.
        session.survivalSeconds = 1000000f;
        typeof(GameSession).GetProperty("BossWarning").SetValue(session, false);
        typeof(GameSession).GetMethod("EndlessBossDown", PrivateInstance).Invoke(session, new object[] { session.Boss.Health });
        elapsed.SetValue(session, session.Elapsed + 60f);
        yield return Wait(.2f);
        Require(Mathf.Abs(progress.rectTransform.anchorMax.x - .2f) < .005f && caption.text.Contains("ROUND 2"), "Endless round did not reset clock progress: " + progress.rectTransform.anchorMax.x + "; " + caption.text);
        WorkshopTreeChecks.Capture(canvas, "Logs/clock-endless-en-wide.png", 2560, 1080, validate);
        GameSession.TestMode = true;
        yield return Wait(.2f);
        Require(!plate.gameObject.activeSelf, "Clock visible in test arena");
        GameSession.TestMode = false;
        GameSession.EndlessMode = false;
        Pass("Next endless round resets progress/duration; EN 2560x1080; clock hidden in test arena");
    }

    private static Health Dummy(Vector3 position)
    {
        var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Aim check target";
        item.tag = "Enemy";
        item.transform.position = position;
        var health = item.AddComponent<Health>();
        health.SetHealth(100000);
        health.experienceReward = 0;
        item.AddComponent<EnemyAI>().MovementLocked = true;
        return health;
    }

    private static float AimError(Transform muzzle, Transform target)
    {
        var fx = muzzle.GetComponentInParent<WeaponFireAnimation>();
        return Vector3.Angle(fx.BarrelForward, target.GetComponent<Collider>().bounds.center - muzzle.position);
    }

    private static IEnumerator Realtime(float seconds)
    {
        double until=EditorApplication.timeSinceStartup+seconds;
        while(EditorApplication.timeSinceStartup<until)yield return null;
    }

    private static IEnumerator Checks()
    {
        yield return null;
        MetaProgress.UseMemory();
        MetaProgress.Data.currency=100000;
        MetaProgress.Data.ranks.Add(new MetaEntry { key = "workshop:project:generator", value = 2 });
        foreach(var unlock in MetaProgress.Unlocks)MetaProgress.Unlock(unlock.id);
        var shooter=Object.FindObjectOfType<AutoShoter>();
        var player=shooter.GetComponent<PlayerHealth>();
        var movement=shooter.GetComponent<PlayMovement>();
        var menu=shooter.GetComponent<StartMenu>();
        var session=shooter.GetComponent<GameSession>();
        yield return null;
        var abilities=shooter.GetComponent<HeroAbilities>();
        Require(abilities!=null && shooter.GetComponent<HeroPresentation>()!=null,"Hero modules missing");
        Require(!abilities.TryPulse() && !abilities.TryOverdrive(),"Abilities active in menu");
        menu.ShowWeapons();
        typeof(StartMenu).GetMethod("BeginGame",PrivateInstance).Invoke(menu,new object[]{1});
        yield return Wait(.15f);
        Object.FindObjectOfType<WaveManager>().StopSpawning();
        shooter.SetGrenadeUnlocked(false);
        shooter.range=25;
        foreach(var enemy in Object.FindObjectsOfType<EnemyAI>())Object.Destroy(enemy.gameObject);
        yield return null;
        player.GrantInvulnerability(100);
        Vector3 center=shooter.transform.position;
        var enemyA=Dummy(center+new Vector3(2.5f,0,1));
        var enemyB=Dummy(center+new Vector3(-2.5f,0,1));
        var boss=BossController.Create(center+new Vector3(0,0,4),shooter.transform,BossKind.Guardian);
        yield return null;
        Require(abilities.TryPulse() && !abilities.TryPulse(),"Pulse activation/cooldown");
        var impulse=typeof(EnemyAI).GetField("heroImpulseUntil",PrivateInstance);
        Require((float)impulse.GetValue(enemyA.GetComponent<EnemyAI>())>Time.time,"Pulse did not push regular enemy");
        var bossAI=boss.GetComponent<EnemyAI>();
        Require(bossAI==null || (float)impulse.GetValue(bossAI)<=Time.time,"Boss received knockback");
        Vector3 before=enemyA.transform.position;
        yield return Wait(.2f);
        Require(Vector3.Distance(before,enemyA.transform.position)>.15f,"Knockback did not move enemy");
        Object.Destroy(boss.gameObject);
        Pass("Pulse activation, cooldown, actual enemy displacement and boss immunity");
        float speed=movement.EffectiveSpeed,interval=shooter.CurrentFireInterval;
        Require(abilities.TryOverdrive()&&!abilities.TryOverdrive(),"Overdrive activation/cooldown");
        Require(Mathf.Abs(movement.EffectiveSpeed-speed*1.25f)<.01f && Mathf.Abs(shooter.CurrentFireInterval-interval/1.3f)<.001f,"Temporary factors incorrect");
        session.TogglePause();
        float remaining=abilities.ActiveRemaining,pulse=abilities.PulseRemaining;
        yield return Realtime(.2f);
        Require(Mathf.Abs(abilities.ActiveRemaining-remaining)<.001f && Mathf.Abs(abilities.PulseRemaining-pulse)<.001f && !abilities.TryPulse(),"Cooldowns advanced while paused");
        session.TogglePause();
        Require(session.EnterUpgrade(),"Upgrade state unavailable");
        Require(!abilities.TryOverdrive()&&!abilities.TryPulse(),"Skills fired over upgrade screen");
        session.LeaveUpgrade();
        shooter.SetProgressionMultipliers(1.2f,1.4f);
        movement.SpeedMultiplier=1.2f;
        float boostedInterval=shooter.CurrentFireInterval;
        Require(Mathf.Abs(movement.EffectiveSpeed-movement.speed*1.2f*1.25f)<.01f,"Overdrive overwrote permanent movement bonus");
        yield return Wait(.3f);
        foreach(var language in new[]{GameLanguage.Russian,GameLanguage.English})
        {
            GameText.SetLanguage(language);
            Capture("hero-overhaul-hud-"+(language==GameLanguage.Russian?"ru":"en"),shooter.transform,false);
        }
        Capture("hero-overhaul-rifle",shooter.transform,true);
        Pass("Overdrive factors, pause/upgrade restrictions, upgrade stacking and RU/EN HUD");
        movement.CheckInput=new Vector2(.4f,.6f);
        // Editor ticks may precede FixedUpdate; allow input Update and a subsequent physics frame.
        yield return Wait(.7f);
        Require(Vector3.ProjectOnPlane(shooter.GetComponent<Rigidbody>().velocity,Vector3.up).magnitude>movement.EffectiveSpeed*.3f,"Run did not accelerate: v="+shooter.GetComponent<Rigidbody>().velocity+" speed="+movement.EffectiveSpeed+" state="+session.State+" input="+movement.CheckInput);
        movement.CheckInput=Vector2.zero;
        yield return Wait(.5f);
        Require(Vector3.ProjectOnPlane(shooter.GetComponent<Rigidbody>().velocity,Vector3.up).magnitude<.1f,"Stop overshot");
        movement.CheckInput=Vector2.up;
        yield return Wait(.4f);
        Vector3 dashStart=shooter.transform.position;
        movement.RequestDash();
        yield return Wait(.4f);
        Require(Vector3.Distance(dashStart,shooter.transform.position)>2 && movement.DashCooldownRemaining>0,"Dash did not move hero");
        movement.CheckInput=Vector2.zero;
        yield return Wait(.5f);
        Require(!movement.Dashing && Vector3.ProjectOnPlane(shooter.GetComponent<Rigidbody>().velocity,Vector3.up).magnitude<.1f,"Dash momentum leaked into normal movement");
        movement.CheckInput=null;
        Pass("Responsive run/stop and dash without momentum leakage");
        yield return Wait(5.2f);
        Require(!abilities.Overdriving && Mathf.Abs(movement.EffectiveSpeed-movement.speed*1.2f)<.01f && Mathf.Abs(shooter.CurrentFireInterval-boostedInterval*1.3f)<.001f,"Temporary factors did not expire cleanly");
        Require(!abilities.TryOverdrive(),"Cooldown ended with duration");
        Pass("Duration expires while cooldown continues; permanent upgrades preserved");
        // Exercise the real ready transition without waiting through the remainder of both cooldowns.
        var abilityCanvas = (Canvas)typeof(HeroAbilities).GetField("canvas", PrivateInstance).GetValue(abilities);
        typeof(HeroAbilities).GetField("pulseReady", PrivateInstance).SetValue(abilities, Time.time + .12f);
        typeof(HeroAbilities).GetField("overdriveReady", PrivateInstance).SetValue(abilities, Time.time + .12f);
        yield return Wait(.2f);
        var readyFlash = abilityCanvas.transform.Find("Ability panel/Pulse/Ready flash").GetComponent<Image>();
        Require(readyFlash.color.a > 0f, "Cooldown recovery did not flash once");
        yield return Wait(.5f);
        Require(readyFlash.color.a == 0f, "Ready flash did not finish");
        Capture("abilities-ready-en", shooter.transform, false);
        Pass("Ready transition flashes briefly and settles; compact ready HUD captured");
        typeof(PlayerHealth).GetField("invulnerableUntil",PrivateInstance).SetValue(player,0f);
        shooter.Stats.Armor=0;
        int hp=player.CurrentHP;
        player.TakeDamage(10,HeroDamageCause.Contact);
        player.TakeDamage(10,HeroDamageCause.Projectile);
        Require(player.CurrentHP==hp-10,"Simultaneous attacks bypassed post-hit protection");
        yield return Wait(.2f);
        player.TakeDamage(10,HeroDamageCause.Projectile);
        Require(player.CurrentHP==hp-20,"Protection never expired");
        player.GrantInvulnerability(100);
        Pass("Post-hit protection prevents same-frame damage stacks and expires");
        // Exercise the extended normal-weapon posing and retain the existing dual path.
        foreach(int weapon in new[]{0,2,1})
        {
            shooter.SelectWeapon(weapon); shooter.range=25;
            yield return Wait(.35f);
            Require(ModelSkins.FindMuzzle(shooter.transform)!=null,"Weapon muzzle missing "+weapon);
        }
        WeaponElements.Buy(0,WeaponElement.Dual); WeaponElements.Equip(0,WeaponElement.Dual);
        shooter.SelectWeapon(0); shooter.range=25;
        var third=Dummy(shooter.transform.position+new Vector3(6,0,3));
        yield return Wait(.8f);
        Require(shooter.DualAiming && shooter.LeftAimTarget!=null,"Dual aiming regressed");
        Require(AimError(ModelSkins.FindMuzzle(shooter.transform),shooter.RightAimTarget)<9,"Dual right barrel mismatch");
        Capture("hero-overhaul-dual",shooter.transform,true);
        Pass("Pistol/rifle/shotgun switch and dual aiming");
        CoopMode.Players=2;
        var second=CoopMode.SpawnSecond(shooter.gameObject,1);
        yield return Wait(.15f);
        var secondSkills=second.GetComponent<HeroAbilities>();
        Require(secondSkills!=null && secondSkills!=abilities,"Second player shares skill component");
        float primaryCooldown=abilities.PulseRemaining;
        Require(secondSkills.TryPulse() && Mathf.Abs(abilities.PulseRemaining-primaryCooldown)<.01f,"Second cooldown not independent");
        Require(!secondSkills.Overdriving,"Second player inherited overdrive");
        var secondCanvas = (Canvas)typeof(HeroAbilities).GetField("canvas", PrivateInstance).GetValue(secondSkills);
        WorkshopTreeChecks.Capture(secondCanvas, "Logs/abilities-coop-1024.png", 1024, 768, () =>
        {
            foreach (Text label in secondCanvas.transform.Find("Ability panel").GetComponentsInChildren<Text>())
                Require(label.preferredWidth <= label.rectTransform.rect.width + 1f, "Ability label clipped at 1024x768: " + label.text);
        });
        Object.Destroy(second);
        yield return Wait(.4f);
        Pass("Second hero has separate abilities, cooldowns and HUD");
        typeof(PlayerHealth).GetField("invulnerableUntil",PrivateInstance).SetValue(player,0f);
        player.TakeDamage(200000,HeroDamageCause.Lightning);
        yield return Realtime(.2f);
        Require(player.IsDead && session.IsFinished && !abilities.TryPulse() && !abilities.TryOverdrive(),"Dead hero can use abilities");
        Require((float)typeof(HeroLocomotion).GetField("deathTime",PrivateInstance).GetValue(shooter.GetComponent<HeroLocomotion>())>0,"Death pose did not advance");
        Pass("Actual hero death, disabled skills and sampled death animation");
    }

    private static void Capture(string name, Transform hero, bool closeup)
    {
        Camera camera = Camera.main;
        Vector3 position = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        bool orthographic = camera.orthographic;
        float size = camera.orthographicSize;
        var canvases = new List<Canvas>();
        foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.enabled || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvases.Add(canvas);
            if (closeup) canvas.enabled = false;
        }
        if (closeup)
        {
            camera.transform.position = hero.position + new Vector3(0, 5, -4);
            camera.transform.LookAt(hero.position + Vector3.up);
            camera.orthographic = true;
            camera.orthographicSize = 2f;
        }
        var texture = new RenderTexture(1280,720,24);
        RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
        camera.targetTexture = texture;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = texture;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1280,720),0,0);
        image.Apply();
        File.WriteAllBytes("Logs/"+name+".png",image.EncodeToPNG());
        camera.targetTexture = previous;
        RenderTexture.active = active;
        camera.transform.SetPositionAndRotation(position,rotation);
        camera.orthographic = orthographic;
        camera.orthographicSize = size;
        foreach (Canvas canvas in canvases) { canvas.enabled=true; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; }
        Object.Destroy(image);
        Object.Destroy(texture);
    }
}
