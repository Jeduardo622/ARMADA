#if UNITY_EDITOR
using Armada.Client.Bootstrap;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.Services;
using Armada.Client.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds (or rebuilds) Assets/Scenes/Campaign.unity: the campaign front
/// end. A mission-select menu lists all ten missions; picking one shows its
/// briefing, Set Sail hands it to the mission-agnostic MissionPlayController,
/// and a finished run offers Retry or a return to the list. Deterministic
/// and idempotent so the checked-in scene can be regenerated after tuning;
/// runs from the menu, from Rebuild All Generated Scenes, or via
/// -batchmode -executeMethod CampaignSceneBuilder.Build.
///
/// Layout follows the other generated scenes (D2-B mobile-first): canvas
/// scaled from a 1920×1080 reference by height, every HUD element under the
/// safe-area wrapper, 190×140 button cells (≥ 44 pt on iPhone 8), wrapping
/// bottom strips restacked at runtime by BottomStripStacker.
/// </summary>
public static class CampaignSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Campaign.unity";
    private const string ConfigAssetPath = "Assets/Scenes/CampaignClientConfig.asset";
    // The painterly sea material is shared with every generated scene.
    private const string BoardMaterialPath = "Assets/Art/Shared/mat-sea-painterly.mat";

    [MenuItem("Assets/Armada/Build Campaign Scene")]
    public static void Build()
    {
        // Opening a new scene in Single mode discards the current one; give
        // the user the standard save/discard/cancel prompt first. Returns
        // true without prompting in batch mode.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        var config = LoadOrCreateConfig();
        var boardMaterial = LoadOrCreateBoardMaterial();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Top-down orthographic camera framing sim space (x 0-250, y ±60 at
        // 0.1 world units per sim unit); the follow camera re-frames it to
        // the living fleets every tick.
        var cameraObject = new GameObject("Main Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.08f, 0.15f);
        cameraObject.transform.position = new Vector3(12.5f, 20f, 0f);
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var lightObject = new GameObject("Directional Light", typeof(Light));
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.position = new Vector3(12.5f, -0.55f, 0f);
        // Same roomy board as the Mission 10 play scene: freely-manoeuvring
        // fleets sail well outside any fixture line in every mission.
        board.transform.localScale = new Vector3(120f, 1f, 100f);
        board.GetComponent<Renderer>().sharedMaterial = boardMaterial;
        var water = board.AddComponent<WaterAnimator>();
        SetReference(water, "waterRenderer", board.GetComponent<Renderer>());

        var canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        ConfigureScaler(canvasObject.GetComponent<CanvasScaler>());

        // uGUI buttons need an EventSystem to receive clicks (legacy input
        // module; the Input System package is not in-project). Touch input
        // flows through the same module.
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        var safeArea = CreateSafeArea(canvasObject.transform);

        // --- Always-on HUD zones (W4 IA items 1-3) ---
        var hudLabel = CreateLabel(safeArea, "SpectatorHud", anchorTop: true, height: 84f, offsetY: -16f);
        hudLabel.text = "Armada: authenticating...";
        var statusLabel = CreateLabel(safeArea, "MissionStatus", anchorTop: true, height: 56f, offsetY: -112f);
        statusLabel.text = string.Empty;
        var conditionsLabel = CreateLabel(safeArea, "Conditions", anchorTop: true, height: 44f, offsetY: -180f);
        conditionsLabel.fontSize = 24f;
        conditionsLabel.text = string.Empty;

        var spectatorObject = new GameObject("Spectator", typeof(SpectatorRenderer));
        var spectator = spectatorObject.GetComponent<SpectatorRenderer>();
        SetReference(spectator, "hudLabel", hudLabel);
        SetReference(spectator, "conditionsLabel", conditionsLabel);
        SetReference(spectator, "followCamera", camera);
        ShipViewProviderWiring.Attach(spectator);
        BoardFeatureWiring.Attach(spectator);

        var playUIObject = new GameObject("MissionPlayUI", typeof(MissionPlayController));
        var playUI = playUIObject.GetComponent<MissionPlayController>();

        var missionUIObject = new GameObject("MissionUI", typeof(MissionUIController));
        var missionUI = missionUIObject.GetComponent<MissionUIController>();
        SetReference(missionUI, "statusLabel", statusLabel);
        // Inactive: this scene has no use for the automatic missions-list
        // refresh in MissionUIController.Start. CompletePlayableAsync is a
        // plain method call and still reports on the inactive object.
        missionUIObject.SetActive(false);

        var campaignObject = new GameObject("Campaign", typeof(CampaignController));
        var campaign = campaignObject.GetComponent<CampaignController>();

        // --- Battle controls (shown only in battle) ---
        var battleControls = CreateStretchPanel(safeArea, "BattleControls");

        var orderLabel = CreateLabel(battleControls, "OrderPanel", anchorTop: false, height: 200f, offsetY: 180f);
        orderLabel.text = string.Empty;
        var orderRowsObject = new GameObject("OrderRows", typeof(RectTransform), typeof(OrderPanelView));
        orderRowsObject.transform.SetParent(battleControls, worldPositionStays: false);
        var orderRowsRect = orderRowsObject.GetComponent<RectTransform>();
        orderRowsRect.anchorMin = new Vector2(0f, 0f);
        orderRowsRect.anchorMax = new Vector2(1f, 0f);
        orderRowsRect.pivot = new Vector2(0.5f, 0f);
        orderRowsRect.anchoredPosition = new Vector2(24f, 390f);
        orderRowsRect.sizeDelta = new Vector2(-48f, 200f);
        var orderPanel = orderRowsObject.GetComponent<OrderPanelView>();
        SetReference(orderPanel, "rowContainer", orderRowsRect);

        var playbackObject = new GameObject("PlaybackControls", typeof(PlaybackControlsController));
        var playbackControls = playbackObject.GetComponent<PlaybackControlsController>();
        SetReference(playbackControls, "spectator", spectator);
        var playbackGrid = CreateButtonGrid(battleControls, "PlaybackButtons", edgeOffset: 24f);
        var pauseCaption = CreateButton(playbackGrid, "Pause", playbackControls.OnTogglePause);
        SetReference(playbackControls, "pauseLabel", pauseCaption);
        CreateButton(playbackGrid, "Step", playbackControls.OnStep);
        CreateButton(playbackGrid, "Speed -", playbackControls.OnSpeedDown);
        CreateButton(playbackGrid, "Speed +", playbackControls.OnSpeedUp);

        var orderGrid = CreateButtonGrid(battleControls, "OrderButtons", edgeOffset: 24f);
        CreateButton(orderGrid, "Next Ship", playUI.OnNextShip);
        CreateButton(orderGrid, "Turn <", playUI.OnTurnLeft);
        CreateButton(orderGrid, "Turn >", playUI.OnTurnRight);
        CreateButton(orderGrid, "Speed -", playUI.OnSpeedDown);
        CreateButton(orderGrid, "Speed +", playUI.OnSpeedUp);
        CreateButton(orderGrid, "Target", playUI.OnCycleTarget);
        var ammoButton = CreateButton(orderGrid, "Ammo", playUI.OnToggleAmmo).transform.parent.gameObject;
        var boardButton = CreateButton(orderGrid, "Board", playUI.OnToggleBoarding).transform.parent.gameObject;
        CreateButton(orderGrid, "Confirm Turn", playUI.OnConfirmTurn);
        CreateButton(orderGrid, "Undo Turn", playUI.OnUndoTurn);

        SetReference(playUI, "orderLabel", orderLabel);
        SetReference(playUI, "orderPanel", orderPanel);
        SetReference(playUI, "statusLabel", statusLabel);
        SetReference(playUI, "ammoControl", ammoButton);
        SetReference(playUI, "boardingControl", boardButton);

        // Runtime stacking: grids wrap to different row counts as the
        // viewport narrows, so the stacker repositions the strips (and the
        // order text/rows riding above them) from their live heights.
        var stackerObject = new GameObject("HudStripStacker", typeof(BottomStripStacker));
        var stacker = stackerObject.GetComponent<BottomStripStacker>();
        SetStripArray(stacker, "strips", (RectTransform)orderGrid, (RectTransform)playbackGrid);
        SetStripArray(stacker, "risers", (RectTransform)orderLabel.transform, orderRowsRect);

        // --- Result controls (shown once a run has ended) ---
        var resultControls = CreateStretchPanel(safeArea, "ResultControls");
        var resultGrid = CreateButtonGrid(resultControls, "ResultButtons", edgeOffset: 24f);
        CreateButton(resultGrid, "Retry", campaign.OnRetry);
        CreateButton(resultGrid, "Missions", campaign.OnReturnToMissions);

        // --- Mission select menu (shown outside battle) ---
        var menuPanel = CreateStretchPanel(safeArea, "MenuPanel");
        // Inset below the narration/status/conditions zones (top 224 units)
        // so the HUD line stays readable over the menu.
        ((RectTransform)menuPanel).offsetMax = new Vector2(0f, -224f);
        var menuBackdrop = menuPanel.gameObject.AddComponent<Image>();
        // Parchment skin slot (art-needs P4); a dim navy wash until then.
        menuBackdrop.color = new Color(0.02f, 0.06f, 0.12f, 0.82f);

        // Two columns above the wrapped button strips: eleven list lines at
        // 24 pt fit 340 units, which clears three 152-unit strip rows on the
        // 1080 reference.
        var listLabel = CreateLabel(menuPanel, "MissionList", anchorTop: true, height: 340f, offsetY: -6f);
        var listRect = (RectTransform)listLabel.transform;
        listRect.anchorMax = new Vector2(0.5f, 1f);
        listRect.anchoredPosition = new Vector2(32f, -6f);
        listRect.sizeDelta = new Vector2(-48f, 340f);
        listLabel.fontSize = 24f;
        listLabel.text = "CAMPAIGN";

        var briefingLabel = CreateLabel(menuPanel, "Briefing", anchorTop: true, height: 340f, offsetY: -6f);
        var briefingRect = (RectTransform)briefingLabel.transform;
        briefingRect.anchorMin = new Vector2(0.5f, 1f);
        briefingRect.anchoredPosition = new Vector2(-16f, -6f);
        briefingRect.sizeDelta = new Vector2(-48f, 340f);
        briefingLabel.fontSize = 24f;
        briefingLabel.text = "Choose a mission.";

        var menuActions = CreateButtonGrid(menuPanel, "MenuActions", edgeOffset: 24f);
        CreateButton(menuActions, "Set Sail", campaign.OnSetSail);
        CreateButton(menuActions, "Back", campaign.OnReturnToMissions);

        var missionGrid = CreateButtonGrid(menuPanel, "MissionButtons", edgeOffset: 24f);
        foreach (var mission in PlayableMissionCatalog.All)
        {
            CreateIntButton(missionGrid, $"{mission.Number:00} {mission.Title}", campaign.OnSelectMission, mission.Number);
        }

        var menuStackerObject = new GameObject("MenuStripStacker", typeof(BottomStripStacker));
        var menuStacker = menuStackerObject.GetComponent<BottomStripStacker>();
        SetStripArray(menuStacker, "strips", (RectTransform)menuActions, (RectTransform)missionGrid);

        SetReference(campaign, "menuPanel", menuPanel.gameObject);
        SetReference(campaign, "battleControls", battleControls.gameObject);
        SetReference(campaign, "resultControls", resultControls.gameObject);
        SetReference(campaign, "missionListLabel", listLabel);
        SetReference(campaign, "briefingLabel", briefingLabel);
        SetReference(campaign, "playController", playUI);

        // The campaign opens on the menu once authenticated; until then only
        // the menu backdrop shows.
        battleControls.gameObject.SetActive(false);
        resultControls.gameObject.SetActive(false);

        var bootstrapObject = new GameObject("CampaignBootstrap", typeof(DeterministicSimHooks), typeof(CampaignBootstrap), typeof(MobilePresentation));
        var bootstrap = bootstrapObject.GetComponent<CampaignBootstrap>();
        SetReference(bootstrap, "clientConfig", config);
        SetReference(bootstrap, "determinism", bootstrapObject.GetComponent<DeterministicSimHooks>());
        SetReference(bootstrap, "campaign", campaign);
        SetReference(bootstrap, "playUI", playUI);
        SetReference(bootstrap, "missionUI", missionUI);
        SetReference(bootstrap, "spectator", spectator);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CampaignSceneBuilder] Saved {ScenePath}");
    }

    private static ArmadaClientConfig LoadOrCreateConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<ArmadaClientConfig>(ConfigAssetPath);
        if (config == null)
        {
            // Defaults point at the local backend (http://localhost:4500).
            config = ScriptableObject.CreateInstance<ArmadaClientConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
        }

        return config;
    }

    private static Material LoadOrCreateBoardMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(BoardMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Armada/WaterPainterly"))
            {
                // The reviewed sea base color; the painterly shader bands
                // around it.
                color = new Color(0.07f, 0.22f, 0.36f)
            };
            AssetDatabase.CreateAsset(material, BoardMaterialPath);
        }

        return material;
    }

    private static void ConfigureScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
    }

    private static Transform CreateSafeArea(Transform canvas)
    {
        var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaInsets));
        safeAreaObject.transform.SetParent(canvas, worldPositionStays: false);
        var rect = safeAreaObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return safeAreaObject.transform;
    }

    // Full-stretch container filling the safe area, so strips parented to it
    // see the same HUD rect the stacker's aspect logic measures.
    private static Transform CreateStretchPanel(Transform parent, string name)
    {
        var panelObject = new GameObject(name, typeof(RectTransform));
        panelObject.transform.SetParent(parent, worldPositionStays: false);
        var rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return panelObject.transform;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, bool anchorTop, float height, float offsetY)
    {
        var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, worldPositionStays: false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorTop ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = anchorTop ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = anchorTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, offsetY);
        rect.sizeDelta = new Vector2(-64f, height);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 30f;
        label.color = Color.white;
        return label;
    }

    // Wrapping button strip; 190×140 cells keep iPhone 8 landscape above the
    // 44 pt touch floor (see Mission10PlayDemoSceneBuilder for the math).
    private static Transform CreateButtonGrid(Transform parent, string name, float edgeOffset)
    {
        var gridObject = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        gridObject.transform.SetParent(parent, worldPositionStays: false);
        var rect = gridObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, edgeOffset);
        rect.sizeDelta = new Vector2(-48f, 0f);
        var grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(190f, 140f);
        grid.spacing = new Vector2(12f, 12f);
        grid.startCorner = GridLayoutGroup.Corner.LowerLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.LowerLeft;
        gridObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return gridObject.transform;
    }

    private static TextMeshProUGUI CreateButton(Transform parent, string label, UnityAction handler)
    {
        var button = CreateButtonObject(parent, label, out var text);
        UnityEventTools.AddVoidPersistentListener(button.onClick, handler);
        return text;
    }

    private static TextMeshProUGUI CreateIntButton(Transform parent, string label, UnityAction<int> handler, int argument)
    {
        var button = CreateButtonObject(parent, label, out var text);
        UnityEventTools.AddIntPersistentListener(button.onClick, handler, argument);
        return text;
    }

    private static Button CreateButtonObject(Transform parent, string label, out TextMeshProUGUI text)
    {
        var buttonObject = new GameObject($"Button-{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, worldPositionStays: false);
        buttonObject.GetComponent<Image>().color = new Color(0.15f, 0.25f, 0.4f, 0.9f);

        var labelObject = new GameObject("Label", typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, worldPositionStays: false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return buttonObject.GetComponent<Button>();
    }

    private static void SetStripArray(Component component, string fieldName, params RectTransform[] strips)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogError($"[CampaignSceneBuilder] Missing serialized field '{fieldName}' on {component.GetType().Name}.");
            return;
        }

        property.arraySize = strips.Length;
        for (var i = 0; i < strips.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = strips[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(Component component, string fieldName, Object value)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogError($"[CampaignSceneBuilder] Missing serialized field '{fieldName}' on {component.GetType().Name}.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
