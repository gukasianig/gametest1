using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Screen layout follows the approved top-down constellation concept. All interaction is real UI.
public class WorkshopTree : MonoBehaviour
{
    private class Node
    {
        public int weapon = -1;
        public MetaUpgrade body;
        public WeaponElement element;
        public Button button;
        public WorkshopDiamond diamond;
        public Image picture;
        public Text state;
        public Text badge;
        public RectTransform rect;
        public string title;
        public bool owned, affordable;
    }

    // Each branch line follows the state of the node it leads to: lit and pulsing when owned, soft when
    // purchasable, dim when out of reach.
    private readonly List<KeyValuePair<int, Node>> links = new List<KeyValuePair<int, Node>>();
    private WorkshopTreeLines treeLines;
    private readonly List<Node> nodes = new List<Node>();
    private readonly Dictionary<string, Sprite> art = new Dictionary<string, Sprite>();
    private AutoShoter shooter;
    private Action back;
    private Node selected;
    private Text currency, title, description, state, buyText, equipText, category;
    private Image preview, elementPreview;
    private Button buy, equip;
    private RectTransform content, board;
    private float boardHeight, previousWidth;
    private float scrollProgress = 1f, zoom = 1f;
    private bool panning;
    private Vector2 lastMouse;
    private const float BoardWidth = 1500f;
    private const float Top = 295f, RootX = 270f, BodyX = 70f, BodyStep = 135f, WeaponRowStep = 235f, TrunkX = 470f;
    private const int WeaponColumns = 3;
    public ScrollRect Scroll { get; private set; }
    public int NodeCount { get { return nodes.Count; } }
    public string SelectedTitle { get { return selected != null ? selected.title : ""; } }

    private static readonly string[] WeaponArt = { "", "", "", "", "w_saw", "w_mines", "w_rocket",
        "w_arc", "w_flame", "w_ice_cannon", "w_rail", "w_turret", "w_drones" };
    private static readonly Color Dim = new Color(.34f, .41f, .48f);
    private static readonly Color Gold = new Color(1f, .74f, .32f);

