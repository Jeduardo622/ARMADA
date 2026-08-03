#if UNITY_EDITOR
using System.Collections.Generic;
using Armada.Client.Playback;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the greybox ship prefabs (art-needs.md §3 P1): one authored
/// hull/rig/trim mesh set per class, two livery prefabs per class, under
/// Assets/Art/Ships/ per docs/design/asset-pipeline.md. Deterministic and
/// idempotent: meshes and materials are updated in place and prefabs saved
/// to the same paths, so GUIDs — load-bearing for the serialized provider
/// references (asset-pipeline.md §5) — never churn on rebuild.
///
/// Every prefab honors the full ShipView contract (art-needs.md §2):
/// waterline pivot at the root, bow on local +z, directional top-down
/// silhouette (tapered bow), honest TopClearance (masthead height), hull as
/// the tint surface and rig as the accent, livery on a third renderer the
/// runtime never recolors.
///
/// Runs from the menu or -batchmode -executeMethod
/// GreyboxShipPrefabBuilder.BuildAll.
/// </summary>
public static class GreyboxShipPrefabBuilder
{
    private const string ShipsRoot = "Assets/Art/Ships";
    private const string SharedRoot = "Assets/Art/Shared";
    private const string UrpLitShader = "Universal Render Pipeline/Lit";

    // Greybox palette: hull and rig are runtime-tinted (side color / accent
    // lightening), so they ship neutral; trim carries the authored livery
    // (art-direction.md §1: deep blues and golds vs crimson).
    private static readonly Color HullGray = new Color(0.55f, 0.50f, 0.45f);
    private static readonly Color RigCanvas = new Color(0.90f, 0.87f, 0.80f);
    private static readonly Color AurorianTrim = new Color(0.72f, 0.55f, 0.24f);
    private static readonly Color CrimsonTrim = new Color(0.55f, 0.12f, 0.16f);

    private sealed class ClassSpec
    {
        public string Name;          // PascalCase folder / kebab-case file stem
        public float Length;         // hull length, world units (art-needs §2)
        public float Beam;
        public float BowLength;      // taper length at the +z end
        public float DeckY;
        public float KeelY;
        public float MastHeight;     // above deck
        public float[] MastZ;
        public Vector2 SailSize;     // width × height
        public bool RamBow;          // brig: reinforced bow block
    }

    private static readonly ClassSpec[] Specs =
    {
        new ClassSpec { Name = "Sloop", Length = 1.0f, Beam = 0.34f, BowLength = 0.30f, DeckY = 0.16f, KeelY = -0.10f, MastHeight = 0.85f, MastZ = new[] { -0.05f }, SailSize = new Vector2(0.34f, 0.30f) },
        new ClassSpec { Name = "Frigate", Length = 1.4f, Beam = 0.44f, BowLength = 0.38f, DeckY = 0.18f, KeelY = -0.12f, MastHeight = 1.05f, MastZ = new[] { -0.35f, 0.15f }, SailSize = new Vector2(0.42f, 0.34f) },
        new ClassSpec { Name = "Clipper", Length = 1.2f, Beam = 0.32f, BowLength = 0.42f, DeckY = 0.16f, KeelY = -0.10f, MastHeight = 1.25f, MastZ = new[] { -0.38f, -0.02f, 0.30f }, SailSize = new Vector2(0.30f, 0.42f) },
        new ClassSpec { Name = "Brig", Length = 1.2f, Beam = 0.42f, BowLength = 0.30f, DeckY = 0.17f, KeelY = -0.11f, MastHeight = 0.90f, MastZ = new[] { -0.32f, 0.12f }, SailSize = new Vector2(0.40f, 0.32f), RamBow = true },
        new ClassSpec { Name = "Capital", Length = 2.2f, Beam = 0.68f, BowLength = 0.55f, DeckY = 0.24f, KeelY = -0.16f, MastHeight = 1.45f, MastZ = new[] { -0.70f, -0.05f, 0.55f }, SailSize = new Vector2(0.62f, 0.50f) }
    };

