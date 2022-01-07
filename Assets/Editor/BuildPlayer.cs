using System;
using System.IO;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

using UnityEditor.Build.Reporting;

using GooglePlayServices;

public class BuildPlayer
{
    static string[] scenes = { "Assets/MainScene.unity" };

    public static void SetBundleId()
    {
        string appId = null;

        string[] args = System.Environment.GetCommandLineArgs();
        int argIdx = System.Array.IndexOf(args, "BuildPlayer.SetBundleId");
        if(argIdx > -1 && args.Length > argIdx)
        {
            appId = args[argIdx + 1];
        }

        Debug.Log("[teak-unity-cleanroom] Setting App Identifier to " + appId);

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, appId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, appId);
    }

    static void CheckLicense()
    {
        bool hasPro = UnityEditorInternal.InternalEditorUtility.HasPro();
        if (!hasPro) {
            Debug.LogError("[teak-unity-cleanroom] Unity is not running in 'Pro' mode!");
            EditorApplication.Exit(1);
        }
    }

    static void SetAppleTeamId()
    {
        string appleTeamId = null;

        string[] args = System.Environment.GetCommandLineArgs();
        int argIdx = System.Array.IndexOf(args, "BuildPlayer.SetAppleTeamId");
        if(argIdx > -1 && args.Length > argIdx)
        {
            appleTeamId = args[argIdx + 1];
        }

        Debug.Log("[teak-unity-cleanroom] Setting Apple Team Id to " + appleTeamId);

        PlayerSettings.iOS.appleDeveloperTeamID = appleTeamId;
    }

    // Must be run *without* the -quit option
    static void ResolveDependencies()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        if(System.Array.IndexOf(args, "-quit") > -1 ||
           System.Array.IndexOf(args, "-nographcis") > -1)
        {
            Debug.LogError("[teak-unity-cleanroom] ResolveDependencies must be run without the '-quit' or '-nographics' options.");
            EditorApplication.Exit(1);
        }

        EditorPrefs.SetString("AndroidSdkRoot", System.Environment.GetEnvironmentVariable("ANDROID_HOME"));
        EditorPrefs.SetString("AndroidNdkRoot", System.Environment.GetEnvironmentVariable("ANDROID_NDK_HOME"));

        Debug.Log("[teak-unity-cleanroom] Resolving dependencies with Play Services Resolver");

        PlayServicesResolver.Resolve(
            resolutionCompleteWithResult: (success) => {
                if (!success) {
                    Debug.Log("[teak-unity-cleanroom] FAILED to resolve dependencies");
                    EditorApplication.Exit(1);
                } else {
                    Debug.Log("[teak-unity-cleanroom] Resolved dependencies");
                    EditorApplication.Exit(0);
                }
            },
            forceResolution: false);
    }

    static void DoBuildPlayer(BuildPlayerOptions buildPlayerOptions) {
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if(report.summary.result == BuildResult.Succeeded) {
            // Preserve the merged AndroidManifest.xml
            if (buildPlayerOptions.target == BuildTarget.Android) {
                string mergedManifestPath = System.IO.Path.GetFullPath(Application.dataPath + "/../Temp/StagingArea/AndroidManifest.xml");
                string outputManifestPath = System.IO.Path.GetFullPath(Application.dataPath + "/../AndroidManifest.merged.xml");
                if (System.IO.File.Exists(mergedManifestPath)) {
                    System.IO.File.Copy(mergedManifestPath, outputManifestPath, true);
                }
            }
            EditorApplication.Exit(0);
        } else if(report.summary.result == BuildResult.Failed) {
            EditorApplication.Exit(1);
        }
    }

    static void WebGL()
    {
#if !TEAK_NOT_AVAILABLE
        TeakSettings.JustShutUpIKnowWhatImDoing = false;
#endif
        string buildPath = System.IO.Path.GetFullPath(Application.dataPath + "/../WebGLBuild");

        Dictionary<string, object> parsedArgs = GetArgsAfter("BuildPlayer.WebGL");

        // #defines
        if (parsedArgs.ContainsKey("define")) {
            string[] defines = parsedArgs["define"] as string[];
            if (defines == null) {
                defines = new string[] { parsedArgs["define"] as string };
            }
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL, string.Join(";", defines));
        } else {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL, "");
        }

        // Debug build?
        bool isDevelopmentBuild = parsedArgs.ContainsKey("debug");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = BuildPlayer.scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.WebGL;
        if (isDevelopmentBuild) buildPlayerOptions.options = BuildOptions.Development;
        DoBuildPlayer(buildPlayerOptions);
    }

    static void Android()
    {
#if !TEAK_NOT_AVAILABLE
        TeakSettings.JustShutUpIKnowWhatImDoing = false;
#endif
        string buildPath = System.IO.Path.GetFullPath(Application.dataPath + "/../teak-unity-cleanroom.apk");

        Dictionary<string, object> parsedArgs = GetArgsAfter("BuildPlayer.Android");

        // Target API Level
        int targetApiVersion = 0;
        if (parsedArgs.ContainsKey("api") && System.Int32.TryParse(parsedArgs["api"] as string, out targetApiVersion)) {
            Debug.Log("[teak-unity-cleanroom] Setting Target API Level to " + targetApiVersion);
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)targetApiVersion;
        } else {
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        }

        // Signing Key
        if (parsedArgs.ContainsKey("keystore")) {
            string keystore = parsedArgs["keystore"] as string;
            Debug.Log("[teak-unity-cleanroom] Signing with key " + keystore);
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = "pointless";
            PlayerSettings.Android.keyaliasName = "alias_name";
            PlayerSettings.Android.keyaliasPass = "pointless";
        }

        Debug.Log("[teak-unity-cleanroom] Setting AndroidSdkRoot to " + System.Environment.GetEnvironmentVariable("ANDROID_HOME"));
        EditorPrefs.SetString("AndroidSdkRoot", System.Environment.GetEnvironmentVariable("ANDROID_HOME"));
        Debug.Log("[teak-unity-cleanroom] Setting AndroidNdkRoot to " + System.Environment.GetEnvironmentVariable("ANDROID_NDK_HOME"));
        EditorPrefs.SetString("AndroidNdkRoot", System.Environment.GetEnvironmentVariable("ANDROID_NDK_HOME"));

        // #defines
        string[] defines = null;
        if (parsedArgs.ContainsKey("define")) {
            defines = parsedArgs["define"] as string[];
            if (defines == null) {
                defines = new string[] { parsedArgs["define"] as string };
            }
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, string.Join(";", defines));
        } else {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, "");
        }

        // Debug build?
        bool isDevelopmentBuild = parsedArgs.ContainsKey("debug");

        // IL2CPP?
        if (parsedArgs.ContainsKey("il2cpp")) {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        } else {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.Mono2x);
        }

        // UnityIAP. This changes Assets/Resources/BillingMode.json
        if (defines != null && Array.Exists(defines, element => element == "AMAZON")) {
            UnityEditor.Purchasing.UnityPurchasingEditor.TargetAndroidStore(UnityEngine.Purchasing.AndroidStore.AmazonAppStore);
        } else {
            UnityEditor.Purchasing.UnityPurchasingEditor.TargetAndroidStore(UnityEngine.Purchasing.AndroidStore.NotSpecified);
        }

        PlayerSettings.Android.androidIsGame = true;

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = BuildPlayer.scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.Android;
        if (isDevelopmentBuild) buildPlayerOptions.options = BuildOptions.Development;
        DoBuildPlayer(buildPlayerOptions);
    }

