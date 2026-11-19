using System;
using UnityEngine;

// Menu and pause each own a settings panel. Re-read values on opening either instance.
public class SettingsPanelRefresh : MonoBehaviour
{
    public Action Refresh;

    private void OnEnable()
    {
        GameText.LanguageChanged += RefreshValues;
        RefreshValues();
    }

    private void OnDisable()
    {
        GameText.LanguageChanged -= RefreshValues;
    }

    private void RefreshValues() { Refresh?.Invoke(); }
}
