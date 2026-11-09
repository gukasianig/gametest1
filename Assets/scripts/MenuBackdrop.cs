using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Фон главного меню: ночная арена из Blender (tools/blender/make_menu_background.py) на весь экран
// без искажений (cover), затемнение слева под надписи. Живой фон: медленное «дыхание» и параллакс за мышью,
// дрейф тумана, искры над воротами, пульс света ворот, мерцающие звёзды и редкие падающие звёзды.
// На остальных экранах меню (оружие, условия, настройки) фон приглушается.
public class MenuBackdrop : MonoBehaviour
{
    private Image glow, dim, streak;
    private RectTransform home, motion, frame;
    private Image[] stars;
    private float[] starPhase;
    private RawImage[] fog;
    private float[] fogSpeed;
    private Vector2 gate, look;

    private struct Ember
    {
        public Vector2 at, velocity;
        public float life, span, size, phase;
    }
    private Ember[] embers;
    private Image[] emberImages;

    private Vector2 streakFrom, streakVelocity;
    private float streakLife = -1f, streakWait = 4f;
    private const float StreakTime = 0.9f;

    [System.Serializable]
    private class Layout { public float[] gate; }

    public static MenuBackdrop Build(RectTransform background, RectTransform home)
    {
        Texture2D texture = Resources.Load<Texture2D>("UI/Menu/menu_background");
        if (texture == null) return null;
        MenuBackdrop backdrop = background.gameObject.AddComponent<MenuBackdrop>();
        backdrop.home = home;

        // Слой движения (параллакс, «дыхание»): картинка внутри него, чтобы AspectRatioFitter не спорил со сдвигом.
        RectTransform motion = new GameObject("Menu Motion", typeof(RectTransform)).GetComponent<RectTransform>();
        motion.SetParent(background, false);
        motion.anchorMin = Vector2.zero;
        motion.anchorMax = Vector2.one;
        motion.offsetMin = motion.offsetMax = Vector2.zero;
        motion.SetAsFirstSibling();
        backdrop.motion = motion;

        // Картинка во весь экран с сохранением пропорций (лишнее обрезается по краям).
        RectTransform frame = GameUi.Box(motion, "Menu Art", Vector2.zero, Vector2.one, Color.white);
        backdrop.frame = frame;
        Image art = frame.GetComponent<Image>();
        art.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        AspectRatioFitter fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = texture.width / (float)texture.height;

        // Два слоя тумана внизу, плывут в разные стороны (под тенями, чтобы не мешать надписям).
        Texture2D fogTexture = FogTexture();
        backdrop.fog = new[]
        {
            Fog(frame, fogTexture, new Vector2(0f, 0.06f), new Vector2(1f, 0.36f), new Color(0.55f, 0.68f, 0.8f, 0.16f), 1.6f, 0.2f),
            Fog(frame, fogTexture, new Vector2(0f, 0.16f), new Vector2(1f, 0.44f), new Color(0.42f, 0.56f, 0.7f, 0.11f), 2.3f, 0.65f)
        };
        backdrop.fogSpeed = new[] { 0.007f, -0.0045f };

        // Тень слева (под заголовок и кнопки) и снизу.
        Image shade = GameUi.Box(frame, "Left Shade", Vector2.zero, new Vector2(0.55f, 1f), Color.white).GetComponent<Image>();
        shade.sprite = Gradient(true);
        shade.color = new Color(0.01f, 0.02f, 0.03f, 0.85f);
        Image bottom = GameUi.Box(frame, "Bottom Shade", Vector2.zero, new Vector2(1f, 0.25f), Color.white).GetComponent<Image>();
        bottom.sprite = Gradient(false);
        bottom.color = new Color(0.01f, 0.02f, 0.03f, 0.7f);

        // Свет ворот: координаты из json рядом с картинкой.
        Vector2 gate = new Vector2(0.66f, 0.4f);
        TextAsset json = Resources.Load<TextAsset>("UI/Menu/menu_background");
        if (json != null)
        {
            Layout layout = JsonUtility.FromJson<Layout>(json.text);
            if (layout != null && layout.gate != null && layout.gate.Length >= 2) gate = new Vector2(layout.gate[0], layout.gate[1]);
        }
        backdrop.gate = gate;
        backdrop.glow = GameUi.Box(frame, "Gate Glow", gate - new Vector2(0.09f, 0.16f), gate + new Vector2(0.09f, 0.16f), Color.white).GetComponent<Image>();
        backdrop.glow.sprite = Radial();

        // Несколько мерцающих звёзд поверх неба.
        System.Random random = new System.Random(5);
        backdrop.stars = new Image[18];
        backdrop.starPhase = new float[18];
        for (int i = 0; i < backdrop.stars.Length; i++)
        {
            Vector2 at = new Vector2(0.05f + 0.9f * (float)random.NextDouble(), 0.55f + 0.42f * (float)random.NextDouble());
            float size = 0.0012f + 0.0012f * (float)random.NextDouble();
            Image star = GameUi.Box(frame, "Star", at - new Vector2(size, size * 1.78f), at + new Vector2(size, size * 1.78f), Color.white).GetComponent<Image>();
            star.sprite = backdrop.glow.sprite;
            backdrop.stars[i] = star;
            backdrop.starPhase[i] = (float)random.NextDouble() * 10f;
        }

        // Падающая звезда: яркая голова у точки опоры, хвост тянется назад.
        backdrop.streak = Point(frame, "Shooting Star", Gradient(true), new Vector2(0f, 0.5f));
        backdrop.streak.enabled = false;

        // Искры и бирюзовые огоньки поднимаются над воротами и ареной.
        backdrop.embers = new Ember[30];
        backdrop.emberImages = new Image[backdrop.embers.Length];
        for (int i = 0; i < backdrop.embers.Length; i++)
        {
            backdrop.emberImages[i] = Point(frame, "Ember", Radial(), new Vector2(0.5f, 0.5f));
            backdrop.Spawn(i, Random.value);
        }

        backdrop.dim = GameUi.Box(background, "Dim", Vector2.zero, Vector2.one, new Color(0.01f, 0.02f, 0.03f, 0.72f)).GetComponent<Image>();
        backdrop.dim.transform.SetSiblingIndex(1);
        return backdrop;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

        // Медленное «дыхание» и параллакс за мышью. Запас масштаба (≥1,038) закрывает сдвиг до ~16 px.
        Vector2 mouse = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width), Input.mousePosition.y / Mathf.Max(1, Screen.height));
        mouse = new Vector2(Mathf.Clamp(mouse.x * 2f - 1f, -1f, 1f), Mathf.Clamp(mouse.y * 2f - 1f, -1f, 1f));
        look = Vector2.Lerp(look, mouse, 1f - Mathf.Exp(-dt * 2.5f));
        if (motion != null)
        {
            motion.localScale = Vector3.one * (1.05f + 0.012f * Mathf.Sin(t * 0.23f));
            motion.anchoredPosition = new Vector2(-look.x * 11f + 5f * Mathf.Sin(t * 0.11f), -look.y * 6f + 3f * Mathf.Sin(t * 0.15f + 1f));
        }

        if (fog != null)
            for (int i = 0; i < fog.Length; i++)
            {
                Rect uv = fog[i].uvRect;
                uv.x = Mathf.Repeat(uv.x + fogSpeed[i] * dt, 1f);
                fog[i].uvRect = uv;
            }

        if (glow != null) glow.color = new Color(1f, 0.6f, 0.25f, 0.28f + 0.12f * Mathf.Sin(t * 1.7f) + 0.05f * Mathf.Sin(t * 5.3f));
        if (stars != null)
            for (int i = 0; i < stars.Length; i++)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * (1.2f + i % 5 * 0.4f) + starPhase[i]);
                stars[i].color = new Color(0.8f, 0.9f, 1f, 0.1f + 0.6f * k * k);
            }

        UpdateEmbers(t, dt);
        UpdateStreak(dt);
        if (dim != null && home != null) dim.enabled = !home.gameObject.activeSelf;
    }

    private void Spawn(int i, float age)
    {
        Ember e = new Ember
        {
            at = new Vector2(gate.x + Random.Range(-0.24f, 0.24f), Random.Range(0.16f, gate.y + 0.04f)),
            velocity = new Vector2(Random.Range(-0.006f, 0.006f), Random.Range(0.014f, 0.042f)),
            span = Random.Range(3f, 7f),
            size = Random.Range(2f, 4.5f),
            phase = Random.Range(0f, 10f)
        };
        e.life = e.span * age;
        e.at += e.velocity * e.life;
        embers[i] = e;
        // Каждый четвёртый — холодный бирюзовый огонёк, остальные — тёплые искры.
        emberImages[i].color = i % 4 == 0 ? new Color(0.45f, 0.95f, 1f, 0f) : new Color(1f, 0.58f, 0.22f, 0f);
    }

    private void UpdateEmbers(float t, float dt)
    {
        if (embers == null) return;
        for (int i = 0; i < embers.Length; i++)
        {
            Ember e = embers[i];
            e.life += dt;
            if (e.life >= e.span) { Spawn(i, 0f); e = embers[i]; }
            e.at += new Vector2(e.velocity.x + 0.005f * Mathf.Sin(t * 1.3f + e.phase), e.velocity.y) * dt;
            embers[i] = e;
            Image image = emberImages[i];
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = e.at;
            rect.sizeDelta = new Vector2(e.size, e.size);
            Color c = image.color;
            c.a = 0.75f * Mathf.Sin(Mathf.PI * e.life / e.span) * (0.7f + 0.3f * Mathf.Sin(t * 9f + e.phase));
            image.color = c;
        }
    }

    private void UpdateStreak(float dt)
    {
        if (streak == null) return;
        if (streakLife < 0f)
        {
            streakWait -= dt;
            if (streakWait > 0f) return;
            // Старт в верхней части неба, полёт вниз влево или вправо.
            float side = Random.value < 0.5f ? -1f : 1f;
            streakFrom = new Vector2(Random.Range(0.4f, 0.85f), Random.Range(0.8f, 0.95f));
            streakVelocity = new Vector2(side * Random.Range(0.18f, 0.28f), -Random.Range(0.08f, 0.14f));
            streakLife = 0f;
            streak.enabled = true;
            Vector2 size = frame != null ? frame.rect.size : new Vector2(1280f, 720f);
            Vector2 pixels = new Vector2(streakVelocity.x * size.x, streakVelocity.y * size.y);
            streak.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(-pixels.y, -pixels.x) * Mathf.Rad2Deg);
            streak.rectTransform.sizeDelta = new Vector2(Random.Range(90f, 140f), 2f);
        }
        streakLife += dt;
        float p = streakLife / StreakTime;
        if (p >= 1f)
        {
            streak.enabled = false;
            streakLife = -1f;
            streakWait = Random.Range(7f, 15f);
            return;
        }
        RectTransform rect = streak.rectTransform;
        rect.anchorMin = rect.anchorMax = streakFrom + streakVelocity * streakLife;
        streak.color = new Color(0.85f, 0.93f, 1f, 0.85f * Mathf.Sin(Mathf.PI * p));
    }

    // Точка с размером в пикселях холста (anchorMin = anchorMax = положение в долях кадра).
    private static Image Point(Transform parent, string name, Sprite sprite, Vector2 pivot)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.rectTransform.pivot = pivot;
        image.sprite = sprite;
        image.raycastTarget = false;
        image.color = Color.clear;
        return image;
    }

    private static RawImage Fog(Transform parent, Texture2D texture, Vector2 min, Vector2 max, Color color, float width, float offset)
    {
        RawImage image = new GameObject("Fog", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.texture = texture;
        image.color = color;
        image.uvRect = new Rect(offset, 0f, width, 1f);
        image.raycastTarget = false;
        return image;
    }

    // Клочья тумана: периодический по горизонтали шум (плитка без шва), к верху и низу прозрачнее.
    private static Texture2D FogTexture()
    {
        const int width = 256, height = 64;
        System.Random random = new System.Random(11);
        int[] cells = { 4, 8, 16 };
        float[] weights = { 0.55f, 0.3f, 0.15f };
        float[][,] grids = new float[cells.Length][,];
        for (int o = 0; o < cells.Length; o++)
        {
            grids[o] = new float[cells[o], cells[o] / 2 + 2];
            for (int x = 0; x < cells[o]; x++)
                for (int y = 0; y < cells[o] / 2 + 2; y++)
                    grids[o][x, y] = (float)random.NextDouble();
        }
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)width, v = y / (float)(height - 1);
                float value = 0f;
                for (int o = 0; o < cells.Length; o++)
                    value += weights[o] * Noise(grids[o], cells[o], u * cells[o], v * (cells[o] / 2));
                float density = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.8f, value));
                float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 1.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, density * envelope));
            }
        texture.Apply();
        return texture;
    }

    private static float Noise(float[,] grid, int wrap, float gx, float gy)
    {
        int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy);
        float fx = gx - x0, fy = gy - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int xa = ((x0 % wrap) + wrap) % wrap, xb = (xa + 1) % wrap;
        int ya = Mathf.Clamp(y0, 0, grid.GetLength(1) - 1), yb = Mathf.Clamp(y0 + 1, 0, grid.GetLength(1) - 1);
        float bottom = Mathf.Lerp(grid[xa, ya], grid[xb, ya], fx);
        float top = Mathf.Lerp(grid[xa, yb], grid[xb, yb], fx);
        return Mathf.Lerp(bottom, top, fy);
    }

    // Мягкое круглое пятно (свет, звезда, искра).
    private static Sprite radial;
    public static Sprite Radial()
    {
        if (radial != null) return radial;
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        texture.Apply();
        radial = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return radial;
    }

    // Градиент прозрачности: слева направо (horizontal) или снизу вверх.
    public static Sprite Gradient(bool horizontal)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(horizontal ? size : 1, horizontal ? 1 : size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int i = 0; i < size; i++)
        {
            float a = 1f - i / (size - 1f);
            Color c = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            if (horizontal) texture.SetPixel(i, 0, c); else texture.SetPixel(0, i, c);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    // Мягкая вертикальная полоса (блик на кнопке): прозрачная по краям, яркая в середине.
    private static Sprite band;
    public static Sprite Band()
    {
        if (band != null) return band;
        const int size = 32;
        Texture2D texture = new Texture2D(size, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int i = 0; i < size; i++)
        {
            float a = Mathf.Sin(Mathf.PI * (i + 0.5f) / size);
            texture.SetPixel(i, 0, new Color(1f, 1f, 1f, a * a));
        }
        texture.Apply();
        band = Sprite.Create(texture, new Rect(0, 0, size, 1), new Vector2(0.5f, 0.5f));
        return band;
    }
}

