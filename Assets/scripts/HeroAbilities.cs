using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Per-hero state. Cooldowns use game time, so pauses and upgrade screens freeze them.
public sealed class HeroAbilities : MonoBehaviour
{
    public const float PulseCooldown = 12f, PulseRadius = 5.5f;
    public const float OverdriveCooldown = 22f, OverdriveDuration = 5f;
    private float pulseReady, overdriveReady, overdriveUntil;
    private PlayerHealth health;
    private PlayMovement movement;
    private Canvas canvas;
    private sealed class AbilitySlot
    {
        public Text State;
        public Image Fill, Flash;
        public Outline Border;
        public UpgradeIcon Icon;
        public bool Ready = true;
        public float FlashTime;
    }

    private AbilitySlot pulseSlot, driveSlot;
    private static readonly Color ReadyColor = new Color(.16f, .72f, .9f);
    private static readonly Color ActiveColor = new Color(1f, .72f, .24f);
    private static readonly Color CoolingColor = new Color(.38f, .48f, .55f);
    private RectTransform help;
    private Text helpText;
    public float PulseRemaining => Mathf.Max(0, pulseReady - Time.time);
    public float OverdriveRemaining => Mathf.Max(0, overdriveReady - Time.time);
    public float ActiveRemaining => Mathf.Max(0, overdriveUntil - Time.time);
    public bool Overdriving => health != null && !health.IsDead && ActiveRemaining > 0;
    public float SpeedFactor => Overdriving ? 1.25f : 1f;
    public float FireRateFactor => Overdriving ? 1.3f : 1f;
    public bool PulseAvailable => GameSession.TestMode || WorkshopProjects.PulseUnlocked;
    public bool OverdriveAvailable => GameSession.TestMode || WorkshopProjects.DriveUnlocked;
    private bool CanActivate => isActiveAndEnabled && health != null && !health.IsDead &&
        GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing && Time.timeScale > 0;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayMovement>();
    }

    private void Start()
    {
        canvas = GameUi.Canvas("Hero abilities", 76);
        bool second = movement != null && movement.playerIndex > 0;
        float bottom = second ? .145f : .025f;
        var panel = GameUi.Box(canvas.transform, "Ability panel", new Vector2(.39f, bottom), new Vector2(.69f, bottom + .10f), Color.clear);
        pulseSlot = BuildSlot(panel, false, second ? "Num7" : "Q", 0);
        driveSlot = BuildSlot(panel, true, second ? "Num9" : "E", .52f);
        help = GameUi.Box(canvas.transform,"Ability help",new Vector2(.35f,bottom+.105f),new Vector2(.70f,bottom+.22f),GameUi.Background);
        UiArtwork.Round(help.GetComponent<Image>());
        helpText = GameUi.Label(help,"",Vector2.zero,Vector2.one,15);
        help.gameObject.SetActive(false);
        canvas.enabled = false;
    }

    private AbilitySlot BuildSlot(Transform parent, bool drive, string key, float x)
    {
        var slot = new AbilitySlot();
        var button = GameUi.Button(parent, "", new Vector2(x, 0), new Vector2(x+.48f, 1), () => { if (drive) TryOverdrive(); else TryPulse(); });
        button.name = drive ? "Overdrive" : "Pulse";
        button.GetComponent<Image>().color = new Color(.025f,.065f,.085f,.96f);
        slot.Border = button.gameObject.AddComponent<Outline>();
        slot.Border.effectDistance = new Vector2(1f, -1f);
        slot.Border.effectColor = new Color(ReadyColor.r, ReadyColor.g, ReadyColor.b, .5f);
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            helpText.text = (drive ? OverdriveAvailable : PulseAvailable) ? GameText.Source(drive ? "hero.drive.description" : "hero.pulse.description") + "\n" +
                GameText.Source("projects.cooldown") + ((drive ? OverdriveCooldown : PulseCooldown) * WorkshopProjects.CooldownFactor).ToString("0.0") + GameText.Source("arenaevents.006") : GameText.Source("projects.ability.locked");
            help.gameObject.SetActive(true);
        });
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => help.gameObject.SetActive(false));
        trigger.triggers.Add(enter); trigger.triggers.Add(exit);
        UiArtwork.Round(button.GetComponent<Image>());
        var glyph = new GameObject("Ability icon", typeof(RectTransform), typeof(UpgradeIcon));
        glyph.transform.SetParent(button.transform, false);
        slot.Icon = glyph.GetComponent<UpgradeIcon>();
        slot.Icon.Effect = drive ? UpgradeEffect.AttackSpeed : UpgradeEffect.GrenadeRadius;
        slot.Icon.color = ReadyColor;
        slot.Icon.raycastTarget = false;
        slot.Icon.rectTransform.anchorMin = new Vector2(.055f, .45f);
        slot.Icon.rectTransform.anchorMax = new Vector2(.23f, .92f);
        slot.Icon.rectTransform.offsetMin = slot.Icon.rectTransform.offsetMax = Vector2.zero;
        var keyBadge = GameUi.Box(button.transform, "Key", new Vector2(.05f,.14f), new Vector2(.28f,.41f), new Color(.09f,.16f,.2f));
        UiArtwork.Round(keyBadge.GetComponent<Image>());
        Text keyText = GameUi.Label(keyBadge, key, Vector2.zero, Vector2.one, 13, new Color(.8f,.9f,.95f));
        keyText.rectTransform.offsetMin = keyText.rectTransform.offsetMax = Vector2.zero;
        GameUi.Label(button.transform, GameText.Source(drive ? "hero.drive" : "hero.pulse"), new Vector2(.27f,.5f), new Vector2(.98f,.99f), 13);
        slot.State = GameUi.Label(button.transform, "", new Vector2(.27f,.13f), new Vector2(.98f,.55f), 14);
        var track = GameUi.Box(button.transform, "Cooldown", new Vector2(.06f,.055f), new Vector2(.94f,.10f), new Color(.12f,.18f,.22f));
        slot.Fill = GameUi.Box(track, "Ready", Vector2.zero, Vector2.one, ReadyColor).GetComponent<Image>();
        slot.Flash = GameUi.Box(button.transform, "Ready flash", Vector2.zero, Vector2.one, Color.clear).GetComponent<Image>();
        UiArtwork.Round(slot.Flash);
        return slot;
    }

    public bool TryPulse()
    {
        if (!CanActivate || !PulseAvailable || PulseRemaining > 0) return false;
        pulseReady = Time.time + PulseCooldown * WorkshopProjects.CooldownFactor;
        foreach (EnemyAI enemy in EnemyAI.All)
        {
            if (enemy == null || enemy.Health == null || enemy.Health.isDead ||
                enemy.GetComponent<BossController>() != null || enemy.GetComponent<MiniBoss>() != null) continue;
            Vector3 away = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, Vector3.up);
            if (away.sqrMagnitude > PulseRadius * PulseRadius) continue;
            if (away.sqrMagnitude < .001f) away = transform.forward;
            float resistance = enemy.name.StartsWith("Elite") ? .5f : 1f;
            enemy.ApplyHeroImpulse(away.normalized * (14f * resistance), .32f);
        }
        EnemyFx.Ring(transform.position, PulseRadius, GameUi.Accent, .45f, .16f);
        EnemyFx.LightFlash(transform.position + Vector3.up, GameUi.Accent, 5, 1.1f, .18f);
        GameAudio.Play(Sfx.Zap, .45f);
        return true;
    }

    public bool TryOverdrive()
    {
        if (!CanActivate || !OverdriveAvailable || OverdriveRemaining > 0) return false;
        overdriveReady = Time.time + OverdriveCooldown * WorkshopProjects.CooldownFactor;
        overdriveUntil = Time.time + OverdriveDuration;
        EnemyFx.Ring(transform.position, 1.2f, new Color(1,.72f,.24f), .4f, .10f);
        GameAudio.Play(Sfx.LevelUp, .3f);
        return true;
    }

    private void Update()
    {
        bool second = movement != null && movement.playerIndex > 0;
        if (Input.GetKeyDown(second ? KeyCode.Keypad7 : KeyCode.Q) || (second && Input.GetKeyDown(KeyCode.JoystickButton5))) TryPulse();
        if (Input.GetKeyDown(second ? KeyCode.Keypad9 : KeyCode.E) || (second && Input.GetKeyDown(KeyCode.JoystickButton6))) TryOverdrive();
        if (health != null && health.IsDead) overdriveUntil = 0;
        if (canvas == null) return;
        var session = GameSession.Instance;
        canvas.enabled = session != null && !health.IsDead && (session.State == GameRunState.Playing || session.State == GameRunState.Paused);
        UpdateSlot(pulseSlot, PulseRemaining, PulseCooldown * WorkshopProjects.CooldownFactor, 0f, PulseAvailable);
        UpdateSlot(driveSlot, OverdriveRemaining, OverdriveCooldown * WorkshopProjects.CooldownFactor, ActiveRemaining, OverdriveAvailable);
    }

    private void UpdateSlot(AbilitySlot slot, float remaining, float cooldown, float active, bool unlocked)
    {
        if (!unlocked)
        {
            slot.State.text = GameText.Source("workshoptree.465");
            slot.State.color = slot.Icon.color = CoolingColor;
            slot.Fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            slot.Border.effectColor = new Color(CoolingColor.r, CoolingColor.g, CoolingColor.b, .4f);
            slot.Flash.color = Color.clear;
            slot.Ready = true;
            return;
        }
        bool ready = remaining <= 0f;
        bool running = active > 0f;
        // One soft flash on the transition to ready; no looping pulse or initial-entry flash.
        if (ready && !slot.Ready && canvas.enabled) slot.FlashTime = .4f;
        slot.Ready = ready;
        slot.FlashTime = Mathf.Max(0f, slot.FlashTime - Time.unscaledDeltaTime);
        Color color = running ? ActiveColor : ready ? ReadyColor : CoolingColor;
        slot.State.text = running ? GameText.Source("hero.active") + active.ToString("0.0") : ready ? GameText.Source("hero.ready") : remaining.ToString("0.0") + GameText.Source("arenaevents.006");
        slot.State.color = color;
        slot.Icon.color = color;
        slot.Fill.color = running || ready ? color : ReadyColor;
        slot.Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(running ? active / OverdriveDuration : 1f - remaining / cooldown), 1f);
        slot.Border.effectColor = new Color(color.r, color.g, color.b, running ? .8f : .45f);
        slot.Flash.color = new Color(ReadyColor.r, ReadyColor.g, ReadyColor.b, .22f * slot.FlashTime / .4f);
    }

    private void OnDisable() { overdriveUntil = 0; if (canvas != null) canvas.enabled = false; if (help != null) help.gameObject.SetActive(false); }
    private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
}
