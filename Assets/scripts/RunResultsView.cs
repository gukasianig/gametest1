using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Created once at the end of the run; scroll areas keep large builds readable.
public static class RunResultsView
{
    private static readonly Color Muted = new Color(.56f, .66f, .73f);
    private static readonly Color Plate = new Color(.045f, .075f, .10f);
    private static readonly Color Warm = new Color(1f, .57f, .35f);

    private static Text Label(Transform parent, string value, Vector2 min, Vector2 max, int size, Color? color = null)
    {
        Text label = GameUi.Label(parent, value, min, max, size, color);
        label.alignment = TextAnchor.MiddleLeft;
        return label;
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform panel = GameUi.Box(parent, name, min, max, Plate);
        UiArtwork.Round(panel.GetComponent<Image>());
        return panel;
    }

    private static void Metric(Transform parent, int index, string caption, string value, Color color)
    {
        float left = .05f + index * .182f;
        var tile = Panel(parent, "Metric " + index, new Vector2(left, .68f), new Vector2(left + .172f, .81f));
        GameUi.Box(tile, "Accent", new Vector2(0, .15f), new Vector2(.016f, .85f), color);
        Label(tile, caption, new Vector2(.07f, .57f), new Vector2(.97f, .96f), 12, Muted);
        Label(tile, value, new Vector2(.07f, .05f), new Vector2(.97f, .65f), 30, color).fontStyle = FontStyle.Bold;
    }

    private static RectTransform List(Transform parent, string title, Vector2 min, Vector2 max)
    {
        var panel = Panel(parent, title, min, max);
        Label(panel, title, new Vector2(.025f, .84f), new Vector2(.97f, 1), 16, GameUi.Accent).fontStyle = FontStyle.Bold;
        var viewport = GameUi.Box(panel, "Viewport", new Vector2(.025f, .04f), new Vector2(.975f, .83f), Color.clear, true);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = Vector2.zero;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32;
        var rail = GameUi.Box(panel, "Scroll rail", new Vector2(.978f, .055f), new Vector2(.988f, .82f), new Color(.10f, .16f, .19f), true);
        var handle = GameUi.Box(rail, "Handle", Vector2.zero, Vector2.one, new Color(.26f, .43f, .46f), true);
        var scrollbar = rail.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }

    private static RectTransform Row(RectTransform content, ref float y, float height, string name)
    {
        var row = Panel(content, name, Vector2.zero, Vector2.one);
        row.anchorMin = new Vector2(0, 1);
        row.anchorMax = Vector2.one;
        row.pivot = new Vector2(.5f, 1);
        row.offsetMin = new Vector2(0, -y - height);
        row.offsetMax = new Vector2(-5, -y);
        row.GetComponent<Image>().color = new Color(.075f, .115f, .145f);
        y += height + 6;
        content.sizeDelta = new Vector2(0, y);
        return row;
    }