// Кнопка главного меню: тёмная плашка с цветной полосой слева. При наведении подпись сдвигается и светлеет,
// плашка подсвечивается цветом полосы, по ней пробегает блик; при нажатии кнопка «проседает».
// pulse — мягкое дыхание свечения и периодический блик (кнопка «Старт»).
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image stripe;
    public Text label;
    public Color accent = GameUi.Accent;
    public bool pulse;
    private Image plate, glow, shine;
    private Color plateColor;
    private bool over, down, ready;
    private float k, press, shineTime = -1f, nextShine = 2.5f;
    private Vector2 basePosition;
    private const float ShineDuration = 0.5f;

    // Создаёт подсветку и блик; вызывать после того, как заданы stripe, label и accent.
    public void Init(Image plate)
    {
        this.plate = plate;
        plateColor = plate.color;
        gameObject.AddComponent<RectMask2D>();
        glow = GameUi.Box(transform, "Hover Glow", Vector2.zero, Vector2.one, Color.clear).GetComponent<Image>();
        glow.sprite = MenuBackdrop.Gradient(true);
        glow.transform.SetAsFirstSibling();
        shine = GameUi.Box(transform, "Shine", new Vector2(0f, -0.4f), new Vector2(0f, 1.4f), Color.clear).GetComponent<Image>();
        shine.sprite = MenuBackdrop.Band();
        shine.rectTransform.sizeDelta = new Vector2(46f, 0f);
        shine.rectTransform.localEulerAngles = new Vector3(0f, 0f, -18f);
        shine.enabled = false;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) { over = true; shineTime = 0f; }
    public void OnPointerExit(PointerEventData eventData) { over = false; down = false; }
    public void OnPointerDown(PointerEventData eventData) { down = true; }
    public void OnPointerUp(PointerEventData eventData) { down = false; }

    private void OnDisable()
    {
        over = down = false;
        k = press = 0f;
        shineTime = -1f;
        Apply();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        k = Mathf.MoveTowards(k, over ? 1f : 0f, dt * 8f);
        press = Mathf.MoveTowards(press, down ? 1f : 0f, dt * 14f);
        if (pulse && !over)
        {
            nextShine -= dt;
            if (nextShine <= 0f) { shineTime = 0f; nextShine = 3.2f; }
        }
        if (shineTime >= 0f)
        {
            shineTime += dt;
            if (shineTime > ShineDuration) shineTime = -1f;
        }
        Apply();
    }

    private void Apply()
    {
        float breath = pulse ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f) : 0f;
        if (label != null)
        {
            if (!ready) { basePosition = label.rectTransform.anchoredPosition; ready = true; }
            label.rectTransform.anchoredPosition = basePosition + new Vector2(10f * k, 0f);
            label.color = Color.Lerp(new Color(0.86f, 0.9f, 0.94f), Color.white, k);
        }
        if (stripe != null)
        {
            stripe.color = Color.Lerp(accent * new Color(0.75f, 0.75f, 0.75f, 1f), accent, Mathf.Max(k, breath * 0.6f));
            stripe.rectTransform.anchorMax = new Vector2(Mathf.Lerp(0.018f, 0.03f, k), 0.88f);
        }
        if (plate != null) plate.color = Color.Lerp(plateColor, new Color(plateColor.r + accent.r * 0.12f,
            plateColor.g + accent.g * 0.12f, plateColor.b + accent.b * 0.12f, Mathf.Min(1f, plateColor.a + 0.08f)), k);
        if (glow != null) glow.color = new Color(accent.r, accent.g, accent.b, 0.28f * k + 0.1f * breath * (1f - k));
        if (shine != null)
        {
            shine.enabled = shineTime >= 0f;
            if (shine.enabled)
            {
                float p = shineTime / ShineDuration;
                float x = Mathf.Lerp(-0.1f, 1.1f, p * p * (3f - 2f * p));
                shine.rectTransform.anchorMin = new Vector2(x, -0.4f);
                shine.rectTransform.anchorMax = new Vector2(x, 1.4f);
                shine.color = new Color(1f, 1f, 1f, 0.22f * Mathf.Sin(Mathf.PI * p));
            }
        }
        transform.localScale = Vector3.one * (1f + 0.015f * k - 0.04f * press);
    }
}