    [MenuItem("Assets/Armada/Build Greybox Ship Prefabs")]
    public static void BuildAll()
    {
        EnsureFolder("Assets", "Art");
        EnsureFolder("Assets/Art", "Ships");
        EnsureFolder("Assets/Art", "Shared");

        var hullMat = EnsureMaterial($"{SharedRoot}/mat-greybox-hull.mat", HullGray);
        var rigMat = EnsureMaterial($"{SharedRoot}/mat-greybox-rig.mat", RigCanvas);
        var trims = new Dictionary<string, Material>
        {
            ["aurorian"] = EnsureMaterial($"{SharedRoot}/mat-trim--aurorian.mat", AurorianTrim),
            ["crimson"] = EnsureMaterial($"{SharedRoot}/mat-trim--crimson.mat", CrimsonTrim)
        };

        foreach (var spec in Specs)
        {
            BuildClass(spec, hullMat, rigMat, trims);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[GreyboxShipPrefabBuilder] {Specs.Length} classes × 2 liveries built under {ShipsRoot}.");
    }

    private static void BuildClass(
        ClassSpec spec, Material hullMat, Material rigMat, Dictionary<string, Material> trims)
    {
        var folder = $"{ShipsRoot}/{spec.Name}";
        EnsureFolder(ShipsRoot, spec.Name);
        var stem = spec.Name.ToLowerInvariant();

        var hullMesh = EnsureMesh($"{folder}/shp-{stem}-hull.asset", BuildHullMesh(spec));
        var rigMesh = EnsureMesh($"{folder}/shp-{stem}-rig.asset", BuildRigMesh(spec));
        var trimMesh = EnsureMesh($"{folder}/shp-{stem}-trim.asset", BuildTrimMesh(spec));
        var topClearance = spec.DeckY + spec.MastHeight;

        foreach (var livery in trims)
        {
            BuildPrefab(
                $"{folder}/shp-{stem}--{livery.Key}.prefab",
                hullMesh, hullMat, rigMesh, rigMat, trimMesh, livery.Value, topClearance);
        }
    }

    private static void BuildPrefab(
        string path,
        Mesh hullMesh, Material hullMat,
        Mesh rigMesh, Material rigMat,
        Mesh trimMesh, Material trimMat,
        float topClearance)
    {
        var root = new GameObject("ship");
        try
        {
            var hull = AddMeshChild(root.transform, "hull", hullMesh, hullMat);
            var rig = AddMeshChild(root.transform, "rig", rigMesh, rigMat);
            AddMeshChild(root.transform, "trim", trimMesh, trimMat);

            var view = root.AddComponent<ShipView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("tintRenderer").objectReferenceValue = hull;
            serialized.FindProperty("accentRenderer").objectReferenceValue = rig;
            serialized.FindProperty("topClearance").floatValue = topClearance;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Renderer AddMeshChild(Transform parent, string name, Mesh mesh, Material material)
    {
        var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        child.transform.SetParent(parent, worldPositionStays: false);
        child.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return renderer;
    }

    // ---- mesh construction -------------------------------------------------

    private static Mesh BuildHullMesh(ClassSpec spec)
    {
        var builder = new FlatMeshBuilder();
        builder.AddPrism(BoatOutline(spec.Length, spec.Beam, spec.BowLength), spec.KeelY, spec.DeckY);
        if (spec.RamBow)
        {
            // The reinforced ram reads the m09 mechanic (art-needs §3): a
            // squat block protruding past the bow point at the waterline.
            var half = spec.Length / 2f;
            builder.AddBox(new Vector3(0f, 0.02f, half + 0.05f), new Vector3(0.12f, 0.12f, 0.18f));
        }

        return builder.Build();
    }

    private static Mesh BuildRigMesh(ClassSpec spec)
    {
        var builder = new FlatMeshBuilder();
        foreach (var z in spec.MastZ)
        {
            var mastCenter = spec.DeckY + spec.MastHeight / 2f;
            builder.AddBox(new Vector3(0f, mastCenter, z), new Vector3(0.04f, spec.MastHeight, 0.04f));
            // Square sail: a thin plate across the beam, its face toward the
            // bow — readable as rigging from the top-down gameplay camera.
            var sailCenter = spec.DeckY + spec.MastHeight * 0.62f;
            builder.AddBox(
                new Vector3(0f, sailCenter, z + 0.03f),
                new Vector3(spec.SailSize.x, spec.SailSize.y, 0.02f));
        }

        return builder.Build();
    }

    private static Mesh BuildTrimMesh(ClassSpec spec)
    {
        var builder = new FlatMeshBuilder();
        var half = spec.Length / 2f;
        var straight = spec.Length - spec.BowLength;
        var railY = spec.DeckY + 0.03f;
        // Gunwale rails along the straight run of each side, and a stern
        // band: enough authored livery to make the factions read even before
        // sourced art replaces the greybox.
        builder.AddBox(new Vector3(-spec.Beam / 2f, railY, -half + straight / 2f), new Vector3(0.03f, 0.06f, straight));
        builder.AddBox(new Vector3(spec.Beam / 2f, railY, -half + straight / 2f), new Vector3(0.03f, 0.06f, straight));
        builder.AddBox(new Vector3(0f, railY, -half + 0.015f), new Vector3(spec.Beam, 0.08f, 0.03f));
        return builder.Build();
    }

    /// <summary>Top-down boat outline (clockwise in x-right/z-up): flat
    /// stern, parallel sides, tapering to a bow point on +z — the
    /// directional silhouette the heading contract requires.</summary>
    private static Vector2[] BoatOutline(float length, float beam, float bowLength)
    {
        var half = length / 2f;
        var bowStart = half - bowLength;
        return new[]
        {
            new Vector2(-beam / 2f, -half),
            new Vector2(-beam / 2f, bowStart),
            new Vector2(0f, half),
            new Vector2(beam / 2f, bowStart),
            new Vector2(beam / 2f, -half)
        };
    }

    // ---- asset persistence (GUID-stable) -----------------------------------

    internal static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    internal static Material EnsureMaterial(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(UrpLitShader));
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = Shader.Find(UrpLitShader);
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    internal static Mesh EnsureMesh(string path, Mesh built)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(built, path);
            return built;
        }

        existing.Clear();
        existing.vertices = built.vertices;
        existing.triangles = built.triangles;
        existing.normals = built.normals;
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(built);
        return existing;
    }

}
#endif
