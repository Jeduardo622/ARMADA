#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Generates the board-feature art (art-needs.md §3 P2): three authored
/// rock variants and a translucent debris patch under Assets/Art/Board/,
/// plus the painterly sea material in Art/Shared/. Deterministic and
/// GUID-stable like the ship builder: meshes/materials update in place,
/// prefabs save to the same paths. Rocks/debris span a unit footprint —
/// the renderer scales x/z by the sim radius and leaves authored height.
/// Runs from the menu or -batchmode -executeMethod BoardArtBuilder.BuildAll.
/// </summary>
public static class BoardArtBuilder
{
    private const string BoardRoot = "Assets/Art/Board";
    public const string SeaMaterialPath = "Assets/Art/Shared/mat-sea-painterly.mat";
    private const string WaterShaderName = "Armada/WaterPainterly";

    // Runtime tinting recolors both to the reviewed spectator-tuning values
    // (obstacleColor / slowZoneColor); these authored bases only show in
    // the inspector.
    private static readonly Color RockBase = new Color(0.25f, 0.22f, 0.18f);
    private static readonly Color DebrisBase = new Color(0.55f, 0.62f, 0.60f, 0.5f);

    [MenuItem("Assets/Armada/Build Board Art")]
    public static void BuildAll()
    {
        GreyboxShipPrefabBuilder.EnsureFolder("Assets", "Art");
        GreyboxShipPrefabBuilder.EnsureFolder("Assets/Art", "Shared");
        GreyboxShipPrefabBuilder.EnsureFolder("Assets/Art", "Board");

        BuildSeaMaterial();

        var rockMaterial = GreyboxShipPrefabBuilder.EnsureMaterial($"{BoardRoot}/mat-rock.mat", RockBase);
        var debrisMaterial = EnsureTransparentMaterial($"{BoardRoot}/mat-debris.mat", DebrisBase);

        // Three fixed irregular outlines: same footprint, different reads.
        BuildRock("a", rockMaterial, new[] { 0.50f, 0.34f, 0.46f, 0.28f, 0.44f, 0.38f, 0.30f }, 0.34f, 0.55f);
        BuildRock("b", rockMaterial, new[] { 0.42f, 0.50f, 0.30f, 0.40f, 0.26f, 0.48f, 0.36f, 0.32f }, 0.28f, 0.46f);
        BuildRock("c", rockMaterial, new[] { 0.36f, 0.28f, 0.50f, 0.32f, 0.42f, 0.26f }, 0.40f, 0.62f);
        BuildDebris(debrisMaterial);

        AssetDatabase.SaveAssets();
        Debug.Log($"[BoardArtBuilder] Sea material, 3 rocks, and debris built under {BoardRoot}.");
    }

    private static void BuildSeaMaterial()
    {
        var shader = Shader.Find(WaterShaderName);
        if (shader == null)
        {
            throw new System.InvalidOperationException($"missing shader {WaterShaderName}");
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(SeaMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SeaMaterialPath);
        }

        material.shader = shader;
        // Serialized _Animate stays 0: every headless capture renders the
        // frozen painterly frame; WaterAnimator turns it on in play mode.
        material.SetFloat("_Animate", 0f);
        EditorUtility.SetDirty(material);
    }

    private static Material EnsureTransparentMaterial(string path, Color color)
    {
        var material = GreyboxShipPrefabBuilder.EnsureMaterial(path, color);
        // URP/Lit transparent surface, configured the way the shader GUI
        // would: the debris patch honors the slow-zone color's 0.5 alpha.
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildRock(string variant, Material material, float[] radii, float baseHeight, float peakHeight)
    {
        var builder = new FlatMeshBuilder();
        builder.AddRadialPrism(IrregularOutline(radii, 1f), 0f, baseHeight);
        // A smaller offset peak breaks the extruded-polygon silhouette.
        builder.AddRadialPrism(IrregularOutline(radii, 0.45f, offsetX: 0.08f, offsetZ: -0.06f), baseHeight - 0.05f, peakHeight);

        var mesh = GreyboxShipPrefabBuilder.EnsureMesh($"{BoardRoot}/env-rock-{variant}.asset", builder.Build());
        SavePrefab($"{BoardRoot}/env-rock-{variant}.prefab", mesh, material);
    }

    private static void BuildDebris(Material material)
    {
        var builder = new FlatMeshBuilder();
        builder.AddRadialPrism(IrregularOutline(new[] { 0.46f, 0.34f, 0.50f, 0.38f, 0.44f, 0.30f, 0.48f, 0.36f, 0.42f }, 1f), 0f, 0.02f);
        var mesh = GreyboxShipPrefabBuilder.EnsureMesh($"{BoardRoot}/env-debris.asset", builder.Build());
        SavePrefab($"{BoardRoot}/env-debris.prefab", mesh, material);
    }

    /// <summary>Clockwise irregular polygon (unit-ish footprint) from
    /// per-vertex radii; deterministic — no RNG.</summary>
    private static Vector2[] IrregularOutline(float[] radii, float scale, float offsetX = 0f, float offsetZ = 0f)
    {
        var outline = new Vector2[radii.Length];
        for (var i = 0; i < radii.Length; i++)
        {
            // Negative angle direction yields clockwise winding in
            // x-right/z-up, matching FlatMeshBuilder's convention.
            var angle = -2f * Mathf.PI * i / radii.Length;
            outline[i] = new Vector2(
                offsetX + Mathf.Cos(angle) * radii[i] * scale,
                offsetZ + Mathf.Sin(angle) * radii[i] * scale);
        }

        return outline;
    }

    private static void SavePrefab(string path, Mesh mesh, Material material)
    {
        var root = new GameObject("feature", typeof(MeshFilter), typeof(MeshRenderer));
        try
        {
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
#endif
