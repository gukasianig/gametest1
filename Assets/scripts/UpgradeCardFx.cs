using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Карточка улучшения (19.11.2026): короткое появление (подъём на 18 px и проявление, по очереди), эффекты по редкости
// (обычная — ровное свечение, редкая — бегущий блик, особая/эволюция — пульс рамки и искры), «живая» иконка
// и уход при выборе (Leave: выбранная вспыхивает и улетает к уведомлению, остальные гаснут вниз).
// Всё на unscaled time: окно выбора стоит на паузе.
public class UpgradeCardFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
{
    public const float DealStep = 0.06f, DealTime = 0.2f;
    private const int SparkCount = 7;
    // Копия карточки для анимации ухода не должна строить свои эффекты заново.
    private static bool cloning;

    private Outline border;
    private Shadow shadow;
    private Image glow, sheen, iconGlow;
    private CanvasGroup group;
    private RectTransform rect, sheenRect;
    private readonly RectTransform[] sparks = new RectTransform[SparkCount];
    private readonly Image[] sparkImages = new Image[SparkCount];
    private readonly float[] sparkAge = new float[SparkCount];
    private readonly float[] sparkLife = new float[SparkCount];
    private Color tint = GameUi.Accent;
    private UpgradeRarity rarity;
    private bool golden;
    private float age, emphasis, delay, phase;
    private bool hovered, selected, pressed;
    private Vector2 basePosition;
    private Vector3 iconBase;
    private bool iconBaseKnown;
    public Transform icon;

    private void Awake()
    {
        if (cloning) return;
        rect = GetComponent<RectTransform>();
        basePosition = rect.anchoredPosition;
        UiArtwork.Round(GetComponent<Image>());
        gameObject.AddComponent<RectMask2D>();
        border = gameObject.AddComponent<Outline>();
        border.effectDistance = new Vector2(2f, -2f);
        shadow = gameObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(0f, -7f);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        // Мягкая подсветка цвета редкости под текстом: заметнее при наведении.
        glow = Layer("Rarity Glow", true);
        UiArtwork.Round(glow);
        // Бегущий блик редких и особых карточек.
        GameObject sheenObject = new GameObject("Sheen", typeof(RectTransform), typeof(Image));
        sheenObject.transform.SetParent(transform, false);
        sheenRect = sheenObject.GetComponent<RectTransform>();
        sheenRect.anchorMin = new Vector2(0f, -0.4f);
        sheenRect.anchorMax = new Vector2(0f, 1.4f);
        sheenRect.sizeDelta = new Vector2(46f, 0f);
        sheenRect.localRotation = Quaternion.Euler(0f, 0f, -22f);
        sheen = sheenObject.GetComponent<Image>();
        sheen.raycastTarget = false;
        sheen.color = Color.clear;
        for (int i = 0; i < SparkCount; i++)
        {
            GameObject spark = new GameObject("Spark " + i, typeof(RectTransform), typeof(Image));
            spark.transform.SetParent(transform, false);
            sparks[i] = spark.GetComponent<RectTransform>();
            sparks[i].anchorMin = sparks[i].anchorMax = new Vector2(0.5f, 0f);
            sparks[i].sizeDelta = new Vector2(7f, 7f);
            sparks[i].localRotation = Quaternion.Euler(0f, 0f, 45f);
            sparkImages[i] = spark.GetComponent<Image>();
            sparkImages[i].raycastTarget = false;
            sparkImages[i].color = Color.clear;
            sparkAge[i] = sparkLife[i] = 1f;
        }
        group = gameObject.AddComponent<CanvasGroup>();
    }

