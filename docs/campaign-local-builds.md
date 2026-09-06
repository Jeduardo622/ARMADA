# Local campaign builds

Use Unity 2022.3.62f3 with Windows standalone and Android SDK/NDK/OpenJDK modules installed. Set `UNITY_EDITOR_PATH` to the licensed Editor executable, then run:

```powershell
node scripts/campaign/build-local.mjs windows
node scripts/campaign/build-local.mjs android
```

The runner copies the project into a temporary sandbox and builds `Campaign.unity` as the only entry scene. It changes player settings in that sandbox. Windows uses Mono; Android uses IL2CPP/ARM64. Both are development builds. A failed build or stale artifact is reported as failure.

Outputs are `reports/campaign/windows/Armada.exe` (keep its adjacent data files) and `reports/campaign/android/armada.apk`. Logs are `build.log` beside each output. Start the local backend on port 4500 before playing. Windows opens a 1600×900 window and stores its guest credential with current-user DPAPI.

For an attached Android device with USB debugging enabled:

```powershell
adb install -r reports/campaign/android/armada.apk
adb reverse tcp:4500 tcp:4500
adb shell monkey -p com.armada.campaign.dev 1
```

The reverse tunnel lets the checked-in loopback endpoint reach the workstation without allowing guest credentials over arbitrary HTTP origins. Android credentials use Keystore. These builds are local test artifacts; no store upload or production deployment is performed.

Build success does not establish device performance, Android Keystore runtime behavior, or iOS support. Record actual device measurements against `device-baseline.md`. Revert the build runner and `CampaignLocalBuild.cs` to remove this entry point; existing demo builders are independent.