#if !TEAK_NOT_AVAILABLE && UNITY_IOS
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject) {
        if (target != BuildTarget.iOS) return;

        string plistPath = pathToBuiltProject + "/Info.plist";
        PlistDocument plist = new PlistDocument();
        plist.ReadFromString(File.ReadAllText(plistPath));

        // Skip App Store Connect export compliance questionnaire
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);

        // Trace logging
        plist.root.SetBoolean("TeakLogTrace", true);

        // Add a non-Teak URL scheme
        AddURLSchemeToPlist(plist, "nonteak");

        File.WriteAllText(plistPath, plist.WriteToString());
    }
#endif

    static void iOS()
    {
#if !TEAK_NOT_AVAILABLE
        TeakSettings.JustShutUpIKnowWhatImDoing = false;
#endif
        string buildPath = System.IO.Path.GetFullPath(Application.dataPath + "/../Unity-iPhone");
        Directory.CreateDirectory(buildPath);

        Dictionary<string, object> parsedArgs = GetArgsAfter("BuildPlayer.iOS");

        PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);

        // Debug build?
        bool isDevelopmentBuild = parsedArgs.ContainsKey("debug");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = BuildPlayer.scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.iOS;
        if (isDevelopmentBuild) buildPlayerOptions.options = BuildOptions.Development;
        DoBuildPlayer(buildPlayerOptions);
    }

    static Dictionary<string, object> GetArgsAfter(string arg) {
        string[] args = System.Environment.GetCommandLineArgs();
        int argIdx = System.Array.IndexOf(args, arg);
        Dictionary<string, object> parsedArgs = null;
        if (argIdx > -1 && args.Length > argIdx) {
            int len = args.Length - argIdx - 1;
            string[] buildArgs = new string[len];
            Array.Copy(args, argIdx + 1, buildArgs, 0, len);
            parsedArgs = ParseArgs(buildArgs);
        }
        return parsedArgs;
    }

    static Dictionary<string, object> ParseArgs(string[] args) {
        Dictionary<string, object> buildOptions = new Dictionary<string, object>();
        string lastKey = null;
        foreach (string arg in args) {
            if (arg.StartsWith("--")) {
                lastKey = arg.Substring(2).ToLower();
                if (!buildOptions.ContainsKey(lastKey)) {
                    buildOptions.Add(lastKey, null);
                }
            } else if (buildOptions.ContainsKey(lastKey)) {
                if (buildOptions[lastKey] != null) {
                    if (buildOptions[lastKey] is string) {
                        buildOptions[lastKey] = new string[] { buildOptions[lastKey] as string };
                    }

                    string[] current = buildOptions[lastKey] as string[];
                    string[] next = new string[current.Length + 1];
                    current.CopyTo(next, 0);
                    new string[]{ arg }.CopyTo(next, current.Length);
                } else {
                    buildOptions[lastKey] = arg;
                }
            }
        }
        return buildOptions;
    }

