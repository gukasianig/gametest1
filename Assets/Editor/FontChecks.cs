using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Шрифт интерфейса (21.11.2026): GameUi.DefaultFont берёт Assets/Resources/Fonts/GameFont.ttf, а не встроенный LegacyRuntime;
// латиница, кириллица и служебные символы интерфейса (→ ★ ● ○ ► ■ ↑ ↓ ✓ и др.) получают глифы — свои или из fallback-шрифтов.
// Batch: -batchmode -quit -executeMethod FontChecks.RunBatch. Отчёт: Logs/font-results.txt.
public static class FontChecks
{
    private const string FontAsset = "Assets/Resources/Fonts/GameFont.ttf", Report = "Logs/font-results.txt";

    // Символы, которые реально выводит интерфейс (строки каталога и составные подписи в коде).
    private const string Symbols = "→←↑↓↔×•·★●○►◄▲■✓—…«»№%";

    public static void RunBatch()
    {
        bool ok = Run(out string report);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, report);
        Debug.Log(report);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    [MenuItem("Tools/Checks/Game font")]
    public static void RunMenu()
    {
        bool ok = Run(out string report);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    private static bool Run(out string report)
    {
        var text = new StringBuilder();
        bool ok = true;
        AssetDatabase.ImportAsset(FontAsset, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(FontAsset) as TrueTypeFontImporter;
        if (importer == null) { report = "FAIL: no TrueTypeFontImporter for " + FontAsset; return false; }
        text.AppendLine("Importer: mode=" + importer.fontTextureCase + ", fontNames=[" + string.Join(", ", importer.fontNames ?? new string[0]) +
            "], fallback assets=" + (importer.fontReferences != null ? importer.fontReferences.Length : 0) + ", includeFontData=" + importer.includeFontData);

        Font loaded = Resources.Load<Font>(GameUi.GameFontPath);
        Font used = GameUi.DefaultFont;
        bool fromResources = loaded != null && used == loaded;
        text.AppendLine((fromResources ? "PASS" : "FAIL") + ": Resources.Load(\"" + GameUi.GameFontPath + "\") = " + (loaded != null ? loaded.name : "null") +
            ", GameUi.DefaultFont = " + (used != null ? used.name : "null") + ", dynamic=" + (used != null && used.dynamic));
        ok &= fromResources;

        if (used != null)
        {
            ok &= Glyphs(text, used, "Latin", Range(0x20, 0x7E), true);
            ok &= Glyphs(text, used, "Cyrillic", Range(0x410, 0x44F) + "Ёё", true);
            Glyphs(text, used, "UI symbols", Symbols, false);   // нехватка символов — предупреждение, не провал
        }
        text.AppendLine(ok ? "PASS: Game font checks complete." : "FAIL: Game font checks failed.");
        report = text.ToString();
        return ok;
    }

    private static string Range(int from, int to)
    {
        var s = new StringBuilder();
        for (int c = from; c <= to; c++) s.Append((char)c);
        return s.ToString();
    }

    // Растеризация как у Text: RequestCharactersInTexture, затем GetCharacterInfo. HasCharacter у динамического шрифта
// учитывает fallback-шрифты (21.11: в Jura нет → ★ ● ○ ■ и др., а HasCharacter = true), поэтому «own» — не гарантия файла.
    private static bool Glyphs(StringBuilder text, Font font, string label, string chars, bool required)
    {
        const int size = 24;
        font.RequestCharactersInTexture(chars, size, FontStyle.Normal);
        var own = new List<char>();
        var rendered = new List<char>();
        var missing = new List<char>();
        foreach (char c in chars)
        {
            if (c == ' ') continue;
            bool inFile = font.HasCharacter(c);
            bool hasInfo = font.GetCharacterInfo(c, out CharacterInfo info, size, FontStyle.Normal) && (info.glyphWidth > 0 || info.advance > 0);
            if (inFile) own.Add(c); else if (hasInfo) rendered.Add(c); else missing.Add(c);
        }
        bool pass = missing.Count == 0;
        text.AppendLine((pass ? "PASS" : required ? "FAIL" : "WARN") + ": " + label + " — own glyphs " + own.Count + ", via fallback " + rendered.Count +
            (rendered.Count > 0 ? " (" + new string(rendered.ToArray()) + ")" : "") + ", missing " + missing.Count +
            (missing.Count > 0 ? " (" + new string(missing.ToArray()) + ")" : ""));
        return pass || !required;
    }
}
