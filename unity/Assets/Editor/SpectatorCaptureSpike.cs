#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Spike: proves that batchmode Unity can render the spectator framing
/// offscreen and write a PNG, so art iteration can run headlessly instead of
/// requiring a human to press Play and describe the result.
///
/// Deliberately does NOT drive SpectatorRenderer or the sim: the open
/// question is only whether offscreen rendering + PNG encoding works on this
/// machine. The stage below mirrors PvPNetplayDemoSceneBuilder's camera,
/// light, and board exactly, with markers at the pinned pvp-skirmish-2v2
/// opening positions, so the captured frame is representative of the real
/// opening shot.
///
/// Writes to the gitignored reports/unity/ tree. Never saves a scene.
/// Usage: -batchmode -executeMethod SpectatorCaptureSpike.Capture
/// (must NOT pass -nographics: offscreen rendering needs a graphics device)
/// </summary>
public static class SpectatorCaptureSpike
{
    private const string OutputPath = "../reports/unity/spike/spectator-frame.png";
    private const int Width = 1920;
    private const int Height = 1080;

    // Mirrors PvPNetplayDemoSceneBuilder.
    private const float CameraOrthographicSize = 8.5f;
    private static readonly Vector3 CameraPosition = new Vector3(11f, 20f, 0f);
    private static readonly Color BackgroundColor = new Color(0.03f, 0.08f, 0.15f);
    private static readonly Color BoardColor = new Color(0.07f, 0.22f, 0.36f);

    // Mirrors SpectatorRenderer's design-tunable placeholders.
    private const float WorldUnitsPerSimUnit = 0.1f;
    private const float MarkerHeight = 0.5f;
    private static readonly Color PlayerColor = new Color(0.20f, 0.75f, 0.35f);
    private static readonly Color EnemyColor = new Color(0.85f, 0.25f, 0.20f);

    // Pinned pvp-skirmish-2v2 opening state (src/sim/pvpScenario.ts).
    private static readonly (int x, int y, float heading, bool player)[] Fleet =
    {
        (0, 30, 0f, true),
        (0, -30, 0f, true),
        (220, 30, 180f, false),
        (220, -30, 180f, false)
    };

    [MenuItem("Assets/Armada/Capture Spectator Frame (Spike)")]
    public static void Capture()
    {
        RenderTexture target = null;
        Texture2D readback = null;

        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Pin ambient so the frame does not depend on a lighting bake.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.42f);

            var camera = BuildStage();

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };

            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            var png = readback.EncodeToPNG();
            if (png == null || png.Length == 0)
            {
                Fail("EncodeToPNG produced no bytes");
                return;
            }

            var fullPath = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, png);

            Debug.Log($"[SpectatorCaptureSpike] Wrote {png.Length} bytes to {fullPath}");
        }
        catch (System.Exception ex)
        {
            Fail(ex.ToString());
            return;
        }
        finally
        {
            if (readback != null) Object.DestroyImmediate(readback);
            if (target != null) Object.DestroyImmediate(target);
        }
    }

    private static Camera BuildStage()
    {
        var cameraObject = new GameObject("Main Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = CameraOrthographicSize;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundColor;
        cameraObject.transform.position = CameraPosition;
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var lightObject = new GameObject("Directional Light", typeof(Light));
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.position = new Vector3(11f, -0.55f, 0f);
        board.transform.localScale = new Vector3(140f, 1f, 120f);
        board.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard"))
        {
            color = BoardColor
        };

        foreach (var ship in Fleet)
        {
            var marker = GameObject.CreatePrimitive(ship.player ? PrimitiveType.Cube : PrimitiveType.Capsule);
            marker.transform.position = new Vector3(
                ship.x * WorldUnitsPerSimUnit,
                MarkerHeight,
                ship.y * WorldUnitsPerSimUnit);
            marker.transform.rotation = Quaternion.Euler(0f, ship.heading, 0f);
            marker.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard"))
            {
                color = ship.player ? PlayerColor : EnemyColor
            };
        }

        return camera;
    }

    private static void Fail(string reason)
    {
        Debug.LogError($"[SpectatorCaptureSpike] Capture failed: {reason}");
        EditorApplication.Exit(1);
    }
}
#endif