#if !TEAK_NOT_AVAILABLE && UNITY_IOS
    public static void AddURLSchemeToPlist(PlistDocument plist, string urlSchemeToAdd) {
        // Get/create array of URL types
        PlistElementArray urlTypesArray = null;
        if (!plist.root.values.ContainsKey("CFBundleURLTypes")) {
            urlTypesArray = plist.root.CreateArray("CFBundleURLTypes");
        } else {
            urlTypesArray = plist.root.values["CFBundleURLTypes"].AsArray();
        }
        if (urlTypesArray == null) {
            urlTypesArray = plist.root.CreateArray("CFBundleURLTypes");
        }

        // Get/create an entry in the array
        PlistElementDict urlTypesItems = null;
        if (urlTypesArray.values.Count == 0) {
            urlTypesItems = urlTypesArray.AddDict();
        } else {
            urlTypesItems = urlTypesArray.values[0].AsDict();

            if (urlTypesItems == null) {
                urlTypesItems = urlTypesArray.AddDict();
            }
        }

        // Get/create array of URL schemes
        PlistElementArray urlSchemesArray = null;
        if (!urlTypesItems.values.ContainsKey("CFBundleURLSchemes")) {
            urlSchemesArray = urlTypesItems.CreateArray("CFBundleURLSchemes");
        } else {
            urlSchemesArray = urlTypesItems.values["CFBundleURLSchemes"].AsArray();

            if (urlSchemesArray == null) {
                urlSchemesArray = urlTypesItems.CreateArray("CFBundleURLSchemes");
            }
        }

        // Add URL scheme
        if (!urlSchemesArray.ContainsElement(urlSchemeToAdd)) {
            urlSchemesArray.Add(urlSchemeToAdd);
        }
    }
#endif // #if !TEAK_NOT_AVAILABLE && UNITY_IOS
}
