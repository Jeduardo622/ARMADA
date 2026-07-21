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
/// Builds (or rebuilds) Assets/Scenes/PvPHotseatDemo.unity: the two-player
/// hot-seat PvP demo scene. Deterministic and idempotent so the checked-in
/// scene can be regenerated after tuning the placeholder constants. Runs
/// from the menu or via -batchmode -executeMethod PvPHotseatDemoSceneBuilder.Build.
/// </summary>
public static class PvPHotseatDemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/PvPHotseatDemo.unity";
    private const string ConfigAssetPath = "Assets/Scenes/PvPHotseatClientConfig.asset";
    // The board material is shared with the spectator demo scene.
    private const string BoardMaterialPath = "Assets/Scenes/SpectatorBoardMat.mat";

    [MenuItem("Assets/Armada/Build PvP Hotseat Demo Scene")]
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
        // 0.1 world units per sim unit). Placeholder framing values shared
        // with the spectator demo.
        var cameraObject = new GameObject("Main Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.08f, 0.15f);
        cameraObject.transform.position = new Vector3(11f, 20f, 0f);
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var lightObject = new GameObject("Directional Light", typeof(Light));
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.position = new Vector3(11f, -0.55f, 0f);
        board.transform.localScale = new Vector3(30f, 1f, 16f);
        board.GetComponent<Renderer>().sharedMaterial = boardMaterial;

        var canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // uGUI buttons need an EventSystem to receive clicks (legacy input
        // module; the Input System package is not in-project).
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        var hudLabel = CreateLabel(canvasObject.transform, "SpectatorHud", anchorTop: true, height: 60f, offsetY: -10f);
        hudLabel.text = "PvP hot-seat: waiting for match start...";
        var statusLabel = CreateLabel(canvasObject.transform, "PhaseStatus", anchorTop: true, height: 40f, offsetY: -75f);
        statusLabel.text = string.Empty;
        var orderLabel = CreateLabel(canvasObject.transform, "OrderPanel", anchorTop: false, height: 140f, offsetY: 70f);
        orderLabel.text = string.Empty;

        var spectatorObject = new GameObject("Spectator", typeof(SpectatorRenderer));
        var spectator = spectatorObject.GetComponent<SpectatorRenderer>();
        SetReference(spectator, "hudLabel", hudLabel);

        var orderUIObject = new GameObject("PvpOrderUI", typeof(PvpHotseatUIController));
        var orderUI = orderUIObject.GetComponent<PvpHotseatUIController>();
        SetReference(orderUI, "orderLabel", orderLabel);
        SetReference(orderUI, "statusLabel", statusLabel);

        // Order-entry button strip along the bottom edge, above the order
        // panel text. Layout values are design-tunable placeholders.
        var buttons = new (string label, UnityAction handler)[]
        {
            ("Next Ship", orderUI.OnNextShip),
            ("Turn <", orderUI.OnTurnLeft),
            ("Turn >", orderUI.OnTurnRight),
            ("Speed -", orderUI.OnSpeedDown),
            ("Speed +", orderUI.OnSpeedUp),
            ("Target", orderUI.OnCycleTarget),
            ("Ammo", orderUI.OnToggleAmmo),
            ("Confirm Side", orderUI.OnConfirmSide)
        };
        for (var i = 0; i < buttons.Length; i++)
        {
            CreateButton(canvasObject.transform, buttons[i].label, i, buttons.Length, buttons[i].handler);
        }

        var bootstrapObject = new GameObject("PvpHotseatBootstrap", typeof(DeterministicSimHooks), typeof(PvpHotseatBootstrap));
        var bootstrap = bootstrapObject.GetComponent<PvpHotseatBootstrap>();
        SetReference(bootstrap, "clientConfig", config);
        SetReference(bootstrap, "determinism", bootstrapObject.GetComponent<DeterministicSimHooks>());
        SetReference(bootstrap, "orderUI", orderUI);
        SetReference(bootstrap, "spectator", spectator);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PvPHotseatDemoSceneBuilder] Saved {ScenePath}");
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
            material = new Material(Shader.Find("Standard"))
            {
                // Placeholder sea color pending art direction.
                color = new Color(0.07f, 0.22f, 0.36f)
            };
            AssetDatabase.CreateAsset(material, BoardMaterialPath);
        }

        return material;
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
        rect.sizeDelta = new Vector2(-40f, height);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 18f;
        label.color = Color.white;
        return label;
    }

    private static void CreateButton(Transform parent, string label, int index, int count, UnityAction handler)
    {
        var buttonObject = new GameObject($"Button-{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, worldPositionStays: false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        var width = 130f;
        rect.anchoredPosition = new Vector2(20f + index * (width + 8f), 20f);
        rect.sizeDelta = new Vector2(width, 40f);

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.15f, 0.25f, 0.4f, 0.9f);

        var button = buttonObject.GetComponent<Button>();
        UnityEventTools.AddVoidPersistentListener(button.onClick, handler);

        var labelObject = new GameObject("Label", typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, worldPositionStays: false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        var text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 16f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    private static void SetReference(Component component, string fieldName, Object value)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogError($"[PvPHotseatDemoSceneBuilder] Missing serialized field '{fieldName}' on {component.GetType().Name}.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
