using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Проверка синтеза звука без запуска игры: все варианты эффектов, петля пилы и музыка создаются, не пустые,
// не тише порога, без NaN; у петель стык не больше обычного шага между соседними отсчётами.
// Заодно пишет WAV для прослушки вне Unity: Logs/audio-preview/*.wav.
// Запуск: Unity -batchmode -projectPath . -executeMethod AudioChecks.RunBatch -logFile audio.log
public static class AudioChecks
{
    private const string PreviewDir = "Logs/audio-preview";

    public static void RunBatch()
    {
        var sb = new StringBuilder();
        bool ok = true;
        try
        {
            Directory.CreateDirectory(PreviewDir);
            Type synth = typeof(GameAudio).GetNestedType("Synth", BindingFlags.NonPublic);
            MethodInfo set = synth.GetMethod("Set");
            foreach (Sfx s in Enum.GetValues(typeof(Sfx)))
            {
                var clips = (AudioClip[])set.Invoke(null, new object[] { s });
                if (clips.Length < 2) { ok = false; sb.AppendLine(s + " only " + clips.Length + " variant(s)"); }
                for (int v = 0; v < clips.Length; v++) ok &= Report(sb, s + "_" + v, clips[v], false, 0.2f);
                if (clips.Length >= 2) ok &= Differ(sb, s.ToString(), clips[0], clips[1]);
            }
            foreach (SfxLoop l in Enum.GetValues(typeof(SfxLoop)))
                ok &= Report(sb, l + "Loop", (AudioClip)synth.GetMethod("Loop").Invoke(null, new object[] { l }), true, 0.2f);
            float t0 = Time.realtimeSinceStartup;
            ok &= Report(sb, "MenuMusic", (AudioClip)synth.GetMethod("MenuMusic").Invoke(null, null), false, 0.2f);
            float t1 = Time.realtimeSinceStartup;
            ok &= Report(sb, "GameMusic", (AudioClip)synth.GetMethod("GameMusic").Invoke(null, null), true, 0.2f);
            float t2 = Time.realtimeSinceStartup;
            sb.AppendLine("synth time: menu " + (t1 - t0).ToString("F2") + "s, game " + (t2 - t1).ToString("F2") + "s");
        }
        catch (Exception e) { ok = false; sb.AppendLine(e.ToString()); }
        sb.AppendLine(ok ? "RESULT OK" : "RESULT FAIL");
        File.WriteAllText("Logs/audio-check.txt", sb.ToString());
        EditorApplication.Exit(ok ? 0 : 1);
    }

    // ---- Play Mode: петля пилы (Hold/Load/затухание) и фоновая музыка меню/забега ----
    // Batch (Unity закрыт, без -quit): -batchmode -projectPath . -executeMethod AudioChecks.RunPlayBatch
    // Отчёт: Logs/audio-play-results.txt. Проверяет логику и состояние источников, не то, как это звучит.
    private const string PlayRequest = "Logs/audio-play.request";
    private const string PlayReport = "Logs/audio-play-results.txt";
    private static IEnumerator playRoutine;
    private static readonly List<string> playLines = new List<string>();
    private static int playErrors;
    private static double playStarted;

