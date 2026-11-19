using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public enum GameLanguage { English, Russian }

// Canonical Russian strings are composed before translation at the UI boundary. This keeps
// saved data, object names and cached catalogs independent of the selected display language.
public static class GameText
{
    [Serializable] public class Entry { public string id, ru, en; }
    [Serializable] private class Catalog { public Entry[] entries; }
    [Serializable] private class Preferences { public string language = "en"; }
    private static Dictionary<string, string> translations;
    private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    private static Regex phrases;
    private static bool loaded;
    private static GameLanguage language;
    public static event Action LanguageChanged;

    public static string SettingsPath => Path.Combine(Application.persistentDataPath, "settings.json");
    public static GameLanguage Language { get { Load(); return language; } }
    public static string LanguageName => Language == GameLanguage.English ? "English" : "Русский";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        loaded = false;
        cache.Clear();
        LanguageChanged = null;
    }

    private static void Load()
    {
        if (loaded) return;
        language = GameLanguage.English;
        try
        {
            if (File.Exists(SettingsPath))
            {
                Preferences preferences = JsonUtility.FromJson<Preferences>(File.ReadAllText(SettingsPath));
                if (preferences != null && preferences.language == "ru") language = GameLanguage.Russian;
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            Debug.LogWarning("Cannot load language settings: " + exception.Message);
        }
        if (translations == null) LoadCatalog();
        loaded = true;
    }

    private static void LoadCatalog()
    {
        TextAsset asset = Resources.Load<TextAsset>("Localization/game-text");
        if (asset == null) throw new InvalidOperationException("Missing Localization/game-text.json");
        Catalog catalog = JsonUtility.FromJson<Catalog>(asset.text);
        translations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Entry entry in catalog.entries)
        {
            if (string.IsNullOrEmpty(entry.id) || string.IsNullOrEmpty(entry.ru) || string.IsNullOrEmpty(entry.en))
                throw new InvalidOperationException("Incomplete bilingual entry: " + entry.id);
            translations[entry.ru] = entry.en;
        }
        // Headers and weapon details sometimes uppercase the canonical text before display.
        foreach (var pair in translations.ToArray())
        {
            AddVariant(pair.Key.ToUpperInvariant(), pair.Value.ToUpperInvariant());
            AddVariant(pair.Key.ToLowerInvariant(), pair.Value.ToLowerInvariant());
        }
        // Lookahead also finds overlapping phrases: a leading-space suffix must not hide
        // a longer sentence starting one character later. Prefer the most specific phrase.
        phrases = new Regex("(?=(?<phrase>" + string.Join("|", translations.Keys.OrderByDescending(key => key.Length)
            .ThenBy(key => key, StringComparer.Ordinal).Select(PhrasePattern)) + "))", RegexOptions.CultureInvariant);
    }

    private static string PhrasePattern(string source)
    {
        // A unit fragment such as space + Cyrillic m must not consume the first letter of
        // the next sentence (e.g. a description beginning with Mines in Russian).
        bool firstLetter = IsCyrillic(source[0]), lastLetter = IsCyrillic(source[source.Length - 1]);
        return (firstLetter ? "(?<![\\u0400-\\u04ff])" : "") + Regex.Escape(source) +
            (lastLetter ? "(?![\\u0400-\\u04ff])" : "");
    }

    private static bool IsCyrillic(char value) { return value >= '\u0400' && value <= '\u04ff'; }

    private static void AddVariant(string source, string value)
    {
        if (!translations.ContainsKey(source)) translations.Add(source, value);
    }

    public static string Source(string id)
    {
        // MonoBehaviour static catalogs may initialize on Unity's serialization thread.
        // Source lookup must never touch Resources, persistentDataPath or other Unity APIs.
        if (!GameTextSources.Values.TryGetValue(id, out string value)) throw new ArgumentException("Unknown game text: " + id);
        return value;
    }

    public static string Translate(string source)
    {
        if (string.IsNullOrEmpty(source)) return source ?? "";
        Load();
        if (language == GameLanguage.Russian) return source;
        if (cache.TryGetValue(source, out string found)) return found;
        bool cyrillic = false;
        foreach (char c in source) if (c >= '\u0400' && c <= '\u04ff') { cyrillic = true; break; }
        if (!cyrillic) return source;
        var occupied = new bool[source.Length];
        var selected = new List<Group>();
        foreach (Group phrase in phrases.Matches(source).Cast<Match>().Select(match => match.Groups["phrase"])
            .OrderByDescending(group => group.Length).ThenBy(group => group.Index))
        {
            bool overlaps = false;
            for (int i = phrase.Index; i < phrase.Index + phrase.Length; i++) if (occupied[i]) { overlaps = true; break; }
            if (overlaps) continue;
            for (int i = phrase.Index; i < phrase.Index + phrase.Length; i++) occupied[i] = true;
            selected.Add(phrase);
        }
        var text = new StringBuilder(source.Length);
        int offset = 0;
        foreach (Group phrase in selected.OrderBy(group => group.Index))
        {
            text.Append(source, offset, phrase.Index - offset);
            text.Append(translations[phrase.Value]);
            offset = phrase.Index + phrase.Length;
        }
        text.Append(source, offset, source.Length - offset);
        string result = text.ToString();
        if (cache.Count >= 1024) cache.Clear();
        cache[source] = result;
        return result;
    }

    public static string EnemyName(string internalName)
    {
        string id = "enemy." + (internalName ?? "").ToLowerInvariant();
        return GameTextSources.Values.TryGetValue(id, out string source) ? source : internalName;
    }

    public static void SetLanguage(GameLanguage value)
    {
        if (value != GameLanguage.English && value != GameLanguage.Russian) throw new ArgumentOutOfRangeException(nameof(value));
        Load();
        language = value;
        Save();
        cache.Clear();
        LanguageChanged?.Invoke();
    }

    public static void Save()
    {
        Load();
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(new Preferences
                { language = language == GameLanguage.Russian ? "ru" : "en" }, true));
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning("Cannot save language settings: " + exception.Message);
        }
    }
}
