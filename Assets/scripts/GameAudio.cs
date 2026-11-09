using System;
using System.Threading.Tasks;
using UnityEngine;

// Новые значения добавлять только в конец: числа enum используются как индексы.
public enum Sfx { Shot, Rail, Arc, Rocket, Ice, Flame, Throw, Explosion, Hit, Kill, SawCut, Shotgun, Zap, Deploy, AutoShot }

// Зацикленные звуки, которые звучат, пока их «держат» каждый кадр (GameAudio.Hold).
public enum SfxLoop { Saw, Flame }

// Звук игры. По умолчанию всё синтезируется кодом при первом использовании (эффекты 44,1 кГц, по 3 варианта
// на частые звуки; музыка 22 кГц). Любой звук можно заменить файлами: положить .ogg/.wav в
// Assets/Audio/Resources/Sfx/<Имя>/ (например Sfx/Arc/, Sfx/SawLoop/) - все файлы папки становятся вариантами.
// Громкость: PlayerPrefs "vol_music" и "vol_sfx" (0..1).
public class GameAudio : MonoBehaviour
{
    private const int FxRate = 44100;
    private const int MusicRate = 22050;
    private const int Voices = 12;
    private const int Variants = 3;
    private const string RecordedAudio = "RecordedAudio/";

    private class LoopState
    {
        public AudioSource source;
        public int heldFrame = -10;
        public float wantVolume, wantPitch = 1f, volume, pitch = 1f, load = 1f;
    }

    private static GameAudio instance;
    private AudioClip[][] clips;
    private float[] lastPlay;
    private int[] lastVariant;
    private AudioSource[] voices;
    private int nextVoice;
    private LoopState[] loops;
    private AudioSource music;
    private AudioClip menuMusic, gameMusic;
    private AudioClip wantedMusic;
    private Task<float[]> menuTask, gameTask;
    private int musicWanted = -1;   // -1 нет, 0 меню, 1 забег
    private float musicFade;

    public static float MusicVolume { get { return PlayerPrefs.GetFloat("vol_music", 0.5f); } set { PlayerPrefs.SetFloat("vol_music", Mathf.Clamp01(value)); } }
    public static float SfxVolume { get { return PlayerPrefs.GetFloat("vol_sfx", 0.7f); } set { PlayerPrefs.SetFloat("vol_sfx", Mathf.Clamp01(value)); } }

    private static GameAudio Get()
    {
        if (instance != null) return instance;
        var go = new GameObject("Game Audio");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameAudio>();
        instance.Init();
        return instance;
    }

    public static void Play(Sfx sfx, float volume = 1f, float pitchJitter = 0.04f)
    {
        GameAudio a = Get();
        int i = (int)sfx;
        float now = Time.unscaledTime;
        if (now - a.lastPlay[i] < MinGap(sfx)) return;   // не забивать эфир сотнями одинаковых звуков
        a.lastPlay[i] = now;
        AudioClip[] set = a.clips[i];
        // Случайный вариант, но не тот же, что в прошлый раз.
        int v = set.Length <= 1 ? 0 : (a.lastVariant[i] + 1 + UnityEngine.Random.Range(0, set.Length - 1)) % set.Length;
        a.lastVariant[i] = v;
        AudioSource s = a.voices[a.nextVoice];
        a.nextVoice = (a.nextVoice + 1) % Voices;
        s.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
        s.PlayOneShot(set[v], volume * SfxVolume);
    }

    // Держать петлю включённой: вызывать каждый кадр, пока источник звука существует.
    // Несколько источников в кадре: берётся самый громкий и самый высокий.
    public static void Hold(SfxLoop loop, float volume = 1f, float pitch = 1f)
    {
        LoopState s = Get().loops[(int)loop];
        if (s.heldFrame != Time.frameCount)
        {
            s.heldFrame = Time.frameCount;
            s.wantVolume = 0f;
            s.wantPitch = 0f;
        }
        s.wantVolume = Mathf.Max(s.wantVolume, volume);
        s.wantPitch = Mathf.Max(s.wantPitch, pitch);
    }

    // Кратко «нагрузить» петлю: высота проседает до factor и плавно возвращается (пила вгрызается во врага).
    public static void Load(SfxLoop loop, float factor)
    {
        LoopState s = Get().loops[(int)loop];
        s.load = Mathf.Min(s.load, factor);
    }

    public static void SetMusic(bool inGame)
    {
        Get().musicWanted = inGame ? 1 : 0;
    }

