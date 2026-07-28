#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot rebuild of every generated scene, in the order they are
/// documented. Scenes are never hand-edited (docs/pvp.md); any builder or
/// serialized-default change regenerates them all with a single Unity
/// launch instead of four:
/// -batchmode -quit -executeMethod RebuildAllGeneratedScenes.BuildAll
/// </summary>
public static class RebuildAllGeneratedScenes
{
    [MenuItem("Assets/Armada/Rebuild All Generated Scenes")]
    public static void BuildAll()
    {
        SpectatorDemoSceneBuilder.Build();
        Mission10PlayDemoSceneBuilder.Build();
        PvPHotseatDemoSceneBuilder.Build();
        PvPNetplayDemoSceneBuilder.Build();
        Debug.Log("[RebuildAllGeneratedScenes] All four generated scenes rebuilt.");
    }
}
#endif
