using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ощущение от боя (11.11.2026): короткая остановка кадра и вспышка при убийстве элиты, замедленная смерть Стража,
// красная рамка и сердцебиение при низком здоровье, стрелки к элитам и Стражу за краем экрана.
// Создаётся GameSession на отдельном объекте (не на герое: копия героя в кооперативе его не получает).
public class CombatFeel : MonoBehaviour
{
    public static CombatFeel Instance { get; private set; }

    const float LowHealth = 0.35f;     // с этой доли HP появляется красная рамка
    const float Heartbeat = 0.25f;     // с этой — стук сердца
    const int Arrows = 6;

    private Canvas canvas;
    private Image flash, vignette;
    private readonly Text[] arrows = new Text[Arrows];
    private float flashAlpha;
    private Color flashColor = Color.white;
    private float nextBeat, vignetteAlpha;
    private bool stopping;
    private readonly List<Health> enemies = new List<Health>();

    public static CombatFeel Create()
    {
        if (Instance != null) return Instance;
        return new GameObject("Combat Feel").AddComponent<CombatFeel>();
    }

    private void Awake()
    {
        Instance = this;
        canvas = GameUi.Canvas("Combat Feel", 60);
        vignette = GameUi.Box(canvas.transform, "Low HP Vignette", Vector2.zero, Vector2.one, new Color(0.85f, 0.05f, 0.05f, 0f)).GetComponent<Image>();
        vignette.sprite = VignetteSprite();
        vignette.raycastTarget = false;
        flash = GameUi.Box(canvas.transform, "Flash", Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
        flash.raycastTarget = false;
        for (int i = 0; i < Arrows; i++)
        {
            Text arrow = GameUi.Readable(GameUi.Label(canvas.transform, "▲", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 34), 1.5f);
            arrow.rectTransform.offsetMin = arrow.rectTransform.offsetMax = Vector2.zero;
            arrow.rectTransform.sizeDelta = new Vector2(48f, 48f);
            arrow.gameObject.SetActive(false);
            arrows[i] = arrow;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (canvas != null) Destroy(canvas.gameObject);
    }

    private static bool Playing => GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing;

    // ---------------- остановка кадра и вспышка ----------------

    // Убийство элиты: 0,07 с почти стоп-кадра, золотая вспышка, кольцо и тряска.
    public static void EliteKilled(Vector3 position)
    {
        CombatFeel feel = Instance;
        if (feel == null) return;
        feel.Flash(new Color(1f, 0.85f, 0.4f), 0.22f);
        feel.HitStop(0.07f, 0.05f);
        Vector3 ground = new Vector3(position.x, GroundHeight.At(position) + 0.05f, position.z);
        EnemyFx.Ring(ground, 3f, new Color(1f, 0.8f, 0.3f), 0.4f, 0.35f);
        ScreenShake.AddAt(position, 0.3f);
    }

    public void Flash(Color color, float alpha)
    {
        flashColor = color;
        flashAlpha = Mathf.Max(flashAlpha, alpha);
    }

    public void HitStop(float seconds, float scale)
    {
        if (stopping || !Playing || Time.timeScale < 0.99f) return;
        StartCoroutine(Stop(seconds, scale));
    }

    private IEnumerator Stop(float seconds, float scale)
    {
        stopping = true;
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(seconds);
        // Пауза, окно карточек или конец боя за это время сами задали свою скорость — не трогаем.
        if (Mathf.Approximately(Time.timeScale, scale) && Playing) Time.timeScale = 1f;
        stopping = false;
    }

    // Смерть Стража: замедление, серия взрывов, белая вспышка и сильная тряска; затем finish (экран победы).
    public static void BossKilled(Vector3 position, System.Action finish)
    {
        if (Instance == null) { finish(); return; }
        Instance.StartCoroutine(Instance.BossDeath(position, finish));
    }

    private IEnumerator BossDeath(Vector3 position, System.Action finish)
    {
        foreach (PlayerHealth hero in CoopMode.Heroes) if (hero != null) hero.GrantInvulnerability(5f);
        Time.timeScale = 0.3f;
        Vector3 ground = new Vector3(position.x, GroundHeight.At(position), position.z);
        for (int i = 0; i < 5; i++)
        {
            Vector3 at = ground + new Vector3(Random.Range(-1.8f, 1.8f), 0f, Random.Range(-1.8f, 1.8f));
            EnemyFx.Explosion(at, 2.2f + i * 0.4f, 0.8f, true);
            GameAudio.Play(Sfx.Explosion, 0.9f, 0.08f);
            Flash(new Color(1f, 0.75f, 0.45f), 0.25f);
            yield return new WaitForSecondsRealtime(0.22f);
        }
        EnemyFx.Explosion(ground, 6f, 1f, true);
        EnemyFx.Ring(ground + Vector3.up * 0.1f, 9f, new Color(1f, 0.85f, 0.5f), 0.8f, 0.5f);
        EnemyFx.LightFlash(ground + Vector3.up * 3f, new Color(1f, 0.85f, 0.6f), 30f, 8f, 0.8f);
        ScreenShake.AddAt(ground, 1f);
        Flash(Color.white, 0.85f);
        yield return new WaitForSecondsRealtime(0.6f);
        if (Time.timeScale > 0f) Time.timeScale = 1f;
        finish();
    }

    // ---------------- кадр: вспышка, рамка, стрелки ----------------

    private void Update()
    {
        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 2.6f);
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
        bool playing = Playing;
        UpdateVignette(playing);
        UpdateArrows(playing);
    }

    private void UpdateVignette(bool playing)
    {
        float share = 1f;
        foreach (PlayerHealth hero in CoopMode.Heroes)
            if (hero != null && !hero.IsDead && hero.maxHP > 0) share = Mathf.Min(share, (float)hero.CurrentHP / hero.maxHP);
        float target = playing && share < LowHealth ? Mathf.Lerp(0.25f, 0.6f, 1f - share / LowHealth) : 0f;
        // Пульс в такт сердцу: чем меньше HP, тем чаще.
        float rate = Mathf.Lerp(1.1f, 0.6f, Mathf.Clamp01(1f - share / Heartbeat));
        float pulse = target > 0f ? 0.75f + 0.25f * Mathf.Cos((Time.unscaledTime % rate) / rate * Mathf.PI * 2f) : 1f;
        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, target, Time.unscaledDeltaTime * 1.5f);
        vignette.color = new Color(0.85f, 0.05f, 0.05f, vignetteAlpha * pulse);
        if (playing && share < Heartbeat && Time.timeScale > 0f && Time.unscaledTime >= nextBeat)
        {
            nextBeat = Time.unscaledTime + rate;
            GameAudio.Play(Sfx.Heartbeat, 0.8f, 0f);
        }
    }