    public void Build(AutoShoter owner, Action returnHome)
    {
        shooter = owner;
        back = returnHome;
        Image backdrop = GameUi.Box(transform, "Constellations", Vector2.zero, Vector2.one, Color.white, true).GetComponent<Image>();
        backdrop.sprite = Art("tree_background");
        if (backdrop.sprite == null) backdrop.color = new Color(.025f, .045f, .075f);
        GameUi.Box(transform, "Header shade", new Vector2(0, .88f), Vector2.one, new Color(.02f, .04f, .07f, .85f));
        GameUi.Label(transform, "МАСТЕРСКАЯ", new Vector2(.22f, .91f), new Vector2(.7f, .99f), 34, new Color(.78f, .94f, 1f));
        GameUi.Label(transform, "ДЕРЕВО ТЕХНОЛОГИЙ", new Vector2(.22f, .875f), new Vector2(.7f, .92f), 14, Dim * 1.7f);
        currency = GameUi.Label(transform, "", new Vector2(.73f, .92f), new Vector2(.99f, .99f), 22, GameUi.Accent);
        Button exit = GameUi.Button(transform, "‹  НАЗАД", new Vector2(.02f, .92f), new Vector2(.17f, .985f), () => back());
        UiArtwork.Round(exit.GetComponent<Image>());

        RectTransform viewport = GameUi.Box(transform, "Tree viewport", new Vector2(.015f, .095f), new Vector2(.715f, .875f), Color.clear, true);
        viewport.gameObject.AddComponent<RectMask2D>();
        Scroll = viewport.gameObject.AddComponent<ScrollRect>();
        Scroll.viewport = viewport;
        Scroll.horizontal = true;
        Scroll.scrollSensitivity = 0f; // wheel zooms; dragging pans
        Scroll.elasticity = .08f;
        Scroll.movementType = ScrollRect.MovementType.Clamped;
        Scroll.inertia = true;
        RectTransform sky = new GameObject("Tree sky", typeof(RectTransform), typeof(WorkshopSky)).GetComponent<RectTransform>();
        sky.SetParent(viewport, false);
        sky.anchorMin = Vector2.zero;
        sky.anchorMax = Vector2.one;
        sky.offsetMin = sky.offsetMax = Vector2.zero;
        sky.GetComponent<WorkshopSky>().raycastTarget = false;
        content = new GameObject("Tree content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(0, 1);
        content.pivot = new Vector2(0, 1);
        int weapons = shooter.weapons != null ? shooter.weapons.Length : 0;
        boardHeight = Mathf.Max(Top + (MetaProgress.Catalog.Length - 1) * BodyStep + 100,
            Top + (Mathf.CeilToInt(weapons / (float)WeaponColumns) - 1) * WeaponRowStep + 170);
        Scroll.content = content;
        board = Fixed("Tree board", content, 0, 0, BoardWidth, boardHeight);
        board.pivot = new Vector2(0, 1);
        board.anchoredPosition = Vector2.zero;
        WorkshopTreeLines lines = new GameObject("Branch connections", typeof(RectTransform), typeof(WorkshopTreeLines)).GetComponent<WorkshopTreeLines>();
        lines.transform.SetParent(board, false);
        RectTransform lineRect = (RectTransform)lines.transform;
        lineRect.anchorMin = Vector2.zero;
        lineRect.anchorMax = Vector2.one;
        lineRect.offsetMin = lineRect.offsetMax = Vector2.zero;
        lineRect.pivot = new Vector2(0, 1);
        lines.raycastTarget = false;
        treeLines = lines;
        WorkshopTreePulses pulses = new GameObject("Branch pulses", typeof(RectTransform), typeof(WorkshopTreePulses)).GetComponent<WorkshopTreePulses>();
        pulses.transform.SetParent(board, false);
        RectTransform pulseRect = (RectTransform)pulses.transform;
        pulseRect.anchorMin = Vector2.zero;
        pulseRect.anchorMax = Vector2.one;
        pulseRect.offsetMin = pulseRect.offsetMax = Vector2.zero;
        pulseRect.pivot = new Vector2(0, 1);
        pulses.raycastTarget = false;
        pulses.Source = lines;

        RectTransform auraRect = Fixed("Root aura", board, RootX, 90, 300, 300);
        auraRect.gameObject.AddComponent<WorkshopRootAura>().raycastTarget = false;
        Image root = Fixed("Root", board, RootX, 90, 140, 140).gameObject.AddComponent<Image>();
        root.sprite = Art("root_workshop");
        root.preserveAspect = true;
        root.raycastTarget = false;
        Header(board, "ТЕЛО", BodyX, 232, 120, GameUi.Accent);
        Header(board, "АРСЕНАЛ", TrunkX, 232, 150, Gold);
        lines.Connect(new Vector2(RootX, 150), new Vector2(BodyX, 218), GameUi.Accent);
        lines.Connect(new Vector2(RootX, 150), new Vector2(TrunkX, 218), Gold);
        // Category rails feed siblings. No lines run from one independent purchase through another.
        lines.Connect(new Vector2(BodyX, 250), new Vector2(BodyX, Top + (MetaProgress.Catalog.Length - 1) * BodyStep), GameUi.Accent * .6f);
        lines.Connect(new Vector2(TrunkX, 250), new Vector2(TrunkX, Top + (Mathf.CeilToInt(weapons / (float)WeaponColumns) - 1) * WeaponRowStep), Gold * .6f);

        for (int i = 0; i < MetaProgress.Catalog.Length; i++)
        {
            MetaUpgrade upgrade = MetaProgress.Catalog[i];
            float y = Top + i * BodyStep;
            int bodyLink = lines.Connect(new Vector2(BodyX, y), new Vector2(112, y), GameUi.Accent);
            Node node = new Node { body = upgrade, title = upgrade.title };
            AddNode(node, 150, y, 76, UiArtwork.Upgrade(upgrade.effect), board);
            links.Add(new KeyValuePair<int, Node>(bodyLink, node));
        }

        for (int i = 0; i < weapons; i++)
        {
            WeaponData weapon = shooter.weapons[i];
            if (weapon == null || !weapon.IsUsable) continue;
            int column = i % WeaponColumns;
            float x = 640 + column * 320;
            float y = Top + i / WeaponColumns * WeaponRowStep;
            float from = column == 0 ? TrunkX : x - 320 + 38;
            int weaponLink = lines.Connect(new Vector2(from, y), new Vector2(x - 38, y), Gold * .55f);
            RectTransform group = Fixed("Weapon group " + i, board, x, y + 60, 300, 180);
            Image groupShade = group.gameObject.AddComponent<Image>();
            groupShade.color = new Color(.02f, .06f, .09f, .32f);
            groupShade.raycastTarget = false;
            UiArtwork.Round(groupShade);
            Node node = new Node { weapon = i, title = WeaponTitle(i) };
            AddNode(node, 150, 30, 76, WeaponIcon(i), group);
            links.Add(new KeyValuePair<int, Node>(weaponLink, node));
            for (int e = 1; e <= 4; e++)
            {
                WeaponElement element = (WeaponElement)e;
                float offset = (e - 2.5f) * 64;
                int elementLink = lines.Connect(new Vector2(x, y + 38), new Vector2(x + offset, y + 55), WeaponElements.Colors[e] * .7f);
                Node elementNode = new Node { weapon = i, element = element, title = node.title + ": " + WeaponElements.Names[e] };
                AddNode(elementNode, 150 + offset, 108, 46, Art(WeaponElements.Icons[e]), group);
                links.Add(new KeyValuePair<int, Node>(elementLink, elementNode));
            }
        }

        RectTransform track = GameUi.Box(transform, "Tree scroll track", new Vector2(.727f, .105f), new Vector2(.735f, .865f), new Color(.15f, .25f, .3f, .55f), true);
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        RectTransform handle = GameUi.Box(track, "Scroll thumb", Vector2.zero, Vector2.one, GameUi.Accent, true);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        Scroll.verticalScrollbar = scrollbar;
        Scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        RectTransform panel = GameUi.Box(transform, "Upgrade details", new Vector2(.755f, .09f), new Vector2(.985f, .86f), new Color(.025f, .065f, .1f, .94f), true);
        UiArtwork.Round(panel.GetComponent<Image>());
        category = GameUi.Label(panel, "", new Vector2(.025f, .925f), new Vector2(.975f, .995f), 13, GameUi.Accent);
        preview = UiArtwork.Picture(panel, null, new Vector2(.12f, .70f), new Vector2(.88f, .92f));
        elementPreview = UiArtwork.Picture(panel, null, new Vector2(.67f, .685f), new Vector2(.98f, .81f));
        title = GameUi.Label(panel, "", new Vector2(.025f, .59f), new Vector2(.975f, .70f), 23);
        description = GameUi.Label(panel, "", new Vector2(.04f, .305f), new Vector2(.96f, .585f), 17, new Color(.72f, .81f, .88f));
        state = GameUi.Label(panel, "", new Vector2(.03f, .225f), new Vector2(.97f, .30f), 16, GameUi.Accent);
        buy = GameUi.Button(panel, "", new Vector2(.07f, .13f), new Vector2(.93f, .215f), PurchaseSelected);
        buy.gameObject.name = "Tree purchase";
        buyText = buy.GetComponentInChildren<Text>();
        buyText.resizeTextMaxSize = 19;
        UiArtwork.Round(buy.GetComponent<Image>());
        equip = GameUi.Button(panel, "", new Vector2(.07f, .035f), new Vector2(.93f, .12f), EquipSelected);
        equip.gameObject.name = "Tree equip";
        equipText = equip.GetComponentInChildren<Text>();
        equipText.resizeTextMaxSize = 17;
        UiArtwork.Round(equip.GetComponent<Image>());
        GameUi.Label(transform, "Колесо — приблизить / отдалить  ·  Перетаскивание — двигать  ·  Стихии связаны только со своим оружием", new Vector2(.015f, .012f), new Vector2(.735f, .075f), 14, new Color(.53f, .66f, .75f));
        selected = nodes.Count > 0 ? nodes[0] : null;
        Refresh();
        Canvas.ForceUpdateCanvases();
        RefreshLayout();
        Scroll.verticalNormalizedPosition = 1f;
    }

    public void RefreshLayout()
    {
        if (Scroll == null || board == null) return;
        float width = Scroll.viewport.rect.width;
        if (width <= 0f) return;
        float scale = width / BoardWidth * zoom;
        board.localScale = Vector3.one * scale;
        content.sizeDelta = new Vector2(BoardWidth * scale, boardHeight * scale);
        previousWidth = width;
        Scroll.verticalNormalizedPosition = scrollProgress;
        Scroll.StopMovement();
    }

    private void ZoomWithWheel()
    {
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(wheel, 0f)) return;
        RectTransform view = Scroll.viewport;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(view, Input.mousePosition, camera)) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view, Input.mousePosition, camera, out Vector2 local)) return;
        float fit = view.rect.width / BoardWidth;
        float oldScale = fit * zoom;
        float newZoom = Mathf.Clamp(zoom * Mathf.Pow(1.14f, wheel), .45f, 2.6f);
        if (Mathf.Approximately(newZoom, zoom)) return;
        Vector2 cursor = local - new Vector2(view.rect.xMin, view.rect.yMax);
        Vector2 boardPoint = (cursor - content.anchoredPosition) / oldScale;
        zoom = newZoom;
        float scale = fit * zoom;
        board.localScale = Vector3.one * scale;
        content.sizeDelta = new Vector2(BoardWidth * scale, boardHeight * scale);
        Vector2 position = cursor - boardPoint * scale;
        float minX = Mathf.Min(0f, view.rect.width - content.sizeDelta.x);
        float maxY = Mathf.Max(0f, content.sizeDelta.y - view.rect.height);
        content.anchoredPosition = new Vector2(Mathf.Clamp(position.x, minX, 0f), Mathf.Clamp(position.y, 0f, maxY));
        Scroll.StopMovement();
        RememberScrollPosition();
    }

    private void PanWithMouse()
    {
        bool held = Input.GetMouseButton(1) || Input.GetMouseButton(2);
        Vector2 mouse = Input.mousePosition;
        if (!held) { panning = false; return; }
        RectTransform view = Scroll.viewport;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!panning)
        {
            panning = RectTransformUtility.RectangleContainsScreenPoint(view, mouse, camera);
            lastMouse = mouse;
            return;
        }
        Vector2 delta = (mouse - lastMouse) / (canvas != null ? canvas.scaleFactor : 1f);
        lastMouse = mouse;
        float minX = Mathf.Min(0f, view.rect.width - content.sizeDelta.x);
        float maxY = Mathf.Max(0f, content.sizeDelta.y - view.rect.height);
        Vector2 position = content.anchoredPosition + delta;
        content.anchoredPosition = new Vector2(Mathf.Clamp(position.x, minX, 0f), Mathf.Clamp(position.y, 0f, maxY));
        Scroll.StopMovement();
    }

    private void Update()
    {
        if (Scroll == null || board == null) return;
        ZoomWithWheel();
        PanWithMouse();
    }

    private void LateUpdate()
    {
        if (Scroll != null && !Mathf.Approximately(previousWidth, Scroll.viewport.rect.width)) RefreshLayout();
        else RememberScrollPosition();
    }

    public void RememberScrollPosition()
    {
        if (Scroll != null) scrollProgress = Scroll.verticalNormalizedPosition;
    }

    private static RectTransform Fixed(string name, Transform parent, float x, float y, float width, float height)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Text LabelAt(Transform parent, string value, float x, float y, float width, float height, int size, Color color)
    {
        RectTransform rect = Fixed(value, parent, x, y, width, height);
        return GameUi.Label(rect, value, Vector2.zero, Vector2.one, size, color);
    }

    private static void Header(Transform parent, string value, float x, float y, float width, Color tint)
    {
        Text text = LabelAt(parent, value, x, y, width, 30, 18, tint);
        Image plate = text.transform.parent.gameObject.AddComponent<Image>();
        plate.color = new Color(.025f, .065f, .09f, 1f);
        plate.raycastTarget = false;
        UiArtwork.Round(plate);
    }

    private void AddNode(Node node, float x, float y, float size, Sprite sprite, RectTransform parent)
    {
        string id = node.body != null ? node.body.id : "weapon:" + node.weapon + (node.element == WeaponElement.None ? "" : ":" + node.element);
        RectTransform rect = Fixed("Node " + id, parent, x, y, size, size);
        node.rect = rect;
        node.diamond = rect.gameObject.AddComponent<WorkshopDiamond>();
        node.diamond.raycastTarget = true;
        node.button = rect.gameObject.AddComponent<Button>();
        node.button.targetGraphic = node.diamond;
        node.button.transition = Selectable.Transition.None;
        node.button.onClick.AddListener(() => { selected = node; Refresh(); Reveal(node); });
        node.picture = UiArtwork.Picture(rect, sprite, new Vector2(.04f, .04f), new Vector2(.96f, .96f));
        if (sprite == null) node.picture.enabled = false;
        string caption = node.element != WeaponElement.None ? WeaponElements.Names[(int)node.element] : node.title;
        bool module = node.element != WeaponElement.None;
        Text captionText = LabelAt(rect, caption, size * .5f, module ? size + 11 : size * .5f - 54,
            module ? 62 : node.body != null ? 185 : 270, module ? 18 : 28, module ? 12 : 15, new Color(.84f, .9f, .94f));
        captionText.transform.parent.name = "Node caption";
        captionText.rectTransform.offsetMin = captionText.rectTransform.offsetMax = Vector2.zero;
        node.state = LabelAt(rect, "", module ? size * .5f : size + 44,
            module ? size + 28 : size * .5f + 22, module ? 62 : 72, module ? 16 : 28, 12, GameUi.Accent);
        node.state.transform.parent.name = "Node state";
        node.state.rectTransform.offsetMin = node.state.rectTransform.offsetMax = Vector2.zero;
        node.badge = LabelAt(rect, "", size - 3, 1, 36, 18, 10, GameUi.Accent);
        node.badge.transform.parent.name = "Active badge";
        node.badge.resizeTextMinSize = 10;
        node.badge.rectTransform.offsetMin = node.badge.rectTransform.offsetMax = Vector2.zero;
        nodes.Add(node);
    }

    private void Reveal(Node node)
    {
        Canvas.ForceUpdateCanvases();
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Scroll.viewport, node.rect);
        Rect visible = Scroll.viewport.rect;
        float delta = bounds.max.y > visible.yMax - 12 ? visible.yMax - 12 - bounds.max.y :
            bounds.min.y < visible.yMin + 64 ? visible.yMin + 64 - bounds.min.y : 0f;
        content.anchoredPosition += Vector2.up * delta;
        float limit = Mathf.Max(0, content.rect.height - visible.height);
        float limitX = Mathf.Min(0f, visible.width - content.rect.width);
        content.anchoredPosition = new Vector2(Mathf.Clamp(content.anchoredPosition.x, limitX, 0), Mathf.Clamp(content.anchoredPosition.y, 0, limit));
        Scroll.StopMovement();
        RememberScrollPosition();
    }

    private string WeaponTitle(int index)
    {
        string[] basic = { "Пистолет", "Автомат", "Дробовик", "Тяжёлая винтовка" };
        return index < 4 ? basic[index] : !string.IsNullOrEmpty(shooter.weapons[index].title) ? shooter.weapons[index].title : shooter.weapons[index].weaponName;
    }

    public Sprite Art(string id)
    {
        if (art.TryGetValue(id, out Sprite cached)) return cached;
        Texture2D texture = Resources.Load<Texture2D>("UI/Tree/" + id);
        Sprite sprite = texture != null ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100f) : null;
        art[id] = sprite;
        return sprite;
    }

    private Sprite WeaponIcon(int index)
    {
        if (index == 2)
        {
            const string key = "shotgun_cropped";
            if (!art.TryGetValue(key, out Sprite clean))
            {
                Sprite original = UiArtwork.Weapon(index);
                if (original == null) return null;
                Rect rect = original.rect;
                // The old atlas region extends into the shield on the next row. Crop this view only.
                float trim = rect.height * .27f;
                rect.yMin += trim;
                clean = Sprite.Create(original.texture, rect, Vector2.one * .5f, 100f);
                art[key] = clean;
            }
            return clean;
        }
        if (index < 4) return UiArtwork.Weapon(index);
        return index < WeaponArt.Length ? Art(WeaponArt[index]) ?? UiArtwork.WeaponGlyph(shooter.weapons[index].kind) : null;
    }

    public void Refresh()
    {
        if (currency == null) return;
        currency.text = "ДЕТАЛИ  " + MetaProgress.Currency;
        foreach (Node node in nodes)
        {
            bool owned = false, affordable = false;
            node.badge.text = "";
            if (node.body != null)
            {
                int rank = MetaProgress.Rank(node.body.id);
                owned = rank > 0;
                affordable = MetaProgress.CanBuy(node.body);
                node.state.text = rank + " / " + node.body.maxRank;
            }
            else if (node.element == WeaponElement.None)
            {
                owned = MetaProgress.IsWeaponUnlocked(node.weapon);
                MetaUnlock unlock = MetaProgress.FindUnlock(MetaProgress.WeaponKey(node.weapon));
                affordable = !owned && unlock != null && MetaProgress.Currency >= unlock.cost;
                node.state.text = owned ? "Открыто" : unlock != null ? unlock.cost + "\nдеталей" : "Закрыто";
            }
            else
            {
                int rank = WeaponElements.Rank(node.weapon, node.element);
                owned = rank > 0;
                affordable = WeaponElements.CanBuy(node.weapon, node.element);
                node.state.text = !MetaProgress.IsWeaponUnlocked(node.weapon) ? "Закрыто" :
                    rank + "/3";
                if (rank > 0 && WeaponElements.Equipped(node.weapon) == node.element) node.badge.text = "ВКЛ";
            }
            Color accent = node.element == WeaponElement.None ? node.body != null ? GameUi.Accent : Gold : WeaponElements.Colors[(int)node.element];
            node.state.color = owned ? GameUi.Accent : affordable ? Gold : new Color(.55f, .62f, .68f);
            node.diamond.Edge = node == selected ? Gold : owned ? accent : affordable ? accent * .7f : Dim;
            node.diamond.Selected = node == selected;
            node.diamond.Glow = affordable;
            node.diamond.SetVerticesDirty();
            node.picture.color = owned || affordable || node == selected ? Color.white : new Color(.4f, .45f, .5f, .7f);
            node.owned = owned;
            node.affordable = affordable;
        }
        if (treeLines != null)
            foreach (KeyValuePair<int, Node> link in links)
                treeLines.SetBrightness(link.Key, link.Value.owned ? 1.35f : link.Value.affordable ? .9f : .45f);
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (selected == null) return;
        title.text = selected.title;
        state.color = GameUi.Accent;
        bool module = selected.element != WeaponElement.None;
        preview.sprite = module ? WeaponIcon(selected.weapon) : selected.picture.sprite;
        elementPreview.sprite = module ? selected.picture.sprite : null;
        elementPreview.gameObject.SetActive(module);
        category.text = selected.body != null ? "ПОСТОЯННОЕ УЛУЧШЕНИЕ" : module ? "СТИХИЯ ОРУЖИЯ" : "ОТКРЫТИЕ ОРУЖИЯ";
        equip.gameObject.SetActive(selected.element != WeaponElement.None);
        buy.interactable = false;
        if (selected.body != null)
        {
            MetaUpgrade upgrade = selected.body;
            int rank = MetaProgress.Rank(upgrade.id);
            description.text = upgrade.description + "\n\nСейчас: " + MetaProgress.EffectText(upgrade, rank) +
                (rank < upgrade.maxRank ? "\nСледующий: " + MetaProgress.EffectText(upgrade, rank + 1) : "");
            state.text = "Ранг " + rank + " / " + upgrade.maxRank;
            buyText.text = rank == upgrade.maxRank ? "МАКСИМУМ" : "УЛУЧШИТЬ · " + upgrade.Cost(rank);
            buy.interactable = MetaProgress.CanBuy(upgrade);
            if (!buy.interactable && rank < upgrade.maxRank) state.text = "Не хватает " + (upgrade.Cost(rank) - MetaProgress.Currency) + " деталей";
        }
        else if (selected.element == WeaponElement.None)
        {
            bool owned = MetaProgress.IsWeaponUnlocked(selected.weapon);
            MetaUnlock unlock = MetaProgress.FindUnlock(MetaProgress.WeaponKey(selected.weapon));
            description.text = "Четыре узла под оружием — его стихии.\n\nОдна активная стихия на оружие. Купленные варианты переключаются бесплатно.\n\nДругие оружия покупать не требуется.";
            state.text = owned ? "Оружие открыто" : "Нужно открыть оружие";
            buyText.text = owned ? "ОТКРЫТО" : "ОТКРЫТЬ · " + (unlock != null ? unlock.cost : 0);
            buy.interactable = !owned && unlock != null && MetaProgress.Currency >= unlock.cost;
            if (!owned && unlock != null && !buy.interactable) state.text = "Не хватает " + (unlock.cost - MetaProgress.Currency) + " деталей";
        }
        else
        {
            int rank = WeaponElements.Rank(selected.weapon, selected.element);
            bool unlocked = MetaProgress.IsWeaponUnlocked(selected.weapon);
            bool active = WeaponElements.Equipped(selected.weapon) == selected.element;
            description.text = (rank == 0 ? "После открытия:\n" : "Сейчас:\n") + Summary(selected.element, Mathf.Max(1, rank)) +
                (rank > 0 && rank < 3 ? "\n\nРанг " + (rank + 1) + ":\n" + Summary(selected.element, rank + 1) : "") +
                "\n\nТолько для этого оружия.";
            state.text = !unlocked ? "Сначала открой оружие" : rank == 0 ? "Стихия не открыта" : "Ранг " + rank + "/3" + (active ? " · активна" : " · не активна");
            buyText.text = rank == 3 ? "МАКСИМУМ" : (rank == 0 ? "ОТКРЫТЬ · " : "УЛУЧШИТЬ · ") + WeaponElements.Cost(rank);
            if (!unlocked) buyText.text = "ОРУЖИЕ ЗАКРЫТО";
            buy.interactable = WeaponElements.CanBuy(selected.weapon, selected.element);
            equip.gameObject.SetActive(rank > 0 && unlocked);
            equip.interactable = unlocked && rank > 0;
            equipText.text = active ? "СНЯТЬ СТИХИЮ" : "ПРИМЕНИТЬ";
            if (unlocked && rank < 3 && !buy.interactable) state.text = "Не хватает " + (WeaponElements.Cost(rank) - MetaProgress.Currency) + " деталей";
        }
        if (state.text.StartsWith("Не хватает") || state.text.StartsWith("Сначала")) state.color = Gold;
    }

    private static string Summary(WeaponElement element, int rank)
    {
        switch (element)
        {
            case WeaponElement.Fire: return "Горение " + rank * 8 + "% урона/с · 3 с\nОбновление без стаков; Страж: ×0.5";
            case WeaponElement.Frost: return "Замедление " + (10 + rank * 10) + "% · 2 с\nНа Стража вдвое слабее";
            case WeaponElement.Lightning: return "Соседей: " + rank + " · радиус 5 м\n30% урона, затухание ×0.65 · 0.4 с";
            case WeaponElement.Poison: return "Яд " + rank * 5 + "% урона/с · 4 с\nДо 3 стаков; Страж: ×0.5";
            default: return "";
        }
    }

    public void PurchaseSelected()
    {
        if (selected == null) return;
        Node bought = selected;
        int before = MetaProgress.Currency;
        if (selected.body != null) MetaProgress.Buy(selected.body.id);
        else if (selected.element == WeaponElement.None) MetaProgress.TryUnlock(MetaProgress.WeaponKey(selected.weapon));
        else WeaponElements.Buy(selected.weapon, selected.element);
        Refresh();
        if (MetaProgress.Currency < before && bought.rect != null) Burst(bought);
    }

    // Expanding ring around a node that was just bought.
    private void Burst(Node node)
    {
        WorkshopBurst burst = new GameObject("Purchase burst", typeof(RectTransform), typeof(WorkshopBurst)).GetComponent<WorkshopBurst>();
        RectTransform rect = (RectTransform)burst.transform;
        rect.SetParent(node.rect, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        burst.raycastTarget = false;
        burst.Tint = node.diamond != null ? node.diamond.Edge : GameUi.Accent;
    }

    public void EquipSelected()
    {
        if (selected == null || selected.element == WeaponElement.None) return;
        WeaponElements.Equip(selected.weapon, WeaponElements.Equipped(selected.weapon) == selected.element ? WeaponElement.None : selected.element);
        Refresh();
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in art.Values) if (sprite != null) Destroy(sprite);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public class WorkshopDiamond : MaskableGraphic
{
    public Color Edge = GameUi.Accent;
    public bool Selected, Glow;

    // Nodes that can be bought right now breathe with a soft halo.
    private void Update()
    {
        if (Glow) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        Vector2[] corners = { new Vector2(center.x, rect.yMax), new Vector2(rect.xMax, center.y),
            new Vector2(center.x, rect.yMin), new Vector2(rect.xMin, center.y) };
        mesh.AddVert(center, new Color(.025f, .07f, .11f, .92f), Vector2.zero);
        for (int i = 0; i < 4; i++) mesh.AddVert(corners[i] * .94f, new Color(.035f, .1f, .15f, .92f), Vector2.zero);
        for (int i = 0; i < 4; i++) mesh.AddTriangle(0, i + 1, (i + 1) % 4 + 1);
        var outline = new List<Vector2>(corners);
        if (Glow)
        {
            Color halo = Edge;
            halo.a = .16f + .2f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 3.2f));
            WorkshopTreeLines.Strip(mesh, outline, 10f, 6f, halo, true);
        }
        WorkshopTreeLines.Strip(mesh, outline, 2f, 1f, Edge, true);
        if (Selected)
        {
            var halo = new List<Vector2>();
            foreach (Vector2 corner in corners) halo.Add(corner * 1.12f);
            WorkshopTreeLines.Strip(mesh, halo, 1.5f, 1f, Edge, true);
        }
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public class WorkshopTreeLines : MaskableGraphic
{
    private struct Path { public Vector2 a, b; public Color color; public float brightness; }
    private readonly List<Path> paths = new List<Path>();

    public int Connect(Vector2 a, Vector2 b, Color tint)
    {
        paths.Add(new Path { a = a, b = b, color = tint, brightness = 1f });
        SetVerticesDirty();
        return paths.Count - 1;
    }

    public void SetBrightness(int index, float value)
    {
        if (index < 0 || index >= paths.Count || Mathf.Approximately(paths[index].brightness, value)) return;
        Path path = paths[index];
        path.brightness = value;
        paths[index] = path;
        SetVerticesDirty();
    }

    private static Vector2 Point(Path path, float t)
    {
        Vector2 a = new Vector2(path.a.x, -path.a.y), b = new Vector2(path.b.x, -path.b.y);
        Vector2 c = a + Vector2.down * Mathf.Abs(b.y - a.y) * .6f;
        Vector2 d = b + Vector2.up * Mathf.Abs(b.y - a.y) * .6f;
        float u = 1f - t;
        return u * u * u * a + 3 * u * u * t * c + 3 * u * t * t * d + t * t * t * b;
    }

    // Small light packets travelling down the branches that lead to owned nodes.
    public void AppendPulses(VertexHelper mesh, float time)
    {
        for (int i = 0; i < paths.Count; i++)
        {
            Path path = paths[i];
            if (path.brightness < 1.2f) continue;
            float lead = Mathf.Repeat(time * .32f + i * .173f, 1.7f);
            for (int tail = 0; tail < 4; tail++)
            {
                float t = lead - tail * .04f;
                if (t < 0f || t > 1f) continue;
                Color tint = Color.Lerp(path.color, Color.white, .65f);
                tint.a = .95f - tail * .22f;
                Dot(mesh, Point(path, t), 4f - tail * .7f, tint);
            }
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        foreach (Path path in paths)
        {
            Vector2 a = new Vector2(path.a.x, -path.a.y), b = new Vector2(path.b.x, -path.b.y);
            Vector2 c = a + Vector2.down * Mathf.Abs(b.y - a.y) * .6f;
            Vector2 d = b + Vector2.up * Mathf.Abs(b.y - a.y) * .6f;
            Color lineColor = path.color;
            lineColor.a = Mathf.Clamp01(lineColor.a * path.brightness);
            // One continuous strip per curve (no per-segment overlaps, so no beads at the joints) with
            // feathered edges: the outer vertices fade to transparent, which gives smooth anti-aliasing.
            var points = new List<Vector2>(33);
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f, u = 1f - t;
                points.Add(u * u * u * a + 3 * u * u * t * c + 3 * u * t * t * d + t * t * t * b);
            }
            Color glow = lineColor; glow.a *= .16f * Mathf.Max(1f, path.brightness);
            Strip(mesh, points, 7f, 4f, glow, false);
            Strip(mesh, points, 1.6f * Mathf.Max(1f, path.brightness * .9f), 1.1f, lineColor, false);
            Dot(mesh, a, 4f, lineColor);
            Dot(mesh, b, 4f, lineColor);
        }
    }

    // Anti-aliased polyline: core of full alpha with a soft transparent rim of `feather` on both sides.
    public static void Strip(VertexHelper mesh, List<Vector2> points, float width, float feather, Color tint, bool closed)
    {
        int count = points.Count;
        if (count < 2) return;
        float half = width * .5f;
        float core = Mathf.Max(half - feather, 0f);
        float rim = half + feather;
        Color clear = tint; clear.a = 0f;
        int start = mesh.currentVertCount;
        for (int i = 0; i < count; i++)
        {
            Vector2 previous = i > 0 ? points[i - 1] : (closed ? points[count - 1] : points[i]);
            Vector2 next = i < count - 1 ? points[i + 1] : (closed ? points[0] : points[i]);
            Vector2 tangent = (next - previous).normalized;
            if (tangent == Vector2.zero) tangent = Vector2.right;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            if (closed)
            {
                // Mitre the corners of a closed outline so adjacent edges meet without gaps.
                Vector2 before = (points[i] - previous).normalized, after = (next - points[i]).normalized;
                Vector2 bisector = new Vector2(-(before + after).y, (before + after).x).normalized;
                float cosine = Mathf.Max(Vector2.Dot(bisector, new Vector2(-after.y, after.x)), .5f);
                normal = bisector / cosine;
            }
            mesh.AddVert(points[i] - normal * rim, clear, Vector2.zero);
            mesh.AddVert(points[i] - normal * core, tint, Vector2.zero);
            mesh.AddVert(points[i] + normal * core, tint, Vector2.zero);
            mesh.AddVert(points[i] + normal * rim, clear, Vector2.zero);
        }
        int segments = closed ? count : count - 1;
        for (int i = 0; i < segments; i++)
        {
            int a = start + i * 4, b = start + (i + 1) % count * 4;
            for (int k = 0; k < 3; k++)
            {
                mesh.AddTriangle(a + k, a + k + 1, b + k);
                mesh.AddTriangle(a + k + 1, b + k + 1, b + k);
            }
        }
    }

    // Soft round junction marker: opaque centre fading out at the rim.
    public static void Dot(VertexHelper mesh, Vector2 center, float radius, Color tint)
    {
        Color clear = tint; clear.a = 0f;
        int index = mesh.currentVertCount;
        mesh.AddVert(center, tint, Vector2.zero);
        const int sides = 20;
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * .55f, tint, Vector2.zero);
        }
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 1.5f, clear, Vector2.zero);
        }
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            mesh.AddTriangle(index, index + 1 + i, index + 1 + next);
            mesh.AddTriangle(index + 1 + i, index + 1 + sides + i, index + 1 + next);
            mesh.AddTriangle(index + 1 + next, index + 1 + sides + i, index + 1 + sides + next);
        }
    }
}

