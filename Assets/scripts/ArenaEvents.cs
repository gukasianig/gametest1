using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ArenaEventKind { MeteorShower, MagneticStorm, RepairField, SupplyDrop, HoldPoint, Ambush, ToxicPools, Wildfire, GuardChest }

// Random map events: every minute or so something changes on the arena for a short time.
// Meteor shower (telegraphed blasts hurt everyone), magnetic storm (faster enemies, double experience),
// repair field (a zone that heals the player) and supply drop (a cluster of experience crystals).
public class ArenaEvents : MonoBehaviour
{
    // Read by EnemyAI and Health; both stay at 1 outside the storm.
    public static float EnemySpeedFactor { get; private set; } = 1f;
    public static float ExperienceFactor { get; private set; } = 1f;
    public static ArenaEvents Instance { get; private set; }

    [Min(5f)] public float firstEventAt = 45f;
    [Min(5f)] public float minGap = 50f;
    [Min(5f)] public float maxGap = 80f;
    [Min(5f)] public float eventSeconds = 22f;
    public float stopBeforeBossSeconds = 25f;

    public bool Active { get; private set; }
    public ArenaEventKind Current { get; private set; }
    public int StartedCount { get; private set; }
    public float Remaining { get { return Mathf.Max(0f, endsAt - clock); } }

    private float clock, nextAt, endsAt, tick;
    private bool hasLast;
    private ArenaEventKind last;
    private Transform player;
    private PlayerHealth playerHealth;
    private readonly List<GameObject> props = new List<GameObject>();
    private readonly List<Health> buffer = new List<Health>();
    private Vector3 fieldCenter;
    private const float FieldRadius = 4.5f;

    private void Awake()
    {
        Instance = this;
        ResetFactors();
        nextAt = firstEventAt;
    }

    private void OnDestroy()
    {
        ClearProps();
        ResetFactors();
        if (hudCanvas != null) Destroy(hudCanvas.gameObject);
        if (Instance == this) Instance = null;
    }

    private static void ResetFactors()
    {
        EnemySpeedFactor = 1f;
        ExperienceFactor = 1f;
    }

