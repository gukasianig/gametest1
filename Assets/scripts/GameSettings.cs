using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Пользовательские настройки (звук, графика, интерфейс). Хранятся в PlayerPrefs отдельно от мета-прогресса
// и применяются при запуске игры. Громкость хранит GameAudio (ключи vol_music / vol_sfx).
public static class GameSettings
{
    public static readonly int[] FpsOptions = { 30, 60, 120, 144, -1 };
    public static readonly FullScreenMode[] ScreenModes =
        { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
    public static readonly string[] ScreenModeNames = { "Полный экран (окно)", "Полный экран", "В окне" };

    public static int FpsIndex
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt("set_fps", 1), 0, FpsOptions.Length - 1); }
        set { PlayerPrefs.SetInt("set_fps", value); }
    }

    public static bool VSync
    {
        get { return PlayerPrefs.GetInt("set_vsync", 0) == 1; }
        set { PlayerPrefs.SetInt("set_vsync", value ? 1 : 0); }
    }

    public static int Quality
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt("set_quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1); }
        set { PlayerPrefs.SetInt("set_quality", value); }
    }

    public static int ScreenModeIndex
    {
        get { return Mathf.Clamp(PlayerPrefs.GetInt("set_screen", 0), 0, ScreenModes.Length - 1); }
        set { PlayerPrefs.SetInt("set_screen", value); }
    }

    public static bool DamageNumbers
    {
        get { return PlayerPrefs.GetInt("set_damage_numbers", 1) == 1; }
        set { PlayerPrefs.SetInt("set_damage_numbers", value ? 1 : 0); }
    }

    public static bool ScreenShakeEnabled
    {
        get { return PlayerPrefs.GetInt("set_shake", 1) == 1; }
        set { PlayerPrefs.SetInt("set_shake", value ? 1 : 0); }
    }

    // Список разрешений без повторов (Screen.resolutions повторяет размеры с разной частотой).
    public static List<Vector2Int> Resolutions()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (UnityEngine.Resolution r in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(r.width, r.height);
            if (!list.Contains(size)) list.Add(size);
        }
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        return list;
    }

    public static Vector2Int ScreenSize
    {
        get
        {
            int w = PlayerPrefs.GetInt("set_res_w", 0), h = PlayerPrefs.GetInt("set_res_h", 0);
            if (w <= 0 || h <= 0)
            {
                UnityEngine.Resolution current = Screen.currentResolution;
                return new Vector2Int(current.width, current.height);
            }
            return new Vector2Int(w, h);
        }
        set
        {
            PlayerPrefs.SetInt("set_res_w", value.x);
            PlayerPrefs.SetInt("set_res_h", value.y);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ApplyAll()
    {
        ApplyGraphics();
        // В редакторе не трогаем окно Game: разрешение и режим экрана — только в сборке.
        if (!Application.isEditor) ApplyScreen();
    }

    public static void ApplyGraphics()
    {
        if (PlayerPrefs.HasKey("set_quality")) QualitySettings.SetQualityLevel(Quality, true);
        // Уровень качества может сам включить VSync, поэтому VSync задаётся после него.
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FpsOptions[FpsIndex];
    }

    public static void ApplyScreen()
    {
        if (Application.isEditor) return;
        Vector2Int size = ScreenSize;
        Screen.SetResolution(size.x, size.y, ScreenModes[ScreenModeIndex]);
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }
}

// Экран настроек: одна и та же сборка в главном меню и в паузе забега.
public static class SettingsPanel
{
    public static RectTransform Build(Transform parent, System.Action onBack)
    {
        RectTransform panel = GameUi.Box(parent, "Settings", Vector2.zero, Vector2.one, GameUi.Background, true);
        GameUi.Label(panel, "НАСТРОЙКИ", new Vector2(0.2f, 0.87f), new Vector2(0.8f, 0.96f), 36);

        List<System.Action> refreshers = new List<System.Action>();
        float y = 0.82f;
        const float Step = 0.06f;

        Header(panel, "Звук", ref y);
        Row(panel, "Музыка", ref y, Step, refreshers,
            d => GameAudio.MusicVolume = StepVolume(GameAudio.MusicVolume, d),
            () => Percent(GameAudio.MusicVolume));
        Row(panel, "Эффекты", ref y, Step, refreshers,
            d => { GameAudio.SfxVolume = StepVolume(GameAudio.SfxVolume, d); GameAudio.Play(Sfx.Hit); },
            () => Percent(GameAudio.SfxVolume));

        Header(panel, "Графика", ref y);
        Row(panel, "Качество", ref y, Step, refreshers,
            d => { GameSettings.Quality = Wrap(GameSettings.Quality + d, QualitySettings.names.Length); GameSettings.ApplyGraphics(); },
            () => QualitySettings.names[GameSettings.Quality]);
        Row(panel, "Ограничение FPS", ref y, Step, refreshers,
            d => { GameSettings.FpsIndex = Wrap(GameSettings.FpsIndex + d, GameSettings.FpsOptions.Length); GameSettings.ApplyGraphics(); },
            () =>
            {
                int fps = GameSettings.FpsOptions[GameSettings.FpsIndex];
                string text = fps < 0 ? "Без ограничения" : fps + " FPS";
                return GameSettings.VSync ? text + " (VSync)" : text;
            });
        Row(panel, "Вертикальная синхр.", ref y, Step, refreshers,
            d => { GameSettings.VSync = !GameSettings.VSync; GameSettings.ApplyGraphics(); },
            () => OnOff(GameSettings.VSync));
        Row(panel, "Режим экрана", ref y, Step, refreshers,
            d => { GameSettings.ScreenModeIndex = Wrap(GameSettings.ScreenModeIndex + d, GameSettings.ScreenModes.Length); GameSettings.ApplyScreen(); },
            () => GameSettings.ScreenModeNames[GameSettings.ScreenModeIndex]);
        Row(panel, "Разрешение", ref y, Step, refreshers,
            d =>
            {
                List<Vector2Int> list = GameSettings.Resolutions();
                int index = list.IndexOf(GameSettings.ScreenSize);
                if (index < 0) index = list.Count - 1;
                GameSettings.ScreenSize = list[Wrap(index + d, list.Count)];
                GameSettings.ApplyScreen();
            },
            () => GameSettings.ScreenSize.x + " × " + GameSettings.ScreenSize.y);

        Header(panel, "Интерфейс", ref y);
        Row(panel, "Цифры урона", ref y, Step, refreshers,
            d => GameSettings.DamageNumbers = !GameSettings.DamageNumbers,
            () => OnOff(GameSettings.DamageNumbers));
        Row(panel, "Тряска камеры", ref y, Step, refreshers,
            d => GameSettings.ScreenShakeEnabled = !GameSettings.ScreenShakeEnabled,
            () => OnOff(GameSettings.ScreenShakeEnabled));

        Button back = GameUi.Button(panel, "Назад", new Vector2(0.4f, 0.03f), new Vector2(0.6f, 0.1f), () =>
        {
            GameSettings.Save();
            if (onBack != null) onBack();
        });
        UiArtwork.Round(back.GetComponent<Image>());
        panel.gameObject.SetActive(false);
        return panel;
    }

    private static void Header(RectTransform panel, string title, ref float y)
    {
        Text label = GameUi.Label(panel, title.ToUpperInvariant(), new Vector2(0.22f, y - 0.04f), new Vector2(0.78f, y), 16, GameUi.Accent);
        label.alignment = TextAnchor.MiddleLeft;
        y -= 0.04f;
    }

    private static void Row(RectTransform panel, string title, ref float y, float step, List<System.Action> refreshers,
        System.Action<int> change, System.Func<string> value)
    {
        float top = y, bottom = y - step + 0.01f;
        Text name = GameUi.Label(panel, title, new Vector2(0.22f, bottom), new Vector2(0.5f, top), 20,
            new Color(0.85f, 0.89f, 0.93f));
        name.alignment = TextAnchor.MiddleLeft;
        RectTransform field = GameUi.Box(panel, title + " value", new Vector2(0.56f, bottom), new Vector2(0.72f, top),
            new Color(0.05f, 0.1f, 0.13f));
        Text shown = GameUi.Label(field, value(), Vector2.zero, Vector2.one, 19);
        System.Action refresh = () => { foreach (System.Action r in refreshers) r(); };
        refreshers.Add(() => shown.text = value());
        GameUi.Button(panel, "◄", new Vector2(0.51f, bottom), new Vector2(0.555f, top), () => { change(-1); refresh(); });
        GameUi.Button(panel, "►", new Vector2(0.725f, bottom), new Vector2(0.77f, top), () => { change(1); refresh(); });
        y -= step;
    }

    private static float StepVolume(float volume, int direction)
    {
        return Mathf.Clamp01(Mathf.Round(volume * 10f + direction) / 10f);
    }

    private static int Wrap(int value, int count)
    {
        return ((value % count) + count) % count;
    }

    private static string Percent(float value) { return Mathf.RoundToInt(value * 100f) + "%"; }
    private static string OnOff(bool value) { return value ? "Вкл" : "Выкл"; }
}
