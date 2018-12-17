using System;
using System.IO;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

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
        string error = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (string.IsNullOrEmpty(error)) {
            // Preserve the merged AndroidManifest.xml
            if (buildPlayerOptions.target == BuildTarget.Android) {
                string mergedManifestPath = System.IO.Path.GetFullPath(Application.dataPath + "/../Temp/StagingArea/AndroidManifest.xml");
                string outputManifestPath = System.IO.Path.GetFullPath(Application.dataPath + "/../AndroidManifest.merged.xml");
                System.IO.File.Copy(mergedManifestPath, outputManifestPath, true);
            }

            EditorApplication.Exit(0);
        } else {
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

        // #defines
        if (parsedArgs.ContainsKey("define")) {
            string[] defines = parsedArgs["define"] as string[];
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

        EditorPrefs.SetString("AndroidSdkRoot", System.Environment.GetEnvironmentVariable("ANDROID_HOME"));
        EditorPrefs.SetString("AndroidNdkRoot", System.Environment.GetEnvironmentVariable("ANDROID_NDK_HOME"));

        PlayerSettings.Android.androidIsGame = true;

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = BuildPlayer.scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.Android;
        if (isDevelopmentBuild) buildPlayerOptions.options = BuildOptions.Development;
        DoBuildPlayer(buildPlayerOptions);
    }

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
                buildOptions.Add(lastKey, null);
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
}
