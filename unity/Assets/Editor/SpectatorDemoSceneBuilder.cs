#if UNITY_EDITOR
using Armada.Client.Bootstrap;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    private const string BoardMaterialPath = "Assets/Scenes/SpectatorBoardMat.mat";

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

        var canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var hudLabel = CreateLabel(canvasObject.transform, "SpectatorHud", anchorTop: true);
        hudLabel.text = "Waiting for run... (Space pause, Right Arrow step, 1-4 speed, +/- cycle)";
        var statusLabel = CreateLabel(canvasObject.transform, "MissionStatus", anchorTop: false);
        statusLabel.text = string.Empty;

        var spectatorObject = new GameObject("Spectator", typeof(SpectatorRenderer));
        var spectator = spectatorObject.GetComponent<SpectatorRenderer>();
        SetReference(spectator, "hudLabel", hudLabel);

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

        var bootstrapObject = new GameObject("Mission10Bootstrap", typeof(DeterministicSimHooks), typeof(Mission10Bootstrap));
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
            material = new Material(Shader.Find("Standard"))
            {
                // Placeholder sea color pending art direction.
                color = new Color(0.07f, 0.22f, 0.36f)
            };
            AssetDatabase.CreateAsset(material, BoardMaterialPath);
        }

        return material;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, bool anchorTop)
    {
        var labelObject = new GameObject(name, typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, worldPositionStays: false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorTop ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = anchorTop ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = anchorTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchorTop ? new Vector2(0f, -10f) : new Vector2(0f, 10f);
        rect.sizeDelta = new Vector2(-40f, 60f);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 20f;
        label.color = Color.white;
        return label;
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
