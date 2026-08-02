#if UNITY_EDITOR
using Armada.Client.Playback;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared builder step: attaches a <see cref="PrefabShipViewProvider"/>
/// beside a <see cref="SpectatorRenderer"/> and wires every class × livery
/// slot from the greybox prefab paths (assets referenced by path through
/// the builders, per docs/design/asset-pipeline.md §5). Missing prefab
/// assets leave their slot empty — the provider falls back to primitives
/// per ship — so scene rebuilds never hard-fail on absent art.
/// </summary>
public static class ShipViewProviderWiring
{
    private static readonly (string field, string path)[] Slots =
    {
        ("sloopAurorian", "Assets/Art/Ships/Sloop/shp-sloop--aurorian.prefab"),
        ("sloopCrimson", "Assets/Art/Ships/Sloop/shp-sloop--crimson.prefab"),
        ("frigateAurorian", "Assets/Art/Ships/Frigate/shp-frigate--aurorian.prefab"),
        ("frigateCrimson", "Assets/Art/Ships/Frigate/shp-frigate--crimson.prefab"),
        ("clipperAurorian", "Assets/Art/Ships/Clipper/shp-clipper--aurorian.prefab"),
        ("clipperCrimson", "Assets/Art/Ships/Clipper/shp-clipper--crimson.prefab"),
        ("brigAurorian", "Assets/Art/Ships/Brig/shp-brig--aurorian.prefab"),
        ("brigCrimson", "Assets/Art/Ships/Brig/shp-brig--crimson.prefab"),
        ("capitalAurorian", "Assets/Art/Ships/Capital/shp-capital--aurorian.prefab"),
        ("capitalCrimson", "Assets/Art/Ships/Capital/shp-capital--crimson.prefab")
    };

    /// <summary>Adds the provider on the renderer's GameObject, fills its
    /// prefab slots, and points the renderer's serialized provider field at
    /// it. Logs (without failing) any slot whose prefab asset is missing.</summary>
    public static void Attach(SpectatorRenderer spectator)
    {
        var provider = spectator.gameObject.AddComponent<PrefabShipViewProvider>();
        var serialized = new SerializedObject(provider);
        foreach (var (field, path) in Slots)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<ShipView>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[ShipViewProviderWiring] No prefab at {path}; '{field}' falls back to primitives.");
                continue;
            }

            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[ShipViewProviderWiring] Missing serialized field '{field}' on PrefabShipViewProvider.");
                continue;
            }

            property.objectReferenceValue = prefab;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();

        var rendererSerialized = new SerializedObject(spectator);
        rendererSerialized.FindProperty("shipViewProvider").objectReferenceValue = provider;
        rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
