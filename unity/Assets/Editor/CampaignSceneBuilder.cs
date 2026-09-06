#if UNITY_EDITOR
using Armada.Client.Bootstrap;
using Armada.Client.Core;
using Armada.Client.Playback;
using Armada.Client.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Rebuilds the connected campaign without modifying any demo scenes.</summary>
public static class CampaignSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Campaign.unity";
    [MenuItem("Assets/Armada/Build Campaign Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var config = AssetDatabase.LoadAssetAtPath<ArmadaClientConfig>("Assets/Scenes/CampaignClientConfig.asset");
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ArmadaClientConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Scenes/CampaignClientConfig.asset");
        }
        const string artPath = "Assets/Art/UI/ui-harbor-dawn.png";
        var importer = AssetImporter.GetAtPath(artPath) as TextureImporter;
        if (importer == null) throw new System.InvalidOperationException("Campaign harbor art is missing.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        var art = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shared/mat-sea-painterly.mat");
        if (material == null || art == null) throw new System.InvalidOperationException("Required campaign art assets are missing.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.08f, 0.15f);
        camera.rect = new Rect(0.30f, 0.30f, 0.70f, 0.56f);
        cameraObject.transform.position = new Vector3(12.5f, 20, 0);
        cameraObject.transform.rotation = Quaternion.Euler(90, 0, 0);
        var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Sea";
        board.transform.position = new Vector3(12.5f, -0.55f, 0);
        board.transform.localScale = new Vector3(120, 1, 100);
        board.GetComponent<Renderer>().sharedMaterial = material;
        var water = board.AddComponent<WaterAnimator>();
        Set(water, "waterRenderer", board.GetComponent<Renderer>());
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var spectator = new GameObject("FleetPlayback", typeof(SpectatorRenderer)).GetComponent<SpectatorRenderer>();
        Set(spectator, "followCamera", camera);
        ShipViewProviderWiring.Attach(spectator);
        BoardFeatureWiring.Attach(spectator);
        var root = new GameObject("Campaign", typeof(CampaignUIController), typeof(CampaignPlayController), typeof(CampaignBootstrap), typeof(MobilePresentation), typeof(CampaignAudio));
        var bootstrap = root.GetComponent<CampaignBootstrap>();
        Set(bootstrap, "clientConfig", config);
        Set(bootstrap, "harborArt", art);
        Set(bootstrap, "view", root.GetComponent<CampaignUIController>());
        Set(bootstrap, "play", root.GetComponent<CampaignPlayController>());
        Set(bootstrap, "spectator", spectator);
        var audio = root.GetComponent<CampaignAudio>();
        Set(audio, "spectator", spectator);
        Set(audio, "view", root.GetComponent<CampaignUIController>());
        Set(audio, "sea", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/sfx-sea-wave--01.flac"));
        Set(audio, "music", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/mus-battle-theme.ogg"));
        Set(audio, "click", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/sfx-ui-click--001.ogg"));
        Set(audio, "cannon", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/sfx-cannon-fire.ogg"));
        Set(audio, "impact", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/sfx-cannon-hit.ogg"));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[CampaignSceneBuilder] Saved " + ScenePath);
    }
    private static void Set(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
