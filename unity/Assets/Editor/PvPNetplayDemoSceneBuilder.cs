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
/// Builds (or rebuilds) Assets/Scenes/PvPNetplayDemo.unity: the networked
/// two-client PvP demo scene (create/join by code, own-side order entry,
/// polling, per-turn spectator playback). Deterministic and idempotent.
/// Runs from the menu or via -batchmode -executeMethod
/// PvPNetplayDemoSceneBuilder.Build.
/// </summary>
public static class PvPNetplayDemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/PvPNetplayDemo.unity";
    private const string ConfigAssetPath = "Assets/Scenes/PvPNetplayClientConfig.asset";
    // The board material is shared with the spectator demo scene.
    private const string BoardMaterialPath = "Assets/Scenes/SpectatorBoardMat.mat";

    [MenuItem("Assets/Armada/Build PvP Netplay Demo Scene")]
    public static void Build()
    {
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

        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        var hudLabel = CreateLabel(canvasObject.transform, "SpectatorHud", anchorTop: true, height: 60f, offsetY: -10f);
        hudLabel.text = "PvP netplay: waiting for sign-in...";
        var statusLabel = CreateLabel(canvasObject.transform, "PhaseStatus", anchorTop: true, height: 40f, offsetY: -75f);
        statusLabel.text = string.Empty;
        var orderLabel = CreateLabel(canvasObject.transform, "OrderPanel", anchorTop: false, height: 140f, offsetY: 116f);
        orderLabel.text = string.Empty;

        var spectatorObject = new GameObject("Spectator", typeof(SpectatorRenderer));
        var spectator = spectatorObject.GetComponent<SpectatorRenderer>();
        SetReference(spectator, "hudLabel", hudLabel);

        var netplayUIObject = new GameObject("PvpNetplayUI", typeof(PvpNetplayUIController));
        var netplayUI = netplayUIObject.GetComponent<PvpNetplayUIController>();
        SetReference(netplayUI, "orderLabel", orderLabel);
        SetReference(netplayUI, "statusLabel", statusLabel);

        // Menu row: Create, code input, Join.
        CreateButton(canvasObject.transform, "Create Match", 0, netplayUI.OnCreateMatch, rowY: 66f);
        CreateJoinCodeInput(canvasObject.transform, netplayUI, rowY: 66f, slot: 1);
        CreateButton(canvasObject.transform, "Join Match", 2, netplayUI.OnJoinMatch, rowY: 66f);

        // Order-entry row (own side only).
        var orderButtons = new (string label, UnityAction handler)[]
        {
            ("Next Ship", netplayUI.OnNextShip),
            ("Turn <", netplayUI.OnTurnLeft),
            ("Turn >", netplayUI.OnTurnRight),
            ("Speed -", netplayUI.OnSpeedDown),
            ("Speed +", netplayUI.OnSpeedUp),
            ("Target", netplayUI.OnCycleTarget),
            ("Ammo", netplayUI.OnToggleAmmo),
            ("Confirm Orders", netplayUI.OnConfirmOrders)
        };
        for (var i = 0; i < orderButtons.Length; i++)
        {
            CreateButton(canvasObject.transform, orderButtons[i].label, i, orderButtons[i].handler, rowY: 20f);
        }

        var bootstrapObject = new GameObject("PvpNetplayBootstrap", typeof(DeterministicSimHooks), typeof(PvpNetplayBootstrap));
        var bootstrap = bootstrapObject.GetComponent<PvpNetplayBootstrap>();
        SetReference(bootstrap, "clientConfig", config);
        SetReference(bootstrap, "determinism", bootstrapObject.GetComponent<DeterministicSimHooks>());
        SetReference(bootstrap, "netplayUI", netplayUI);
        SetReference(bootstrap, "spectator", spectator);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PvPNetplayDemoSceneBuilder] Saved {ScenePath}");
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

    private static void CreateButton(Transform parent, string label, int slot, UnityAction handler, float rowY)
    {
        var buttonObject = new GameObject($"Button-{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, worldPositionStays: false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        var width = 130f;
        rect.anchoredPosition = new Vector2(20f + slot * (width + 8f), rowY);
        rect.sizeDelta = new Vector2(width, 40f);

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
        text.fontSize = 16f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    // Join-code entry uses the legacy uGUI InputField (not TMP_InputField)
    // so the interactive path has no dependency on the TMP Essentials
    // import; the built-in LegacyRuntime font renders it.
    private static void CreateJoinCodeInput(Transform parent, PvpNetplayUIController netplayUI, float rowY, int slot)
    {
        var inputObject = new GameObject("JoinCodeInput", typeof(RectTransform), typeof(Image), typeof(InputField));
        inputObject.transform.SetParent(parent, worldPositionStays: false);

        var rect = inputObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        var width = 130f;
        rect.anchoredPosition = new Vector2(20f + slot * (width + 8f), rowY);
        rect.sizeDelta = new Vector2(width, 40f);
        inputObject.GetComponent<Image>().color = new Color(0.9f, 0.9f, 0.9f, 0.95f);

        var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(inputObject.transform, worldPositionStays: false);
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-12f, -8f);
        var text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.color = Color.black;
        text.supportRichText = false;

        var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        placeholderObject.transform.SetParent(inputObject.transform, worldPositionStays: false);
        var placeholderRect = placeholderObject.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.sizeDelta = new Vector2(-12f, -8f);
        var placeholder = placeholderObject.GetComponent<Text>();
        placeholder.font = text.font;
        placeholder.fontSize = 18;
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.color = new Color(0.4f, 0.4f, 0.4f);
        placeholder.text = "MATCH CODE";

        var input = inputObject.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = 8;
        UnityEventTools.AddPersistentListener(input.onValueChanged, netplayUI.SetJoinCode);
    }

    private static void SetReference(Component component, string fieldName, Object value)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogError($"[PvPNetplayDemoSceneBuilder] Missing serialized field '{fieldName}' on {component.GetType().Name}.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
