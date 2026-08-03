#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-launch batch entry for the board art drop (lane B): builds the sea
/// material and board-feature prefabs, registers the board Addressables
/// group, and rebuilds every generated scene so the builders pick up the
/// painterly water and feature wiring.
/// -batchmode -quit -executeMethod ArtBoardSetup.BuildAll
/// </summary>
public static class ArtBoardSetup
{
    [MenuItem("Assets/Armada/Build Board Art + Scenes")]
    public static void BuildAll()
    {
        BoardArtBuilder.BuildAll();
        AddressablesGroupSetup.EnsureBoardGroup();
        RebuildAllGeneratedScenes.BuildAll();
        Debug.Log("[ArtBoardSetup] Board art, board group, and scenes rebuilt.");
    }
}
#endif
