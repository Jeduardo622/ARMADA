#if UNITY_EDITOR
using Armada.Client.Bootstrap;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds (or rebuilds) Assets/Scenes/SpectatorDemo.unity: the spectate-only
/// Mission 10 scene. Deterministic and idempotent so the checked-in scene can
/// be regenerated after tuning the placeholder constants. Runs from the menu
/// or via -batchmode -executeMethod SpectatorDemoSceneBuilder.Build.
/// </summary>
public static class SpectatorDemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/SpectatorDemo.unity";
    private const string ConfigAssetPath = "Assets/Scenes/SpectatorDemoClientConfig.asset";
    private const string BoardMaterialPath = "Assets/Art/Shared/mat-sea-painterly.mat";

    [MenuItem("Assets/Armada/Build Spectator Demo Scene")]
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
        // 0.1 world units per sim unit). Placeholder framing values.
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
        board.transform.localScale = new Vector3(30f, 1f, 16f);
        board.GetComponent<Renderer>().sharedMaterial = boardMaterial;
        var water = board.AddComponent<WaterAnimator>();
        SetReference(water, "waterRenderer", board.GetComponent<Renderer>());

        // GraphicRaycaster + EventSystem are new with the D2-B playback
        // buttons: this scene was previously spectate-only with no
        // interactive UI at all.
        var canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        ConfigureScaler(canvasObject.GetComponent<CanvasScaler>());
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        // Every HUD element parents under the safe-area wrapper so notches
        // and rounded corners never clip it on device (D2-B, mobile-first).
        var safeArea = CreateSafeArea(canvasObject.transform);

        var hudLabel = CreateLabel(safeArea, "SpectatorHud", anchorTop: true);
        hudLabel.text = "Waiting for run... (buttons below; keys: Space pause, Right Arrow step, 1-4 speed, +/- cycle)";
        var statusLabel = CreateLabel(safeArea, "MissionStatus", anchorTop: false);
        statusLabel.text = string.Empty;
        // Conditions zone (W4 IA item 3): compact numeric wind/turn readout
        // below the narration; sized to clear the portrait 3-line wrap of
        // this scene's long narration/hint line.
        var conditionsLabel = CreateLabel(safeArea, "Conditions", anchorTop: true);
        var conditionsRect = (RectTransform)conditionsLabel.transform;
        conditionsRect.anchoredPosition = new Vector2(0f, -180f);
        conditionsRect.sizeDelta = new Vector2(-64f, 44f);
        conditionsLabel.fontSize = 24f;
        conditionsLabel.text = string.Empty;

        var spectatorObject = new GameObject("Spectator", typeof(SpectatorRenderer));
        var spectator = spectatorObject.GetComponent<SpectatorRenderer>();
        SetReference(spectator, "hudLabel", hudLabel);
        SetReference(spectator, "conditionsLabel", conditionsLabel);
        ShipViewProviderWiring.Attach(spectator);
        BoardFeatureWiring.Attach(spectator);

        // On-screen playback controls (D2-B touch controls): pause/step/speed
        // buttons calling the same renderer API as the keyboard bindings.
        var playbackObject = new GameObject("PlaybackControls", typeof(PlaybackControlsController));
        var playbackControls = playbackObject.GetComponent<PlaybackControlsController>();
        SetReference(playbackControls, "spectator", spectator);
        var playbackGrid = CreateButtonGrid(safeArea, "PlaybackButtons", edgeOffset: 116f);
        var pauseCaption = CreateButton(playbackGrid, "Pause", playbackControls.OnTogglePause);
        SetReference(playbackControls, "pauseLabel", pauseCaption);
        CreateButton(playbackGrid, "Step", playbackControls.OnStep);
        CreateButton(playbackGrid, "Speed -", playbackControls.OnSpeedDown);
        CreateButton(playbackGrid, "Speed +", playbackControls.OnSpeedUp);

        // Runtime stacking (Codex P2 on PR #84): a single strip here, kept
        // above the bottom status label; the stacker still owns its offset so
        // the convention matches the interactive scenes.
        var stackerObject = new GameObject("HudStripStacker", typeof(BottomStripStacker));
        var stacker = stackerObject.GetComponent<BottomStripStacker>();
        SetStripArray(stacker, "strips", (RectTransform)playbackGrid);
        SetFloat(stacker, "edgeOffset", 116f);

        var missionUIObject = new GameObject("MissionUI", typeof(MissionUIController));
        var missionUI = missionUIObject.GetComponent<MissionUIController>();
        SetReference(missionUI, "statusLabel", statusLabel);
        // Inactive because the spectator demo has no use for the automatic
        // missions-list refresh in MissionUIController.Start. (Historically
        // this also avoided a startup auth race, before AuthService shared
        // its in-flight token request among concurrent callers.)
        // CompleteMission10 is a plain method call and still reports through
        // the status label on the inactive object.
        missionUIObject.SetActive(false);

        var bootstrapObject = new GameObject("Mission10Bootstrap", typeof(DeterministicSimHooks), typeof(Mission10Bootstrap), typeof(MobilePresentation));
        var bootstrap = bootstrapObject.GetComponent<Mission10Bootstrap>();
        SetReference(bootstrap, "clientConfig", config);
        SetReference(bootstrap, "determinism", bootstrapObject.GetComponent<DeterministicSimHooks>());
        SetReference(bootstrap, "missionUI", missionUI);
        SetReference(bootstrap, "spectator", spectator);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[SpectatorDemoSceneBuilder] Saved {ScenePath}");
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

    // Mobile-first canvas scaling (D2-B): author at 1920×1080 landscape and
    // scale with screen height, so text keeps physical size across
    // phone/tablet aspect ratios; wider screens gain horizontal room.
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

    // Wrapping button strip (shared geometry with the other scene builders;
    // see the D2-B notes there): 190×140 cells clear the 44 pt touch floor on
    // the minimum supported device, and the grid wraps on narrow aspects.
    private static Transform CreateButtonGrid(Transform parent, string name, float edgeOffset, bool anchorTop = false)
    {
        var gridObject = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        gridObject.transform.SetParent(parent, worldPositionStays: false);
        var rect = gridObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorTop ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = anchorTop ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = anchorTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, anchorTop ? -edgeOffset : edgeOffset);
        rect.sizeDelta = new Vector2(-48f, 0f);
        var grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(190f, 140f);
        grid.spacing = new Vector2(12f, 12f);
        grid.startCorner = anchorTop ? GridLayoutGroup.Corner.UpperLeft : GridLayoutGroup.Corner.LowerLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = anchorTop ? TextAnchor.UpperLeft : TextAnchor.LowerLeft;
        gridObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return gridObject.transform;
    }

    private static TextMeshProUGUI CreateButton(Transform parent, string label, UnityAction handler)
    {
        var buttonObject = new GameObject($"Button-{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, worldPositionStays: false);

        buttonObject.GetComponent<Image>().color = new Color(0.15f, 0.25f, 0.4f, 0.9f);
        UnityEventTools.AddVoidPersistentListener(buttonObject.GetComponent<Button>().onClick, handler);

        var labelObject = new GameObject("Label", typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, worldPositionStays: false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        var text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 26f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return text;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, bool anchorTop)
    {
        var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, worldPositionStays: false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorTop ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = anchorTop ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = anchorTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchorTop ? new Vector2(0f, -16f) : new Vector2(0f, 16f);
        rect.sizeDelta = new Vector2(-64f, 84f);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 32f;
        label.color = Color.white;
        return label;
    }


    private static void SetStripArray(Component component, string fieldName, params RectTransform[] strips)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogError($"[SpectatorDemoSceneBuilder] Missing serialized field '{fieldName}' on {component.GetType().Name}.");
            return;
        }

        property.arraySize = strips.Length;
        for (var i = 0; i < strips.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = strips[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Component component, string fieldName, float value)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogError($"[SpectatorDemoSceneBuilder] Missing serialized field '{fieldName}' on {component.GetType().Name}.");
            return;
        }

        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetReference(Component component, string fieldName, Object value)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogError($"[SpectatorDemoSceneBuilder] Missing serialized field '{fieldName}' on {component.GetType().Name}.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
