using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Workshop map and inspection pages share the same purchase state and real UI interaction.
public partial class WorkshopTree : MonoBehaviour
{
    private class Node
    {
        public int weapon = -1;
        public MetaUpgrade body;
        public string project;
        public WeaponElement element;
        public Button button;
        public WorkshopDiamond diamond;
        public Image picture;
        public Image keyIcon;
        public Text state;
        public Text badge;
        public RectTransform rect;
        public WorkshopPlatform platform;
        public string title;
        public bool owned, affordable;
    }

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
    private float scrollProgress = 1f;
    private float zoom = 1f, horizontalProgress, rootY;
    private bool fitMode, centerMode;
    private Text zoomLabel, goalLabel;
    private Button projectOption;
    private Text projectOptionText;
    private int projectRevision;
    private readonly List<Image> weaponGroups = new List<Image>();
    private CanvasGroup detailGroup, entrance;
    private RectTransform detailPanel;
    private Node detailNode;
    private float detailAge = 1f, entranceAge, navigationAge;
    private bool motionReady, navigationMoving;
    private Vector2 navigationFrom, navigationTo;
    private float navigationFromZoom, navigationToZoom;
    private Image rootPicture;
    public bool AnimateNavigation { get; set; } = true;
    public bool NavigationMoving => navigationMoving;
    private const float NavigationDuration = .48f;
    private readonly List<Sprite> created = new List<Sprite>(); // только свои Sprite.Create; ассеты Resources не уничтожаем
    private bool panning;
    private Vector2 lastMouse;
    private const float BoardWidth = 1800f;
    private const float RootX = BoardWidth * .5f;
    private const float WeaponX = 1140f, WeaponPitch = 258f, GroupWidth = 242f, ElementStep = 55f;
    private const float WeaponTop = 100f, WeaponRowStep = 260f;
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
        GameUi.Label(transform, GameText.Source("workshoptree.458"), new Vector2(.22f, .91f), new Vector2(.7f, .99f), 34, new Color(.78f, .94f, 1f));
        GameUi.Label(transform, GameText.Source("workshoptree.459"), new Vector2(.22f, .875f), new Vector2(.7f, .92f), 14, Dim * 1.7f);
        currency = GameUi.Label(transform, "", new Vector2(.73f, .92f), new Vector2(.99f, .99f), 22, GameUi.Accent);
        Button exit = GameUi.Button(transform, GameText.Source("workshoptree.460"), new Vector2(.02f, .92f), new Vector2(.17f, .985f), () => back());
        UiArtwork.Round(exit.GetComponent<Image>());

        RectTransform viewport = GameUi.Box(transform, "Tree viewport", new Vector2(.015f, .16f), new Vector2(.69f, .82f), Color.clear, true);
        viewport.gameObject.AddComponent<RectMask2D>();
        var navigation = viewport.gameObject.AddComponent<WorkshopScrollRect>();
        navigation.Tree = this;
        Scroll = navigation;
        Scroll.viewport = viewport;
        Scroll.horizontal = true;
        Scroll.scrollSensitivity = 40f;
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
        BuildEquipmentMap();

        string[] sectionIds = { "hero", "weapons", "projects" };
        for (int i = 0; i < 3; i++)
        {
            string section = sectionIds[i];
            var shortcut = GameUi.Button(transform, GameText.Source(section == "hero" ? "challengescreen.076" : section == "weapons" ? "weaponselectscreen.439" : "projects.nav.projects"), new Vector2(.015f + i * .225f, .835f), new Vector2(.23f + i * .225f, .88f), () => FocusSection(section));
            shortcut.name = "Workshop section " + section;
            shortcut.GetComponentInChildren<Text>().resizeTextMaxSize = 16;
            UiArtwork.Round(shortcut.GetComponent<Image>());
        }
        var centre = GameUi.Button(transform, GameText.Source("workshop.layout.center"), new Vector2(.015f, .095f), new Vector2(.15f, .14f), CenterRoot);
        centre.name = "Workshop centre";
        centre.GetComponentInChildren<Text>().resizeTextMaxSize = 14;
        GameUi.Button(transform, "−", new Vector2(.42f, .095f), new Vector2(.465f, .14f), () => ZoomAt(-1f, ViewCentre()));
        zoomLabel = GameUi.Label(transform, "", new Vector2(.465f, .095f), new Vector2(.545f, .14f), 15);
        GameUi.Button(transform, "+", new Vector2(.545f, .095f), new Vector2(.59f, .14f), () => ZoomAt(1f, ViewCentre()));
        var fit = GameUi.Button(transform, GameText.Source("projects.fit"), new Vector2(.60f, .095f), new Vector2(.69f, .14f), FitTree);
        fit.name = "Workshop fit";
        fit.GetComponentInChildren<Text>().resizeTextMaxSize = 14;
        var goal = GameUi.Button(transform, "", new Vector2(.015f, .04f), new Vector2(.69f, .09f), () =>
        {
            if (WorkshopProjects.Target >= 0) FocusProject(WorkshopProjects.Ids[WorkshopProjects.Target]);
        });
        goal.name = "Workshop tracked goal";
        goalLabel = goal.GetComponentInChildren<Text>();
        goalLabel.resizeTextMaxSize = 14;
        UiArtwork.Round(goal.GetComponent<Image>());

