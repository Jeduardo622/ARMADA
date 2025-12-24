#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public static class AddressablesProfileSetup
{
    private const string RemoteProfileName = "Remote";

    [MenuItem("Assets/Armada/Configure Addressables")]
    public static void Configure()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Addressables", "Enable Addressables first (Window → Asset Management → Addressables).", "Ok");
            return;
        }

        EnsureRemoteProfile(settings);
        EnsureLabels(settings);

        EditorUtility.DisplayDialog("Addressables", "Profiles and labels set.", "Ok");
    }

    private static void EnsureRemoteProfile(AddressableAssetSettings settings)
    {
        var profileId = settings.profileSettings.GetProfileId(RemoteProfileName);
        if (string.IsNullOrEmpty(profileId))
        {
            profileId = settings.profileSettings.AddProfile(RemoteProfileName, settings.profileSettings.GetProfileId("Default"));
        }

        settings.profileSettings.SetValue(profileId, "RemoteBuildPath", "ServerData/[BuildTarget]");
        settings.profileSettings.SetValue(profileId, "RemoteLoadPath", "http://localhost:4500/content/{Platform}");
    }

    private static void EnsureLabels(AddressableAssetSettings settings)
    {
        AddLabel(settings, "core");
        AddLabel(settings, "ui");
        AddLabel(settings, "audio");
        AddLabel(settings, "sim-data");
    }

    private static void AddLabel(AddressableAssetSettings settings, string label)
    {
        if (!settings.GetLabels().Contains(label))
        {
            settings.AddLabel(label);
        }
    }
}
#endif