    private void UpdateArrows(bool playing)
    {
        int used = 0;
        Camera view = Camera.main;
        if (playing && view != null)
        {
            WeaponTargets.Collect(enemies);
            foreach (Health enemy in enemies)
            {
                if (used >= Arrows) break;
                if (enemy == null || enemy.isDead) continue;
                bool boss = enemy.GetComponent<BossController>() != null || enemy.GetComponent<MiniBoss>() != null;
                if (!boss && !enemy.IsElite) continue;
                Vector3 vp = view.WorldToViewportPoint(enemy.transform.position + Vector3.up);
                if (vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f) continue;   // видно на экране
                Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                if (vp.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 1e-6f) continue;
                // Точка на краю экрана по направлению к врагу.
                float k = 0.46f / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
                Vector2 at = new Vector2(0.5f, 0.5f) + dir * k;
                Text arrow = arrows[used++];
                arrow.gameObject.SetActive(true);
                arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = at;
                arrow.rectTransform.anchoredPosition = Vector2.zero;
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                arrow.color = boss ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.82f, 0.3f);
                arrow.fontSize = arrow.resizeTextMaxSize = boss ? 40 : 30;
            }
        }
        for (int i = used; i < Arrows; i++) if (arrows[i].gameObject.activeSelf) arrows[i].gameObject.SetActive(false);
    }

    public int VisibleArrows { get { int n = 0; foreach (Text a in arrows) if (a.gameObject.activeSelf) n++; return n; } }
    public float VignetteAlpha => vignetteAlpha;

    // Прозрачный центр, красные края: радиальный градиент 64×64.
    private static Sprite VignetteSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01((r - 0.55f) / 0.6f), 1.4f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
