using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Opt-in presentation hooks: a one-shot request changes only the current Play session.
[InitializeOnLoad]
public static class UiArtworkPreview
{
    private const string Request = "Logs/ui-art-preview.request";
    private static double nextPoll;
    static UiArtworkPreview()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode && File.Exists(Request)) File.Delete(Request);
        };
    }

    private static void Poll()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.25;
        if (!File.Exists(Request) || PlayerProgression.Instance == null || GameSession.Instance == null) return;
        StartMenu menu = PlayerProgression.Instance.GetComponent<StartMenu>();
        if (GameSession.Instance.State == GameRunState.Menu && !menu.WaitingForWeapon) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            for (int i = 0; i < 16; i++)
            {
                Sprite sprite = UiArtwork.Icon(i);
                if (sprite == null || sprite.rect.width <= 0 || sprite.rect.height <= 0)
                    throw new Exception("Missing artwork sprite " + i);
            }
            PlayerProgression progression = PlayerProgression.Instance;
            if (command == "weapons") menu.ShowWeapons();
            else if (command == "upgrades")
            {
                if (GameSession.Instance.State == GameRunState.Menu) menu.SendMessage("BeginGame", 1);
                if (GameSession.Instance.State != GameRunState.Playing) throw new Exception("Preview requires active combat.");
                int amount = -progression.Experience;
                for (int level = progression.Level; level < Mathf.Max(3, progression.Level + 1); level++)
                    amount += progression.settings.ExperienceForLevel(level);
                progression.AddExperience(amount);
                List<UpgradeDefinition> offered = (List<UpgradeDefinition>)progression.Offered;
                offered.Clear();
                foreach (UpgradeEffect effect in new[] { UpgradeEffect.Damage, UpgradeEffect.GrenadeDamage, UpgradeEffect.MaxHealth })
                {
                    UpgradeDefinition entry = progression.catalog.entries.Find(item => item.effect == effect && progression.Available(item));
                    if (entry != null) offered.Add(entry);
                }
                if (offered.Count != 3) throw new Exception("Cannot preview all three rarity cards in this run.");
                progression.SendMessage("RefreshCards");
            }
            else if (command == "health")
            {
                if (GameSession.Instance.State == GameRunState.Menu) menu.SendMessage("BeginGame", 1);
                int attempts = 0;
                while (progression.Choosing && attempts++ < 20)
                    if (!progression.SelectOfferedUpgrade(0)) throw new Exception("Could not close pending choice.");
                PlayerHealth health = progression.GetComponent<PlayerHealth>();
                health.TakeDamage(health.CurrentHP - Mathf.RoundToInt(health.maxHP * 0.2f));
            }
            else if (command == "reset") GameSession.Instance.Reload(false);
            else throw new Exception("Unknown presentation request.");
            File.AppendAllText("Logs/ui-art-preview-report.txt", "PASS: 16 atlas sprites; presentation " + command + "\n");
        }
        catch (Exception exception)
        {
            File.AppendAllText("Logs/ui-art-preview-report.txt", "FAIL: " + exception + "\n");
            Debug.LogException(exception);
        }
    }
}
