#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-shot URP conversion (decision D3, docs/design/render-pipeline.md):
/// creates the pipeline and renderer assets under Assets/Settings, assigns
/// them to Graphics and Quality settings, and upgrades the one committed
/// material (the shared board) from Standard to URP/Lit preserving its
/// color. Everything else is code-created and swaps shader by name in the
/// builders/providers. Deterministic and idempotent; run once and commit
/// the results, keep the entry point for a future re-run after a pipeline
/// package upgrade.
/// Usage: -batchmode -quit -projectPath unity -executeMethod ConvertToUrp.Apply
/// </summary>
public static class ConvertToUrp
{
    private const string SettingsFolder = "Assets/Settings";
    private const string RendererPath = "Assets/Settings/UniversalRenderer.asset";
    private const string PipelinePath = "Assets/Settings/UniversalRenderPipeline.asset";
    private const string BoardMaterialPath = "Assets/Scenes/SpectatorBoardMat.mat";

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

        var board = AssetDatabase.LoadAssetAtPath<Material>(BoardMaterialPath);
        if (board != null && board.shader != null && board.shader.name == "Standard")
        {
            var color = board.color;
            board.shader = Shader.Find("Universal Render Pipeline/Lit");
            board.color = color;
            EditorUtility.SetDirty(board);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[ConvertToUrp] URP pipeline assigned and board material upgraded.");
    }
}
#endif