// Animated light packets live in their own graphic so only a few hundred vertices are rebuilt per frame.
[RequireComponent(typeof(CanvasRenderer))]
public class WorkshopTreePulses : MaskableGraphic
{
    public WorkshopTreeLines Source;

    private void Update()
    {
        if (Source != null) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (Source != null) Source.AppendPulses(mesh, Time.unscaledTime);
    }
}

// One-shot ring that expands and fades around a node after a purchase.
[RequireComponent(typeof(CanvasRenderer))]
public class WorkshopBurst : MaskableGraphic
{
    public Color Tint = Color.white;
    private const float Duration = .65f;
    private float age;

    private void Update()
    {
        age += Time.unscaledDeltaTime;
        if (age >= Duration) { Destroy(gameObject); return; }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float progress = Mathf.Clamp01(age / Duration);
        float eased = 1f - (1f - progress) * (1f - progress);
        Rect rect = rectTransform.rect;
        float radius = rect.width * (.5f + eased * .75f);
        Color ring = Tint;
        ring.a = (1f - progress) * .9f;
        var points = new List<Vector2>(40);
        for (int i = 0; i < 40; i++)
        {
            float angle = i * Mathf.PI * 2f / 40f;
            points.Add(rect.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        WorkshopTreeLines.Strip(mesh, points, 3f * (1f - progress) + 1f, 2f, ring, true);
    }
}

// Slow drifting stars and soft colour clouds behind the tree; purely decorative.
[RequireComponent(typeof(CanvasRenderer))]
public class WorkshopSky : MaskableGraphic
{
    private const int Stars = 70;
    private float[] sx, sy, speed, size, phase;

    private void Update()
    {
        SetVerticesDirty();
    }

    private void Init()
    {
        sx = new float[Stars]; sy = new float[Stars]; speed = new float[Stars]; size = new float[Stars]; phase = new float[Stars];
        var random = new System.Random(7);
        for (int i = 0; i < Stars; i++)
        {
            sx[i] = (float)random.NextDouble(); sy[i] = (float)random.NextDouble();
            speed[i] = .004f + (float)random.NextDouble() * .014f;
            size[i] = 1.2f + (float)random.NextDouble() * 2.4f;
            phase[i] = (float)random.NextDouble() * 6.28f;
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (sx == null) Init();
        Rect rect = rectTransform.rect;
        float time = Application.isPlaying ? Time.unscaledTime : 0f;
        Color[] cloud = { new Color(.1f, .45f, .65f, .10f), new Color(.75f, .45f, .15f, .08f), new Color(.35f, .2f, .6f, .08f) };
        for (int i = 0; i < cloud.Length; i++)
        {
            float t = time * (.05f + i * .02f) + i * 2.1f;
            Vector2 center = new Vector2(rect.xMin + rect.width * (.25f + i * .3f + Mathf.Sin(t) * .08f),
                rect.yMin + rect.height * (.7f - i * .25f + Mathf.Cos(t * .8f) * .08f));
            WorkshopTreeLines.Dot(mesh, center, rect.width * .42f, cloud[i]);
        }
        for (int i = 0; i < Stars; i++)
        {
            float y = Mathf.Repeat(sy[i] + time * speed[i], 1f);
            float x = sx[i] + Mathf.Sin(time * .3f + phase[i]) * .008f;
            float twinkle = .35f + .65f * (.5f + .5f * Mathf.Sin(time * (.8f + speed[i] * 60f) + phase[i]));
            Color tint = new Color(.7f, .9f, 1f, .5f * twinkle);
            WorkshopTreeLines.Dot(mesh, new Vector2(rect.xMin + rect.width * x, rect.yMin + rect.height * y), size[i], tint);
        }
    }
}

// Pulsing halo and slowly rotating arcs around the workshop root icon.
[RequireComponent(typeof(CanvasRenderer))]
public class WorkshopRootAura : MaskableGraphic
{
    private void Update()
    {
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float time = Application.isPlaying ? Time.unscaledTime : 0f;
        Vector2 center = rectTransform.rect.center;
        float pulse = .5f + .5f * Mathf.Sin(time * 1.6f);
        WorkshopTreeLines.Dot(mesh, center, 62f + pulse * 10f, new Color(.4f, .85f, 1f, .22f + pulse * .12f));
        for (int ring = 0; ring < 2; ring++)
        {
            float radius = 84f + ring * 26f;
            float spin = time * (ring == 0 ? .5f : -.35f);
            Color tint = ring == 0 ? new Color(.5f, .9f, 1f, .7f) : new Color(1f, .78f, .38f, .55f);
            for (int arc = 0; arc < 3; arc++)
            {
                float start = spin + arc * Mathf.PI * 2f / 3f;
                var points = new List<Vector2>(14);
                for (int i = 0; i <= 13; i++)
                {
                    float angle = start + i / 13f * 1.2f;
                    points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
                WorkshopTreeLines.Strip(mesh, points, 2.2f, 1.6f, tint, false);
            }
        }
    }
}