    private Image Layer(string name, bool first)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform), typeof(Image));
        layer.transform.SetParent(transform, false);
        if (first) layer.transform.SetAsFirstSibling();
        RectTransform layerRect = layer.GetComponent<RectTransform>();
        layerRect.anchorMin = Vector2.zero;
        layerRect.anchorMax = Vector2.one;
        layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;
        Image image = layer.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    // Режим запрета (D06, 21.11.2026): рамка и свечение карточки красно-янтарные, пока игрок выбирает, что убрать.
    public bool banishHighlight;
    private static readonly Color BanishTint = new Color(1f, 0.38f, 0.12f);

    public void Present(Color color, int index) { Present(color, index, UpgradeRarity.Common, false); }

    public void Present(Color color, int index, UpgradeRarity cardRarity, bool evolution)
    {
        tint = color;
        rarity = cardRarity;
        golden = evolution;
        if (golden) tint = new Color(1f, 0.78f, 0.25f);
        delay = index * DealStep;
        age = -delay;
        phase = index * 1.7f;
        hovered = selected = pressed = false;
        emphasis = 0f;
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; }
        for (int i = 0; i < SparkCount; i++) { sparkAge[i] = sparkLife[i] = 1f; if (sparkImages[i] != null) sparkImages[i].color = Color.clear; }
        EnsureIconGlow();
    }

    // Подсветка под иконкой (внутри значка карточки), пульсирует и ярче при наведении.
    private void EnsureIconGlow()
    {
        if (iconGlow != null || icon == null || icon.parent == null) return;
        GameObject layer = new GameObject("Icon Glow", typeof(RectTransform), typeof(Image));
        layer.transform.SetParent(icon.parent, false);
        layer.transform.SetSiblingIndex(icon.GetSiblingIndex());
        RectTransform layerRect = layer.GetComponent<RectTransform>();
        layerRect.anchorMin = new Vector2(0.12f, 0.12f);
        layerRect.anchorMax = new Vector2(0.88f, 0.88f);
        layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;
        iconGlow = layer.GetComponent<Image>();
        iconGlow.raycastTarget = false;
        UiArtwork.Round(iconGlow);
    }

    private void OnEnable() { if (!cloning) age = -delay; }

    private void Update()
    {
        if (rect == null) return;
        float dt = Time.unscaledDeltaTime;
        age += dt;
        // Раздача: подъём снизу с лёгким перелётом и переворот (scale.x = |cos|), рубашка до середины.
        float k = Mathf.Clamp01(age / DealTime);
        float ease = 1f - Mathf.Pow(1f - k, 3f);
        group.alpha = k;
        bool solid = k >= 1f;
        border.enabled = shadow.enabled = solid;
        group.blocksRaycasts = age > 0.15f;
        rect.anchoredPosition = basePosition + new Vector2(0f, -18f * (1f - ease));
        emphasis = Mathf.Lerp(emphasis, hovered || selected ? 1f : 0f, 1f - Mathf.Exp(-18f * dt));
        float size = Mathf.Lerp(0.97f, 1f, ease) + emphasis * 0.018f - (pressed ? 0.012f : 0f);
        rect.localScale = Vector3.one * size;
        if (age < 0f) return;

        bool fancy = golden || rarity == UpgradeRarity.Special;
        float breathe = 0.5f + 0.5f * Mathf.Sin(age * 2.4f + phase);
        float pulse = fancy ? 0.5f + 0.5f * Mathf.Sin(age * 5f + phase) : 0f;
        Color edge = banishHighlight ? BanishTint : tint;
        float warn = banishHighlight ? 0.5f + 0.5f * Mathf.Sin(age * 7f) : 0f;
        border.effectColor = new Color(edge.r, edge.g, edge.b, banishHighlight ? 0.75f + 0.25f * warn : 0.4f + emphasis * 0.5f + pulse * 0.35f);
        border.effectDistance = banishHighlight ? new Vector2(3f, -3f) : fancy ? new Vector2(2f + pulse * 1.5f, -2f - pulse * 1.5f) : new Vector2(2f, -2f);
        glow.color = new Color(edge.r, edge.g, edge.b, banishHighlight ? 0.12f + 0.1f * warn + emphasis * 0.14f : 0.06f + breathe * 0.03f + emphasis * 0.14f + pulse * 0.04f);
        UpdateSheen(fancy);
        if (fancy) UpdateSparks(dt);
        UpdateIcon(breathe);
    }

    private void UpdateSheen(bool fancy)
    {
        if (rarity == UpgradeRarity.Common && !golden) { sheen.color = Color.clear; return; }
        float period = fancy ? 1.9f : 2.7f;
        float t = Mathf.Repeat(age - 0.35f + phase * 0.2f, period) / 0.75f;
        if (age < 0.35f || t > 1f) { sheen.color = Color.clear; return; }
        sheenRect.anchorMin = new Vector2(Mathf.Lerp(-0.1f, 1.1f, t), -0.4f);
        sheenRect.anchorMax = new Vector2(sheenRect.anchorMin.x, 1.4f);
        float fade = Mathf.Sin(t * Mathf.PI);
        Color light = Color.Lerp(Color.white, tint, 0.35f);
        sheen.color = new Color(light.r, light.g, light.b, (fancy ? 0.2f : 0.13f) * fade);
    }

    private void UpdateSparks(float dt)
    {
        for (int i = 0; i < SparkCount; i++)
        {
            sparkAge[i] += dt;
            if (sparkAge[i] >= sparkLife[i])
            {
                if (Random.value > 0.04f) continue;   // искры появляются по одной, а не все разом
                sparkAge[i] = 0f;
                sparkLife[i] = Random.Range(0.7f, 1.3f);
                sparks[i].anchorMin = sparks[i].anchorMax = new Vector2(Random.Range(0.04f, 0.96f), 0f);
                sparks[i].sizeDelta = Vector2.one * Random.Range(4f, 8f);
            }
            float u = sparkAge[i] / sparkLife[i];
            sparks[i].anchoredPosition = new Vector2(Mathf.Sin(u * 6f + i) * 5f, Mathf.Lerp(4f, rect.rect.height * 0.75f, u));
            Color color = Color.Lerp(tint, Color.white, 0.3f);
            sparkImages[i].color = new Color(color.r, color.g, color.b, Mathf.Sin(u * Mathf.PI) * 0.85f);
        }
    }

    // «Живая» иконка: покачивание и дыхание, при наведении приподнимается и светится.
    private void UpdateIcon(float breathe)
    {
        if (icon == null) return;
        if (!iconBaseKnown) { iconBase = icon.localPosition; iconBaseKnown = true; }
        float bob = Mathf.Sin(age * 2.2f + phase) * 2.5f;
        icon.localPosition = iconBase + new Vector3(0f, bob + emphasis * 5f, 0f);
        icon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 1.6f + phase) * 2.5f * (1f - emphasis));
        icon.localScale = Vector3.one * (1f + 0.025f * breathe + emphasis * 0.09f);
        if (iconGlow != null) iconGlow.color = new Color(tint.r, tint.g, tint.b, 0.1f + 0.08f * breathe + emphasis * 0.3f);
    }

    // Уход карточки при выборе: копия остаётся поверх окна и доигрывает анимацию, само окно сразу закрывается
    // или раздаёт следующие карточки. target — центр уведомления о взятой карточке.
    public void Leave(bool chosen, RectTransform layer, RectTransform target)
    {
        if (layer == null || !gameObject.activeInHierarchy) return;
        cloning = true;
        GameObject copy;
        try { copy = Instantiate(gameObject, layer); }
        finally { cloning = false; }
        copy.name = gameObject.name + (chosen ? " (chosen)" : " (left)");
        RectTransform copyRect = (RectTransform)copy.transform;
        // Копия встаёт точно на место карточки: якоря — доли слоя (как у самой карточки), поэтому размер и перенос
        // текста не зависят от масштаба интерфейса; дальше она анимируется в координатах слоя.
        Rect area = layer.rect;
        Vector2 min = (Vector2)layer.InverseTransformPoint(rect.TransformPoint(rect.rect.min)) - area.min;
        Vector2 max = (Vector2)layer.InverseTransformPoint(rect.TransformPoint(rect.rect.max)) - area.min;
        copyRect.pivot = new Vector2(0.5f, 0.5f);
        copyRect.anchorMin = new Vector2(min.x / area.width, min.y / area.height);
        copyRect.anchorMax = new Vector2(max.x / area.width, max.y / area.height);
        copyRect.offsetMin = copyRect.offsetMax = Vector2.zero;
        copyRect.localScale = Vector3.one;
        copyRect.localRotation = Quaternion.identity;
        foreach (UpgradeCardFx fx in copy.GetComponents<UpgradeCardFx>()) Destroy(fx);
        foreach (Selectable control in copy.GetComponentsInChildren<Selectable>()) control.enabled = false;
        CanvasGroup copyGroup = copy.GetComponent<CanvasGroup>();
        if (copyGroup != null) { copyGroup.blocksRaycasts = false; copyGroup.alpha = 1f; }
        Vector3 goal = target != null ? layer.InverseTransformPoint(target.TransformPoint(target.rect.center)) : copyRect.localPosition + Vector3.up * 200f;
        UpgradeCardGhost ghost = copy.AddComponent<UpgradeCardGhost>();
        ghost.Begin(chosen, goal, tint);
        copy.transform.SetAsLastSibling();
    }

    public void OnPointerEnter(PointerEventData data) { hovered = true; }
    public void OnPointerExit(PointerEventData data) { hovered = pressed = false; }
    public void OnSelect(BaseEventData data) { selected = true; }
    public void OnDeselect(BaseEventData data) { selected = pressed = false; }
    public void OnPointerDown(PointerEventData data) { pressed = true; }
    public void OnPointerUp(PointerEventData data) { pressed = false; }
}

