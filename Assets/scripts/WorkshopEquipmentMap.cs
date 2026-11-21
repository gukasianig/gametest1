using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class WorkshopTree
{
    // Раздел проектов до 2-го забега (WorkshopProjects.SectionUnlocked): карточки проектов скрыты, вместо них плашка-замок.
    private readonly List<GameObject> projectTiles = new List<GameObject>();
    private RectTransform projectsLock;
    private Text projectsLockTitle;
    private readonly Image[] projectsLockPips = new Image[WorkshopProjects.SectionRuns];

    private void BuildProjectsLock()
    {
        projectsLock = EquipmentCard("Projects lock", 505f, 905f, 640f, 220f).rectTransform;
        var badge = Fixed("Projects lock key", projectsLock, 78f, 110f, 84f, 84f).gameObject.AddComponent<Image>();
        badge.color = new Color(.03f, .09f, .12f, 1f);
        badge.raycastTarget = false;
        UiArtwork.Round(badge);
        UiArtwork.Picture(badge.transform, KeySprite(), new Vector2(.16f, .16f), new Vector2(.84f, .84f)).raycastTarget = false;
        projectsLockTitle = LabelAt(projectsLock, "", 375f, 70f, 470f, 56f, 20, Gold);
        projectsLockTitle.transform.parent.name = "Projects lock title";
        Text hint = LabelAt(projectsLock, GameText.Source("projects.locked.hint"), 375f, 132f, 470f, 48f, 15, new Color(.72f, .82f, .88f));
        hint.transform.parent.name = "Projects lock hint";
        for (int i = 0; i < projectsLockPips.Length; i++)
        {
            projectsLockPips[i] = Fixed("Projects lock run " + i, projectsLock, 330f + i * 90f, 182f, 80f, 10f).gameObject.AddComponent<Image>();
            projectsLockPips[i].raycastTarget = false;
            UiArtwork.Round(projectsLockPips[i]);
        }
    }

    private void RefreshProjectsLock()
    {
        bool open = WorkshopProjects.SectionUnlocked;
        foreach (GameObject tile in projectTiles) tile.SetActive(open);
        if (projectsLock == null) return;
        projectsLock.gameObject.SetActive(!open);
        if (open) return;
        int runs = Mathf.Clamp(MetaProgress.Data.runs, 0, WorkshopProjects.SectionRuns);
        projectsLockTitle.text = GameText.Source("projects.locked.title") + runs + "/" + WorkshopProjects.SectionRuns + ")";
        for (int i = 0; i < projectsLockPips.Length; i++) projectsLockPips[i].color = i < runs ? GameUi.Accent : new Color(.1f, .19f, .23f);
    }

    private Image EquipmentCard(string name, float x, float y, float width, float height)
    {
        var rect = Fixed(name, board, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.02f, .06f, .09f, .55f);
        image.raycastTarget = false;
        UiArtwork.Round(image);
        var border = new GameObject("Equipment card border", typeof(RectTransform), typeof(WorkshopCardBorder)).GetComponent<WorkshopCardBorder>();
        border.transform.SetParent(rect, false);
        border.rectTransform.anchorMin = Vector2.zero;
        border.rectTransform.anchorMax = Vector2.one;
        border.rectTransform.offsetMin = border.rectTransform.offsetMax = Vector2.zero;
        border.color = new Color(.3f, .6f, .7f, .3f);
        border.raycastTarget = false;
        return image;
    }

    private void BuildEquipmentMap()
    {
        boardHeight = 1180f;
        rootY = boardHeight * .5f;
        Scroll.content = content;
        board = Fixed("Tree board", content, 0, 0, BoardWidth, boardHeight);
        board.pivot = new Vector2(0, 1);
        board.anchoredPosition = Vector2.zero;
        var aura = Fixed("Root aura", board, RootX, rootY, 220, 220);
        aura.localScale = Vector3.one * .8f;
        aura.gameObject.AddComponent<WorkshopRootAura>().raycastTarget = false;
        rootPicture = Fixed("Root", board, RootX, rootY, 100, 100).gameObject.AddComponent<Image>();
        rootPicture.sprite = Art("root_workshop");
        rootPicture.preserveAspect = true;
        rootPicture.raycastTarget = false;
        var hubBase = Fixed("Hub platform", board, RootX, rootY + 45f, 150, 50);
        hubBase.SetAsFirstSibling();
        hubBase.gameObject.AddComponent<WorkshopPlatform>().raycastTarget = false;
        Header(board, GameText.Source("challengescreen.076"), 425f, 70f, 300f, GameUi.Accent);
        Header(board, GameText.Source("weaponselectscreen.439"), WeaponX + WeaponPitch, 40f, 340f, Gold);
        Header(board, GameText.Source("projects.heading"), 505f, 650f, 400f, GameUi.Accent);
        for (int i = 0; i < MetaProgress.Catalog.Length; i++)
        {
            float x = 260f + i % 2 * 330f, y = 220f + i / 2 * 225f;
            var tile = EquipmentCard("Hero tile " + i, x, y + 5f, 295f, 200f);
            var upgrade = MetaProgress.Catalog[i];
            AddNode(new Node { body = upgrade, title = upgrade.title, frame = tile.rectTransform }, x, y, 92f, UiArtwork.Upgrade(upgrade.effect), board);
        }
        int slot = 0;
        for (int i = 0; shooter.weapons != null && i < shooter.weapons.Length; i++)
        {
            if (shooter.weapons[i] == null || !shooter.weapons[i].IsSelectable) continue;
            float x = WeaponX + slot % WeaponColumns * WeaponPitch;
            float y = WeaponTop + slot / WeaponColumns * WeaponRowStep;
            slot++;
            var group = EquipmentCard("Weapon group " + i, x, y + 120f, GroupWidth, 240f);
            weaponGroups.Add(group);
            var parent = group.rectTransform;
            // 21.11.2026: оружие без своего дерева веток (WeaponElements.IsLocked) показывается одним узлом по центру карточки,
            // без замочков-заглушек. Как только HasBranches вернёт true, узлы веток появятся сами.
            bool keyed = WeaponElements.IsLocked(i);
            AddNode(new Node { weapon = i, title = WeaponTitle(i), frame = parent }, GroupWidth * .5f, keyed ? 125f : 80f, 82f, WeaponIcon(i), parent);
            if (keyed) continue;
            bool branched = WeaponElements.HasBranches(i);
            int first = branched ? (int)WeaponElements.FirstBranch(i) : 1;
            int last = branched ? (int)WeaponElements.LastBranch(i) : 4;
            for (int e = first; e <= last; e++)
            {
                float offset = branched ? (e - first - 1) * 66f : (e - 2.5f) * ElementStep;
                var element = (WeaponElement)e;
                AddNode(new Node { weapon = i, element = element, title = WeaponTitle(i) + ": " + WeaponElements.Names[e], frame = parent },
                    GroupWidth * .5f + offset, 168f, 40f, Art(WeaponElements.Icons[e]), parent);
            }
        }
        UpgradeEffect[] icons = { UpgradeEffect.DashCooldown, UpgradeEffect.Pickup, UpgradeEffect.Luck, UpgradeEffect.DroneCount, UpgradeEffect.Damage };
        for (int i = 0; i < WorkshopProjects.Ids.Length; i++)
        {
            float x = i < 3 ? 235f + i * 270f : 370f + (i - 3) * 270f;
            float y = i < 3 ? 810f : 1030f;
            var projectTile = EquipmentCard("Project tile " + i, x, y + 5f, 250f, 190f);
            projectTiles.Add(projectTile.gameObject);
            AddNode(new Node { project = WorkshopProjects.Ids[i], title = WorkshopProjects.Title(WorkshopProjects.Ids[i]), frame = projectTile.rectTransform },
                x, y, 92f, UiArtwork.Upgrade(icons[i]), board);
        }
        BuildProjectsLock();
    }
}
