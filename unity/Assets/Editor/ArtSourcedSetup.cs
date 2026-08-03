#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-launch batch entry for the sourced-art drop (lane C): builds the
/// sourced ship prefabs, refreshes the ships Addressables group, and
/// rebuilds every generated scene so the wiring picks the sourced
/// prefabs over the greybox.
/// -batchmode -quit -executeMethod ArtSourcedSetup.BuildAll
/// </summary>
public static class ArtSourcedSetup
{
    [MenuItem("Assets/Armada/Build Sourced Art + Scenes")]
    public static void BuildAll()
    {
        SourcedShipPrefabBuilder.BuildAll();
        AddressablesGroupSetup.EnsureShipsGroup();
        RebuildAllGeneratedScenes.BuildAll();
        Debug.Log("[ArtSourcedSetup] Sourced prefabs, ships group, and scenes rebuilt.");
    }
}
#endif