    private void Update()
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.State != GameRunState.Playing) return;
        if (session.Boss != null || session.BossWarning) { if (Active) End(false); return; }
        clock += Time.deltaTime;
        if (Active)
        {
            Tick();
            if (clock >= endsAt) End(true);
            return;
        }
        if (clock >= nextAt && session.Elapsed < session.survivalSeconds - stopBeforeBossSeconds) Begin(Pick());
        else if (clock >= nextAt) nextAt = float.MaxValue;
    }

    private ArenaEventKind Pick()
    {
        ArenaEventKind kind;
        int guard = 0;
        do kind = (ArenaEventKind)Random.Range(0, System.Enum.GetValues(typeof(ArenaEventKind)).Length);
        while (hasLast && kind == last && ++guard < 8);
        return kind;
    }

    // Public so checks can start a chosen event without waiting for the timer.
    public void Begin(ArenaEventKind kind)
    {
        if (Active) End(false);
        FindPlayer();
        Active = true;
        Current = kind;
        last = kind;
        hasLast = true;
        StartedCount++;
        endsAt = clock + eventSeconds;
        tick = 0f;
        string title = "";
        switch (kind)
        {
            case ArenaEventKind.MeteorShower:
                title = "МЕТЕОРИТНЫЙ ДОЖДЬ — уходи с красных кругов!";
                break;
            case ArenaEventKind.MagneticStorm:
                EnemySpeedFactor = 1.3f;
                ExperienceFactor = 2f;
                title = "МАГНИТНАЯ БУРЯ — враги быстрее, опыт ×2";
                break;
            case ArenaEventKind.RepairField:
                fieldCenter = FindSpot(14f, 26f, FieldRadius);
                SpawnField(fieldCenter);
                title = "РЕМОНТНАЯ ЗОНА — встань в зелёный круг";
                break;
            case ArenaEventKind.SupplyDrop:
                SupplyDrop();
                title = "СБРОС ПРИПАСОВ — забери кристаллы у маяка";
                endsAt = clock + 12f;
                break;
            case ArenaEventKind.HoldPoint:
                holdCentre = FindSpot(16f, 28f, HoldRadius);
                holdProgress = 0f;
                holdWon = false;
                props.Add(Flat("Hold point", holdCentre, HoldRadius * 2f, new Color(0.55f, 0.45f, 0.1f), 0.04f));
                holdFill = Flat("Hold fill", holdCentre, 0.1f, new Color(1f, 0.8f, 0.2f), 0.06f);
                props.Add(holdFill);
                title = "УДЕРЖИ ТОЧКУ — стой в золотом круге " + Mathf.RoundToInt(holdSeconds) + " с";
                endsAt = clock + 32f;
                break;
            case ArenaEventKind.Ambush:
                StartCoroutine(AmbushRoutine());
                title = "ЗАСАДА — враги окружают!";
                endsAt = clock + 6f;
                break;
            case ArenaEventKind.ToxicPools:
                SpawnPools();
                title = "ТОКСИЧНЫЕ ЛУЖИ — вредят и вам, и врагам";
                break;
            case ArenaEventKind.Wildfire:
                SpawnWildfire();
                title = "ПОЖАР — очаги разрастаются, обходи огонь";
                break;
            case ArenaEventKind.GuardChest:
                StartChest();
                title = "ЗАЩИТИ СУНДУК — продержись " + Mathf.RoundToInt(chestSeconds) + " с";
                endsAt = clock + chestSeconds + 6f;
                break;
        }
        StartFx(kind);
        // Вместо текстового анонса — баннер (см. AnimateBanner); title остаётся для логов и старых вызовов.
        bannerAt = Time.unscaledTime;
        bannerTitle = title;
    }

    // --- Баннер события: въезжает слева по центру экрана, цвет по типу (опасно/полезно), ~3 с,
    // затем уезжает вверх, а событие остаётся в компактной плашке справа сверху.
    private const float BannerIn = 0.3f, BannerHold = 2.6f, BannerOut = 0.45f;
    private float bannerAt = -100f;
    private string bannerTitle = "";
    private RectTransform banner;
    private Image bannerBack, bannerStripe, plateStripe;
    private Text bannerName, bannerHint;
    private CanvasGroup bannerGroup;

    public static bool Dangerous(ArenaEventKind kind)
    {
        return kind == ArenaEventKind.MeteorShower || kind == ArenaEventKind.Ambush ||
               kind == ArenaEventKind.ToxicPools || kind == ArenaEventKind.Wildfire;
    }

    public static Color Tone(ArenaEventKind kind)
    {
        if (Dangerous(kind)) return new Color(1f, 0.4f, 0.28f);
        if (kind == ArenaEventKind.MagneticStorm) return new Color(1f, 0.82f, 0.3f);
        if (kind == ArenaEventKind.GuardChest) return new Color(0.85f, 0.5f, 1f);
        return new Color(0.35f, 0.95f, 0.55f);
    }

    public bool BannerVisible => banner != null && banner.gameObject.activeSelf;

    private void BuildBanner()
    {
        banner = GameUi.Box(hudCanvas.transform, "Event Banner", new Vector2(0f, 0.6f), new Vector2(1f, 0.76f), new Color(0.02f, 0.03f, 0.045f, 0.88f));
        bannerBack = banner.GetComponent<Image>();
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        bannerStripe = GameUi.Box(banner, "Stripe", new Vector2(0f, 0f), new Vector2(1f, 0.05f), Color.white).GetComponent<Image>();
        GameUi.Box(banner, "Stripe Top", new Vector2(0f, 0.95f), new Vector2(1f, 1f), new Color(1f, 1f, 1f, 0.15f));
        GameUi.Label(banner, "СОБЫТИЕ АРЕНЫ", new Vector2(0.2f, 0.74f), new Vector2(0.8f, 0.94f), 14, new Color(0.65f, 0.72f, 0.8f));
        bannerName = GameUi.Readable(GameUi.Label(banner, "", new Vector2(0.1f, 0.34f), new Vector2(0.9f, 0.78f), 40), 2f);
        bannerName.fontStyle = FontStyle.Bold;
        bannerHint = GameUi.Readable(GameUi.Label(banner, "", new Vector2(0.1f, 0.07f), new Vector2(0.9f, 0.36f), 21, new Color(0.93f, 0.95f, 0.97f)), 1f);
        banner.gameObject.SetActive(false);
    }

    private void AnimateBanner()
    {
        float age = Time.unscaledTime - bannerAt;
        bool visible = Active && age < BannerIn + BannerHold + BannerOut;
        banner.gameObject.SetActive(visible);
        if (!visible) return;
        Color tone = Tone(Current);
        bannerStripe.color = tone;
        bannerName.color = tone;
        bannerName.text = Title(Current);
        bannerHint.text = Hint(Current);
        float width = ((RectTransform)hudCanvas.transform).rect.width;
        float x = 0f, y = 0f, alpha = 1f;
        if (age < BannerIn)
        {
            float k = 1f - Mathf.Pow(1f - age / BannerIn, 3f);
            x = Mathf.Lerp(-width, 0f, k);
        }
        else if (age > BannerIn + BannerHold)
        {
            float k = (age - BannerIn - BannerHold) / BannerOut;
            y = k * k * 140f;
            alpha = 1f - k;
        }
        banner.anchoredPosition = new Vector2(x, y);
        bannerGroup.alpha = alpha;
        // Короткая вспышка фона при появлении.
        float flash = Mathf.Clamp01(1f - age / 0.5f);
        bannerBack.color = Color.Lerp(new Color(0.02f, 0.03f, 0.045f, 0.88f), new Color(tone.r * 0.5f, tone.g * 0.5f, tone.b * 0.5f, 0.92f), flash);
    }

    public void End(bool announce)
    {
        if (!Active) return;
        Active = false;
        StopAllCoroutines();
        ResetFactors();
        if (Current == ArenaEventKind.GuardChest) EndChest();
        else EndFx(Current);
        ClearProps();
        poolCentres.Clear();
        // Огонь не исчезает мгновенно: языки опускаются, нагар держится ~2,5 с.
        foreach (GroundFireZone fire in fires) if (fire != null) fire.Extinguish();
        fires.Clear();
        bool lost = (Current == ArenaEventKind.HoldPoint && !holdWon) || (Current == ArenaEventKind.GuardChest && chestLost);
        nextAt = clock + Random.Range(minGap, maxGap);
        GameSession session = GameSession.Instance;
        if (announce && session != null) session.Announce(lost ? (Current == ArenaEventKind.GuardChest ? "Сундук потерян" : "Точка потеряна") : "Событие закончилось", 2f);
    }

    // --- On-screen line under the timer: the running event, its time left and the hold-point progress.
    private Canvas hudCanvas;
    private Text hudText;

    public static string Title(ArenaEventKind kind)
    {
        switch (kind)
        {
            case ArenaEventKind.MeteorShower: return "МЕТЕОРИТНЫЙ ДОЖДЬ";
            case ArenaEventKind.MagneticStorm: return "МАГНИТНАЯ БУРЯ";
            case ArenaEventKind.RepairField: return "РЕМОНТНАЯ ЗОНА";
            case ArenaEventKind.SupplyDrop: return "СБРОС ПРИПАСОВ";
            case ArenaEventKind.HoldPoint: return "УДЕРЖИ ТОЧКУ";
            case ArenaEventKind.Ambush: return "ЗАСАДА";
            case ArenaEventKind.Wildfire: return "ПОЖАР";
            case ArenaEventKind.GuardChest: return "ЗАЩИТИ СУНДУК";
            default: return "ТОКСИЧНЫЕ ЛУЖИ";
        }
    }

    private void LateUpdate()
    {
        GameSession session = GameSession.Instance;
        bool show = Active && session != null && session.State == GameRunState.Playing;
        if (hudText == null)
        {
            if (!show) return;
            hudCanvas = GameUi.Canvas("Event HUD", 78);
            // Компактная плашка справа сверху: название с таймером и что делать; цветная полоса слева — тип.
            RectTransform box = GameUi.Box(hudCanvas.transform, "Event", new Vector2(0.735f, 0.8f), new Vector2(0.99f, 0.935f),
                new Color(0.02f, 0.035f, 0.05f, 0.85f));
            UiArtwork.Round(box.GetComponent<Image>());
            plateStripe = GameUi.Box(box, "Tone", new Vector2(0.015f, 0.12f), new Vector2(0.035f, 0.88f), GameUi.Accent).GetComponent<Image>();
            hudText = GameUi.Readable(GameUi.Label(box, "", new Vector2(0.05f, 0.52f), new Vector2(0.98f, 0.96f), 20, GameUi.Accent), 1f);
            hudText.alignment = TextAnchor.MiddleLeft;
            hudText.fontStyle = FontStyle.Bold;
            hudHint = GameUi.Label(box, "", new Vector2(0.05f, 0.04f), new Vector2(0.98f, 0.54f), 15, new Color(0.92f, 0.94f, 0.96f));
            hudHint.alignment = TextAnchor.UpperLeft;
            hudHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            BuildBanner();
            arrow = GameUi.Label(hudCanvas.transform, "►", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 44, new Color(1f, 0.85f, 0.25f));
            arrow.rectTransform.sizeDelta = new Vector2(70f, 70f);
            arrowDistance = GameUi.Label(hudCanvas.transform, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 18, new Color(1f, 0.85f, 0.25f));
            arrowDistance.rectTransform.sizeDelta = new Vector2(110f, 30f);
        }
        hudCanvas.enabled = show || (Active && session != null && session.State == GameRunState.Paused);
        if (!Active)
        {
            if (banner != null && banner.gameObject.activeSelf) banner.gameObject.SetActive(false);
            return;
        }
        string line = Title(Current) + "  " + Mathf.CeilToInt(Remaining) + " с";
        if (Current == ArenaEventKind.HoldPoint)
            line = holdWon ? "ТОЧКА УДЕРЖАНА" : "УДЕРЖИ ТОЧКУ  " + Mathf.FloorToInt(holdProgress) + " / " + Mathf.RoundToInt(holdSeconds) + " с  •  осталось " + Mathf.CeilToInt(Remaining) + " с";
        if (Current == ArenaEventKind.GuardChest)
            line = chestOpened ? "СУНДУК ОТКРЫТ" : chestLost ? "СУНДУК РАЗРУШЕН" :
                "СУНДУК  " + Mathf.CeilToInt(Mathf.Max(0f, chestSeconds - chestTimer)) + " с  •  прочность " + Mathf.CeilToInt(chestHealth / ChestMaxHealth * 100f) + "%";
        hudText.text = line;
        hudText.color = Tone(Current);
        plateStripe.color = Tone(Current);
        hudHint.text = Hint(Current);
        AnimateBanner();
        UpdateArrow();
    }

    // --- Подсказка «что делать» под названием события и стрелка к цели (круг, маяк).
    private Text hudHint, arrow, arrowDistance;
    private Vector3 supplyCentre;

    public static string Hint(ArenaEventKind kind)
    {
        switch (kind)
        {
            case ArenaEventKind.MeteorShower: return "Уходи с красных кругов — туда падают метеориты";
            case ArenaEventKind.MagneticStorm: return "Враги быстрее, но опыт ×2 — собирай кристаллы";
            case ArenaEventKind.RepairField: return "Иди по стрелке и встань в зелёный круг — он лечит";
            case ArenaEventKind.SupplyDrop: return "Иди по стрелке к жёлтому маяку и собери кристаллы";
            case ArenaEventKind.HoldPoint: return "Иди по стрелке и стой в золотом круге, пока идёт счёт";
            case ArenaEventKind.Ambush: return "Враги появятся вокруг тебя — будь готов к рывку";
            case ArenaEventKind.Wildfire: return "Огонь разрастается — не заходи в него, заманивай врагов";
            case ArenaEventKind.GuardChest: return "Не дай врагам сломать сундук в центре арены — когда время выйдет, он даст особую награду";
            default: return "Не стой в зелёной кислоте — она жжёт и врагов";
        }
    }

    // Цель, к которой надо идти; у опасных событий цели нет.
    public bool TryGetTarget(out Vector3 target)
    {
        target = Vector3.zero;
        if (!Active) return false;
        switch (Current)
        {
            case ArenaEventKind.RepairField: target = fieldCenter; return true;
            case ArenaEventKind.SupplyDrop: target = supplyCentre; return true;
            case ArenaEventKind.HoldPoint: target = holdCentre; return !holdWon;
            case ArenaEventKind.GuardChest: target = chestCentre; return !chestOpened && !chestLost;
            default: return false;
        }
    }

    private void UpdateArrow()
    {
        Camera view = Camera.main;
        bool visible = view != null && player != null && TryGetTarget(out Vector3 target);
        arrow.gameObject.SetActive(visible);
        arrowDistance.gameObject.SetActive(visible);
        if (!visible) return;

        TryGetTarget(out target);
        float metres = WeaponTargets.Flat(player.position, target);
        RectTransform canvasRect = (RectTransform)hudCanvas.transform;
        Vector2 size = canvasRect.rect.size;
        Vector3 screen = view.WorldToScreenPoint(target + Vector3.up * 1.5f);
        Vector2 local = new Vector2(screen.x / Screen.width * size.x - size.x * 0.5f, screen.y / Screen.height * size.y - size.y * 0.5f);
        if (screen.z < 0f) local = -local;
        Vector2 half = size * 0.5f - new Vector2(60f, 60f);
        bool onScreen = screen.z > 0f && Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
        float angle;
        if (onScreen)
        {
            // Цель на экране: стрелка висит над ней и смотрит вниз, мягко покачиваясь.
            local.y += 40f + 8f * Mathf.Sin(Time.unscaledTime * 6f);
            angle = -90f;
        }
        else
        {
            // Цель за экраном: стрелка у края экрана в её сторону.
            angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            float scale = Mathf.Min(half.x / Mathf.Max(0.01f, Mathf.Abs(local.x)), half.y / Mathf.Max(0.01f, Mathf.Abs(local.y)));
            local *= scale;
        }
        arrow.rectTransform.anchoredPosition = local;
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        arrowDistance.rectTransform.anchoredPosition = local + (onScreen ? new Vector2(0f, 45f) : -local.normalized * 55f);
        arrowDistance.text = Mathf.RoundToInt(metres) + " м";
    }

    private void FindPlayer()
    {
        if (player != null) return;
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null) return;
        player = found.transform;
        playerHealth = found.GetComponent<PlayerHealth>();
    }

    private void Tick()
    {
        FindPlayer();
        if (player == null) return;
        Atmosphere();
        if (Current == ArenaEventKind.GuardChest) { TickChest(); return; }
        tick -= Time.deltaTime;
        if (Current == ArenaEventKind.MeteorShower)
        {
            if (tick > 0f) return;
            tick = 0.55f;
            Vector3 point = Random.value < 0.65f ? FindSpot(0f, 13f, 2f) : FindSpot(0f, 40f, 2f);
            Meteor.Create(point, this);
        }
        else if (Current == ArenaEventKind.HoldPoint)
        {
            if (holdWon) return;
            Vector3 offset = player.position - holdCentre;
            offset.y = 0f;
            if (offset.magnitude <= HoldRadius) holdProgress += Time.deltaTime;
            float share = Mathf.Clamp01(holdProgress / Mathf.Max(0.1f, holdSeconds));
            if (holdFill != null) holdFill.transform.localScale = new Vector3(HoldRadius * 2f * share, 0.01f, HoldRadius * 2f * share);
            if (share >= 1f) HoldSucceeded();
        }
        else if (Current == ArenaEventKind.ToxicPools)
        {
            if (clock <= poolsActiveAt + 0.1f) SpreadPools();
            if (tick > 0f) return;
            tick = 0.5f;
            TickPools();
        }
        else if (Current == ArenaEventKind.RepairField)
        {
            if (tick > 0f) return;
            tick = 0.5f;
            Vector3 offset = player.position - fieldCenter;
            offset.y = 0f;
            if (playerHealth != null && offset.magnitude <= FieldRadius) playerHealth.Heal(3);
        }
    }

    // A random walkable point around the player, between minDistance and maxDistance.
    private Vector3 FindSpot(float minDistance, float maxDistance, float radius)
    {
        Vector3 centre = player != null ? player.position : Vector3.zero;
        for (int i = 0; i < 24; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(minDistance, maxDistance);
            Vector3 point = new Vector3(centre.x + circle.x, 0f, centre.z + circle.y);
            point.y = GroundHeight.At(point);
            if (ArenaLayout.Instance == null || ArenaLayout.Instance.IsWalkable(point, radius)) return point;
        }
        Vector3 fallback = new Vector3(centre.x, 0f, centre.z);
        fallback.y = GroundHeight.At(fallback);
        return fallback;
    }

    private void SpawnField(Vector3 centre)
    {
        GameObject disc = Flat("Repair field", centre, FieldRadius * 2f, new Color(0.15f, 0.8f, 0.4f), 0.05f);
        props.Add(disc);
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Repair beacon";
        Destroy(beam.GetComponent<Collider>());
        beam.transform.position = centre + Vector3.up * 3f;
        beam.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
        Paint(beam, new Color(0.3f, 1f, 0.55f));
        props.Add(beam);
    }

    private void SupplyDrop()
    {
        Vector3 centre = FindSpot(18f, 30f, 3f);
        supplyCentre = centre;
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Supply beacon";
        Destroy(beam.GetComponent<Collider>());
        beam.transform.position = centre + Vector3.up * 5f;
        beam.transform.localScale = new Vector3(0.6f, 5f, 0.6f);
        Paint(beam, new Color(1f, 0.8f, 0.2f));
        props.Add(beam);
        props.Add(Flat("Supply marker", centre, 6f, new Color(0.9f, 0.7f, 0.15f), 0.05f));
        int reward = 6 + Mathf.Min(10, StartedCount * 2);
        for (int i = 0; i < 14; i++)
        {
            Vector2 around = Random.insideUnitCircle * 2.6f;
            ExperiencePickup.Spawn(centre + new Vector3(around.x, 0f, around.y), reward);
        }
    }

    // --- Hold the point: stand in the circle for holdSeconds (the clock only runs inside) to earn a crystal burst and a repair kit.
    [Min(1f)] public float holdSeconds = 10f;
    public float HoldProgress { get { return holdProgress; } }
    private const float HoldRadius = 5f;
    private Vector3 holdCentre;
    private float holdProgress;
    private bool holdWon;
    private GameObject holdFill;

    private void HoldSucceeded()
    {
        holdWon = true;
        int reward = 4 + Mathf.Min(8, StartedCount);
        for (int i = 0; i < 12; i++)
        {
            Vector2 around = Random.insideUnitCircle * 3f;
            ExperiencePickup.Spawn(holdCentre + new Vector3(around.x, 0f, around.y), reward);
        }
        DropPickup.Spawn(holdCentre, PickupKind.Health);
        EnemyFx.Ring(holdCentre, HoldRadius * 1.6f, new Color(1f, 0.85f, 0.3f), 0.9f, 0.5f);
        EnemyFx.LightFlash(holdCentre + Vector3.up * 2f, new Color(1f, 0.85f, 0.4f), 14f, 5f, 0.9f);
        EnemyFx.Blobs(holdCentre + Vector3.up * 0.8f, new Color(1f, 0.8f, 0.25f), 26, 8f, 0.2f, 1f, holdCentre.y);
        ScreenShake.AddAt(holdCentre, 0.2f);
        GameAudio.Play(Sfx.Zap, 0.6f);
        GameSession session = GameSession.Instance;
        if (session != null) session.Announce("ТОЧКА УДЕРЖАНА — награда!", 3f);
        endsAt = Mathf.Min(endsAt, clock + 1.5f);
    }

    // --- Ambush: red markers around the hero, then a ring of cheap enemies appears on them.
    public int AmbushCount { get; private set; }

    private IEnumerator AmbushRoutine()
    {
        FindPlayer();
        if (player == null) yield break;
        var points = new List<Vector3>();
        var markers = new List<GameObject>();
        int count = Mathf.Min(16, 8 + Mathf.Max(0, StartedCount - 1) * 2);
        for (int i = 0; i < count; i++)
        {
            float angle = (i + Random.value * 0.5f) * Mathf.PI * 2f / count;
            Vector3 point = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(10f, 13f);
            point.y = 0f;
            if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(point, 1f)) continue;
            points.Add(point);
            GameObject marker = Flat("Ambush marker", point, 2.2f, new Color(0.8f, 0.1f, 0.1f), 0.05f);
            markers.Add(marker);
            props.Add(marker);
        }
        float end = Time.time + 1.6f;
        while (Time.time < end) yield return null;
        foreach (GameObject marker in markers) if (marker != null) Destroy(marker);
        WaveManager waves = FindObjectOfType<WaveManager>();
        if (waves != null && GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing)
        {
            AmbushCount += waves.SpawnAt(points);
            // Вспышка и дым на каждой точке появления.
            foreach (Vector3 point in points)
            {
                Vector3 ground = new Vector3(point.x, GroundHeight.At(point), point.z);
                EnemyFx.Ring(ground, 2.4f, new Color(1f, 0.3f, 0.2f), 0.6f);
                EnemyFx.Blobs(ground + Vector3.up * 0.4f, new Color(0.8f, 0.2f, 0.15f), 6, 4f, 0.18f, 0.6f, ground.y);
            }
            ScreenShake.AddAt(player.position, 0.2f);
            GameAudio.Play(Sfx.Deploy, 0.6f);
        }
    }

    // --- Toxic pools: green circles that hurt everything inside after a short warning.
    private readonly List<Vector3> poolCentres = new List<Vector3>();
    private float poolsActiveAt;
    private const float PoolRadius = 4f;
    public int PoolCount { get { return poolCentres.Count; } }

    private void SpawnPools()
    {
        poolCentres.Clear();
        poolsActiveAt = clock + 1.5f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 centre = FindSpot(4f + i * 4f, 14f + i * 6f, PoolRadius);
            poolCentres.Add(centre);
            props.Add(AcidPool(centre));
        }
    }

    // Модель AcidPuddle (радиус ~2,7 м при масштабе 1) растягивается по XZ до PoolRadius. Пока идёт
    // предупреждение, лужа растекается от трети размера; без модели остаётся прежний зелёный диск.
    private const float AcidModelRadius = 2.7f;
    private static GameObject acidModel;
    private static bool acidMissing;

    private GameObject AcidPool(Vector3 centre)
    {
        if (acidModel == null && !acidMissing)
        {
            acidModel = Resources.Load<GameObject>("Hazards/AcidPuddle");
            acidMissing = acidModel == null;
            if (acidMissing) Debug.LogWarning("ArenaEvents: Resources/Hazards/AcidPuddle not found; toxic pools use plain discs.");
        }
        if (acidModel == null) return Flat("Toxic pool", centre, PoolRadius * 2f, new Color(0.25f, 0.5f, 0.1f), 0.04f);
        // Лужа ложится по склону (раньше стояла плоско и на холме торчала плитой) и сплюснута по высоте.
        GameObject pool = Instantiate(acidModel, new Vector3(centre.x, GroundHeight.At(centre) + 0.01f + GroundHeight.Lift(centre, PoolRadius) * 0.3f, centre.z),
            GroundHeight.Tilt(centre, Random.Range(0f, 360f)));
        pool.name = "Toxic pool";
        foreach (Collider collider in pool.GetComponentsInChildren<Collider>()) Destroy(collider);
        foreach (Renderer renderer in pool.GetComponentsInChildren<Renderer>())
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pool.transform.localScale = PoolScale(0.35f);
        return pool;
    }

    private static Vector3 PoolScale(float share)
    {
        float s = PoolRadius / AcidModelRadius * share;
        return new Vector3(s, 0.5f, s);
    }

    private void SpreadPools()
    {
        if (acidModel == null) return;
        float share = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(1f - (poolsActiveAt - clock) / 1.5f));
        foreach (GameObject prop in props)
            if (prop != null && prop.name == "Toxic pool") prop.transform.localScale = PoolScale(share);
    }

    private void TickPools()
    {
        if (clock < poolsActiveAt) return;
        WeaponTargets.Collect(buffer);
        foreach (Vector3 centre in poolCentres)
        {
            foreach (Health health in buffer)
                if (health != null && !health.isDead && WeaponTargets.Flat(centre, health.transform.position) <= PoolRadius)
                    health.TakeDamage(12);
            if (playerHealth != null && WeaponTargets.Flat(centre, player.position) <= PoolRadius) playerHealth.TakeDamage(5);
        }
    }

    // --- Wildfire: three hearths start small and spread to 4.5 m; they burn the player and enemies alike.
    private readonly List<GroundFireZone> fires = new List<GroundFireZone>();
    private const float FireStartRadius = 1.2f, FireMaxRadius = 4.5f, FireGrowSeconds = 12f;
    public int FireCount { get { fires.RemoveAll(f => f == null); return fires.Count; } }

    private void SpawnWildfire()
    {
        fires.Clear();
        for (int i = 0; i < 3; i++)
        {
            // Ищем место под полный размер, чтобы разросшийся огонь не уходил в стены.
            Vector3 centre = FindSpot(6f + i * 4f, 14f + i * 6f, FireMaxRadius);
            fires.Add(GroundFireZone.Spawn(centre, FireStartRadius, FireMaxRadius, FireGrowSeconds,
                eventSeconds + 5f, health => health.TakeDamage(12), 5));
        }
    }

    // --- Защити сундук: сундук стоит в центре арены. Враги поблизости ломают его, герой должен их убивать;
    // когда время выйдет, а сундук цел, он открывается и предлагает три особых улучшения (PlayerProgression.OfferChestReward).
    [Min(5f)] public float chestSeconds = 35f;
    public const float ChestMaxHealth = 500f, ChestZoneRadius = 8f, ChestBiteRadius = 3.2f, ChestBiteDps = 4f;
    public float ChestHealth { get { return chestHealth; } }
    public bool ChestOpened { get { return chestOpened; } }
    public bool ChestLost { get { return chestLost; } }
    public Vector3 ChestCentre { get { return chestCentre; } }
    private Vector3 chestCentre;
    private float chestHealth, chestTimer, chestWaveAt, chestRetarget, chestOpenAt, chestHitFlash;
    private bool chestOpened, chestLost, rewardOffered;
    private Transform chestRoot, chestLid;
    private GameObject chestFill;
    private Light chestLight;
    private Renderer[] chestRenderers;

    private void StartChest()
    {
        chestCentre = ChestSpot();
        chestHealth = ChestMaxHealth;
        chestTimer = 0f;
        chestWaveAt = 2.5f;
        chestRetarget = 0f;
        chestOpened = chestLost = rewardOffered = false;
        // Зона защиты и заливка-таймер (растёт к моменту открытия).
        props.Add(Flat("Chest zone", chestCentre, ChestZoneRadius * 2f, new Color(0.3f, 0.12f, 0.45f), 0.04f));
        chestFill = Flat("Chest fill", chestCentre, 0.1f, new Color(0.75f, 0.4f, 1f), 0.06f);
        props.Add(chestFill);
        BuildChest();
    }

    // Центр арены; если там занято — ближайшая свободная точка.
    private Vector3 ChestSpot()
    {
        for (int i = 0; i < 24; i++)
        {
            Vector2 offset = i == 0 ? Vector2.zero : Random.insideUnitCircle.normalized * (2f + i * 1.5f);
            Vector3 point = new Vector3(offset.x, 0f, offset.y);
            point.y = GroundHeight.At(point);
            if (ArenaLayout.Instance == null || ArenaLayout.Instance.IsWalkable(point, 2f)) return point;
        }
        Vector3 fallback = Vector3.zero;
        fallback.y = GroundHeight.At(fallback);
        return fallback;
    }

    private static GameObject ChestPart(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color colour)
    {
        GameObject part = EnemyFx.Prim(type, parent.position, colour);
        part.name = "Chest part";
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return part;
    }

    private void BuildChest()
    {
        var root = new GameObject("Guard chest");
        root.transform.SetPositionAndRotation(chestCentre, GroundHeight.Tilt(chestCentre, 0f));
        props.Add(root);
        chestRoot = root.transform;
        Color wood = new Color(0.42f, 0.24f, 0.11f), gold = new Color(1f, 0.78f, 0.22f), dark = new Color(0.2f, 0.12f, 0.07f);
        ChestPart(chestRoot, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(2.1f, 1f, 1.35f), wood);
        ChestPart(chestRoot, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(2.16f, 0.16f, 1.41f), gold);
        ChestPart(chestRoot, PrimitiveType.Cube, new Vector3(-0.85f, 0.5f, 0f), new Vector3(0.18f, 1.04f, 1.41f), gold);
        ChestPart(chestRoot, PrimitiveType.Cube, new Vector3(0.85f, 0.5f, 0f), new Vector3(0.18f, 1.04f, 1.41f), gold);
        ChestPart(chestRoot, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(2.3f, 0.1f, 1.55f), dark);
        // Крышка вращается вокруг заднего ребра.
        var hinge = new GameObject("Chest lid");
        chestLid = hinge.transform;
        chestLid.SetParent(chestRoot, false);
        chestLid.localPosition = new Vector3(0f, 1f, 0.67f);
        ChestPart(chestLid, PrimitiveType.Cube, new Vector3(0f, 0.22f, -0.67f), new Vector3(2.1f, 0.44f, 1.35f), wood);
        ChestPart(chestLid, PrimitiveType.Cube, new Vector3(0f, 0.22f, -0.67f), new Vector3(2.16f, 0.12f, 1.41f), gold);
        ChestPart(chestLid, PrimitiveType.Cube, new Vector3(0f, 0.06f, -1.38f), new Vector3(0.34f, 0.42f, 0.1f), gold);
        ChestPart(chestRoot, PrimitiveType.Sphere, new Vector3(0f, 1.9f, 0f), new Vector3(0.4f, 0.4f, 0.4f), new Color(0.85f, 0.5f, 1f));
        chestRenderers = chestRoot.GetComponentsInChildren<Renderer>();
        GameObject lightObject = new GameObject("Chest glow");
        lightObject.transform.SetParent(chestRoot, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        chestLight = lightObject.AddComponent<Light>();
        chestLight.type = LightType.Point;
        chestLight.color = new Color(0.8f, 0.5f, 1f);
        chestLight.range = 9f;
        chestLight.intensity = 2.2f;
        chestLight.shadows = LightShadows.None;
    }

    private void TickChest()
    {
        if (chestRoot == null) return;
        float dt = Time.deltaTime;
        // Мерцание свечения и покачивание «самоцвета»; удар по сундуку краснит свет.
        chestHitFlash = Mathf.Max(0f, chestHitFlash - dt * 3f);
        if (chestLight != null)
        {
            chestLight.color = Color.Lerp(new Color(0.8f, 0.5f, 1f), new Color(1f, 0.25f, 0.2f), chestHitFlash);
            chestLight.intensity = (chestOpened ? 3.5f : 2.2f) + 0.4f * Mathf.Sin(Time.time * 5f);
        }
        if (chestOpened)
        {
            float open = Mathf.Clamp01((clock - chestOpenAt) / 0.6f);
            chestLid.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 105f, 1f - (1f - open) * (1f - open)), 0f, 0f);
            if (!rewardOffered && clock >= chestOpenAt + 0.9f) OfferReward();
            return;
        }
        if (chestLost) return;

        chestTimer += dt;
        float share = Mathf.Clamp01(chestTimer / chestSeconds);
        if (chestFill != null) chestFill.transform.localScale = new Vector3(ChestZoneRadius * 2f * share, 0.01f, ChestZoneRadius * 2f * share);

        // Чем меньше прочность, тем сильнее трясётся сундук.
        float danger = 1f - chestHealth / ChestMaxHealth;
        chestRoot.localRotation = GroundHeight.Tilt(chestCentre, Mathf.Sin(Time.time * 40f) * danger * 4f);

        // Враги в 3,2 м от сундука грызут его; чистая рукопашная толпа считается так же, как стрелки.
        chestRetarget -= dt;
        bool retarget = chestRetarget <= 0f;
        if (retarget) chestRetarget = 0.3f;
        int biting = 0;
        foreach (EnemyAI enemy in EnemyAI.All)
        {
            if (enemy == null) continue;
            float toChest = WeaponTargets.Flat(enemy.transform.position, chestCentre);
            if (toChest <= ChestBiteRadius) biting++;
            if (!retarget || enemy.GetComponent<EnemyAbility>() != null) continue;
            // Простые рукопашные враги вдали от героя идут к сундуку, поэтому его надо защищать, а не только убегать.
            float toPlayer = WeaponTargets.Flat(enemy.transform.position, player.position);
            if (toChest <= 18f && toPlayer > 6f) enemy.target = chestRoot;
            else if (enemy.target == chestRoot) enemy.target = player;
        }
        if (biting > 0)
        {
            chestHealth -= biting * ChestBiteDps * dt;
            chestHitFlash = 1f;
            chestBite -= dt;
            if (chestBite <= 0f)
            {
                chestBite = 0.35f;
                EnemyFx.Puff(chestCentre + new Vector3(Random.Range(-1f, 1f), 1.1f, Random.Range(-0.7f, 0.7f)), new Color(1f, 0.45f, 0.3f), 0.5f, 0.45f, Vector3.up * 1.5f);
                ScreenShake.AddAt(chestCentre, 0.05f);
            }
        }
        if (chestHealth <= 0f) { ChestDestroyed(); return; }

        // Волны подкрепления по кольцу вокруг сундука.
        chestWaveAt -= dt;
        if (chestWaveAt <= 0f)
        {
            chestWaveAt = 6f;
            SpawnChestWave();
        }
        if (chestTimer >= chestSeconds) OpenChest();
    }

    private float chestBite;

    private void SpawnChestWave()
    {
        WaveManager waves = FindObjectOfType<WaveManager>();
        if (waves == null) return;
        var points = new List<Vector3>();
        int count = Mathf.Min(10, 4 + StartedCount);
        float start = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float angle = start + i * Mathf.PI * 2f / count;
            Vector3 point = chestCentre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(13f, 16f);
            point.y = 0f;
            if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(point, 1f)) continue;
            points.Add(point);
            Vector3 ground = new Vector3(point.x, GroundHeight.At(point), point.z);
            EnemyFx.Ring(ground, 2.2f, new Color(0.9f, 0.3f, 0.9f), 0.7f);
            EnemyFx.Puff(ground + Vector3.up * 0.3f, new Color(0.7f, 0.3f, 0.8f), 1.2f, 0.7f, Vector3.up * 1.5f);
        }
        AmbushCount += waves.SpawnAt(points);
        GameAudio.Play(Sfx.Deploy, 0.5f);
    }

    private void ChestDestroyed()
    {
        chestLost = true;
        EnemyFx.Explosion(chestCentre, 3.2f, 0.9f);
        GameAudio.Play(Sfx.Explosion, 0.8f);
        if (chestRoot != null) chestRoot.gameObject.SetActive(false);
        ReleaseTargets();
        endsAt = Mathf.Min(endsAt, clock + 1.5f);
    }

    private void OpenChest()
    {
        chestOpened = true;
        chestOpenAt = clock;
        // Враги отпускают сундук, чтобы не бегать вокруг него во время награды.
        ReleaseTargets();
        EnemyFx.Ring(chestCentre, ChestZoneRadius, new Color(1f, 0.85f, 0.3f), 0.9f, 0.6f);
        EnemyFx.Ring(chestCentre, ChestZoneRadius * 0.6f, new Color(0.85f, 0.5f, 1f), 0.7f, 0.5f);
        EnemyFx.LightFlash(chestCentre + Vector3.up * 2f, new Color(1f, 0.85f, 0.4f), 16f, 6f, 1.2f);
        EnemyFx.Blobs(chestCentre + Vector3.up * 1.1f, new Color(1f, 0.8f, 0.25f), 30, 9f, 0.22f, 1.1f, chestCentre.y);
        ScreenShake.AddAt(chestCentre, 0.5f);
        GameAudio.Play(Sfx.Zap, 0.8f);
        // Монетки светлым потоком вверх, пока крышка поднимается.
        for (int i = 0; i < 10; i++)
            EnemyFx.Puff(chestCentre + new Vector3(Random.Range(-0.6f, 0.6f), 1.1f, Random.Range(-0.4f, 0.4f)), new Color(1f, 0.85f, 0.3f), 0.4f, 1.2f, new Vector3(Random.Range(-1f, 1f), Random.Range(3f, 5f), Random.Range(-1f, 1f)));
        endsAt = Mathf.Max(endsAt, clock + 8f);
    }

    private void OfferReward()
    {
        rewardOffered = true;
        PlayerProgression progression = PlayerProgression.Instance;
        if (progression != null && progression.OfferChestReward())
        {
            endsAt = clock + 2f;
            return;
        }
        // Нечего предложить (все карточки взяты) — вместо этого горсть кристаллов.
        for (int i = 0; i < 14; i++)
        {
            Vector2 around = Random.insideUnitCircle * 2.6f;
            ExperiencePickup.Spawn(chestCentre + new Vector3(around.x, 0f, around.y), 10);
        }
        endsAt = clock + 2f;
    }

    private void ReleaseTargets()
    {
        foreach (EnemyAI enemy in EnemyAI.All)
            if (enemy != null && enemy.target == chestRoot && player != null) enemy.target = player;
    }

    private void EndChest()
    {
        ReleaseTargets();
        if (!chestOpened && !chestLost && chestRoot != null)
        {
            // Событие прервано (босс): сундук гаснет без награды.
            EnemyFx.Puff(chestCentre + Vector3.up, new Color(0.6f, 0.4f, 0.8f), 2f, 0.8f, Vector3.up);
        }
        chestRoot = null;
        chestLid = null;
        chestLight = null;
    }

    // --- Эффекты: вспышка при старте события, постоянная «атмосфера» и затухание в конце.
    private float fxTick, ringAt;

    private void StartFx(ArenaEventKind kind)
    {
        FindPlayer();
        Color tone = Tone(kind);
        Vector3 origin = player != null ? new Vector3(player.position.x, GroundHeight.At(player.position), player.position.z) : Vector3.zero;
        switch (kind)
        {
            case ArenaEventKind.RepairField: origin = fieldCenter; break;
            case ArenaEventKind.SupplyDrop: origin = supplyCentre; break;
            case ArenaEventKind.HoldPoint: origin = holdCentre; break;
            case ArenaEventKind.GuardChest: origin = chestCentre; break;
        }
        // Ударная волна от места события (или от героя, если события привязаны к карте целиком).
        EnemyFx.Ring(origin, 9f, tone, 0.8f, 0.5f);
        EnemyFx.Ring(origin, 5f, tone, 0.6f, 0.35f);
        EnemyFx.LightFlash(origin + Vector3.up * 2f, tone, 14f, 4f, 0.9f);
        ScreenShake.AddAt(origin, Dangerous(kind) ? 0.25f : 0.12f);
        GameAudio.Play(Dangerous(kind) ? Sfx.Explosion : Sfx.Deploy, Dangerous(kind) ? 0.35f : 0.6f);
        if (kind == ArenaEventKind.SupplyDrop)
        {
            // Контейнер падает с неба: столб света и россыпь золотых брызг.
            EnemyFx.Blobs(origin + Vector3.up * 0.6f, new Color(1f, 0.8f, 0.25f), 24, 8f, 0.2f, 1f, origin.y);
            EnemyFx.Explosion(origin, 2.2f, 0.4f, false);
        }
        fxTick = 0f;
        ringAt = 0f;
    }

    private void EndFx(ArenaEventKind kind)
    {
        FindPlayer();
        Vector3 origin = Vector3.zero;
        switch (kind)
        {
            case ArenaEventKind.RepairField: origin = fieldCenter; break;
            case ArenaEventKind.SupplyDrop: origin = supplyCentre; break;
            case ArenaEventKind.HoldPoint: origin = holdCentre; break;
            default: return;
        }
        EnemyFx.Ring(origin, 4f, Tone(kind), 0.6f, 0.3f);
        EnemyFx.Puff(origin + Vector3.up * 0.8f, Tone(kind), 1.6f, 0.9f, Vector3.up * 1.5f);
    }

    // Лёгкие частицы-«настроение» события: раз в 0,2 с одна-две частицы, раз в ~1,5 с кольцо.
    private void Atmosphere()
    {
        fxTick -= Time.deltaTime;
        if (fxTick > 0f) return;
        fxTick = 0.2f;
        bool pulse = clock >= ringAt;
        if (pulse) ringAt = clock + 1.6f;
        Color tone = Tone(Current);
        switch (Current)
        {
            case ArenaEventKind.RepairField:
                FieldParticles(fieldCenter, FieldRadius, new Color(0.35f, 1f, 0.55f), pulse);
                if (playerHealth != null && WeaponTargets.Flat(player.position, fieldCenter) <= FieldRadius)
                    EnemyFx.Puff(player.position + Vector3.up * 0.4f + Random.insideUnitSphere * 0.4f, new Color(0.5f, 1f, 0.6f), 0.35f, 0.7f, Vector3.up * 1.8f);
                break;
            case ArenaEventKind.SupplyDrop:
                FieldParticles(supplyCentre, 2.6f, new Color(1f, 0.85f, 0.3f), pulse);
                EnemyFx.Puff(supplyCentre + Vector3.up * Random.Range(0.5f, 8f), new Color(1f, 0.9f, 0.4f), 0.25f, 0.9f, Vector3.up * 0.6f);
                break;
            case ArenaEventKind.HoldPoint:
                if (!holdWon) FieldParticles(holdCentre, HoldRadius, new Color(1f, 0.8f, 0.25f), pulse);
                break;
            case ArenaEventKind.GuardChest:
                if (!chestLost)
                {
                    FieldParticles(chestCentre, ChestZoneRadius, new Color(0.8f, 0.5f, 1f), pulse && !chestOpened);
                    EnemyFx.Puff(chestCentre + new Vector3(Random.Range(-0.8f, 0.8f), 1.5f, Random.Range(-0.5f, 0.5f)),
                        chestOpened ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.55f, 1f), 0.3f, 1f, Vector3.up * 1.4f);
                }
                break;
            case ArenaEventKind.MagneticStorm:
                // Искры бури вокруг героя и редкие вспышки грозы.
                for (int i = 0; i < 2; i++)
                {
                    Vector2 around = Random.insideUnitCircle * 14f;
                    Vector3 spot = player.position + new Vector3(around.x, 0.5f + Random.value * 3f, around.y);
                    EnemyFx.Puff(spot, new Color(0.7f, 0.85f, 1f), 0.22f, 0.5f, Random.insideUnitSphere * 2f);
                }
                if (Random.value < 0.18f)
                {
                    Vector2 flash = Random.insideUnitCircle * 20f;
                    EnemyFx.LightFlash(player.position + new Vector3(flash.x, 6f, flash.y), new Color(0.7f, 0.8f, 1f), 30f, 6f, 0.25f);
                }
                break;
            case ArenaEventKind.ToxicPools:
                foreach (Vector3 centre in poolCentres)
                {
                    Vector2 around = Random.insideUnitCircle * PoolRadius * 0.8f;
                    EnemyFx.Puff(centre + new Vector3(around.x, 0.2f, around.y), new Color(0.45f, 0.95f, 0.2f), 0.45f, 0.9f, Vector3.up * 1.2f);
                    if (pulse) EnemyFx.Ring(centre, PoolRadius, new Color(0.5f, 0.9f, 0.2f), 0.9f, 0.3f);
                }
                break;
            case ArenaEventKind.Wildfire:
                foreach (GroundFireZone fire in fires)
                {
                    if (fire == null) continue;
                    Vector2 around = Random.insideUnitCircle * 3f;
                    EnemyFx.Puff(fire.transform.position + new Vector3(around.x, 1f, around.y), new Color(1f, 0.55f, 0.15f), 0.18f, 1.1f, Vector3.up * 2.5f + Random.insideUnitSphere);
                }
                break;
            case ArenaEventKind.MeteorShower:
                if (pulse) EnemyFx.LightFlash(player.position + Vector3.up * 8f, new Color(1f, 0.5f, 0.2f), 30f, 3f, 0.4f);
                break;
            case ArenaEventKind.Ambush:
                if (pulse) EnemyFx.Ring(player.position, 6f, tone, 0.7f, 0.3f);
                break;
        }
    }

    // Частицы вокруг круга: поднимаются по краю, а на пульсе по земле расходится кольцо.
    private void FieldParticles(Vector3 centre, float radius, Color colour, bool pulse)
    {
        for (int i = 0; i < 2; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            Vector3 spot = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * Random.Range(0.55f, 1f);
            spot.y = GroundHeight.At(spot) + 0.2f;
            EnemyFx.Puff(spot, colour, 0.28f, 1f, Vector3.up * Random.Range(1.2f, 2.4f));
        }
        if (pulse) EnemyFx.Ring(new Vector3(centre.x, GroundHeight.At(centre), centre.z), radius, colour, 1.1f, 0.3f);
    }

    // Damage used by meteors: enemies and the player take it, so staying in the circle is never free.
    public void Blast(Vector3 centre, float radius, int enemyDamage, int playerDamage)
    {
        WeaponTargets.Collect(buffer);
        foreach (Health health in buffer)
            if (health != null && WeaponTargets.Flat(centre, health.transform.position) <= radius + WeaponTargets.BodyRadius(health) * 0.5f)
                health.TakeDamage(enemyDamage);
        FindPlayer();
        if (playerHealth != null && WeaponTargets.Flat(centre, player.position) <= radius) playerHealth.TakeDamage(playerDamage);
        Vector3 impact = new Vector3(centre.x, GroundHeight.At(centre), centre.z);
        EnemyFx.Explosion(impact, radius * 0.85f, 0.8f);
        // Метеорит оставляет воронку-ожог и ударную волну.
        EnemyFx.GroundSplat(impact, radius * 0.8f, new Color(0.08f, 0.06f, 0.05f), 7f);
        EnemyFx.Ring(impact, radius * 1.3f, new Color(1f, 0.55f, 0.2f), 0.5f, 0.4f);
        GameAudio.Play(Sfx.Explosion, 0.7f);
    }

    internal static GameObject Flat(string name, Vector3 centre, float diameter, Color colour, float height)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = name;
        Destroy(disc.GetComponent<Collider>());
        // На аренах с рельефом земля выше нуля: без этого круги событий оказывались под землёй.
        disc.transform.position = new Vector3(centre.x, GroundHeight.At(centre) + height + GroundHeight.Lift(centre, diameter * 0.5f), centre.z);
        disc.transform.rotation = GroundHeight.Tilt(centre);   // по склону зелёной арены
        disc.transform.localScale = new Vector3(diameter, 0.01f, diameter);
        Paint(disc, colour);
        return disc;
    }

    internal static void Paint(GameObject item, Color colour)
    {
        Renderer renderer = item.GetComponent<Renderer>();
        if (renderer != null && ArenaLayout.Instance != null) renderer.sharedMaterial = ArenaLayout.Instance.Material(colour);
    }

    private void ClearProps()
    {
        foreach (GameObject item in props) if (item != null) Destroy(item);
        props.Clear();
    }
}

