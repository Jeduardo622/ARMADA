#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-shot URP conversion (decision D3, docs/design/render-pipeline.md):
/// creates the pipeline and renderer assets under Assets/Settings and
/// assigns them to Graphics and Quality settings. All materials are
/// code-created and swap shader by name in the builders/providers (the
/// formerly committed shared board material was retired by the lane-B
/// painterly sea change). Deterministic and idempotent; run once and
/// commit the results, keep the entry point for a future re-run after a
/// pipeline package upgrade.
/// Usage: -batchmode -quit -projectPath unity -executeMethod ConvertToUrp.Apply
/// </summary>
public static class ConvertToUrp
{
    private const string SettingsFolder = "Assets/Settings";
    private const string RendererPath = "Assets/Settings/UniversalRenderer.asset";
    private const string PipelinePath = "Assets/Settings/UniversalRenderPipeline.asset";

    [MenuItem("Assets/Armada/Convert To URP")]
    public static void Apply()
    {
        if (!AssetDatabase.IsValidFolder(SettingsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (rendererData == null)
        {
            rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, RendererPath);
        }

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }

        // Flat-tinted primitives need no HDR/post; keep the mobile budget lean.
        // SetDirty is load-bearing: without it SaveAssets does not persist
        // these property writes and builds load the asset with HDR still on
        // (Codex P2 on the conversion PR).
        pipeline.supportsHDR = false;
        pipeline.msaaSampleCount = 1;
        EditorUtility.SetDirty(pipeline);
        EditorUtility.SetDirty(rendererData);

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;

        AssetDatabase.SaveAssets();
        Debug.Log("[ConvertToUrp] URP pipeline assigned.");
    }
}
#endif