// Доигрывает уход выбранной/отклонённой карточки и удаляет копию.
public class UpgradeCardGhost : MonoBehaviour
{
    private bool chosen;
    private Vector3 start, goal;
    private float age;
    private CanvasGroup group;
    private Image flash;

    public void Begin(bool isChosen, Vector3 target, Color tint)
    {
        chosen = isChosen;
        start = transform.localPosition;
        goal = target;
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        // У полупрозрачной копии обводки (полные копии фигуры) просвечивают ярким цветом — гасим их.
        if (!chosen) { HideOutlines(); return; }
        GameObject layer = new GameObject("Choice Flash", typeof(RectTransform), typeof(Image));
        layer.transform.SetParent(transform, false);
        RectTransform layerRect = layer.GetComponent<RectTransform>();
        layerRect.anchorMin = Vector2.zero;
        layerRect.anchorMax = Vector2.one;
        layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;
        flash = layer.GetComponent<Image>();
        flash.raycastTarget = false;
        UiArtwork.Round(flash);
        Color light = Color.Lerp(Color.white, tint, 0.3f);
        flash.color = new Color(light.r, light.g, light.b, 0.75f);
        Outline ring = GetComponent<Outline>();
        if (ring != null) { ring.effectColor = light; ring.effectDistance = new Vector2(4f, -4f); }
    }

