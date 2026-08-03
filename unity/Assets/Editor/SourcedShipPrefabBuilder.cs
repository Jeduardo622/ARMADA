#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Armada.Client.Playback;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wraps the sourced Kenney ship models (CC0, docs/asset-licenses.md) in
/// ShipView prefabs honoring the full view contract: waterline pivot,
/// class-normalized hull length, honest masthead TopClearance, hull as
/// the tint surface, a flag as the accent, sails keeping their textured
/// livery (the colormap bakes white vs pirate-dark sails — the faction
/// read the greybox trim approximated). GUID-stable like the other
/// builders. Classes without a sourced model (clipper, brig) keep their
/// greybox prefabs via the wiring fallback.
/// -batchmode -quit -executeMethod SourcedShipPrefabBuilder.BuildAll
/// </summary>
public static class SourcedShipPrefabBuilder
{
    private const string ShipsRoot = "Assets/Art/Ships";
    private const string ColormapPath = "Assets/Art/Ships/tex-ship-colormap.png";
    private const string MaterialPath = "Assets/Art/Ships/mat-ship-src.mat";
    // Fraction of hull height below the waterline pivot (the greybox keel
    // sat ~0.1 of ~0.3 height below; sourced hulls read best slightly
    // deeper).
    private const float SubmergedFraction = 0.15f;

    private sealed class Spec
    {
        public string Class;
        public string Livery;
        public string Model;
        public float Length;
    }

    private static readonly Spec[] Specs =
    {
        new Spec { Class = "Sloop", Livery = "aurorian", Model = "shp-sloop-src--aurorian.fbx", Length = 1.0f },
        new Spec { Class = "Sloop", Livery = "crimson", Model = "shp-sloop-src--crimson.fbx", Length = 1.0f },
        new Spec { Class = "Frigate", Livery = "aurorian", Model = "shp-frigate-src--aurorian.fbx", Length = 1.4f },
        new Spec { Class = "Frigate", Livery = "crimson", Model = "shp-frigate-src--crimson.fbx", Length = 1.4f },
        new Spec { Class = "Capital", Livery = "aurorian", Model = "shp-capital-src--aurorian.fbx", Length = 2.2f },
        new Spec { Class = "Capital", Livery = "crimson", Model = "shp-capital-src--crimson.fbx", Length = 2.2f }
    };

    [MenuItem("Assets/Armada/Build Sourced Ship Prefabs")]
    public static void BuildAll()
    {
        var material = EnsureShipMaterial();
        foreach (var spec in Specs)
        {
            Build(spec, material);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SourcedShipPrefabBuilder] {Specs.Length} sourced ship prefabs built.");
    }

    private static Material EnsureShipMaterial()
    {
        var material = GreyboxShipPrefabBuilder.EnsureMaterial(MaterialPath, Color.white);
        var colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(ColormapPath);
        if (colormap == null)
        {
            throw new System.InvalidOperationException($"missing colormap at {ColormapPath}");
        }

        material.mainTexture = colormap;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Build(Spec spec, Material material)
    {
        var modelPath = $"{ShipsRoot}/{spec.Class}/{spec.Model}";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            throw new System.InvalidOperationException($"missing model at {modelPath}");
        }

        var root = new GameObject("ship");
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, worldPositionStays: false);

            var renderers = instance.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = material;
            }

            // Normalize hull length to the class scale (art-needs §2) and
            // drop the keel below the waterline pivot.
            var bounds = CombinedBounds(renderers);
            var scale = spec.Length / bounds.size.z;
            instance.transform.localScale = Vector3.one * scale;
            bounds = CombinedBounds(renderers);
            instance.transform.localPosition = new Vector3(
                -bounds.center.x,
                -(bounds.min.y + bounds.size.y * SubmergedFraction),
                -bounds.center.z);
            bounds = CombinedBounds(renderers);

            // Hull = the largest non-sail/flag renderer; every sail and
            // flag is an accent surface (art-needs §2: sails/trim follow
            // the accent recolor, so status dimming and sinking reach the
            // whole rig — Codex P2 on PR #103). The accent tint multiplies
            // the colormap, so the white-vs-pirate sail livery still reads.
            var hull = renderers
                .Where(r => !IsSail(r.name) && !IsFlag(r.name))
                .OrderByDescending(r => r.bounds.size.sqrMagnitude)
                .FirstOrDefault() ?? renderers[0];
            var accents = renderers.Where(r => IsSail(r.name) || IsFlag(r.name)).ToList();

            var view = root.AddComponent<ShipView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("tintRenderer").objectReferenceValue = hull;
            serialized.FindProperty("accentRenderer").objectReferenceValue =
                accents.Count > 0 ? accents[0] : null;
            var extras = serialized.FindProperty("extraAccentRenderers");
            extras.arraySize = Mathf.Max(0, accents.Count - 1);
            for (var i = 1; i < accents.Count; i++)
            {
                extras.GetArrayElementAtIndex(i - 1).objectReferenceValue = accents[i];
            }

            serialized.FindProperty("topClearance").floatValue = bounds.max.y;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, $"{ShipsRoot}/{spec.Class}/shp-{spec.Class.ToLowerInvariant()}-src--{spec.Livery}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static bool IsSail(string name) => name.ToLowerInvariant().Contains("sail");

    private static bool IsFlag(string name) => name.ToLowerInvariant().Contains("flag");

    private static Bounds CombinedBounds(IReadOnlyList<Renderer> renderers)
    {
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Count; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }
}
#endif
