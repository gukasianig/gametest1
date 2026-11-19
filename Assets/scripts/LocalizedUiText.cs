using UnityEngine;
using UnityEngine.UI;

// Receives canonical source text even when callers hold a Text reference. Language changes
// refresh visible labels immediately; hidden screens refresh when opened, including at timeScale 0.
public class LocalizedUiText : Text
{
    [SerializeField, TextArea] private string sourceText = "";
    public string SourceText => sourceText;

    public override string text
    {
        get => base.text;
        set
        {
            sourceText = value ?? "";
            RefreshLanguage();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        GameText.LanguageChanged += RefreshLanguage;
        RefreshLanguage();
    }

    protected override void OnDisable()
    {
        GameText.LanguageChanged -= RefreshLanguage;
        base.OnDisable();
    }

    private void RefreshLanguage()
    {
        base.text = GameText.Translate(sourceText);
    }
}