// One meteor: a red circle grows for a moment, then the blast hits.
public class Meteor : MonoBehaviour
{
    public const float Delay = 1.4f, Radius = 3.5f;
    private ArenaEvents owner;
    private Transform fill;
    private float age;

    public static void Create(Vector3 point, ArenaEvents owner)
    {
        GameObject ring = ArenaEvents.Flat("Meteor warning", point, Radius * 2f, new Color(0.5f, 0.08f, 0.08f), 0.04f);
        Meteor meteor = ring.AddComponent<Meteor>();
        meteor.owner = owner;
        GameObject inner = ArenaEvents.Flat("Meteor fill", point, Radius * 2f, new Color(1f, 0.25f, 0.12f), 0.06f);
        inner.transform.SetParent(ring.transform, true);
        meteor.fill = inner.transform;
    }

    // Камень падает с неба наискосок за последние FallTime секунд предупреждения и оставляет огненный след.
    private const float FallTime = 0.8f, FallHeight = 26f;
    private Transform rock;
    private Vector3 fallFrom;
    private float trail;

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Delay);
        if (fill != null)
        {
            float scale = Mathf.Max(0.05f, t) * Radius * 2f / Mathf.Max(0.01f, transform.localScale.x);
            fill.localScale = new Vector3(scale, 1f, scale);
        }
        Vector3 ground = new Vector3(transform.position.x, GroundHeight.At(transform.position), transform.position.z);
        if (age >= Delay - FallTime && age < Delay) AnimateRock(ground, 1f - (Delay - age) / FallTime);
        if (age < Delay) return;
        if (owner != null) owner.Blast(ground, Radius, 60, 15);
        if (rock != null) Destroy(rock.gameObject);
        Destroy(gameObject);
    }

    private void AnimateRock(Vector3 ground, float k)
    {
        if (rock == null)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Meteor rock";
            Destroy(body.GetComponent<Collider>());
            ArenaEvents.Paint(body, new Color(0.32f, 0.16f, 0.1f));
            body.transform.localScale = new Vector3(1.3f, 1.1f, 1.2f);
            rock = body.transform;
            Vector2 side = Random.insideUnitCircle.normalized * 9f;
            fallFrom = ground + new Vector3(side.x, FallHeight, side.y);
        }
        float eased = k * k;
        rock.position = Vector3.Lerp(fallFrom, ground + Vector3.up * 0.6f, eased);
        rock.Rotate(new Vector3(1f, 0.4f, 0.2f), 600f * Time.deltaTime, Space.Self);
        trail -= Time.deltaTime;
        if (trail > 0f) return;
        trail = 0.035f;
        EnemyFx.Puff(rock.position + Random.insideUnitSphere * 0.3f, new Color(1f, 0.55f, 0.15f), 0.9f, 0.35f, Vector3.up * 0.5f);
        EnemyFx.Puff(rock.position + Random.insideUnitSphere * 0.4f, new Color(0.35f, 0.3f, 0.3f), 1.1f, 0.6f, Vector3.up * 1f);
    }

    private void OnDestroy()
    {
        if (rock != null) Destroy(rock.gameObject);
    }
}