    private static float MinGap(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Hit: return 0.07f;
            case Sfx.Kill: return 0.08f;
            case Sfx.Explosion: return 0.07f;
            case Sfx.SawCut: return 0.09f;
            case Sfx.Zap: return 0.06f;
            case Sfx.Arc: return 0.05f;
            default: return 0.03f;
        }
    }

    private void Init()
    {
        int count = Enum.GetValues(typeof(Sfx)).Length;
        clips = new AudioClip[count][];
        lastPlay = new float[count];
        lastVariant = new int[count];
        for (int i = 0; i < count; i++)
        {
            lastPlay[i] = -10f;
            Sfx sfx = (Sfx)i;
            AudioClip recorded = Resources.Load<AudioClip>(RecordedAudio + sfx);
            clips[i] = recorded != null
                ? new[] { recorded }
                : Files(sfx.ToString(), Peak(sfx), MaxSeconds(sfx)) ?? Synth.Set(sfx);
        }
        voices = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
            voices[i].spatialBlend = 0f;
        }
        int loopCount = Enum.GetValues(typeof(SfxLoop)).Length;
        loops = new LoopState[loopCount];
        for (int i = 0; i < loopCount; i++)
        {
            SfxLoop loop = (SfxLoop)i;
            AudioClip[] file = Files(loop + "Loop", 0.6f);
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.clip = file != null ? file[0] : Synth.Loop(loop);
            loops[i] = new LoopState { source = src };
        }
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        music.volume = 0f;
        menuMusic = Resources.Load<AudioClip>(RecordedAudio + "MenuMusic") ?? Files("MenuMusic", 0f)?[0];
        gameMusic = Resources.Load<AudioClip>(RecordedAudio + "GameMusic") ?? Files("GameMusic", 0f)?[0];
        if (menuMusic == null) menuTask = Task.Run(() => Synth.MenuMusicData());
        if (gameMusic == null) gameTask = Task.Run(() => Synth.GameMusicData());
    }

    // Пиковый уровень каждого звука: и синтез, и файлы приводятся к нему, чтобы громкость задавалась здесь
    // и в вызовах Play, а не уровнем записи.
    private static float Peak(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Shot: return 0.7f;
            case Sfx.Shotgun: return 0.9f;
            case Sfx.Rail: return 0.9f;
            case Sfx.Arc: return 0.6f;
            case Sfx.Rocket: return 0.85f;
            case Sfx.Ice: return 0.75f;
            case Sfx.Flame: return 0.7f;
            case Sfx.Throw: return 0.65f;
            case Sfx.Explosion: return 0.95f;
            case Sfx.Hit: return 0.55f;
            case Sfx.Kill: return 0.7f;
            case Sfx.SawCut: return 0.75f;
            case Sfx.Zap: return 0.5f;
            case Sfx.Deploy: return 0.6f;
            default: return 0.7f;
        }
    }

    // Предел длины звуков из файлов: длинный файл не должен звучать секундами после того, как враг уже исчез
    // (смерть врага раньше тянулась 4,7 с). 0 - без ограничения.
    private static float MaxSeconds(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Hit: return 0.2f;
            case Sfx.Kill: return 0.4f;
            case Sfx.Shot: return 0.3f;
            case Sfx.Shotgun: return 0.5f;
            case Sfx.Zap: return 0.25f;
            case Sfx.Deploy: return 0.4f;
            case Sfx.Arc: return 0.3f;
            case Sfx.Flame: return 0.8f;
            default: return 0f;
        }
    }

    // Готовые файлы из Assets/Audio/Resources/Sfx/<name>/ (docs/AUDIO.md). peak > 0 - привести пик к этому уровню
    // (нужен импорт «Decompress On Load», иначе файл играет как есть).
    // maxSeconds > 0 - обрезать более длинные файлы с затуханием 60 мс.
    private static AudioClip[] Files(string name, float peak, float maxSeconds = 0f)
    {
        AudioClip[] found = Resources.LoadAll<AudioClip>("Sfx/" + name);
        if (found == null || found.Length == 0) return null;
        if (peak <= 0f) return found;
        for (int k = 0; k < found.Length; k++)
        {
            AudioClip c = found[k];
            var d = new float[c.samples * c.channels];
            if (c.loadType != AudioClipLoadType.DecompressOnLoad || !c.GetData(d, 0)) continue;
            int frames = c.samples;
            if (maxSeconds > 0f && frames > maxSeconds * c.frequency)
            {
                frames = Mathf.RoundToInt(maxSeconds * c.frequency);
                int fade = Mathf.Max(1, Mathf.Min(frames, Mathf.RoundToInt(0.06f * c.frequency)));
                Array.Resize(ref d, frames * c.channels);
                for (int f = frames - fade; f < frames; f++)
                    for (int ch = 0; ch < c.channels; ch++) d[f * c.channels + ch] *= (frames - 1 - f) / (float)fade;
            }
            float max = 0.0001f;
            foreach (float v in d) max = Mathf.Max(max, Mathf.Abs(v));
            float gain = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= gain;
            AudioClip scaled = AudioClip.Create(c.name, frames, c.channels, c.frequency, false);
            scaled.SetData(d, 0);
            found[k] = scaled;
        }
        return found;
    }

    // Музыка считается ~1-2 с, поэтому в фоновом потоке; клип создаётся на главном, когда данные готовы.
    private AudioClip ReadyMusic(ref AudioClip clip, Task<float[]> task, string name)
    {
        if (clip == null && task != null && task.IsCompleted)
        {
            if (task.Status == TaskStatus.RanToCompletion) clip = Synth.Limit(name, task.Result);
            else Debug.LogError("GameAudio: синтез музыки " + name + " не удался: " + task.Exception);
        }
        return clip;
    }

    private void Update()
    {
        UpdateLoops();
        if (musicWanted < 0) return;
        wantedMusic = musicWanted == 1 ? ReadyMusic(ref gameMusic, gameTask, "music_game") : ReadyMusic(ref menuMusic, menuTask, "music_menu");
        if (wantedMusic == null)
        {
            // Нужная петля ещё считается: прежнюю гасим.
            musicFade = Mathf.MoveTowards(musicFade, 0f, Time.unscaledDeltaTime * 0.8f);
            music.volume = musicFade;
            return;
        }
        float target = MusicVolume * 0.55f;
        float step = Time.unscaledDeltaTime * 0.8f;
        if (music.clip != wantedMusic)
        {
            // Старую петлю гасим, потом включаем новую.
            musicFade = Mathf.MoveTowards(musicFade, 0f, step);
            music.volume = musicFade;
            if (musicFade <= 0.001f)
            {
                music.clip = wantedMusic;
                music.Play();
            }
        }
        else
        {
            if (!music.isPlaying) music.Play();
            musicFade = Mathf.MoveTowards(musicFade, target, step);
            music.volume = musicFade;
        }
    }

    private void UpdateLoops()
    {
        float dt = Time.unscaledDeltaTime;
        foreach (LoopState s in loops)
        {
            // Держали в этом или прошлом кадре (порядок Update между объектами не задан).
            bool held = s.heldFrame >= Time.frameCount - 1;
            float target = held ? s.wantVolume * SfxVolume : 0f;
            s.volume = Mathf.MoveTowards(s.volume, target, dt * (held ? 6f : 3f));
            s.load = Mathf.MoveTowards(s.load, 1f, dt * 1.5f);   // ~0,13 с на восстановление после укуса
            if (held) s.pitch = Mathf.Lerp(s.pitch, s.wantPitch, 1f - Mathf.Exp(-dt * 8f));
            s.source.volume = s.volume;
            s.source.pitch = Mathf.Clamp(s.pitch * s.load, 0.3f, 3f);
            if (s.volume > 0.001f)
            {
                if (!s.source.isPlaying)
                {
                    s.source.time = UnityEngine.Random.Range(0f, s.source.clip.length * 0.9f);
                    s.source.Play();
                }
            }
            else if (s.source.isPlaying) s.source.Stop();
        }
    }

    // ---------------------------------------------------------------- синтез
    private static class Synth
    {
        private const int R = FxRate;
        private const float Tau = 2f * Mathf.PI;
        private static System.Random rng = new System.Random(12345);

        private static float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }
        private static float Rand() { return (float)rng.NextDouble(); }
        private static float Sat(float x) { return (float)Math.Tanh(x); }
        private static float Frac(float x) { return x - Mathf.Floor(x); }

        // Фильтр переменного состояния (TPT SVF): устойчив на любых частотах, даёт ФНЧ/полосу/ФВЧ одновременно.
        private struct Svf
        {
            private float ic1, ic2;
            public float low, band, high;

            public float Run(float x, float cutoff, float q, int rate = R)
            {
                float g = Mathf.Tan(Mathf.PI * Mathf.Clamp(cutoff, 20f, rate * 0.45f) / rate);
                float k = 1f / Mathf.Max(0.05f, q);
                float a1 = 1f / (1f + g * (g + k));
                float a2 = g * a1;
                float a3 = g * a2;
                float v3 = x - ic2;
                float v1 = a1 * ic1 + a2 * v3;
                float v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2f * v1 - ic1;
                ic2 = 2f * v2 - ic2;
                low = v2;
                band = v1;
                high = x - k * v1 - v2;
                return low;
            }
        }

        private static AudioClip Make(string name, float[] data, float peak, int rate = R)
        {
            float max = 0.0001f;
            foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
            float k = peak / max;
            int fade = Mathf.Min(data.Length / 4, rate / 120);   // ~8 мс к нулю: без щелчка в конце
            for (int i = 0; i < data.Length; i++)
            {
                float f = Mathf.Clamp01((data.Length - 1 - i) / (float)fade);
                data[i] *= k * f;
            }
            return Clip(name, data, rate);
        }

        private static AudioClip Clip(string name, float[] data, int rate)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Ранние отражения небольшого помещения: звук не «сухой», но и не тянется.
        private static void Room(float[] d, float amount)
        {
            float[] taps = { 0.019f, 0.031f, 0.053f, 0.079f };
            float[] gains = { 0.32f, 0.22f, 0.14f, 0.08f };
            var dry = (float[])d.Clone();
            for (int k = 0; k < taps.Length; k++)
            {
                int off = Mathf.RoundToInt(taps[k] * R);
                float lp = 0f;
                for (int i = off; i < d.Length; i++)
                {
                    lp += (dry[i - off] - lp) * 0.3f;   // отражения глуше прямого звука
                    d[i] += lp * gains[k] * amount;
                }
            }
        }

        private static float[] Buffer(float seconds) { return new float[Mathf.RoundToInt(seconds * R)]; }

        public static AudioClip[] Set(Sfx sfx)
        {
            int count = sfx == Sfx.Explosion || sfx == Sfx.Rocket || sfx == Sfx.Throw ? 2 : Variants;
            var set = new AudioClip[count];
            for (int v = 0; v < count; v++) set[v] = Effect(sfx, v);
            return set;
        }

        public static AudioClip Effect(Sfx sfx, int variant)
        {
            rng = new System.Random(7919 * ((int)sfx + 1) + 31 * variant);
            float j = 1f + (variant - 1) * 0.045f;   // небольшой разброс тембра между вариантами
            string name = "sfx_" + sfx.ToString().ToLowerInvariant() + "_" + variant;
            switch (sfx)
            {
                case Sfx.Shot:
                case Sfx.AutoShot: return Make(name, Shot(j, 1f), Peak(sfx));
                case Sfx.Shotgun: return Make(name, Shot(j, 1.6f), Peak(sfx));
                case Sfx.Rail: return Make(name, Rail(j), Peak(sfx));
                case Sfx.Arc: return Make(name, Arc(j), Peak(sfx));
                case Sfx.Rocket: return Make(name, Rocket(j), Peak(sfx));
                case Sfx.Ice: return Make(name, Ice(j), Peak(sfx));
                case Sfx.Flame: return Make(name, Flame(j), Peak(sfx));
                case Sfx.Throw: return Make(name, Throw(j), Peak(sfx));
                case Sfx.Explosion: return Make(name, Explosion(j), Peak(sfx));
                case Sfx.Hit: return Make(name, Hit(j), Peak(sfx));
                case Sfx.Kill: return Make(name, Kill(j), Peak(sfx));
                case Sfx.SawCut: return Make(name, SawCut(j), Peak(sfx));
                case Sfx.Zap: return Make(name, Zap(j), Peak(sfx));
                default: return Make(name, Hit(j), 0.5f);
            }
        }

        public static AudioClip Loop(SfxLoop loop)
        {
            rng = new System.Random(4242 + (int)loop);
            return loop == SfxLoop.Flame ? FlameLoop() : SawLoop();
        }

        // Запасная петля огнемёта (если нет файла): ровный рёв шума с низким гулом, бесшовная.
        private static AudioClip FlameLoop()
        {
            const float len = 1.2f;
            int n = Mathf.RoundToInt(len * R);
            int xf = R / 8;
            var raw = new float[n + xf];
            Svf roar = new Svf(), rumble = new Svf();
            for (int i = 0; i < raw.Length; i++)
            {
                float x = Noise();
                roar.Run(x, 1100f, 0.9f);
                rumble.Run(x, 140f, 0.8f);
                raw[i] = Sat(roar.low * 1.8f + rumble.low * 1.6f);
            }
            var d = new float[n];
            for (int i = 0; i < n; i++)
                d[i] = i < xf ? raw[i] * Mathf.Sin(0.5f * Mathf.PI * i / xf) + raw[i + n] * Mathf.Cos(0.5f * Mathf.PI * i / xf) : raw[i];
            float max = 0.0001f;
            foreach (float v in d) max = Mathf.Max(max, Mathf.Abs(v));
            for (int i = 0; i < n; i++) d[i] *= 0.6f / max;
            return Clip("sfx_flame_loop", d, R);
        }

        // Выстрел: щелчок затвора, плотное тело, низкий удар, короткий хвост и отражения.
        // weight > 1 - дробовик: ниже, тяжелее, длиннее.
        private static float[] Shot(float j, float weight)
        {
            var d = Buffer(0.26f + 0.2f * (weight - 1f));
            Svf body = new Svf(), tail = new Svf();
            float prev = 0f, ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float n = Noise();
                float crack = (n - prev) * Mathf.Exp(-t * 400f);
                prev = n;
                body.Run(n, (1500f - 700f * Mathf.Min(1f, t * 25f)) * j / weight, 1.1f);
                float bodyV = body.band * Mathf.Exp(-t * 42f / weight) * 2.4f;
                ph += Tau * (55f + 150f * Mathf.Exp(-t * 45f)) / weight / R;
                float thump = Mathf.Sin(ph) * Mathf.Exp(-t * 24f / weight) * 0.9f * weight;
                tail.Run(n, 250f + 2600f * Mathf.Exp(-t * 18f), 0.7f);
                float tailV = tail.low * Mathf.Exp(-t * 10f / weight) * 0.7f * weight;
                float ring = Mathf.Sin(Tau * 3170f * j * t) * Mathf.Exp(-t * 70f) * 0.07f;
                d[i] = Sat((crack * 1.1f + bodyV + thump + tailV + ring) * 1.7f);
            }
            Room(d, 0.8f);
            return d;
        }

        // Рельсотрон: треск, нисходящий резонансный свист «пчу», металлический звон и низкий удар.
        private static float[] Rail(float j)
        {
            var d = Buffer(0.8f);
            Svf sweep = new Svf();
            float prev = 0f, ph = 0f, boom = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float n = Noise();
                float crack = (n - prev) * Mathf.Exp(-t * 300f) * 1.4f;
                prev = n;
                sweep.Run(n, (300f + 7500f * Mathf.Exp(-t * 9f)) * j, 7f);
                float pchoo = sweep.band * Mathf.Clamp01(t * 400f) * Mathf.Exp(-t * 6f) * 2.2f;
                ph += Tau * (850f + 2400f * Mathf.Exp(-t * 11f)) * j / R;
                float zing = Mathf.Sin(ph + 0.9f * Mathf.Sin(ph * 1.5f)) * Mathf.Exp(-t * 7f) * 0.38f;
                boom += Tau * (40f + 70f * Mathf.Exp(-t * 20f)) / R;
                float low = Mathf.Sin(boom) * Mathf.Exp(-t * 13f) * 0.85f;
                float metal = (Mathf.Sin(Tau * 520f * j * t) + 0.7f * Mathf.Sin(Tau * 1206f * j * t) + 0.45f * Mathf.Sin(Tau * 2210f * j * t)) * Mathf.Exp(-t * 4.5f) * 0.06f;
                d[i] = Sat((crack + pchoo + zing + low + metal) * 1.5f);
            }
            Room(d, 1f);
            return d;
        }

        // Электрический разряд: щелчок пробоя, рваный «зудящий» гул с прыгающей частотой и паузами,
        // россыпь искр-трескотни и шипение. Гул и искры случайны, поэтому каждый вариант трещит по-своему.
        // Электродуга: короткий чистый «зззап» 0,25 с - нисходящий тон с лёгкой частотной модуляцией
        // и тихим потрескиванием, всё под фильтром низких частот (09.11: треск сварки владелец назвал ужасным).
        private static float[] Arc(float j)
        {
            const float Length = 0.25f;
            var d = Buffer(Length);
            Svf tone = new Svf(), crackle = new Svf();
            float phase = 0f, wobble = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float k = t / Length;
                float freq = Mathf.Lerp(1400f, 240f, Mathf.Sqrt(k)) * j;
                phase += freq / R;
                wobble += freq * 1.51f / R;
                float s = Mathf.Sin(Tau * phase + 0.7f * Mathf.Sin(Tau * wobble));
                float buzz = Sat(s * 1.6f) * 0.55f + s * 0.45f;
                float spark = Rand() < 0.2f ? Noise() : 0f;
                crackle.Run(spark, 2600f * j, 1.1f);
                tone.Run(buzz * 0.85f + crackle.band * 0.6f, 3800f, 0.7f);
                float env = Mathf.Clamp01(t * 500f) * Mathf.Pow(1f - k, 1.8f);
                d[i] = tone.low * env;
            }
            return d;
        }

        // Мелкий лучевой выстрел турели/дрона: короткое «пью» с треском.
        private static float[] Zap(float j)
        {
            var d = Buffer(0.13f);
            Svf hp = new Svf();
            float ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                ph += Tau * (650f + 2000f * Mathf.Exp(-t * 35f)) * j / R;
                float tone = Sat(Mathf.Sin(ph) * 2.2f) * Mathf.Exp(-t * 28f);
                hp.Run(Noise(), 3500f, 0.7f);
                d[i] = tone * 0.7f + hp.high * Mathf.Exp(-t * 90f) * 0.5f;
            }
            return d;
        }

        // Ракета: глухой толчок, вспышка зажигания и шипящий двигатель с дрожанием, уходящий вдаль.
        private static float[] Rocket(float j)
        {
            var d = Buffer(0.95f);
            Svf ignition = new Svf(), motor = new Svf(), crackle = new Svf();
            float ph = 0f, flutter = 1f, flutterTarget = 1f;
            int segment = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float n = Noise();
                ph += Tau * (38f + 60f * Mathf.Exp(-t * 14f)) / R;
                float thump = Mathf.Sin(ph) * Mathf.Exp(-t * 12f) * 0.9f;
                ignition.Run(n, (600f + 2200f * Mathf.Clamp01(t * 7f)) * j, 1.4f);
                float ign = ignition.band * Mathf.Clamp01(t * 200f) * Mathf.Exp(-t * 14f) * 1.8f;
                if (--segment <= 0) { segment = R / 35; flutterTarget = 0.6f + 0.4f * Rand(); }
                flutter += (flutterTarget - flutter) * 0.004f;
                motor.Run(n, (1900f - 900f * Mathf.Clamp01(t)) * j, 0.8f);
                float motorV = motor.low * Mathf.Clamp01(t * 25f) * Mathf.Exp(-t * 2.8f) * flutter * 1.6f;
                float impulse = Rand() < 0.01f ? Noise() : 0f;
                crackle.Run(impulse, 1800f, 1f);
                float whistle = Mathf.Sin(Tau * (1250f - 350f * t) * j * t) * Mathf.Clamp01(t * 10f) * Mathf.Exp(-t * 3f) * 0.05f;
                d[i] = Sat((thump + ign + motorV + crackle.band * 1.5f * Mathf.Exp(-t * 2f) + whistle) * 1.3f);
            }
            return d;
        }

        // Ледяной осколок: стеклянный звон с негармоничными обертонами и хрусткий треск инея.
        private static float[] Ice(float j)
        {
            var d = Buffer(0.6f);
            Svf crunch = new Svf(), air = new Svf();
            float f0 = 1650f * j;
            float[] ratio = { 1f, 2.76f, 5.4f, 8.93f };
            float[] gain = { 1f, 0.6f, 0.35f, 0.2f };
            float[] decay = { 8f, 12f, 17f, 24f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float glass = 0f;
                for (int k = 0; k < ratio.Length; k++)
                    glass += gain[k] * Mathf.Sin(Tau * f0 * ratio[k] * t * (1f + 0.003f * Mathf.Sin(Tau * 7f * t))) * Mathf.Exp(-t * decay[k]);
                float impulse = t < 0.18f && Rand() < 0.06f * (1f - t / 0.18f) ? Noise() * (0.5f + Rand()) : 0f;
                crunch.Run(impulse, 3000f, 0.8f);
                air.Run(Noise(), 6500f, 0.6f);
                float chill = air.band * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f)) * 0.25f;
                d[i] = glass * 0.28f * Mathf.Clamp01(t * 600f) + crunch.high * 2.2f + chill;
            }
            Room(d, 0.6f);
            return d;
        }

        // Огнемёт: рёв пламени с неровной силой, низкий гул и потрескивание.
        private static float[] Flame(float j)
        {
            var d = Buffer(0.5f);
            Svf roar = new Svf(), rumble = new Svf(), pops = new Svf();
            float level = 1f, levelTarget = 1f;
            int segment = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float n = Noise();
                if (--segment <= 0) { segment = Mathf.RoundToInt(R * (0.02f + 0.03f * Rand())); levelTarget = 0.55f + 0.45f * Rand(); }
                level += (levelTarget - level) * 0.002f;
                float env = Mathf.Clamp01(t * 25f) * Mathf.Exp(-t * 4f);
                roar.Run(n, (500f + 900f * Mathf.Clamp01(t * 6f)) * j, 0.9f);
                rumble.Run(n, 140f, 0.8f);
                float impulse = Rand() < 0.004f ? Noise() * 1.5f : 0f;
                pops.Run(impulse, 2600f, 1.5f);
                d[i] = Sat((roar.low * 1.8f * level + rumble.low * 1.6f + pops.band * 2f) * env * 1.4f);
            }
            return d;
        }

        // Бросок пилы: металлическое «шшинг» (звон диска и скрежет) и короткий взмах.
        private static float[] Throw(float j)
        {
            var d = Buffer(0.4f);
            Svf scrape = new Svf(), whoosh = new Svf();
            float[] partial = { 1840f, 2970f, 4410f, 6130f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float n = Noise();
                float ring = 0f;
                for (int k = 0; k < partial.Length; k++)
                    ring += Mathf.Sin(Tau * partial[k] * j * t) * Mathf.Exp(-t * (9f + 4f * k)) / (k + 1);
                scrape.Run(n, 5200f * j, 1.5f);
                float scrapeV = scrape.band * Mathf.Clamp01(t * 150f) * Mathf.Exp(-t * 22f) * 1.4f;
                whoosh.Run(n, 400f + 1600f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f)), 1.2f);
                float whooshV = whoosh.band * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f)) * 0.9f;
                d[i] = ring * 0.35f * Mathf.Clamp01(t * 800f) + scrapeV + whooshV;
            }
            return d;
        }

        // Взрыв: треск, глубокий удар с насыщением, грохот со спадающим тембром и разлетающийся мусор.
        private static float[] Explosion(float j)
        {
            var d = Buffer(1.8f);
            Svf rumble = new Svf(), debris = new Svf();
            float prev = 0f, ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float n = Noise();
                float crack = (n - prev) * Mathf.Exp(-t * 180f) * 1.3f;
                prev = n;
                ph += Tau * (32f + 85f * Mathf.Exp(-t * 7f)) * j / R;
                float boom = Sat(Mathf.Sin(ph) * 2f) * Mathf.Exp(-t * 3.6f) * 1.1f;
                rumble.Run(n, 150f + 3600f * Mathf.Exp(-t * 6f), 0.9f);
                float rumbleV = rumble.low * Mathf.Clamp01(t * 250f) * Mathf.Exp(-t * 2.6f) * 2.2f;
                float impulse = t > 0.05f && t < 1.2f && Rand() < 0.006f * Mathf.Exp(-t * 3f) ? Noise() * (0.5f + Rand()) : 0f;
                debris.Run(impulse, 1800f, 1.2f);
                d[i] = Sat((crack + boom + rumbleV + debris.band * 2.5f) * 1.3f);
            }
            Room(d, 1f);
            return d;
        }

        // Попадание по врагу: короткий плотный шлепок (полосовой шум + низкий «тук»).
        private static float[] Hit(float j)
        {
            var d = Buffer(0.11f);
            Svf bp = new Svf();
            float prev = 0f, ph = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float n = Noise();
                bp.Run(n, 2100f * j - 9000f * t, 1.4f);
                float slap = bp.band * Mathf.Exp(-t * 70f) * 1.6f;
                ph += Tau * (90f + 170f * Mathf.Exp(-t * 40f)) * j / R;
                float thud = Mathf.Sin(ph) * Mathf.Exp(-t * 38f) * 0.85f;
                float click = (n - prev) * Mathf.Exp(-t * 1500f) * 0.3f;
                prev = n;
                d[i] = Sat((slap + thud + click) * 1.4f);
            }
            return d;
        }

        // Смерть врага (пещерные существа): влажный «чпок» с резонансным спадом, хруст и пара капель.
        private static float[] Kill(float j)
        {
            var d = Buffer(0.38f);
            Svf squelch = new Svf(), crunch = new Svf(), sub = new Svf();
            float ph = 0f;
            float[] dropAt = { 0.09f + 0.05f * Rand(), 0.17f + 0.08f * Rand() };
            float[] dropF = { 700f + 500f * Rand(), 900f + 600f * Rand() };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float n = Noise();
                squelch.Run(n, (230f + 2300f * Mathf.Exp(-t * 15f)) * j, 3.5f);
                float wet = squelch.low * Mathf.Clamp01(t * 300f) * Mathf.Exp(-t * 10f) * 1.5f;
                ph += Tau * (100f + 330f * Mathf.Exp(-t * 30f)) * j / R;
                float pop = Mathf.Sin(ph) * Mathf.Exp(-t * 20f) * 0.75f;
                float impulse = t < 0.12f && Rand() < 0.012f ? Noise() * 2f : 0f;
                crunch.Run(impulse, 2800f, 1.8f);
                sub.Run(n, 90f, 0.7f);
                float drops = 0f;
                for (int k = 0; k < dropAt.Length; k++)
                {
                    float u = t - dropAt[k];
                    if (u > 0f) drops += Mathf.Sin(Tau * dropF[k] * (1f + 2f * u) * u) * Mathf.Exp(-u * 60f) * 0.25f;
                }
                d[i] = Sat((wet + pop + crunch.band * 1.6f + sub.low * Mathf.Exp(-t * 12f) * 2f + drops) * 1.3f);
            }
            return d;
        }

        // Пила вгрызается: «бжжж-рт» - зубья бьют по телу, визг металла проседает под нагрузкой, глухой удар.
        private static float[] SawCut(float j)
        {
            float len = 0.24f;
            var d = Buffer(len);
            Svf grind = new Svf(), flesh = new Svf();
            float teeth = 0f, whine = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)R;
                float u = t / len;
                float n = Noise();
                teeth += (95f - 30f * u) * j / R;
                float pulse = Mathf.Exp(-Frac(teeth) * 7f);
                grind.Run(n, (3300f - 1000f * u) * j, 2.4f);
                float grindV = grind.band * (0.35f + 1f * pulse) * 1.6f;
                whine += Tau * (820f - 230f * u) * j / R;
                float scream = (Mathf.Sin(whine) + 0.5f * Mathf.Sin(whine * 2.01f) + 0.3f * Mathf.Sin(whine * 3.03f)) * 0.22f;
                flesh.Run(n, 420f, 0.8f);
                float thud = flesh.low * Mathf.Exp(-t * 30f) * 1.8f;
                float env = Mathf.Clamp01(t * 250f) * Mathf.Exp(-t * 8f);
                d[i] = Sat((grindV + scream) * env * 1.8f + thud);
            }
            return d;
        }

        // Петля вращения пилы (1,2 с, бесшовная): гудящий зуд зубьев, металлический призвук и «вжух»
        // воздуха с частотой оборота. Периодические части кратны длине петли, шум сшит перекрёстным затуханием.
        private static AudioClip SawLoop()
        {
            const float len = 1.2f;
            int n = Mathf.RoundToInt(len * R);
            int xf = R / 10;
            var noise = new float[n + xf];
            Svf air = new Svf();
            float rot = Snap(13f, len);
            for (int i = 0; i < noise.Length; i++)
            {
                float t = i / (float)R;
                air.Run(Noise(), 900f + 550f * Mathf.Sin(Tau * rot * t), 1.6f);
                noise[i] = air.band;
            }
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float w = i < xf ? Mathf.Sin(0.5f * Mathf.PI * i / xf) : 1f;
                float back = i < xf ? Mathf.Cos(0.5f * Mathf.PI * i / xf) * noise[i + n] : 0f;
                d[i] = noise[i] * w + back;
            }
            float f0 = Snap(148f, len);
            float m1 = Snap(2350f, len), m2 = Snap(3720f, len), m3 = Snap(5290f, len);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)R;
                float spin = 0.5f + 0.5f * Mathf.Sin(Tau * rot * t);
                float buzz = 0f;
                for (int k = 1; k <= 12; k++) buzz += Mathf.Sin(Tau * f0 * k * t) / k;
                float metal = (Mathf.Sin(Tau * m1 * t) + 0.6f * Mathf.Sin(Tau * m2 * t) + 0.35f * Mathf.Sin(Tau * m3 * t)) * (0.4f + 0.6f * spin);
                d[i] = Sat(d[i] * 1.3f * (0.5f + 0.5f * spin) + buzz * 0.32f * (0.75f + 0.25f * spin) + metal * 0.07f);
            }
            float max = 0.0001f;
            foreach (float v in d) max = Mathf.Max(max, Mathf.Abs(v));
            for (int i = 0; i < n; i++) d[i] *= 0.6f / max;
            return Clip("sfx_saw_loop", d, R);
        }

        // ---- музыка (22 кГц) ----
        private static float Midi(int note) { return 440f * Mathf.Pow(2f, (note - 69) / 12f); }

        // Частота, при которой целое число периодов укладывается в петлю: стык без щелчка.
        private static float Snap(float f, float loopSeconds) { return Mathf.Max(1f, Mathf.Round(f * loopSeconds)) / loopSeconds; }

        public static AudioClip Limit(string name, float[] data)
        {
            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak > 0.95f) { float k = 0.95f / peak; for (int i = 0; i < data.Length; i++) data[i] *= k; }
            return Clip(name, data, MusicRate);
        }

        // Музыка меню: тёплые аккорды Dm9 - Bbmaj7 - Gm7 - Cadd9 (по 2 такта, 72 уд/мин), мягкая мелодия, тихое арпеджио,
        // глубокий бас и реверберация. Петля 8 тактов (~26,7 с), стык без щелчка.
        public static AudioClip MenuMusic() { return Limit("music_menu", MenuMusicData()); }

        // Чистая математика без Unity API: можно считать в фоновом потоке.
        public static float[] MenuMusicData()
        {
            const int Rate = MusicRate;
            float beat = 60f / 72f;
            float len = beat * 32f;
            int n = Mathf.RoundToInt(len * Rate);
            var d = new float[n];
            int[][] chords = { new[] { 50, 53, 57, 60, 64 }, new[] { 46, 53, 57, 62 }, new[] { 43, 50, 55, 58, 62 }, new[] { 48, 52, 55, 62 } };
            int[] bass = { 38, 34, 31, 36 };
            float chordLen = beat * 8f;
            // Пэд: окна по 2 chordLen, шаг chordLen (сумма = 1). Частоты кратны 1/len - петля бесшовна.
            for (int c = 0; c < 4; c++)
            {
                float start = c * chordLen;
                foreach (int note in chords[c])
                {
                    float f = Snap(Midi(note), len);
                    float f2 = Snap(Midi(note) * 1.004f, len);
                    float f3 = Snap(Midi(note) * 0.996f, len);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float u = Mathf.Repeat(t - start, len) / (chordLen * 2f);
                        if (u >= 1f) continue;
                        float w = Mathf.Sin(Mathf.PI * u);
                        w *= w;
                        float p = 2f * Mathf.PI * t;
                        d[i] += 0.07f * w * (Mathf.Sin(p * f) + Mathf.Sin(p * f2) + Mathf.Sin(p * f3) + 0.35f * Mathf.Sin(p * f * 2f) + 0.15f * Mathf.Sin(p * f * 3f));
                    }
                }
                // Бас: длинная синусоида на весь аккорд с мягкими краями.
                float fb = Snap(Midi(bass[c]), len);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float u = Mathf.Repeat(t - start, len) / (chordLen * 2f);
                    if (u >= 1f) continue;
                    float w = Mathf.Sin(Mathf.PI * u);
                    w *= w;
                    d[i] += 0.16f * w * (Mathf.Sin(2f * Mathf.PI * t * fb) + 0.25f * Mathf.Sin(4f * Mathf.PI * t * fb));
                }
            }
            // Мелодия: (нота, доля внутри аккорда, длительность в долях).
            float[][][] melody =
            {
                new[] { new[] { 69f, 1f }, new[] { 72f, 2.5f }, new[] { 74f, 4f }, new[] { 72f, 6f } },
                new[] { new[] { 70f, 0.5f }, new[] { 69f, 2f }, new[] { 65f, 4f }, new[] { 69f, 6f } },
                new[] { new[] { 67f, 1f }, new[] { 70f, 2.5f }, new[] { 74f, 4.5f }, new[] { 70f, 6.5f } },
                new[] { new[] { 72f, 0.5f }, new[] { 67f, 2f }, new[] { 64f, 4f }, new[] { 67f, 6f } },
            };
            for (int c = 0; c < 4; c++)
                foreach (float[] m in melody[c])
                    AddSoft(d, Mathf.RoundToInt((c * 8f + m[1]) * beat * Rate), 3.2f, Midi((int)m[0]), 0.13f, 1.7f);
            // Тихое арпеджио восьмыми.
            for (int step = 0; step < 64; step++)
            {
                int c = step / 16;
                int[] ch = chords[c];
                int note = ch[(step * 2 + (step / 4)) % ch.Length] + 24;
                AddSoft(d, Mathf.RoundToInt(step * beat * 0.5f * Rate), 0.9f, Midi(note), 0.035f, 4.5f);
            }
            Reverb(d, 0.38f);
            for (int i = 0; i < d.Length; i++) d[i] *= 0.5f;   // ровнее по громкости с музыкой забега
            return d;
        }

        // Мягкий «колокольно-клавишный» тон: синус + затухающие гармоники, плавная атака.
        private static void AddSoft(float[] d, int s0, float seconds, float freq, float amp, float decay)
        {
            const int Rate = MusicRate;
            int count = Mathf.RoundToInt(seconds * Rate);
            for (int i = 0; i < count; i++)
            {
                int idx = (s0 + i) % d.Length;
                float t = i / (float)Rate;
                float p = 2f * Mathf.PI * freq * t;
                float v = Mathf.Sin(p) + 0.4f * Mathf.Sin(p * 2f) * Mathf.Exp(-t * 3f) + 0.15f * Mathf.Sin(p * 3f) * Mathf.Exp(-t * 6f);
                d[idx] += amp * v * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 120f);
            }
        }

        // Реверберация: параллельные гребенчатые фильтры по кольцу (петля остаётся бесшовной) + сухой сигнал.
        private static void Reverb(float[] d, float mix)
        {
            const int Rate = MusicRate;
            int n = d.Length;
            float[] delays = { 0.0297f, 0.0371f, 0.0411f, 0.0437f, 0.0613f, 0.0877f };
            var wet = new float[n];
            foreach (float dl in delays)
            {
                int k = Mathf.RoundToInt(dl * Rate);
                var buf = new float[n];
                for (int pass = 0; pass < 3; pass++)
                    for (int i = 0; i < n; i++)
                    {
                        float prev = buf[(i - k + n) % n];
                        buf[i] = d[i] * 0.5f + 0.82f * prev;
                    }
                for (int i = 0; i < n; i++) wet[i] += buf[i] / delays.Length;
            }
            // Мягкое сглаживание влажного сигнала.
            float lp = 0f;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++) { lp += (wet[i] - lp) * 0.35f; wet[i] = lp; }
            for (int i = 0; i < n; i++) d[i] += wet[i] * mix * 3f;
        }

        // Музыка забега: 120 уд/мин, ля минор Am-F-Dm-E | Am-F-G-E (по 2 такта), 16 тактов (32 с).
        // Прямая бочка, хлопок на 2 и 4, хэты (во второй половине - шестнадцатые), пульсирующий бас,
        // пэд из расстроенных пил с «подкачкой» от бочки и арпеджио шестнадцатыми. Хвосты нот заворачиваются
        // в начало петли - стык без щелчка.
        public static AudioClip GameMusic() { return Limit("music_game", GameMusicData()); }

        public static float[] GameMusicData()
        {
            const int Rate = MusicRate;
            const float bpm = 120f;
            float beat = 60f / bpm;
            float len = beat * 64f;
            int n = Mathf.RoundToInt(len * Rate);
            var d = new float[n];
            var pad = new float[n];
            int[] roots = { 45, 41, 38, 40, 45, 41, 43, 40 };
            int[][] triads =
            {
                new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 50, 53, 57 }, new[] { 52, 56, 59 },
                new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 }, new[] { 52, 56, 59 },
            };
            var r = new System.Random(5);
            float chordLen = beat * 8f;

            // Пэд: окна sin^2 длиной 2 аккорда с шагом в аккорд (сумма 1), частоты кратны петле.
            int window = Mathf.RoundToInt(chordLen * 2f * Rate);
            for (int c = 0; c < 8; c++)
            {
                int s0 = Mathf.RoundToInt((c * chordLen - chordLen * 0.5f) * Rate);
                foreach (int note in triads[c])
                    foreach (float detune in new[] { 0.995f, 1f, 1.006f })
                    {
                        float f = Snap(Midi(note) * detune, len);
                        for (int k = 0; k < window; k++)
                        {
                            int i = ((s0 + k) % n + n) % n;
                            float t = i / (float)Rate;
                            float w = Mathf.Sin(Mathf.PI * k / window);
                            pad[i] += w * w * (Frac(f * t) * 2f - 1f);
                        }
                    }
            }
            // Фильтр пэда - два прохода по кольцу, чтобы состояние на стыке совпало.
            Svf padLp = new Svf();
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float v = padLp.Run(pad[i], 900f + 500f * Mathf.Sin(2f * Mathf.PI * t / len * 2f), 0.9f, Rate);
                    if (pass == 1) pad[i] = v;
                }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float sinceBeat = Mathf.Repeat(t, beat);
                float pump = 1f - 0.7f * Mathf.Exp(-sinceBeat * 9f);
                d[i] += pad[i] * 0.022f * pump;
            }

            for (int step = 0; step < 128; step++)   // восьмые
            {
                float at = step * beat * 0.5f;
                int chord = step / 16;
                int s0 = Mathf.RoundToInt(at * Rate);
                bool second = step >= 64;
                // бас: корень, на каждой 4-й восьмой - октава вверх
                int bassNote = roots[chord] + (step % 4 == 3 ? 12 : 0);
                AddTone(d, s0, 0.24f, Midi(bassNote), 0.2f, 9f, true);
                if (step % 2 == 0)
                {
                    // бочка на каждую долю
                    float ph = 0f;
                    int count = Mathf.RoundToInt(0.3f * Rate);
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)Rate;
                        ph += 2f * Mathf.PI * (45f + 120f * Mathf.Exp(-t * 30f)) / Rate;
                        d[(s0 + i) % n] += 0.55f * Mathf.Sin(ph) * Mathf.Exp(-t * 10f);
                    }
                    // хлопок на 2 и 4
                    if (step % 4 == 2)
                    {
                        Svf clap = new Svf();
                        int cc = Mathf.RoundToInt(0.22f * Rate);
                        for (int i = 0; i < cc; i++)
                        {
                            float t = i / (float)Rate;
                            float x = (float)(r.NextDouble() * 2.0 - 1.0);
                            clap.Run(x, 1700f, 1.2f, Rate);
                            float burst = t < 0.03f ? 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 110f) : 1f;
                            d[(s0 + i) % n] += 0.22f * clap.band * burst * Mathf.Exp(-t * 18f)
                                + 0.12f * Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t * 30f);
                        }
                    }
                }
                // хэты: на слабые восьмые, во второй половине - каждая шестнадцатая
                for (int h = 0; h < 2; h++)
                {
                    bool open = step % 2 == 1 && h == 0;
                    if (!open && !second) continue;
                    int hs = s0 + h * Mathf.RoundToInt(beat * 0.25f * Rate);
                    float amp = open ? 0.07f : 0.035f;
                    float decay = open ? 35f : 90f;
                    int count = Mathf.RoundToInt((open ? 0.12f : 0.05f) * Rate);
                    float prev = 0f;
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)Rate;
                        float x = (float)(r.NextDouble() * 2.0 - 1.0);
                        d[(hs + i) % n] += amp * (x - prev) * Mathf.Exp(-t * decay);
                        prev = x;
                    }
                }
                // арпеджио шестнадцатыми
                int[] tri = triads[chord];
                int[] pattern = second ? new[] { 0, 2, 1, 2 } : new[] { 0, 1, 2, 1 };
                for (int h = 0; h < 2; h++)
                {
                    int idx = pattern[(step * 2 + h) % 4];
                    int note = tri[idx] + 12 + (second && (step * 2 + h) % 8 == 7 ? 12 : 0);
                    int a0 = s0 + h * Mathf.RoundToInt(beat * 0.25f * Rate);
                    AddTone(d, a0, 0.16f, Midi(note), 0.045f, 14f, false);
                }
            }
            return d;
        }

        // Тон с атакой без щелчка; хвост заворачивается в начало петли.
        private static void AddTone(float[] d, int s0, float seconds, float freq, float amp, float decay, bool saw)
        {
            const int Rate = MusicRate;
            int count = Mathf.RoundToInt(seconds * Rate);
            float ph = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                ph += freq / Rate;
                ph -= Mathf.Floor(ph);
                float w = saw ? Mathf.Sin(2f * Mathf.PI * ph) * 0.7f + (ph * 2f - 1f) * 0.3f : Mathf.Abs(ph * 4f - 2f) - 1f;
                float release = Mathf.Clamp01((count - i) / (Rate * 0.01f));
                d[(s0 + i) % d.Length] += amp * w * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400f) * release;
            }
        }
    }
}