    public static void Show(RectTransform root, GameSession session, RunStatistics stats, PlayerProgression progression, WaveManager waves)
    {
        RunReport report = stats != null ? stats.Report : new RunReport();
        Metric(root, 0, GameText.Source("results.time"), RunReport.Duration(session.Elapsed), Color.white);
        Metric(root, 1, GameText.Source("results.kills"), (stats != null ? stats.Kills : 0).ToString("N0"), Color.white);
        Metric(root, 2, GameText.Source("results.levelwave"), progression.Level + " / " + (waves != null ? waves.wave : 0), Color.white);
        Metric(root, 3, GameText.Source("results.dealt"), (stats != null ? stats.DamageDealt : 0).ToString("N0"), GameUi.Accent);
        Metric(root, 4, GameText.Source("results.bosstime"), report.bosses.Count == 0 ? "—" : RunReport.Duration(report.BossSeconds), Warm);
        Label(root, WorkshopProjects.Target >= 0 ? GameText.Source("projects.goal.prefix") + WorkshopProjects.Goal : GameText.Source("results.hint"), new Vector2(.05f, .82f), new Vector2(.95f, .87f), 13, Muted);

        var definitions = new Dictionary<string, UpgradeDefinition>();
        foreach (var definition in RunReport.Definitions(progression)) definitions[definition.id] = definition;
        var builds = List(root, GameText.Source("results.build"), new Vector2(.05f, .25f), new Vector2(.55f, .65f));
        float y = 0;
        for (int i = 0; i < report.heroes.Count; i++)
        {
            var hero = report.heroes[i];
            var shooter = session.GetComponent<AutoShoter>();
            WeaponData weapon = hero.weapon >= 0 && hero.weapon < shooter.weapons.Length ? shooter.weapons[hero.weapon] : null;
            string title = weapon != null ? WeaponSelectScreen.Title(weapon, hero.weapon) : "—";
            if (report.heroes.Count > 1) title = GameText.Source("coopmode.107") + (i + 1) + " · " + title;
            var header = Row(builds, ref y, 54, "Weapon");
            var weaponArt = weapon != null ? Resources.Load<Sprite>("UI/Weapons/" + ModelSkins.WeaponKey(weapon, hero.weapon)) : null;
            if (weaponArt != null) UiArtwork.Picture(header, weaponArt, new Vector2(.015f, .05f), new Vector2(.14f, .95f));
            float textLeft = weaponArt != null ? .15f : .02f;
            Label(header, title, new Vector2(textLeft, .44f), new Vector2(.98f, 1), 21, GameUi.Accent).fontStyle = FontStyle.Bold;
            string branch = hero.branch == WeaponElement.None ? GameText.Source("results.base") :
                WeaponElements.Names[(int)hero.branch] + " · " + hero.branchRank + "/" + WeaponElements.MaxRank;
            if (hero.evolved) branch += " · " + GameText.Source("results.evolved");
            Label(header, branch, new Vector2(textLeft, 0), new Vector2(.98f, .48f), 14, Muted);
            if (hero.upgrades.Count == 0)
                Label(Row(builds, ref y, 40, "Empty"), GameText.Source("results.noupgrades"), Vector2.zero, Vector2.one, 16, Muted);
            foreach (var chosen in hero.upgrades)
            {
                if (!definitions.TryGetValue(chosen.id, out var definition)) continue;
                var row = Row(builds, ref y, 42, "Upgrade " + chosen.id);
                Color rarity = UpgradeCatalog.RarityColor(definition.rarity);
                var icon = UiArtwork.Picture(row, definition.effect == UpgradeEffect.Evolution && definition.icon != null ? definition.icon : UiArtwork.Upgrade(definition.effect),
                    new Vector2(.015f, .12f), new Vector2(.09f, .88f));
                icon.preserveAspect = true;
                if (icon.sprite == null) icon.color = Color.clear;
                GameUi.Box(row, "Rarity", Vector2.zero, new Vector2(.006f, 1), rarity);
                Label(row, definition.title, new Vector2(.10f, 0), new Vector2(.83f, 1), 17);
                var rank = Label(row, "×" + chosen.rank, new Vector2(.82f, 0), Vector2.one, 18, rarity);
                rank.alignment = TextAnchor.MiddleRight;
            }
        }

        var damage = List(root, GameText.Source(report.heroes.Count > 1 ? "results.incomingteam" : "results.incoming"),
            new Vector2(.57f, .25f), new Vector2(.95f, .65f));
        y = 0;
        var total = Row(damage, ref y, 44, "Total damage");
        Label(total, GameText.Source("results.hplost"), Vector2.zero, new Vector2(.7f, 1), 15, Muted);
        Label(total, report.DamageTaken.ToString("N0"), new Vector2(.67f, 0), Vector2.one, 24, Warm).alignment = TextAnchor.MiddleRight;
        if (report.incoming.Count == 0)
            Label(Row(damage, ref y, 44, "No damage"), GameText.Source("results.nodamage"), Vector2.zero, Vector2.one, 17, GameUi.Accent);
        foreach (var hit in report.incoming)
        {
            var row = Row(damage, ref y, 48, "Damage " + hit.cause);
            Label(row, GameText.Source("death.cause." + hit.cause.ToString().ToLowerInvariant()), new Vector2(0, .3f), new Vector2(.68f, 1), 15);
            float share = report.DamageTaken > 0 ? (float)hit.amount / report.DamageTaken : 0;
            Label(row, hit.amount.ToString("N0") + " · " + (share * 100).ToString("0") + "%", new Vector2(.65f, .3f), Vector2.one, 15, Warm).alignment = TextAnchor.MiddleRight;
            var track = GameUi.Box(row, "Track", new Vector2(.025f, .12f), new Vector2(.975f, .2f), new Color(.12f, .18f, .21f));
            GameUi.Box(track, "Share", Vector2.zero, new Vector2(share, 1), Warm);
        }
        foreach (var boss in report.bosses)
        {
            var row = Row(damage, ref y, 50, "Boss encounter");
            Label(row, BossController.Titles[(int)boss.kind], new Vector2(0, .4f), Vector2.one, 16, GameUi.Accent);
            Label(row, RunReport.Duration(boss.seconds) + " · " + GameText.Source(boss.defeated ? "results.bosswon" : "results.bossunfinished"),
                Vector2.zero, new Vector2(1, .5f), 14, Muted);
        }
    }
}
