using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public partial class WorkshopTree
{
    private RectTransform equipmentPage;
    private readonly List<GameObject> originalDetails = new List<GameObject>();
    private Text equipmentTitle, tuningLevel, tuningDamage, tuningNote, tuningBuyText, branchDetails, branchBuyText, branchEquipText;
    private Image equipmentPreview;
    private Button tuningBuy, branchBuy, branchEquip;
    private readonly Image[] tuningSegments = new Image[WeaponTuning.MaxRank];
    private readonly Button[] branchTabs = new Button[4];
    private readonly Image[] branchIcons = new Image[4];
    private readonly Text[] branchNames = new Text[4];
    private int detailWeapon = -1;
    private WeaponElement detailElement;

    private Text EquipmentLabel(Transform parent, string text, float bottom, float top, int size, Color? tint = null)
    {
        Text label = GameUi.Label(parent, text, new Vector2(.025f, bottom), new Vector2(.975f, top), size, tint);
        label.resizeTextMinSize = Mathf.Min(10, size);
        label.rectTransform.offsetMin = new Vector2(6, 1);
        label.rectTransform.offsetMax = new Vector2(-6, -1);
        return label;
    }

    private void BuildEquipmentPanel(RectTransform panel)
    {
        foreach (Transform child in panel) originalDetails.Add(child.gameObject);
        equipmentPage = GameUi.Box(panel, "Weapon enhancement panel", Vector2.zero, Vector2.one, Color.clear);
        equipmentTitle = EquipmentLabel(equipmentPage, "", .91f, .99f, 22, GameUi.Accent);
        equipmentPreview = UiArtwork.Picture(equipmentPage, null, new Vector2(.2f, .765f), new Vector2(.8f, .91f));
        var platform = new GameObject("Preview platform", typeof(RectTransform), typeof(WorkshopPlatform)).GetComponent<WorkshopPlatform>();
        platform.transform.SetParent(equipmentPage, false);
        platform.rectTransform.anchorMin = new Vector2(.24f, .755f);
        platform.rectTransform.anchorMax = new Vector2(.76f, .80f);
        platform.rectTransform.offsetMin = platform.rectTransform.offsetMax = Vector2.zero;
        platform.raycastTarget = false;
        var block = GameUi.Box(equipmentPage, "Weapon enhancement block", new Vector2(.035f, .44f), new Vector2(.965f, .75f), new Color(.02f, .11f, .14f, .85f));
        UiArtwork.Round(block.GetComponent<Image>());
        EquipmentLabel(equipmentPage, GameText.Source("tuning.heading"), .695f, .75f, 17, GameUi.Accent);
        tuningLevel = EquipmentLabel(equipmentPage, "", .65f, .695f, 15);
        for (int i = 0; i < tuningSegments.Length; i++)
        {
            tuningSegments[i] = GameUi.Box(equipmentPage, "Weapon rank segment " + i,
                new Vector2(.1f + i * .164f, .614f), new Vector2(.24f + i * .164f, .636f), Dim).GetComponent<Image>();
            UiArtwork.Round(tuningSegments[i]);
        }
        tuningDamage = EquipmentLabel(equipmentPage, "", .554f, .61f, 18);
        tuningNote = EquipmentLabel(equipmentPage, "", .512f, .554f, 12, new Color(.7f, .84f, .88f));
        tuningBuy = GameUi.Button(equipmentPage, "", new Vector2(.07f, .448f), new Vector2(.93f, .512f), PurchaseWeaponTuning);
        tuningBuy.name = "Weapon enhancement purchase";
        tuningBuyText = tuningBuy.GetComponentInChildren<Text>(); tuningBuyText.resizeTextMaxSize = 17;
        UiArtwork.Round(tuningBuy.GetComponent<Image>());
        EquipmentLabel(equipmentPage, GameText.Source("workshop.branch.caption"), .389f, .439f, 17, Gold);
        for (int i = 0; i < branchTabs.Length; i++)
        {
            int tab = i;
            var button = GameUi.Button(equipmentPage, "", Vector2.zero, Vector2.one, () => SelectEquipmentBranch(tab));
            button.name = "Weapon branch tab " + i;
            branchTabs[i] = button;
            branchNames[i] = button.GetComponentInChildren<Text>();
            branchNames[i].resizeTextMaxSize = 12; branchNames[i].resizeTextMinSize = 9;
            branchNames[i].rectTransform.anchorMax = new Vector2(1f, .38f);
            branchNames[i].rectTransform.offsetMin = new Vector2(1, 1); branchNames[i].rectTransform.offsetMax = new Vector2(-1, -1);
            branchIcons[i] = UiArtwork.Picture(button.transform, null, new Vector2(.15f, .40f), new Vector2(.85f, .98f));
            UiArtwork.Round(button.GetComponent<Image>());
        }
        branchDetails = EquipmentLabel(equipmentPage, "", .145f, .285f, 14);
        branchBuy = GameUi.Button(equipmentPage, "", new Vector2(.07f, .077f), new Vector2(.93f, .141f), PurchaseEquipmentBranch);
        branchBuy.name = "Weapon branch purchase";
        branchBuyText = branchBuy.GetComponentInChildren<Text>(); branchBuyText.resizeTextMaxSize = 16;
        UiArtwork.Round(branchBuy.GetComponent<Image>());
        branchEquip = GameUi.Button(equipmentPage, "", new Vector2(.07f, .010f), new Vector2(.93f, .070f), EquipEquipmentBranch);
        branchEquip.name = "Weapon branch equip";
        branchEquipText = branchEquip.GetComponentInChildren<Text>(); branchEquipText.resizeTextMaxSize = 14;
        UiArtwork.Round(branchEquip.GetComponent<Image>());
        equipmentPage.gameObject.SetActive(false);
    }

    private void SetEquipmentPage(bool visible)
    {
        if (equipmentPage == null || equipmentPage.gameObject.activeSelf == visible) return;
        foreach (var item in originalDetails) item.SetActive(!visible);
        equipmentPage.gameObject.SetActive(visible);
    }

    private WeaponElement TabElement(int tab)
    {
        int first = WeaponElements.HasBranches(detailWeapon) ? (int)WeaponElements.FirstBranch(detailWeapon) : 1;
        return (WeaponElement)(first + tab);
    }

    private void SelectEquipmentBranch(int tab)
    {
        if (detailWeapon < 0) return;
        detailElement = TabElement(tab);
        Node node = nodes.Find(n => n.weapon == detailWeapon && n.element == detailElement);
        if (node == null) return;
        selected = node;
        Refresh();
    }

    private void RefreshEquipmentDetails()
    {
        int weapon = selected.weapon;
        bool unlocked = MetaProgress.IsWeaponUnlocked(weapon);
        if (detailWeapon != weapon)
        {
            detailWeapon = weapon;
            detailElement = WeaponElements.Equipped(weapon);
            if (!WeaponElements.Valid(weapon, detailElement)) detailElement = TabElement(0);
        }
        if (selected.element != WeaponElement.None) detailElement = selected.element;
        equipmentTitle.text = WeaponTitle(weapon);
        equipmentPreview.sprite = WeaponIcon(weapon);
        int rank = WeaponTuning.Rank(weapon);
        tuningLevel.text = GameText.Source("playerprogression.255") + rank + (rank < WeaponTuning.MaxRank && unlocked ? " → " + (rank + 1) : "") + " / " + WeaponTuning.MaxRank;
        float current = shooter.weapons[weapon].damage * WeaponTuning.Multiplier(weapon);
        float next = shooter.weapons[weapon].damage * (1f + .1f * Mathf.Min(WeaponTuning.MaxRank, rank + 1));
        tuningDamage.text = GameText.Source("tuning.damage") + current.ToString("0.#", CultureInfo.InvariantCulture) +
            (rank < WeaponTuning.MaxRank && unlocked ? " → <color=#69F3AB>" + next.ToString("0.#", CultureInfo.InvariantCulture) + "</color>" : "");
        tuningNote.text = !unlocked ? GameText.Source("tuning.unlock.first") : rank == WeaponTuning.MaxRank ? GameText.Source("workshoptree.473") :
            WeaponTuning.CanBuy(weapon) ? GameText.Source("tuning.permanent") : GameText.Source("workshoptree.475") + (WeaponTuning.Cost(rank) - MetaProgress.Currency) + GameText.Source("workshoptree.476");
        MetaUnlock unlock = MetaProgress.FindUnlock(MetaProgress.WeaponKey(weapon));
        tuningBuyText.text = !unlocked ? GameText.Source("workshoptree.481") + (unlock != null ? unlock.cost : 0) : rank == WeaponTuning.MaxRank ? GameText.Source("workshoptree.473") : GameText.Source("workshoptree.474") + WeaponTuning.Cost(rank);
        tuningBuy.interactable = unlocked ? WeaponTuning.CanBuy(weapon) : unlock != null && MetaProgress.Currency >= unlock.cost;
        for (int i = 0; i < tuningSegments.Length; i++) tuningSegments[i].color = i < rank ? GameUi.Accent : i == rank && unlocked ? new Color(.2f, .48f, .5f) : new Color(.1f, .19f, .23f);
        int count = WeaponElements.HasBranches(weapon) ? 3 : 4;
        for (int i = 0; i < branchTabs.Length; i++)
        {
            branchTabs[i].gameObject.SetActive(i < count);
            if (i >= count) continue;
            var rect = (RectTransform)branchTabs[i].transform;
            rect.anchorMin = new Vector2(.04f + i * .92f / count, .29f);
            rect.anchorMax = new Vector2(.04f + (i + 1) * .92f / count - .012f, .385f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            WeaponElement element = TabElement(i);
            branchIcons[i].sprite = Art(WeaponElements.Icons[(int)element]);
            branchNames[i].text = WeaponElements.Names[(int)element];
            branchTabs[i].GetComponent<Image>().color = element == detailElement ? new Color(.42f, .31f, .12f) : new Color(.06f, .16f, .21f);
        }
        int branchRank = WeaponElements.Rank(weapon, detailElement);
        bool locked = WeaponElements.IsLocked(weapon);
        branchDetails.text = locked ? GameText.Source("tuning.branch.locked") : GameText.Source("workshoptree.472") + branchRank + " / 3\n" +
            GameText.Source(branchRank < WeaponElements.MaxRank ? "tuning.branch.next" : "tuning.branch.current") + Summary(detailElement, Mathf.Min(3, branchRank + 1));
        branchBuyText.text = locked ? GameText.Source("workshoptree.465") : branchRank == WeaponElements.MaxRank ? GameText.Source("workshoptree.473") :
            GameText.Source(branchRank == 0 ? "workshoptree.481" : "workshoptree.474") + WeaponElements.Cost(branchRank);
        branchBuy.interactable = WeaponElements.CanBuy(weapon, detailElement);
        bool active = WeaponElements.Equipped(weapon) == detailElement;
        branchEquipText.text = GameText.Source(active ? "workshop.branch.unequip" : "tuning.branch.equip");
        branchEquip.interactable = unlocked && branchRank > 0;
    }

    public void PurchaseWeaponTuning()
    {
        if (detailWeapon < 0 || selected == null || selected.weapon != detailWeapon) return;
        bool success = MetaProgress.IsWeaponUnlocked(detailWeapon) ? WeaponTuning.Buy(detailWeapon) : MetaProgress.TryUnlock(MetaProgress.WeaponKey(detailWeapon));
        if (success) EquipmentPurchaseFeedback(nodes.Find(n => n.weapon == detailWeapon && n.element == WeaponElement.None));
        Refresh();
    }

    private void PurchaseEquipmentBranch()
    {
        if (selected == null || selected.weapon < 0 || detailWeapon != selected.weapon) return;
        if (WeaponElements.Buy(detailWeapon, detailElement)) EquipmentPurchaseFeedback(nodes.Find(n => n.weapon == detailWeapon && n.element == detailElement));
        Refresh();
    }

    private void EquipEquipmentBranch()
    {
        if (selected == null || selected.weapon < 0 || detailWeapon != selected.weapon) return;
        WeaponElements.Equip(detailWeapon, WeaponElements.Equipped(detailWeapon) == detailElement ? WeaponElement.None : detailElement);
        Refresh();
    }

    private void EquipmentPurchaseFeedback(Node node)
    {
        detailAge = 0f;
        if (node != null) Burst(node);
    }
}
