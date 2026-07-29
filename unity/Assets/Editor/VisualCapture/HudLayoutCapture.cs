#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Armada.Client.UI;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// HUD aspect-ratio capture matrix (D2-B slice 3): opens each generated
/// scene in edit mode and renders a full mock screenshot — world plus HUD —
/// at phone/tablet aspect ratios, so layout regressions like the PR #83/#84
/// review findings (fixed rows overflowing narrow widths, strips
/// overlapping) surface as diffable images instead of review archaeology.
///
/// The scenes author their canvases as ScreenSpaceOverlay, which bypasses
/// Camera.Render entirely; the capture temporarily switches each canvas to
/// ScreenSpaceCamera over the scene's own main camera (in memory only —
/// the scene is never saved), forces a layout pass, and runs every
/// BottomStripStacker.Restack() because LateUpdate never fires in edit
/// mode. HUD text is the static pre-play copy, so frames are deterministic.
///
/// Same determinism contract as SpectatorVisualCapture: never -nographics,
/// pinned ambient, targetTexture + ReadPixels + EncodeToPNG.
///
/// Env config: ARMADA_HUD_CAPTURE_OUT (default ../reports/unity/visual/hud).
/// Usage: -batchmode -quit -projectPath unity
///        -executeMethod HudLayoutCapture.Capture
/// </summary>
public static class HudLayoutCapture
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/SpectatorDemo.unity",
        "Assets/Scenes/Mission10Play.unity",
        "Assets/Scenes/PvPHotseatDemo.unity",
        "Assets/Scenes/PvPNetplayDemo.unity"
    };

    // Landscape wide (authoring reference), 20:9 tall phone, 4:3 tablet,
    // and portrait phone — the aspects the wrap/stack behavior must survive.
    private static readonly (int width, int height, string label)[] Matrix =
    {
        (1920, 1080, "landscape-16x9"),
        (2400, 1080, "landscape-20x9"),
        (1440, 1080, "landscape-4x3"),
        (1080, 1920, "portrait-9x16")
    };

    [Serializable]
    private sealed class CaptureManifest
    {
        public List<string> Frames { get; } = new List<string>();
    }

    [MenuItem("Assets/Armada/Capture HUD Aspect Matrix")]
    public static void Capture()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        try
        {
            Run();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[HudLayoutCapture] Capture failed: {ex}");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }

    private static void Run()
    {
        var outDir = Path.GetFullPath(
            Environment.GetEnvironmentVariable("ARMADA_HUD_CAPTURE_OUT")
            ?? Path.Combine("..", "reports", "unity", "visual", "hud"));
        Directory.CreateDirectory(outDir);
        var manifest = new CaptureManifest();

        foreach (var scenePath in ScenePaths)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.38f, 0.42f);

            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (camera == null || canvas == null)
            {
                throw new InvalidOperationException($"{scenePath}: camera or canvas missing");
            }

            // In-memory only: the scene is generated and never saved here.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            foreach (var (width, height, label) in Matrix)
            {
                var fileName = $"{sceneName}-{label}.png";
                CaptureFrame(camera, canvas, width, height, Path.Combine(outDir, fileName));
                manifest.Frames.Add(fileName);
            }
        }

        File.WriteAllText(
            Path.Combine(outDir, "manifest.json"),
            JsonConvert.SerializeObject(manifest, Formatting.Indented) + "\n");
        Debug.Log($"[HudLayoutCapture] {manifest.Frames.Count} frames -> {outDir}");

        // Leave a clean scene behind so the modified canvas cannot be saved
        // by a later step.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static void CaptureFrame(Camera camera, Canvas canvas, int width, int height, string path)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 1
        };
        var readback = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.aspect = (float)width / height;

            // The canvas sizes itself from the camera's target texture; force
            // the scaler + layout groups to solve for this aspect, then run
            // the runtime stackers that LateUpdate would normally drive.
            Canvas.ForceUpdateCanvases();
            foreach (var stacker in UnityEngine.Object.FindObjectsByType<BottomStripStacker>(FindObjectsSortMode.None))
            {
                stacker.Restack();
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
            Canvas.ForceUpdateCanvases();

            camera.Render();
            camera.targetTexture = null;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            var png = readback.EncodeToPNG();
            if (png == null || png.Length == 0)
            {
                throw new InvalidOperationException($"EncodeToPNG produced no bytes for {path}");
            }

            File.WriteAllBytes(path, png);
        }
        finally
        {
            camera.targetTexture = null;
            camera.ResetAspect();
            UnityEngine.Object.DestroyImmediate(readback);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
#endif