        RectTransform track = GameUi.Box(transform, "Tree scroll track", new Vector2(.697f, .16f), new Vector2(.705f, .82f), new Color(.15f, .25f, .3f, .55f), true);
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        RectTransform handle = GameUi.Box(track, "Scroll thumb", Vector2.zero, Vector2.one, GameUi.Accent, true);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        Scroll.verticalScrollbar = scrollbar;
        Scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        RectTransform horizontalTrack = GameUi.Box(transform, "Tree horizontal track", new Vector2(.015f, .145f), new Vector2(.69f, .155f), new Color(.15f, .25f, .3f, .55f), true);
        var horizontalBar = horizontalTrack.gameObject.AddComponent<Scrollbar>();
        var horizontalHandle = GameUi.Box(horizontalTrack, "Horizontal thumb", Vector2.zero, Vector2.one, GameUi.Accent, true);
        horizontalBar.handleRect = horizontalHandle;
        horizontalBar.targetGraphic = horizontalHandle.GetComponent<Image>();
        horizontalBar.direction = Scrollbar.Direction.LeftToRight;
        Scroll.horizontalScrollbar = horizontalBar;
        Scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        RectTransform panel = GameUi.Box(transform, "Upgrade details", new Vector2(.725f, .09f), new Vector2(.985f, .86f), new Color(.025f, .065f, .1f, .94f), true);
        detailPanel = panel;
        detailGroup = panel.gameObject.AddComponent<CanvasGroup>();
        UiArtwork.Round(panel.GetComponent<Image>());
        var panelBorder = panel.gameObject.AddComponent<Outline>();
        panelBorder.effectDistance = new Vector2(1f, -1f);
        panelBorder.effectColor = new Color(.24f, .64f, .73f, .3f);
        GameUi.Box(panel, "Detail accent", new Vector2(.04f, .965f), new Vector2(.14f, .97f), new Color(.3f, .85f, .9f, .65f));
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
        projectOption = GameUi.Button(panel, "", new Vector2(.07f, .225f), new Vector2(.93f, .30f), () =>
        {
            if (selected != null && selected.project != null) { WorkshopProjects.CycleOption(selected.project); Refresh(); }
        });
        projectOption.name = "Workshop project option";
        projectOptionText = projectOption.GetComponentInChildren<Text>();
        projectOptionText.resizeTextMaxSize = 14;
        UiArtwork.Round(projectOption.GetComponent<Image>());
        projectOption.gameObject.SetActive(false);
        GameUi.Label(transform, GameText.Source("workshoptree.461"), new Vector2(.015f, .002f), new Vector2(.705f, .037f), 12, new Color(.53f, .66f, .75f));
        BuildEquipmentPanel(panel);
        selected = nodes.Count > 0 ? nodes[0] : null;
        Refresh();
        Canvas.ForceUpdateCanvases();
        RefreshLayout();
        FitTree();
        entrance = gameObject.AddComponent<CanvasGroup>();
        foreach (Button button in GetComponentsInChildren<Button>(true))
            if (button.GetComponent<WorkshopDiamond>() == null) button.gameObject.AddComponent<WorkshopButtonMotion>();
        motionReady = true;
    }

    public void RefreshLayout()
    {
        if (Scroll == null || board == null) return;
        float width = Scroll.viewport.rect.width;
        if (width <= 0f) return;
        bool resized = !Mathf.Approximately(previousWidth, width);
        if (navigationMoving && !resized) return;
        if (resized && navigationMoving) { zoom = navigationToZoom; navigationMoving = false; }
        if (fitMode) zoom = Mathf.Clamp(Mathf.Min(1f, Scroll.viewport.rect.height / boardHeight / (width / BoardWidth)), .3f, 5f);
        float scale = width / BoardWidth * zoom;
        ApplyBoardScale(scale);
        previousWidth = width;
        Scroll.verticalNormalizedPosition = scrollProgress;
        Scroll.horizontalNormalizedPosition = horizontalProgress;
        if (zoomLabel != null) zoomLabel.text = Mathf.RoundToInt(zoom * 100f) + "%";
        Scroll.StopMovement();
        // После смены размера окна выбранный узел остаётся на экране (он может быть в середине доски, а не у края).
        if (centerMode) CenterRootImmediate();
        else if (selected != null && !fitMode) Reveal(selected);
    }

    public float Zoom => zoom;
    private Vector2 ViewCentre()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(camera, Scroll.viewport.TransformPoint(Scroll.viewport.rect.center));
    }

    public void ZoomAt(float steps, Vector2 screenPoint)
    {
        if (board == null || Scroll.viewport.rect.width <= 0f) return;
        CancelNavigation();
        fitMode = false;
        centerMode = false;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Scroll.viewport, screenPoint, camera, out Vector2 local)) return;
        Vector2 mouse = local - new Vector2(Scroll.viewport.rect.xMin, Scroll.viewport.rect.yMax);
        float oldScale = board.localScale.x;
        Vector2 point = (mouse - content.anchoredPosition - board.anchoredPosition) / Mathf.Max(.001f, oldScale);
        zoom = Mathf.Clamp(zoom * Mathf.Pow(1.15f, steps), .3f, 5f);
        float scale = Scroll.viewport.rect.width / BoardWidth * zoom;
        ApplyBoardScale(scale);
        content.anchoredPosition = mouse - point * scale - board.anchoredPosition;
        ClampContent();
        zoomLabel.text = Mathf.RoundToInt(zoom * 100f) + "%";
        RememberScrollPosition();
    }

    public void FitTree()
    {
        Vector2 from = content.anchoredPosition;
        float fromZoom = zoom;
        CancelNavigation();
        fitMode = true;
        centerMode = false;
        zoom = Mathf.Clamp(Mathf.Min(1f, Scroll.viewport.rect.height / boardHeight / (Scroll.viewport.rect.width / BoardWidth)), .3f, 5f);
        float scale = Scroll.viewport.rect.width / BoardWidth * zoom;
        ApplyBoardScale(scale);
        content.anchoredPosition = Vector2.zero;
        ClampContent();
        zoomLabel.text = Mathf.RoundToInt(zoom * 100f) + "%";
        RememberScrollPosition();
        BeginNavigation(from, fromZoom);
    }

    public void CenterRoot()
    {
        if (Scroll == null) return;
        Vector2 from = content.anchoredPosition;
        float fromZoom = zoom;
        CancelNavigation();
        CenterRootImmediate();
        BeginNavigation(from, fromZoom);
    }

    private void CenterRootImmediate()
    {
        if (Scroll == null || Scroll.viewport.rect.width <= 0f) return;
        fitMode = false;
        centerMode = true;
        zoom = Mathf.Max(zoom, 2.4f);
        float scale = Scroll.viewport.rect.width / BoardWidth * zoom;
        ApplyBoardScale(scale);
        content.anchoredPosition = new Vector2(Scroll.viewport.rect.width * .5f - RootX * scale, rootY * scale - Scroll.viewport.rect.height * .5f) - board.anchoredPosition;
        ClampContent();
        zoomLabel.text = Mathf.RoundToInt(zoom * 100f) + "%";
        RememberScrollPosition();
    }

    public void FocusSection(string section)
    {
        Node node = nodes.Find(n => section == "projects" ? n.project != null : section == "hero" ? n.body != null : n.project == null && n.body == null && n.element == WeaponElement.None);
        if (node == null) return;
        Vector2 from = content.anchoredPosition;
        float fromZoom = zoom;
        CancelNavigation();
        centerMode = fitMode = false;
        selected = node;
        Refresh();
        zoom = Mathf.Max(zoom, 2.4f);
        RefreshLayout();
        CenterMapPoint(section == "hero" ? 410f : section == "weapons" ? WeaponX + WeaponPitch : 510f,
            section == "hero" ? 330f : section == "weapons" ? 370f : 920f);
        BeginNavigation(from, fromZoom);
    }

    public void FocusProject(string id)
    {
        Node node = nodes.Find(n => n.project == id);
        if (node == null) return;
        Vector2 from = content.anchoredPosition;
        float fromZoom = zoom;
        CancelNavigation();
        centerMode = fitMode = false;
        selected = node;
        Refresh();
        zoom = Mathf.Max(zoom, 2.4f);
        RefreshLayout();
        BeginNavigation(from, fromZoom);
    }

    public void CancelNavigation() { navigationMoving = false; }

    private void BeginNavigation(Vector2 from, float fromZoom)
    {
        if (!motionReady || !AnimateNavigation || !isActiveAndEnabled) return;
        navigationTo = content.anchoredPosition;
        navigationToZoom = zoom;
        if ((navigationTo - from).sqrMagnitude < 1f && Mathf.Abs(zoom - fromZoom) < .001f) return;
        navigationFrom = from; navigationFromZoom = fromZoom;
        navigationAge = 0f; navigationMoving = true;
        zoom = fromZoom;
        ApplyBoardScale(Scroll.viewport.rect.width / BoardWidth * zoom);
        content.anchoredPosition = from;
        ClampContent();
    }

    public void CompletePresentation()
    {
        if (navigationMoving)
        {
            zoom = navigationToZoom;
            ApplyBoardScale(Scroll.viewport.rect.width / BoardWidth * zoom);
            content.anchoredPosition = navigationTo;
            ClampContent();
            navigationMoving = false;
        }
        entranceAge = detailAge = 1f;
        if (entrance != null) entrance.alpha = 1f;
        if (detailGroup != null) detailGroup.alpha = 1f;
        if (detailPanel != null) detailPanel.anchoredPosition = Vector2.zero;
        if (zoomLabel != null) zoomLabel.text = Mathf.RoundToInt(zoom * 100f) + "%";
        RememberScrollPosition();
    }

    private void ClampContent()
    {
        Vector2 position = content.anchoredPosition;
        content.anchoredPosition = new Vector2(Mathf.Clamp(position.x, Mathf.Min(0f, Scroll.viewport.rect.width - content.rect.width), 0f),
            Mathf.Clamp(position.y, 0f, Mathf.Max(0f, content.rect.height - Scroll.viewport.rect.height)));
        Scroll.StopMovement();
    }

    private void CenterMapPoint(float x, float y)
    {
        float scale = board.localScale.x;
        content.anchoredPosition = new Vector2(Scroll.viewport.rect.width * .5f - x * scale, y * scale - Scroll.viewport.rect.height * .5f) - board.anchoredPosition;
        ClampContent();
        RememberScrollPosition();
    }

    private void ApplyBoardScale(float scale)
    {
        Vector2 drawing = new Vector2(BoardWidth * scale, boardHeight * scale);
        Vector2 view = Scroll.viewport.rect.size;
        board.localScale = Vector3.one * scale;
        content.sizeDelta = new Vector2(Mathf.Max(view.x, drawing.x), Mathf.Max(view.y, drawing.y));
        // A small overview is centred inside a viewport-sized scroll surface.
        board.anchoredPosition = new Vector2(Mathf.Max(0f, (view.x - drawing.x) * .5f), -Mathf.Max(0f, (view.y - drawing.y) * .5f));
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
        Pan(delta);
    }

    public void Pan(Vector2 delta)
    {
        CancelNavigation();
        centerMode = false;
        content.anchoredPosition += delta;
        ClampContent();
        RememberScrollPosition();
    }

    private void Update()
    {
        if (Scroll == null || board == null) return;
        PanWithMouse();
        if (projectRevision != WorkshopProjects.Revision) Refresh();
        TickPresentation();
    }

    private void TickPresentation()
    {
        float dt = Time.unscaledDeltaTime;
        entranceAge += dt;
        if (entrance != null) entrance.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(entranceAge / .35f));
        detailAge += dt;
        float detail = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(detailAge / .24f));
        if (detailGroup != null) detailGroup.alpha = Mathf.Lerp(.3f, 1f, detail);
        if (detailPanel != null) detailPanel.anchoredPosition = new Vector2(0f, -7f * (1f - detail));
        if (preview != null) preview.rectTransform.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, detail);
        if (rootPicture != null)
        {
            rootPicture.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * .65f) * 2f);
            rootPicture.rectTransform.localScale = Vector3.one * (1f + .025f * Mathf.Sin(Time.unscaledTime * 1.4f));
        }
        foreach (Image group in weaponGroups)
        {
            bool chosen = selected != null && selected.rect.parent == group.transform;
            Color target = chosen ? new Color(.04f, .14f, .18f, .78f) : new Color(.02f, .06f, .09f, .32f);
            group.color = Color.Lerp(group.color, target, 1f - Mathf.Exp(-9f * dt));
            var outline = group.GetComponentInChildren<WorkshopCardBorder>();
            outline.color = Color.Lerp(outline.color, chosen ? new Color(.4f, .9f, .95f, .8f) : new Color(.3f, .6f, .7f, .3f), 1f - Mathf.Exp(-9f * dt));
        }
        if (!navigationMoving) return;
        navigationAge += dt;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(navigationAge / NavigationDuration));
        zoom = Mathf.Lerp(navigationFromZoom, navigationToZoom, t);
        ApplyBoardScale(Scroll.viewport.rect.width / BoardWidth * zoom);
        content.anchoredPosition = Vector2.Lerp(navigationFrom, navigationTo, t);
        ClampContent();
        if (zoomLabel != null) zoomLabel.text = Mathf.RoundToInt(zoom * 100f) + "%";
        if (navigationAge >= NavigationDuration) navigationMoving = false;
        RememberScrollPosition();
    }

    private void OnEnable() { entranceAge = 0f; }

    private void OnDisable()
    {
        CancelNavigation();
        if (detailPanel != null) detailPanel.anchoredPosition = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (Scroll != null && !Mathf.Approximately(previousWidth, Scroll.viewport.rect.width)) RefreshLayout();
        else RememberScrollPosition();
    }

    public void RememberScrollPosition()
    {
        if (Scroll != null)
        {
            scrollProgress = Scroll.verticalNormalizedPosition;
            horizontalProgress = Scroll.horizontalNormalizedPosition;
        }
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
        string id = node.project != null ? "project:" + node.project : node.body != null ? node.body.id : "weapon:" + node.weapon + (node.element == WeaponElement.None ? "" : ":" + node.element);
        RectTransform rect = Fixed("Node " + id, parent, x, y, size, size);
        node.rect = rect;
        node.diamond = rect.gameObject.AddComponent<WorkshopDiamond>();
        node.diamond.raycastTarget = true;
        node.button = rect.gameObject.AddComponent<Button>();
        node.button.targetGraphic = node.diamond;
        node.button.transition = Selectable.Transition.None;
        node.button.onClick.AddListener(() => { centerMode = false; selected = node; Refresh(); Reveal(node, true); });
        node.picture = UiArtwork.Picture(rect, sprite, new Vector2(.04f, .04f), new Vector2(.96f, .96f));
        node.diamond.Picture = node.picture.rectTransform;
        node.diamond.HoverChanged = RefreshBranchActivity;
        RectTransform baseRect = Fixed("Holographic base", rect, size * .5f, size * .9f, size * 1.12f, size * .38f);
        baseRect.SetAsFirstSibling();
        node.platform = baseRect.gameObject.AddComponent<WorkshopPlatform>();
        node.platform.raycastTarget = false;
        if (sprite == null) node.picture.enabled = false;
        if (node.element != WeaponElement.None && WeaponElements.IsLocked(node.weapon))
        {
            node.keyIcon = UiArtwork.Picture(rect, KeySprite(), new Vector2(.05f, .05f), new Vector2(.95f, .95f));
            node.keyIcon.raycastTarget = false;
            node.keyIcon.name = "Lock key";
        }
        string caption = node.element != WeaponElement.None ? WeaponElements.Names[(int)node.element] : node.title;
        bool module = node.element != WeaponElement.None;
        bool weapon = node.body == null && node.project == null && !module;
        float captionY = module ? size + 12f : weapon ? -20f : size * .5f - 75f;
        Text captionText = LabelAt(rect, caption, size * .5f, captionY,
            module ? 52f : weapon ? 230f : 240f, module ? 22f : 32f, module ? 14 : 22, new Color(.84f, .9f, .94f));
        captionText.transform.parent.name = "Node caption";
        captionText.resizeTextMinSize = module ? 8 : 12;
        captionText.rectTransform.offsetMin = captionText.rectTransform.offsetMax = Vector2.zero;
        node.state = LabelAt(rect, "", size * .5f, module ? size + 36f : weapon ? size + 13f : size + 25f,
            module ? 52f : 160f, module ? 18f : 26f, module ? 12 : 16, GameUi.Accent);
        node.state.transform.parent.name = "Node state";
        node.state.rectTransform.offsetMin = node.state.rectTransform.offsetMax = Vector2.zero;
        node.badge = LabelAt(rect, "", size - 3, 1, 36, 18, 10, GameUi.Accent);
        node.badge.transform.parent.name = "Active badge";
        node.badge.resizeTextMinSize = 10;
        node.badge.rectTransform.offsetMin = node.badge.rectTransform.offsetMax = Vector2.zero;
        nodes.Add(node);
    }

    // Ключ-замок для закрытых узлов: рисуется в коде (кольцо, стержень, два зубца), золотой с тёмной обводкой.
    private static Sprite keySprite;

    private static Sprite KeySprite()
    {
        if (keySprite != null) return keySprite;
        const int N = 64;
        var texture = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color gold = new Color(1f, .8f, .28f), shade = new Color(.55f, .38f, .08f), outline = new Color(.04f, .05f, .07f, .95f);
        Vector2 ringCentre = new Vector2(20f, 44f), tip = new Vector2(54f, 10f);
        Vector2 axis = (tip - ringCentre).normalized, normal = new Vector2(-axis.y, axis.x);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Vector2 p = new Vector2(x + .5f, y + .5f);
                float ring = Mathf.Abs(Vector2.Distance(p, ringCentre) - 10.5f) - 4.2f;
                Vector2 q = p - ringCentre;
                float along = Vector2.Dot(q, axis), across = Vector2.Dot(q, normal);
                float length = Vector2.Distance(ringCentre, tip);
                float shaft = along > 8f && along < length ? Mathf.Abs(across) - 3.2f : 99f;
                float toothA = (along > length - 17f && along < length - 11f) ? Mathf.Max(Mathf.Abs(across + 6f) - 6.2f, 0f) : 99f;
                float toothB = (along > length - 8f && along < length - 2f) ? Mathf.Max(Mathf.Abs(across + 5f) - 5.2f, 0f) : 99f;
                float d = Mathf.Min(ring, Mathf.Min(shaft, Mathf.Min(toothA > 90f ? 99f : toothA - 0.01f, toothB > 90f ? 99f : toothB - 0.01f)));
                Color c = d < 0f ? (across > 1f || (p.y - ringCentre.y) > 4f && Vector2.Distance(p, ringCentre) > 10f ? gold : Color.Lerp(gold, shade, .45f)) : d < 2.2f ? outline : Color.clear;
                texture.SetPixel(x, y, c);
            }
        texture.Apply();
        keySprite = Sprite.Create(texture, new Rect(0, 0, N, N), new Vector2(.5f, .5f), 64f);
        return keySprite;
    }

    private void Reveal(Node node, bool animate = false)
    {
        Vector2 from = content.anchoredPosition;
        float fromZoom = zoom;
        CancelNavigation();
        Canvas.ForceUpdateCanvases();
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Scroll.viewport, node.rect);
        Rect visible = Scroll.viewport.rect;
        float delta = bounds.max.y > visible.yMax - 12 ? visible.yMax - 12 - bounds.max.y :
            bounds.min.y < visible.yMin + 64 ? visible.yMin + 64 - bounds.min.y : 0f;
        content.anchoredPosition += Vector2.up * delta;
        float horizontal = bounds.min.x < visible.xMin + 10f ? visible.xMin + 10f - bounds.min.x :
            bounds.max.x > visible.xMax - 10f ? visible.xMax - 10f - bounds.max.x : 0f;
        content.anchoredPosition += Vector2.right * horizontal;
        float limit = Mathf.Max(0, content.rect.height - visible.height);
        float limitX = Mathf.Min(0f, visible.width - content.rect.width);
        content.anchoredPosition = new Vector2(Mathf.Clamp(content.anchoredPosition.x, limitX, 0), Mathf.Clamp(content.anchoredPosition.y, 0, limit));
        Scroll.StopMovement();
        RememberScrollPosition();
        if (animate) BeginNavigation(from, fromZoom);
    }

    private string WeaponTitle(int index)
    {
        string[] basic = { GameText.Source("weaponselectscreen.433"), GameText.Source("weaponselectscreen.434"), GameText.Source("metaprogress.179"), GameText.Source("metaprogress.180") };
        return index < 4 ? basic[index] : !string.IsNullOrEmpty(shooter.weapons[index].title) ? shooter.weapons[index].title : shooter.weapons[index].weaponName;
    }

    public Sprite Art(string id)
    {
        if (art.TryGetValue(id, out Sprite cached)) return cached;
        Texture2D texture = Resources.Load<Texture2D>("UI/Tree/" + id);
        Sprite sprite = texture != null ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, 100f) : null;
        if (sprite != null) created.Add(sprite);
        art[id] = sprite;
        return sprite;
    }

    private Sprite WeaponIcon(int index)
    {
        // Те же иконки из боевых моделей, что в экране выбора (Resources/UI/Weapons/<ключ ModelSkinSet>).
        if (shooter != null && shooter.weapons != null && index >= 0 && index < shooter.weapons.Length && shooter.weapons[index] != null)
        {
            string iconKey = "icon:" + ModelSkins.WeaponKey(shooter.weapons[index], index);
            if (!art.TryGetValue(iconKey, out Sprite icon))
            {
                icon = Resources.Load<Sprite>("UI/Weapons/" + iconKey.Substring(5));
                art[iconKey] = icon;
            }
            if (icon != null) return icon;
        }
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
                created.Add(clean);
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
        projectRevision = WorkshopProjects.Revision;
        currency.text = GameText.Source("workshoptree.462") + MetaProgress.Currency;
        if (goalLabel != null) goalLabel.text = GameText.Source("projects.goal.prefix") + WorkshopProjects.Goal;
        foreach (Node node in nodes)
        {
            bool owned = false, affordable = false;
            node.badge.text = "";
            if (node.project != null)
            {
                int rank = WorkshopProjects.Rank(node.project);
                owned = rank > 0;
                affordable = WorkshopProjects.CanBuy(node.project);
                node.state.text = rank + " / 3";
                if (WorkshopProjects.Target >= 0 && WorkshopProjects.Ids[WorkshopProjects.Target] == node.project) node.badge.text = GameText.Source("projects.target");
            }
            else if (node.body != null)
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
                node.state.text = owned ? GameText.Source("tuning.level.short") + WeaponTuning.Rank(node.weapon) + " / 5" : unlock != null ? unlock.cost + GameText.Source("workshoptree.464") : GameText.Source("workshoptree.465");
                affordable = owned ? WeaponTuning.CanBuy(node.weapon) : affordable;
            }
            else
            {
                int rank = WeaponElements.Rank(node.weapon, node.element);
                owned = rank > 0;
                affordable = WeaponElements.CanBuy(node.weapon, node.element);
                node.state.text = WeaponElements.IsLocked(node.weapon) || !MetaProgress.IsWeaponUnlocked(node.weapon) ? GameText.Source("workshoptree.465") :
                    rank + "/3";
                if (rank > 0 && WeaponElements.Equipped(node.weapon) == node.element) node.badge.text = GameText.Source("workshoptree.466");
            }
            Color accent = node.element == WeaponElement.None ? node.body != null || node.project != null ? GameUi.Accent : Gold : WeaponElements.Colors[(int)node.element];
            node.state.color = owned ? GameUi.Accent : affordable ? Gold : new Color(.55f, .62f, .68f);
            node.diamond.Edge = node == selected ? Gold : owned ? accent : affordable ? accent * .7f : Dim;
            node.diamond.Selected = node == selected;
            node.diamond.Glow = affordable;
            node.diamond.SetVerticesDirty();
            node.picture.color = owned || affordable || node == selected ? Color.white : new Color(.4f, .45f, .5f, .7f);
            if (node.keyIcon != null) { bool keyed = WeaponElements.IsLocked(node.weapon); node.keyIcon.enabled = keyed; if (keyed) node.picture.color = new Color(.35f, .4f, .45f, .55f); }
            if (node.platform != null) { node.platform.Tint = node.element == WeaponElement.None ? GameUi.Accent : accent; node.platform.Active = node == selected || owned; }
            node.owned = owned;
            node.affordable = affordable;
        }
        RefreshBranchActivity();
        RefreshDetails();
    }

    private void RefreshBranchActivity()
    {
        foreach (Node node in nodes)
            if (node.platform != null) node.platform.Active = node == selected || node.owned || node.diamond.Hovered;
    }

    private void RefreshDetails()
    {
        if (selected == null) return;
        if (detailNode != selected) { detailNode = selected; detailAge = 0f; }
        bool equipment = selected.weapon >= 0;
        SetEquipmentPage(equipment);
        if (equipment) { RefreshEquipmentDetails(); return; }
        title.text = selected.title;
        state.color = GameUi.Accent;
        bool project = selected.project != null;
        projectOption.gameObject.SetActive(false);
        description.rectTransform.anchorMin = new Vector2(.04f, project ? .36f : .305f);
        description.rectTransform.anchorMax = new Vector2(.96f, project ? .66f : .585f);
        title.rectTransform.anchorMin = new Vector2(.025f, project ? .67f : .59f);
        title.rectTransform.anchorMax = new Vector2(.975f, project ? .77f : .70f);
        preview.rectTransform.anchorMin = new Vector2(.12f, project ? .77f : .70f);
        state.rectTransform.anchorMin = new Vector2(.03f, project ? .31f : .225f);
        state.rectTransform.anchorMax = new Vector2(.97f, project ? .36f : .30f);
        description.resizeTextMaxSize = project ? 14 : 17;
        bool module = selected.element != WeaponElement.None;
        preview.sprite = module ? WeaponIcon(selected.weapon) : selected.picture.sprite;
        elementPreview.sprite = module ? selected.picture.sprite : null;
        elementPreview.gameObject.SetActive(module);
        category.text = project ? GameText.Source("projects.category") : selected.body != null ? GameText.Source("workshoptree.467") : module
            ? GameText.Source(WeaponElements.IsBranch(selected.element) ? "workshop.branch.caption" : "workshoptree.468")
            : GameText.Source("workshoptree.469");
        equip.gameObject.SetActive(selected.element != WeaponElement.None);
        buy.interactable = false;
        if (project)
        {
            string id = selected.project;
            int rank = WorkshopProjects.Rank(id);
            string details = "";
            for (int stage = 0; stage < 3; stage++) details += (stage > 0 ? "\n" : "") + (stage < rank ? "✓ " : "○ ") + WorkshopProjects.Reward(id, stage);
            description.text = details + "\n\n" + (id == "weapons" ? WorkshopProjects.MasteryText(shooter.WeaponIndex) + "\n" + GameText.Source("projects.mastery.bonus") : WorkshopProjects.OptionDescription(id));
            state.text = WorkshopProjects.Requirement(id);
            buyText.text = rank == 3 ? GameText.Source("projects.complete") : GameText.Source("projects.assemble") + WorkshopProjects.Cost(id);
            buy.interactable = WorkshopProjects.CanBuy(id);
            equip.gameObject.SetActive(true);
            equip.interactable = true;
            equipText.text = WorkshopProjects.Target >= 0 && WorkshopProjects.Ids[WorkshopProjects.Target] == id ? GameText.Source("projects.untrack") : GameText.Source("projects.track");
            projectOption.gameObject.SetActive(rank > 0 && (id == "relics" || id == "drones" || id == "scanner"));
            projectOptionText.text = WorkshopProjects.Option(id) + "  →";
        }
        else if (selected.body != null)
        {
            MetaUpgrade upgrade = selected.body;
            int rank = MetaProgress.Rank(upgrade.id);
            description.text = upgrade.description + GameText.Source("workshoptree.470") + MetaProgress.EffectText(upgrade, rank) +
                (rank < upgrade.maxRank ? GameText.Source("workshoptree.471") + MetaProgress.EffectText(upgrade, rank + 1) : "");
            state.text = GameText.Source("workshoptree.472") + rank + " / " + upgrade.maxRank;
            buyText.text = rank == upgrade.maxRank ? GameText.Source("workshoptree.473") : GameText.Source("workshoptree.474") + upgrade.Cost(rank);
            buy.interactable = MetaProgress.CanBuy(upgrade);
            if (!buy.interactable && rank < upgrade.maxRank) state.text = GameText.Source("workshoptree.475") + (upgrade.Cost(rank) - MetaProgress.Currency) + GameText.Source("workshoptree.476");
        }
        else if (selected.element == WeaponElement.None)
        {
            bool owned = MetaProgress.IsWeaponUnlocked(selected.weapon);
            MetaUnlock unlock = MetaProgress.FindUnlock(MetaProgress.WeaponKey(selected.weapon));
            description.text = GameText.Source(WeaponElements.HasBranches(selected.weapon) ? "workshop.branch.description" : "workshoptree.477");
            state.text = owned ? GameText.Source("workshoptree.478") : GameText.Source("workshoptree.479");
            buyText.text = owned ? GameText.Source("workshoptree.480") : GameText.Source("workshoptree.481") + (unlock != null ? unlock.cost : 0);
            buy.interactable = !owned && unlock != null && MetaProgress.Currency >= unlock.cost;
            if (!owned && unlock != null && !buy.interactable) state.text = GameText.Source("workshoptree.475") + (unlock.cost - MetaProgress.Currency) + GameText.Source("workshoptree.476");
        }
        else
        {
            int rank = WeaponElements.Rank(selected.weapon, selected.element);
            bool unlocked = MetaProgress.IsWeaponUnlocked(selected.weapon);
            bool active = WeaponElements.Equipped(selected.weapon) == selected.element;
            description.text = (rank == 0 ? GameText.Source("workshoptree.482") : GameText.Source("workshoptree.483")) + Summary(selected.element, Mathf.Max(1, rank)) +
                (rank > 0 && rank < 3 ? GameText.Source("workshoptree.484") + (rank + 1) + ":\n" + Summary(selected.element, rank + 1) : "") +
                GameText.Source("workshoptree.485");
            state.text = !unlocked ? GameText.Source("workshoptree.486") : rank == 0 ? GameText.Source("workshoptree.487") : GameText.Source("workshoptree.472") + rank + "/3" + (active ? GameText.Source("workshoptree.488") : GameText.Source("workshoptree.489"));
            bool keyedWeapon = WeaponElements.IsLocked(selected.weapon);
            if (keyedWeapon && unlocked) state.text = GameText.Source("workshoptree.490");
            buyText.text = rank == 3 ? GameText.Source("workshoptree.473") : (rank == 0 ? GameText.Source("workshoptree.481") : GameText.Source("workshoptree.474")) + WeaponElements.Cost(rank);
            if (!unlocked) buyText.text = GameText.Source("workshoptree.491");
            buy.interactable = WeaponElements.CanBuy(selected.weapon, selected.element);
            equip.gameObject.SetActive(rank > 0 && unlocked);
            equip.interactable = unlocked && rank > 0;
            equipText.text = active ? GameText.Source(WeaponElements.IsBranch(selected.element) ? "workshop.branch.unequip" : "workshoptree.492") : GameText.Source("workshoptree.493");
            if (keyedWeapon) { if (unlocked) buyText.text = GameText.Source("workshoptree.494"); equip.gameObject.SetActive(false); }
            else             if (unlocked && rank < 3 && !buy.interactable) state.text = GameText.Source("workshoptree.475") + (WeaponElements.Cost(rank) - MetaProgress.Currency) + GameText.Source("workshoptree.476");
        }
        if (((LocalizedUiText)state).SourceText.StartsWith(GameText.Source("workshoptree.495")) || ((LocalizedUiText)state).SourceText.StartsWith(GameText.Source("workshoptree.496"))) state.color = Gold;
    }

    private static string Summary(WeaponElement element, int rank)
    {
        switch (element)
        {
            case WeaponElement.Fire: return GameText.Source("workshoptree.497") + rank * 8 + GameText.Source("workshoptree.498");
            case WeaponElement.Frost: return GameText.Source("workshoptree.499") + (10 + rank * 10) + GameText.Source("workshoptree.500");
            case WeaponElement.Lightning: return GameText.Source("workshoptree.501") + rank + GameText.Source("workshoptree.502");
            case WeaponElement.Poison: return GameText.Source("workshoptree.503") + rank * 5 + GameText.Source("workshoptree.504");
            case WeaponElement.Revolver:
            case WeaponElement.Dual:
            case WeaponElement.Sticky:
            case WeaponElement.Assault:
            case WeaponElement.Minigun:
            case WeaponElement.Heavy: return WeaponElements.Description(element, rank);
            default: return "";
        }
    }

    public void PurchaseSelected()
    {
        if (selected == null) return;
        if (selected.weapon >= 0) { if (selected.element == WeaponElement.None) PurchaseWeaponTuning(); else PurchaseEquipmentBranch(); return; }
        Node bought = selected;
        int before = MetaProgress.Currency;
        if (selected.project != null) WorkshopProjects.Buy(selected.project);
        else if (selected.body != null) MetaProgress.Buy(selected.body.id);
        else if (selected.element == WeaponElement.None) MetaProgress.TryUnlock(MetaProgress.WeaponKey(selected.weapon));
        else WeaponElements.Buy(selected.weapon, selected.element);
        Refresh();
        if (MetaProgress.Currency < before && bought.rect != null) { detailAge = 0f; Burst(bought); }
    }

    // Expanding ring around a node that was just bought.
    private void Burst(Node node)
    {
        if (node.platform != null) node.platform.Celebrate();
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
        if (selected == null) return;
        if (selected.project != null) { WorkshopProjects.Track(selected.project); Refresh(); return; }
        if (selected.element == WeaponElement.None) return;
        WeaponElements.Equip(selected.weapon, WeaponElements.Equipped(selected.weapon) == selected.element ? WeaponElement.None : selected.element);
        Refresh();
    }

    private void OnDestroy()
    {
        // Уничтожаем только созданные здесь спрайты. Иконки оружия — ассеты Resources (общие с экраном выбора):
        // их уничтожение после возврата в меню оставляло мастерскую и выбор оружия без картинок.
        foreach (Sprite sprite in created) if (sprite != null) Destroy(sprite);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public class WorkshopDiamond : MaskableGraphic, IPointerEnterHandler, IPointerExitHandler
{
    public Color Edge = GameUi.Accent;
    public bool Selected, Glow;
    public RectTransform Picture;
    public System.Action HoverChanged;
    public bool Hovered { get; private set; }
    private float emphasis;

    public void OnPointerEnter(PointerEventData data) { Hovered = true; HoverChanged?.Invoke(); }
    public void OnPointerExit(PointerEventData data) { Hovered = false; HoverChanged?.Invoke(); }
    protected override void OnDisable()
    {
        base.OnDisable();
        Hovered = false;
        emphasis = 0f;
        if (Picture != null) Picture.localScale = Vector3.one;
    }

    // Nodes that can be bought right now breathe with a soft halo.
    private void Update()
    {
        float target = Hovered || Selected ? 1f : 0f;
        float previous = emphasis;
        emphasis = Mathf.Lerp(emphasis, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
        if (Picture != null) Picture.localScale = Vector3.one * (1f + emphasis * .06f);
        if (Glow || Selected || !Mathf.Approximately(previous, emphasis)) SetVerticesDirty();
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
        if (Glow || emphasis > .01f)
        {
            Color halo = Edge;
            halo.a = .12f + emphasis * .2f + (Glow ? .14f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 2.4f)) : 0f);
            WorkshopTreeLines.Strip(mesh, outline, 10f, 6f, halo, true);
        }
        WorkshopTreeLines.Strip(mesh, outline, 2f, 1f, Edge, true);
        if (Selected)
        {
            var halo = new List<Vector2>();
            foreach (Vector2 corner in corners) halo.Add(corner * (1.12f + .018f * Mathf.Sin(Time.unscaledTime * 2f)));
            WorkshopTreeLines.Strip(mesh, halo, 1.5f, 1f, Edge, true);
        }
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public class WorkshopTreeLines : MaskableGraphic
{
    private struct Path { public Vector2 a, b; public Color color; public float brightness; public bool active; }
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

    public void SetActivity(int index, bool active)
    {
        if (index < 0 || index >= paths.Count || paths[index].active == active) return;
        Path path = paths[index]; path.active = active; paths[index] = path;
        SetVerticesDirty();
    }

    private static Vector2 Point(Path path, float t)
    {
        Vector2 a = new Vector2(path.a.x, -path.a.y), b = new Vector2(path.b.x, -path.b.y);
        if (Mathf.Approximately(a.x, b.x) || Mathf.Approximately(a.y, b.y)) return Vector2.Lerp(a, b, t);
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
            if (path.brightness < 1.2f && !path.active) continue;
            float lead = Mathf.Repeat(time * (path.active ? .65f : .32f) + i * .173f, path.active ? 1.25f : 1.7f);
            for (int tail = 0; tail < 4; tail++)
            {
                float t = lead - tail * .04f;
                if (t < 0f || t > 1f) continue;
                Color tint = Color.Lerp(path.color, Color.white, .65f);
                tint.a = .95f - tail * .22f;
                Dot(mesh, Point(path, t), (path.active ? 6f : 4f) - tail * .7f, tint);
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
            if (path.active) lineColor = Color.Lerp(lineColor, new Color(.65f, .95f, 1f, .95f), .55f);
            // One continuous strip per curve (no per-segment overlaps, so no beads at the joints) with
            // feathered edges: the outer vertices fade to transparent, which gives smooth anti-aliasing.
            var points = new List<Vector2>(33);
            for (int i = 0; i <= 32; i++)
            {
                points.Add(Point(path, i / 32f));
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
    private const float Duration = .95f;
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
        if (progress < .65f)
        {
            for (int i = 0; i < points.Count; i++) points[i] = rect.center + (points[i] - rect.center) * .78f;
            Color inner = Color.Lerp(Tint, Color.white, .65f); inner.a = (1f - progress / .65f) * .7f;
            WorkshopTreeLines.Strip(mesh, points, 2f, 1f, inner, true);
        }
        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.PI * 2f / 10f + .2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Color spark = Color.Lerp(Tint, Color.white, .55f); spark.a = (1f - progress) * .85f;
            WorkshopTreeLines.Strip(mesh, new List<Vector2> { rect.center + direction * radius,
                rect.center + direction * (radius + 10f * (1f - progress)) }, 2f, 1f, spark, false);
        }
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
        for (int tick = 0; tick < 36; tick++)
        {
            float angle = tick * Mathf.PI * 2f / 36f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float shimmer = .18f + .28f * Mathf.Pow(.5f + .5f * Mathf.Cos(angle - time * .5f), 6f);
            WorkshopTreeLines.Strip(mesh, new List<Vector2> { center + direction * 123f,
                center + direction * (tick % 3 == 0 ? 130f : 126f) }, 1.2f, .8f, new Color(.4f, .85f, 1f, shimmer), false);
        }
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
