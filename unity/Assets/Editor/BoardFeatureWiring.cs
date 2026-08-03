#if UNITY_EDITOR
using Armada.Client.Playback;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared builder step (mirrors <see cref="ShipViewProviderWiring"/>):
/// fills the renderer's board-feature prefab slots from the authored
/// board art. Missing assets leave slots empty — the renderer falls back
/// to the pre-art primitives per feature.
/// </summary>
public static class BoardFeatureWiring
{
    private static readonly string[] RockPaths =
    {
        "Assets/Art/Board/env-rock-a.prefab",
        "Assets/Art/Board/env-rock-b.prefab",
        "Assets/Art/Board/env-rock-c.prefab"
    };

    private const string DebrisPath = "Assets/Art/Board/env-debris.prefab";

    public static void Attach(SpectatorRenderer spectator)
    {
        var serialized = new SerializedObject(spectator);
        var rocks = serialized.FindProperty("rockPrefabs");
        var wired = 0;
        rocks.arraySize = 0;
        foreach (var path in RockPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[BoardFeatureWiring] No prefab at {path}; obstacles fall back to primitives for this slot.");
                continue;
            }

            rocks.arraySize = wired + 1;
            rocks.GetArrayElementAtIndex(wired).objectReferenceValue = prefab;
            wired++;
        }

        var debris = AssetDatabase.LoadAssetAtPath<GameObject>(DebrisPath);
        if (debris == null)
        {
            Debug.LogWarning($"[BoardFeatureWiring] No prefab at {DebrisPath}; slow zones fall back to primitives.");
        }

        serialized.FindProperty("debrisPrefab").objectReferenceValue = debris;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