    [InitializeOnLoadMethod]
    static void HookPlayMode()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(PlayRequest)) EditorApplication.delayCall += BeginPlay;
        };
    }

    public static void RunPlayBatch()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(PlayRequest, "batch");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    static void BeginPlay()
    {
        File.Delete(PlayRequest);
        playLines.Clear();
        playErrors = 0;
        Application.logMessageReceived += OnPlayLog;
        playStarted = EditorApplication.timeSinceStartup;
        playRoutine = PlaySteps();
        EditorApplication.update += PlayTick;
    }

    static void OnPlayLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) { playErrors++; playLines.Add("LOG " + type + ": " + message); }
    }

    static void PlayTick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - playStarted > 90) throw new Exception("timed out");
            if (playRoutine.MoveNext()) return;
            if (playErrors > 0) throw new Exception(playErrors + " error log(s)");
            FinishPlay(true, "audio play checks complete");
        }
        catch (Exception e) { FinishPlay(false, e.Message); }
    }

    static void FinishPlay(bool ok, string text)
    {
        EditorApplication.update -= PlayTick;
        Application.logMessageReceived -= OnPlayLog;
        playLines.Add((ok ? "PASS: " : "FAIL: ") + text);
        File.WriteAllLines(PlayReport, playLines);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static void Need(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        playLines.Add("PASS: " + text);
    }

    static object Field(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(target);
    }

    static IEnumerator PlaySteps()
    {
        AudioClip menuTrack = Resources.Load<AudioClip>("RecordedAudio/MenuMusic");
        AudioClip gameTrack = Resources.Load<AudioClip>("RecordedAudio/GameMusic");
        Need(menuTrack != null && gameTrack != null && menuTrack != gameTrack, "both recorded music assets imported");
        GameAudio.SetMusic(false);
        GameAudio audio = UnityEngine.Object.FindObjectOfType<GameAudio>();
        Need(audio != null, "GameAudio created");
        var music = (AudioSource)Field(audio, "music");
        double until = EditorApplication.timeSinceStartup + 20;
        float audibleVolume = Mathf.Min(0.05f, GameAudio.MusicVolume * 0.55f);
        while ((music.clip != menuTrack || !music.isPlaying || music.volume < audibleVolume) && EditorApplication.timeSinceStartup < until) yield return null;
        Need(music.clip == menuTrack && music.isPlaying && music.loop && music.volume >= audibleVolume, "Factory Ambiance plays in the menu");

        // Пила: держим петлю 0,6 с - источник включён и громкий; нагрузка проседает по высоте.
        var loops = (Array)Field(audio, "loops");
        object saw = loops.GetValue((int)SfxLoop.Saw);
        var src = (AudioSource)Field(saw, "source");
        double holdEnd = EditorApplication.timeSinceStartup + 0.6;
        while (EditorApplication.timeSinceStartup < holdEnd) { GameAudio.Hold(SfxLoop.Saw, 0.55f, 1.1f); yield return null; }
        Need(src.isPlaying && src.volume > 0.2f && src.clip != null, "saw loop plays while held (clip " + src.clip.name + ", vol " + src.volume.ToString("F2") + ", pitch " + src.pitch.ToString("F2") + ")");
        // Какие звуки взяты из файлов, а какие синтезированы.
        var sets = (AudioClip[][])Field(audio, "clips");
        var sources = new StringBuilder();
        foreach (Sfx s in Enum.GetValues(typeof(Sfx))) sources.Append(s + "=" + (sets[(int)s][0].name.StartsWith("sfx_") ? "synth" : "file x" + sets[(int)s].Length) + " ");
        playLines.Add("NOTE: " + sources);
        float before = src.pitch;
        GameAudio.Load(SfxLoop.Saw, 0.8f);
        GameAudio.Play(Sfx.SawCut, 0.8f);
        // В batch шаг кадра бывает крупным и восстановление успевает пройти за кадр, поэтому проверяем
        // сам «укус» (load) и минимальную высоту за следующие кадры.
        float load = (float)Field(saw, "load");
        float minPitch = before;
        for (int i = 0; i < 10; i++) { GameAudio.Hold(SfxLoop.Saw, 0.55f, 1.1f); yield return null; minPitch = Mathf.Min(minPitch, src.pitch); }
        Need(load <= 0.8f, "saw cut loads the loop (load " + load.ToString("F2") + ", min pitch " + before.ToString("F2") + " -> " + minPitch.ToString("F2") + ", dt " + Time.unscaledDeltaTime.ToString("F3") + ")");
        double recoverEnd = EditorApplication.timeSinceStartup + 0.5;
        while (EditorApplication.timeSinceStartup < recoverEnd) { GameAudio.Hold(SfxLoop.Saw, 0.55f, 1.1f); yield return null; }
        Need(src.pitch > before * 0.97f, "loop pitch recovers after cut (" + src.pitch.ToString("F2") + ")");

        // Отпустили - затухает и останавливается.
        double releaseEnd = EditorApplication.timeSinceStartup + 1.0;
        while (EditorApplication.timeSinceStartup < releaseEnd) yield return null;
        Need(!src.isPlaying && src.volume <= 0.001f, "saw loop stops after release");

        // Огнемёт: петля держится без разрывов, пока горит пламя, и гаснет после.
        var flameSrc = (AudioSource)Field(loops.GetValue((int)SfxLoop.Flame), "source");
        double flameEnd = EditorApplication.timeSinceStartup + 0.6;
        // Разрыв считаем только если Hold звали каждый кадр игры (в batch шаг редактора может пропустить кадр,
        // а в игре Hold зовёт Update пламени каждый кадр).
        bool flameGap = false;
        int skipped = 0, firstFrame = Time.frameCount, lastFrame = Time.frameCount;
        float minVol = 1f;
        while (EditorApplication.timeSinceStartup < flameEnd)
        {
            GameAudio.Hold(SfxLoop.Flame, 0.6f);
            yield return null;
            bool everyFrame = Time.frameCount - lastFrame <= 1;
            if (!everyFrame) skipped++;
            lastFrame = Time.frameCount;
            // Первые 10 кадров - нарастание громкости.
            if (Time.frameCount - firstFrame > 10 && everyFrame) { minVol = Mathf.Min(minVol, flameSrc.volume); if (!flameSrc.isPlaying) flameGap = true; }
        }
        Need(flameSrc.isPlaying && !flameGap && flameSrc.volume > 0.2f, "flame loop plays continuously while held (clip " + flameSrc.clip.name + ", vol " + flameSrc.volume.ToString("F2")
            + ", min vol after fade-in " + minVol.ToString("F2") + ", frames " + (Time.frameCount - firstFrame) + ", skipped " + skipped + ")");
        double flameRelease = EditorApplication.timeSinceStartup + 0.8;
        while (EditorApplication.timeSinceStartup < flameRelease) yield return null;
        Need(!flameSrc.isPlaying, "flame loop stops after release");
        var setsLen = (AudioClip[][])Field(audio, "clips");
        Need(setsLen[(int)Sfx.Kill][0].length <= 0.41f && setsLen[(int)Sfx.Hit][0].length <= 0.21f,
            "kill/hit clips are short (kill " + setsLen[(int)Sfx.Kill][0].length.ToString("F2") + "s, hit " + setsLen[(int)Sfx.Hit][0].length.ToString("F2") + "s, arc " + setsLen[(int)Sfx.Arc][0].length.ToString("F2") + "s)");

        // Все эффекты проигрываются без ошибок.
        foreach (Sfx s in Enum.GetValues(typeof(Sfx))) GameAudio.Play(s, 0.1f);
        yield return null;
        Need(true, "all " + Enum.GetValues(typeof(Sfx)).Length + " effects played without errors");

        // Музыка забега.
        GameAudio.SetMusic(true);
        until = EditorApplication.timeSinceStartup + 20;
        while ((music.clip != gameTrack || !music.isPlaying || music.volume < audibleVolume) && EditorApplication.timeSinceStartup < until) yield return null;
        Need(music.clip == gameTrack && music.isPlaying && music.loop && music.volume >= audibleVolume, "Darkness Road replaces the menu loop");

        AutoShoter shooter = GameObject.FindGameObjectWithTag("Player").GetComponent<AutoShoter>();
        var shoot = typeof(AutoShoter).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
        var pelletCount = typeof(AutoShoter).GetField("currentPelletCount", BindingFlags.Instance | BindingFlags.NonPublic);
        var lastPlay = (float[])Field(audio, "lastPlay");
        var target = new GameObject("Audio check target");
        target.transform.position = shooter.transform.position + Vector3.forward * 5f;
        Sfx[] shotSounds = { Sfx.Shot, Sfx.AutoShot, Sfx.Shotgun };
        for (int i = 0; i < shotSounds.Length; i++)
        {
            Sfx sound = shotSounds[i];
            AudioClip recording = Resources.Load<AudioClip>("RecordedAudio/" + sound);
            Need(recording != null && sets[(int)sound][0] == recording, sound + " uses the selected recording");
            Need(shooter.SelectWeapon(i), "selected weapon " + i);
            if (i == 0) pelletCount.SetValue(shooter, 3);
            lastPlay[(int)sound] = float.NegativeInfinity;
            int nextVoice = (int)Field(audio, "nextVoice");
            shoot.Invoke(shooter, new object[] { target.transform });
            Need(lastPlay[(int)sound] == Time.unscaledTime && (int)Field(audio, "nextVoice") != nextVoice,
                "weapon fired with " + sound + (i == 0 ? " even with extra pellets" : ""));
        }
        UnityEngine.Object.Destroy(target);
    }

    static float[] Data(AudioClip clip)
    {
        var d = new float[clip.samples * clip.channels];
        clip.GetData(d, 0);
        return d;
    }

    static bool Report(StringBuilder sb, string name, AudioClip clip, bool loop, float minPeak)
    {
        float[] d = Data(clip);
        float peak = 0f, rms = 0f, maxStep = 0f;
        bool bad = false;
        for (int i = 0; i < d.Length; i++)
        {
            float v = d[i];
            if (float.IsNaN(v) || float.IsInfinity(v)) bad = true;
            peak = Mathf.Max(peak, Mathf.Abs(v));
            rms += v * v;
            if (i > 0) maxStep = Mathf.Max(maxStep, Mathf.Abs(v - d[i - 1]));
        }
        rms = Mathf.Sqrt(rms / d.Length);
        float seam = Mathf.Abs(d[0] - d[d.Length - 1]);
        bool seamOk = !loop || seam <= maxStep;
        bool pass = !bad && peak > minPeak && peak <= 1f && rms > 0.005f && seamOk;
        sb.AppendLine((pass ? "ok   " : "FAIL ") + name + " len=" + clip.length.ToString("F2") + "s rate=" + clip.frequency
            + " peak=" + peak.ToString("F2") + " rms=" + rms.ToString("F3")
            + (loop ? " seam=" + seam.ToString("F3") + " maxStep=" + maxStep.ToString("F3") : "") + (bad ? " NAN" : ""));
        WriteWav(Path.Combine(PreviewDir, name + ".wav"), d, clip.frequency);
        return pass;
    }

    // Варианты одного звука должны отличаться, иначе смысла в них нет.
    static bool Differ(StringBuilder sb, string name, AudioClip a, AudioClip b)
    {
        float[] x = Data(a), y = Data(b);
        int n = Mathf.Min(x.Length, y.Length);
        float diff = 0f;
        for (int i = 0; i < n; i++) diff += Mathf.Abs(x[i] - y[i]);
        bool ok = diff / n > 0.002f;
        if (!ok) sb.AppendLine("FAIL " + name + " variants identical");
        return ok;
    }

    static void WriteWav(string path, float[] d, int rate)
    {
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
