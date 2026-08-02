#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// Creates the local Addressables group for ship prefabs
/// (docs/design/render-pipeline.md §4: art flows through Addressables;
/// docs/design/asset-pipeline.md §7: group per top-level Art/ folder, local
/// packing — the remote catalog is a separate Class C follow-up). The first
/// consumer is the prefab-backed ShipViewProvider, so this runs as part of
/// the greybox setup. Idempotent: existing settings/groups/entries update
/// in place.
/// </summary>
public static class AddressablesGroupSetup
{
    private const string ShipsGroupName = "ships";
    private const string ShipsFolder = "Assets/Art/Ships";

    [MenuItem("Assets/Armada/Configure Addressables Ships Group")]
    public static void EnsureShipsGroup()
    {
        // Creates Assets/AddressableAssetsData (settings, profiles) on first
        // run; subsequent runs load the committed settings.
        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var group = settings.FindGroup(ShipsGroupName);
        if (group == null)
        {
            group = settings.CreateGroup(
                ShipsGroupName,
                setAsDefaultGroup: false,
                readOnly: false,
                postEvent: false,
                schemasToCopy: null,
                types: new[] { typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema) });
        }

        var added = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ShipsFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
            entry.address = $"ships/{System.IO.Path.GetFileNameWithoutExtension(path)}";
            added++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AddressablesGroupSetup] '{ShipsGroupName}' group holds {added} ship prefab entries.");
    }
}
#endif
