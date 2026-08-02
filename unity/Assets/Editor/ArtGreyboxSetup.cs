#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-launch batch entry for the greybox art drop: builds the ship
/// prefabs, registers them in the Addressables ships group, and rebuilds
/// every generated scene so the builders pick the prefabs up — the order
/// matters, and a single Unity launch keeps the ~50 s startup cost paid
/// once. -batchmode -quit -executeMethod ArtGreyboxSetup.BuildAll
/// </summary>
public static class ArtGreyboxSetup
{
    [MenuItem("Assets/Armada/Build Greybox Art + Scenes")]
    public static void BuildAll()
    {
        GreyboxShipPrefabBuilder.BuildAll();
        AddressablesGroupSetup.EnsureShipsGroup();
        RebuildAllGeneratedScenes.BuildAll();
        Debug.Log("[ArtGreyboxSetup] Greybox prefabs, ships group, and scenes rebuilt.");
    }
}
#endif
