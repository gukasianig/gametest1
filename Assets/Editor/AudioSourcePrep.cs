using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Подготовка скачанных CC0-звуков (docs/AUDIO.md): исходники лежат в Assets/Audio/Source/ (в сборку не попадают,
// пока на них нет ссылок), результат - в Assets/Audio/Resources/Sfx/<Имя>/, откуда их берёт GameAudio.
// Анализ: -batchmode -quit -projectPath . -executeMethod AudioSourcePrep.Analyze   -> Logs/audio-source-report.txt
// Сборка: -batchmode -quit -projectPath . -executeMethod AudioSourcePrep.Build     -> Logs/audio-prep.txt
public static class AudioSourcePrep
{
    private const string Source = "Assets/Audio/Source";
    private const string Target = "Assets/Audio/Resources/Sfx";

    public static void Analyze()
    {
        var sb = new StringBuilder();
        try
        {
            foreach (string path in Directory.GetFiles(Source, "*.ogg", SearchOption.AllDirectories))
            {
                string p = path.Replace('\\', '/');
                float[] d = Read(p, out int rate);
                float peak = 0f, rms = 0f;
                foreach (float v in d) { peak = Mathf.Max(peak, Mathf.Abs(v)); rms += v * v; }
                rms = Mathf.Sqrt(rms / Mathf.Max(1, d.Length));
                sb.Append(Path.GetFileName(p) + " len=" + (d.Length / (float)rate).ToString("F2") + "s rate=" + rate + " peak=" + peak.ToString("F2") + " rms=" + rms.ToString("F3"));
                if (p.Contains("BigSoundBank"))
                {
                    // Огибающая по 100 мс: цифра 0-9 = громкость относительно пика (корень, чтобы видеть тихое).
                    int win = rate / 10;
                    sb.AppendLine();
                    sb.Append("  env100ms: ");
                    for (int s = 0; s + win <= d.Length; s += win)
                    {
                        float r = 0f;
                        for (int i = s; i < s + win; i++) r += d[i] * d[i];
                        r = Mathf.Sqrt(r / win);
                        sb.Append(Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(r / Mathf.Max(0.0001f, peak)) * 9f), 0, 9));
                        if ((s / win) % 50 == 49) sb.Append(' ');
                    }
                    // Яркость: доля энергии верхних частот (разность соседних отсчётов) по 1 с.
                    sb.AppendLine();
                    sb.Append("  bright1s: ");
                    for (int s = 0; s + rate <= d.Length; s += rate)
                    {
                        float hi = 0f, all = 0f;
                        for (int i = s + 1; i < s + rate; i++) { float df = d[i] - d[i - 1]; hi += df * df; all += d[i] * d[i]; }
                        sb.Append((hi / Mathf.Max(1e-9f, all)).ToString("F2") + " ");
                    }
                }
                sb.AppendLine();
            }
            File.WriteAllText("Logs/audio-source-report.txt", sb.ToString());
        }
        catch (Exception e) { File.WriteAllText("Logs/audio-source-report.txt", sb + "\n" + e); }
    }

    // Чтение исходника: включаем «распаковку при загрузке», иначе GetData недоступен.
    private static float[] Read(string path, out int rate)
    {
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        if (s.loadType != AudioClipLoadType.DecompressOnLoad || importer.forceToMono != true)
        {
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = s;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        clip.LoadAudioData();
        var d = new float[clip.samples * clip.channels];
        clip.GetData(d, 0);
        rate = clip.frequency;
        return d;
    }

    // ---------------------------------------------------------------- сборка
    private static StringBuilder log;

    public static void Build()
    {
        log = new StringBuilder();
        try
        {
            if (Directory.Exists(Target)) Directory.Delete(Target, true);
            Directory.CreateDirectory(Target);
            BuildFromKenney();
            BuildImpacts();
            BuildGuns();
            BuildFlameLoop();
            BuildSaw();
            // BuildArc(): треск сварки для дуги отключён 09.11 — используется синтез GameAudio.Arc (короткий «зап»).
            AssetDatabase.Refresh();
            log.AppendLine("RESULT OK");
        }
        catch (Exception e) { log.AppendLine(e.ToString()); log.AppendLine("RESULT FAIL"); }
        File.WriteAllText("Logs/audio-prep.txt", log.ToString());
    }

    // Готовые короткие звуки Kenney копируются как есть (варианты = файлы папки).
    private static void BuildFromKenney()
    {
        var map = new Dictionary<string, string[]>
        {
            { "Rail", new[] { "laserLarge_000", "laserLarge_001", "laserLarge_002", "laserLarge_003", "laserLarge_004" } },
            { "Ice", new[] { "impactGlass_medium_000", "impactGlass_medium_001", "impactGlass_medium_002", "impactGlass_medium_003", "impactGlass_medium_004" } },
            // Установка турели: короткий металлический лязг.
            { "Deploy", new[] { "impactMetal_medium_000", "impactMetal_medium_001", "impactMetal_medium_002", "impactMetal_medium_003", "impactMetal_medium_004" } },
        };
        foreach (var pair in map)
        {
            string dir = Target + "/" + pair.Key;
            Directory.CreateDirectory(dir);
            foreach (string name in pair.Value)
            {
                string from = Source + "/Kenney/" + name + ".ogg";
                if (!File.Exists(from)) throw new FileNotFoundException(from);
                File.Copy(from, dir + "/" + name + ".ogg", true);
            }
            log.AppendLine(pair.Key + ": " + pair.Value.Length + " Kenney files");
        }

        // Огнемёт: thrusterFire длится 5 с, а огнемёт стреляет часто - берём начало 0,6 с с затуханием.
        Directory.CreateDirectory(Target + "/Flame");
        for (int k = 0; k < 5; k++)
        {
            float[] d = Read(Source + "/Kenney/thrusterFire_00" + k + ".ogg", out int rate);
            WriteWav(Target + "/Flame/flame_thrusterFire_" + k + ".wav", Slice(d, 0, Mathf.RoundToInt(0.6f * rate), 0.005f, 0.3f, rate), rate);
        }
        log.AppendLine("Flame: thrusterFire_000-004, first 0.6s");

        // Взрыв: хруст explosionCrunch + низкий гул lowFrequency_explosion (по очереди), 1,6 с.
        Directory.CreateDirectory(Target + "/Explosion");
        float[][] low = { Read(Source + "/Kenney/lowFrequency_explosion_000.ogg", out int lowRate), Read(Source + "/Kenney/lowFrequency_explosion_001.ogg", out _) };
        for (int k = 0; k < 5; k++)
        {
            float[] crunch = Read(Source + "/Kenney/explosionCrunch_00" + k + ".ogg", out int rate);
            if (rate != lowRate) throw new Exception("explosion rates differ");
            float[] boom = low[k % 2];
            int n = Mathf.Min(Mathf.RoundToInt(1.6f * rate), Mathf.Max(crunch.Length, boom.Length));
            var mix = new float[n];
            for (int i = 0; i < n; i++) mix[i] = (i < crunch.Length ? crunch[i] : 0f) + 0.7f * (i < boom.Length ? boom[i] : 0f);
            WriteWav(Target + "/Explosion/explosion_mix_" + k + ".wav", Slice(mix, 0, n, 0.001f, 0.3f, rate), rate);
        }
        log.AppendLine("Explosion: explosionCrunch_000-004 + 0.7 x lowFrequency_explosion_000/001, 1.6s");
    }

    private static float[] Kenney(string name, out int rate)
    {
        return Read(Source + "/Kenney/" + name + ".ogg", out rate);
    }

    // Попадание и смерть врага - короткие и глухие: звучат десятки раз в секунду, не должны звенеть и тянуться.
    private static void BuildImpacts()
    {
        // Попадание: мягкий короткий шлепок impactSoft_medium (0,12-0,18 с) без верхов.
        Directory.CreateDirectory(Target + "/Hit");
        for (int k = 0; k < 5; k++)
        {
            float[] d = LowPass(Kenney("impactSoft_medium_00" + k, out int rate), 3200f, rate);
            WriteWav(Target + "/Hit/hit_soft_" + k + ".wav", Slice(d, 0, Mathf.RoundToInt(0.14f * rate), 0.001f, 0.06f, rate), rate);
        }
        log.AppendLine("Hit: impactSoft_medium_000-004, lowpass 3.2k, 0.14s");

        // Смерть: глухой удар impactSoft_heavy + начало влажного slime_000; всего 0,3 с (раньше slime_001 тянулся 4,7 с).
        Directory.CreateDirectory(Target + "/Kill");
        float[] slime = LowPass(Kenney("slime_000", out int slimeRate), 2500f, slimeRate);
        for (int k = 0; k < 5; k++)
        {
            float[] thud = Kenney("impactSoft_heavy_00" + k, out int rate);
            if (rate != slimeRate) throw new Exception("kill rates differ");
            int n = Mathf.RoundToInt(0.3f * rate), offset = Mathf.RoundToInt(0.01f * rate);
            var mix = new float[n];
            for (int i = 0; i < n; i++)
                mix[i] = (i < thud.Length ? thud[i] : 0f) + (i >= offset && i - offset < slime.Length ? slime[i - offset] * 0.6f : 0f);
            WriteWav(Target + "/Kill/kill_thud_" + k + ".wav", Slice(mix, 0, n, 0.001f, 0.14f, rate), rate);
        }
        log.AppendLine("Kill: impactSoft_heavy_000-004 + 0.6 x slime_000, 0.3s");
    }

    // Пистолет и турель: лазеры Kenney пищат, поэтому опускаем их по высоте (медленнее воспроизведение),
    // срезаем верх и подкладываем низкий «тук» - получается плотный короткий выстрел без визга.
    private static void BuildGuns()
    {
        Directory.CreateDirectory(Target + "/Shot");
        Directory.CreateDirectory(Target + "/Zap");
        for (int k = 0; k < 5; k++)
        {
            float[] laser = Kenney("laserSmall_00" + k, out int rate);
            float[] thump = Kenney("impactSoft_medium_00" + k, out int thumpRate);
            float[] click = Kenney("impactMetal_light_00" + k, out int clickRate);
            if (rate != thumpRate || rate != clickRate) throw new Exception("gun rates differ");

            float[] shotLaser = LowPass(Resample(laser, 0.62f), 2600f, rate);
            WriteWav(Target + "/Shot/shot_low_" + k + ".wav", Mix(Mathf.RoundToInt(0.2f * rate), rate, 0.09f,
                shotLaser, 0.75f, LowPass(thump, 1800f, rate), 0.8f), rate);

            // Турель/дрон: тот же принцип, чуть выше и короче, с металлическим щелчком затвора вместо удара.
            float[] zapLaser = LowPass(Resample(laser, 0.78f), 3000f, rate);
            WriteWav(Target + "/Zap/zap_low_" + k + ".wav", Mix(Mathf.RoundToInt(0.16f * rate), rate, 0.07f,
                zapLaser, 0.7f, LowPass(Slice(click, 0, Mathf.RoundToInt(0.05f * rate), 0.001f, 0.03f, rate), 3500f, rate), 0.5f), rate);
        }
        log.AppendLine("Shot: laserSmall x0.62 speed, lowpass 2.6k + impactSoft_medium, 0.2s");
        log.AppendLine("Zap: laserSmall x0.78 speed, lowpass 3k + impactMetal_light click, 0.16s");
    }

    // Огнемёт стреляет каждые 0,15 с, поэтому его звук - бесшовная петля ровного участка thrusterFire,
    // которая держится, пока горит пламя (GameAudio.Hold(SfxLoop.Flame)).
    private static void BuildFlameLoop()
    {
        float[] d = LowPass(Kenney("thrusterFire_001", out int rate), 5000f, rate);
        int start = FindSteady(d, rate, 1.85f, 1)[0];
        float[] loop = MakeLoop(d, start, Mathf.RoundToInt(1.6f * rate), Mathf.RoundToInt(0.25f * rate));
        Directory.CreateDirectory(Target + "/FlameLoop");
        WriteWav(Target + "/FlameLoop/flame_loop_thruster.wav", loop, rate);
        log.AppendLine("FlameLoop: thrusterFire_001 at " + (start / (float)rate).ToString("F2") + "s, 1.6s");
    }

    // Начала самых ровных по громкости (и не тихих) участков длиной seconds, без перекрытия.
    private static List<int> FindSteady(float[] d, int rate, float seconds, int count)
    {
        int win = rate / 20;
        float[] env = Envelope(d, win);
        float mean = 0f;
        foreach (float v in env) mean += v;
        mean /= env.Length;
        int span = Mathf.CeilToInt(seconds * 20f);
        var scores = new List<KeyValuePair<float, int>>();
        for (int s = 0; s + span < env.Length; s++)
        {
            float m = 0f, q = 0f;
            for (int k = s; k < s + span; k++) { m += env[k]; q += env[k] * env[k]; }
            m /= span;
            if (m < mean * 0.8f) continue;
            float cv = Mathf.Sqrt(Mathf.Max(0f, q / span - m * m)) / Mathf.Max(1e-5f, m);
            scores.Add(new KeyValuePair<float, int>(cv, s));
        }
        scores.Sort((a, b) => a.Key.CompareTo(b.Key));
        var picked = new List<int>();
        foreach (var pair in scores)
        {
            if (picked.Count >= count) break;
            bool overlaps = false;
            foreach (int p in picked) if (Mathf.Abs(p - pair.Value) < span) overlaps = true;
            if (!overlaps) picked.Add(pair.Value);
        }
        if (picked.Count < count) throw new Exception("found only " + picked.Count + " steady segments");
        for (int i = 0; i < picked.Count; i++) picked[i] *= win;
        return picked;
    }

    // Петля длиной n из d начиная со start: конец равномощно перетекает в начало (нужно n + x отсчётов).
    private static float[] MakeLoop(float[] d, int start, int n, int x)
    {
        if (start + n + x > d.Length) throw new Exception("loop segment out of range");
        var loop = new float[n];
        for (int i = 0; i < n; i++)
        {
            loop[i] = d[start + i];
            if (i < x)
            {
                float t = i / (float)x;
                loop[i] = d[start + i] * Mathf.Sin(0.5f * Mathf.PI * t) + d[start + n + i] * Mathf.Cos(0.5f * Mathf.PI * t);
            }
        }
        return loop;
    }

    // Медленнее воспроизведение = ниже тон (speed < 1), линейная интерполяция.
    private static float[] Resample(float[] d, float speed)
    {
        int n = Mathf.FloorToInt((d.Length - 1) / speed);
        var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            float p = i * speed;
            int a = (int)p;
            float f = p - a;
            o[i] = d[a] * (1f - f) + d[Mathf.Min(a + 1, d.Length - 1)] * f;
        }
        return o;
    }

    // Двухполюсный ФНЧ (две однополюсные ступени).
    private static float[] LowPass(float[] d, float cutoff, int rate)
    {
        float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / rate);
        var o = new float[d.Length];
        float y1 = 0f, y2 = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            y1 += (d[i] - y1) * a;
            y2 += (y1 - y2) * a;
            o[i] = y2;
        }
        return o;
    }

    // Смесь пар (сигнал, громкость) длиной n с затуханием в конце.
    private static float[] Mix(int n, int rate, float fadeOut, params object[] parts)
    {
        var o = new float[n];
        for (int p = 0; p + 1 < parts.Length; p += 2)
        {
            var d = (float[])parts[p];
            float g = (float)parts[p + 1];
            for (int i = 0; i < n && i < d.Length; i++) o[i] += d[i] * g;
        }
        return Slice(o, 0, n, 0.001f, fadeOut, rate);
    }

    // RMS по окнам win отсчётов.
    private static float[] Envelope(float[] d, int win)
    {
        var e = new float[d.Length / win];
        for (int w = 0; w < e.Length; w++)
        {
            float r = 0f;
            for (int i = w * win; i < (w + 1) * win; i++) r += d[i] * d[i];
            e[w] = Mathf.Sqrt(r / win);
        }
        return e;
    }

    private static float[] Slice(float[] d, int start, int length, float fadeIn, float fadeOut, int rate)
    {
        start = Mathf.Clamp(start, 0, d.Length - 1);
        length = Mathf.Min(length, d.Length - start);
        var o = new float[length];
        int fi = Mathf.Max(1, Mathf.RoundToInt(fadeIn * rate)), fo = Mathf.Max(1, Mathf.RoundToInt(fadeOut * rate));
        for (int i = 0; i < length; i++)
        {
            float g = Mathf.Min(1f, i / (float)fi) * Mathf.Min(1f, (length - 1 - i) / (float)fo);
            o[i] = d[start + i] * g;
        }
        return o;
    }

    // Петля вращения - самый ровный по громкости участок записи 1136; резка - самые громкие и яркие места,
    // разнесённые по времени. Петля сшита равномощным перекрёстным затуханием конца в начало.
    private static void BuildSaw()
    {
        float[] d = Read(Source + "/BigSoundBank/1136.ogg", out int rate);
        int win = rate / 20;   // 50 мс
        float[] env = Envelope(d, win);
        float mean = 0f;
        foreach (float v in env) mean += v;
        mean /= env.Length;

        const float loopLen = 1.6f, xfade = 0.25f;
        int span = Mathf.CeilToInt((loopLen + xfade) * 20f);
        int best = -1;
        float bestScore = float.MaxValue;
        for (int s = 0; s + span < env.Length; s += 2)
        {
            float m = 0f, q = 0f;
            for (int k = s; k < s + span; k++) { m += env[k]; q += env[k] * env[k]; }
            m /= span;
            float cv = Mathf.Sqrt(Mathf.Max(0f, q / span - m * m)) / Mathf.Max(1e-5f, m);
            if (m < mean * 0.5f) continue;   // не тишина
            if (cv < bestScore) { bestScore = cv; best = s; }
        }
        if (best < 0) throw new Exception("no steady saw segment");
        int n = Mathf.RoundToInt(loopLen * rate), x = Mathf.RoundToInt(xfade * rate), s0 = best * win;
        var loop = new float[n];
        for (int i = 0; i < n; i++)
        {
            loop[i] = d[s0 + i];
            if (i < x)
            {
                float t = i / (float)x;
                loop[i] = d[s0 + i] * Mathf.Sin(0.5f * Mathf.PI * t) + d[s0 + n + i] * Mathf.Cos(0.5f * Mathf.PI * t);
            }
        }
        Directory.CreateDirectory(Target + "/SawLoop");
        WriteWav(Target + "/SawLoop/saw_loop_1136.wav", loop, rate);
        log.AppendLine("SawLoop: 1136 at " + (s0 / (float)rate).ToString("F2") + "s, " + loopLen + "s, level cv=" + bestScore.ToString("F3"));

        // Резка: в записи холостой ход чередуется с резами (громче и ярче). Берём начало каждого реза -
        // момент, когда сглаженная громкость поднимается выше середины между холостым ходом и резом.
        var sorted = (float[])env.Clone();
        Array.Sort(sorted);
        float idle = sorted[sorted.Length / 2], cut = sorted[sorted.Length * 9 / 10];
        float threshold = (idle + cut) * 0.5f;
        var onsets = new List<int>();
        float smooth = env[0];
        bool above = false;
        for (int w = 0; w < env.Length; w++)
        {
            smooth += (env[w] - smooth) * 0.35f;
            if (!above && smooth > threshold && (onsets.Count == 0 || w - onsets[onsets.Count - 1] > 60)) { onsets.Add(w); above = true; }
            else if (above && smooth < threshold * 0.9f) above = false;
        }
        log.AppendLine("SawCut: idle=" + idle.ToString("F3") + " cut=" + cut.ToString("F3") + " onsets=" + onsets.Count);
        Directory.CreateDirectory(Target + "/SawCut");
        int made = 0;
        foreach (int w in onsets)
        {
            if (made >= 4) break;
            int start = Mathf.Max(0, w * win - rate / 20);
            WriteWav(Target + "/SawCut/saw_cut_1136_" + made + ".wav", Slice(d, start, Mathf.RoundToInt(0.45f * rate), 0.01f, 0.18f, rate), rate);
            log.AppendLine("SawCut " + made + ": 1136 at " + (start / (float)rate).ToString("F2") + "s");
            made++;
        }
        if (made < 2) throw new Exception("only " + made + " saw cut onsets found");
    }

    // Электро: отдельные разряды из записи коротких сварок (0952), до 4 самых сильных.
    private static void BuildArc()
    {
        float[] d = Read(Source + "/BigSoundBank/0952.ogg", out int rate);
        int win = rate / 100;   // 10 мс
        float[] env = Envelope(d, win);
        float peak = 0f;
        foreach (float v in env) peak = Mathf.Max(peak, v);
        var bursts = new List<Vector2Int>();   // (начало, конец) в окнах
        int startW = -1, quiet = 0;
        for (int w = 0; w < env.Length; w++)
        {
            bool loud = env[w] > peak * 0.25f;
            if (loud) { if (startW < 0) startW = w; quiet = 0; }
            else if (startW >= 0 && ++quiet > 6) { bursts.Add(new Vector2Int(startW, w - quiet)); startW = -1; quiet = 0; }
        }
        if (startW >= 0) bursts.Add(new Vector2Int(startW, env.Length - 1));
        bursts.Sort((a, b) => Energy(env, b).CompareTo(Energy(env, a)));

        // Разряды короткие (0,15-0,38 с), поэтому к каждому подмешан ровный треск длинной сварки 0950:
        // удар разряда + 0,75 с трескотни с затуханием, затем мягкое насыщение - плотнее и громче.
        float[] weld = Read(Source + "/BigSoundBank/0950.ogg", out int weldRate);
        if (weldRate != rate) throw new Exception("arc rates differ");
        const float seconds = 0.75f;
        List<int> steady = FindSteady(weld, rate, seconds, 4);
        Directory.CreateDirectory(Target + "/Arc");
        int made = 0;
        foreach (Vector2Int b in bursts)
        {
            if (made >= 4) break;
            int start = Mathf.Max(0, b.x * win - rate / 200);
            float burstSeconds = Mathf.Clamp((b.y - b.x) * 0.01f + 0.08f, 0.15f, 0.5f);
            float[] burst = Slice(d, start, Mathf.RoundToInt(burstSeconds * rate), 0.003f, 0.08f, rate);
            float[] crackle = Slice(weld, steady[made], Mathf.RoundToInt(seconds * rate), 0.01f, 0.35f, rate);
            float[] mix = Mix(Mathf.RoundToInt(seconds * rate), rate, 0.3f, burst, 1f, crackle, 0.7f);
            float mixPeak = 0.0001f;
            foreach (float v in mix) mixPeak = Mathf.Max(mixPeak, Mathf.Abs(v));
            const float drive = 2.2f;
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < mix.Length; i++) mix[i] = (float)Math.Tanh(mix[i] / mixPeak * drive) / norm;
            WriteWav(Target + "/Arc/arc_long_" + made + ".wav", mix, rate);
            log.AppendLine("Arc " + made + ": 0952 at " + (start / (float)rate).ToString("F2") + "s (" + burstSeconds.ToString("F2") + "s) + 0950 at "
                + (steady[made] / (float)rate).ToString("F2") + "s, " + seconds + "s");
            made++;
        }
        if (made < 2) throw new Exception("only " + made + " arc bursts found");
    }

    private static float Energy(float[] env, Vector2Int b)
    {
        float e = 0f;
        for (int w = b.x; w <= b.y; w++) e += env[w] * env[w];
        return e;
    }

    private static void WriteWav(string path, float[] d, int rate)
    {
        float peak = 0f;
        foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        if (peak > 0.98f) for (int i = 0; i < d.Length; i++) d[i] *= 0.95f / peak;   // без клиппинга после смешивания
        using (var w = new BinaryWriter(File.Create(path)))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + d.Length * 2);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(d.Length * 2);
            foreach (float v in d) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f));
        }
    }
}
