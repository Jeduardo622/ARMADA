#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Local development builds only. The runner uses a disposable project sandbox.</summary>
public static class CampaignLocalBuild
{
    public static void Windows()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        Build(BuildTarget.StandaloneWindows64);
    }

    public static void Android()
    {
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.armada.campaign.dev");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        Build(BuildTarget.Android);
    }

    private static void Build(BuildTarget target)
    {
        var output = Environment.GetEnvironmentVariable("ARMADA_CAMPAIGN_BUILD_OUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Use the local campaign build runner.");
        // Preserve the project's company/product storage namespace so local
        // development builds can reopen the Editor's protected captain profile.
        PlayerSettings.productName = "Armada";
        // Loopback HTTP is a development transport. Guest storage independently
        // rejects non-HTTPS origins other than loopback; Android uses adb reverse.
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Campaign.unity" }, output, target, BuildOptions.Development);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Campaign build {report.summary.result}: {report.summary.totalErrors} errors.");
        Debug.Log($"Campaign build succeeded: {report.summary.totalSize} bytes -> {output}");
    }
}
#endif