    private bool outlinesHidden;

    private void HideOutlines()
    {
        if (outlinesHidden) return;
        outlinesHidden = true;
        foreach (Shadow effect in GetComponentsInChildren<Shadow>()) effect.enabled = false;
    }

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        if (chosen)
        {
            // Вспышка и короткий «вдох», затем полёт к уведомлению с уменьшением.
            float pop = Mathf.Clamp01(age / 0.12f);
            float fly = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 0.1f) / 0.38f));
            float scale = Mathf.Lerp(1f, 1.05f, pop) * Mathf.Lerp(1f, 0.28f, fly);
            transform.localScale = Vector3.one * scale;
            transform.localPosition = Vector3.Lerp(start, goal, fly) + Vector3.up * Mathf.Sin(fly * Mathf.PI) * 60f;
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(fly * Mathf.PI) * -6f);
            if (flash != null) flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, 0.75f * (1f - Mathf.Clamp01(age / 0.3f)));
            group.alpha = 1f - Mathf.Clamp01((fly - 0.65f) / 0.35f);
            if (group.alpha < 1f) HideOutlines();
            if (age > 0.5f) Destroy(gameObject);
        }
        else
        {
            float t = Mathf.Clamp01(age / 0.24f);
            transform.localPosition = start + Vector3.down * (70f * t * t);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, t);
            group.alpha = 1f - t;
            if (age > 0.25f) Destroy(gameObject);
        }
    }
}
