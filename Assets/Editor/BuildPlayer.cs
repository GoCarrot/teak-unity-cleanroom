#if UNITY_3_5 || UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
#else
#  define UNITY_5
#endif

using System.IO;
using UnityEditor;
using UnityEngine;
using GooglePlayServices;

class BuildPlayer
{
    static string[] scenes = { "Assets/MainScene.unity" };

    static void SetBundleId()
    {
        string appId = null;

        string[] args = System.Environment.GetCommandLineArgs();
        int argIdx = System.Array.IndexOf(args, "BuildPlayer.SetBundleId");
        if(argIdx > -1 && args.Length > argIdx)
        {
            appId = args[argIdx + 1];
        }

        Debug.Log("[teak-unity-cleanroom] Setting App Identifier to " + appId);

#if UNITY_5
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, appId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, appId);
#else
        PlayerSettings.bundleIdentifier = appId;
#endif
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

    static void WebGL()
    {
#if !TEAK_NOT_AVAILABLE
        TeakSettings.JustShutUpIKnowWhatImDoing = false;
#endif
        string buildPath = System.IO.Path.GetFullPath(Application.dataPath + "/../WebGLBuild");

        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.WebGL, BuildOptions.Development);
    }

    static void Android()
    {
#if !TEAK_NOT_AVAILABLE
        TeakSettings.JustShutUpIKnowWhatImDoing = false;
#endif
        string buildPath = System.IO.Path.GetFullPath(Application.dataPath + "/../teak-unity-cleanroom.apk");

        int targetApiVersion = 0;
        string[] args = System.Environment.GetCommandLineArgs();
        int argIdx = System.Array.IndexOf(args, "BuildPlayer.Android");
        if(argIdx > -1 && args.Length > argIdx && System.Int32.TryParse(args[argIdx + 1], out targetApiVersion))
        {
            Debug.Log("[teak-unity-cleanroom] Setting Target API Level to " + args[argIdx + 1]);
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)targetApiVersion;
        }
        else
        {
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        }

        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.Android, BuildOptions.Development);
    }

    static void iOS()
    {
#if !TEAK_NOT_AVAILABLE
        TeakSettings.JustShutUpIKnowWhatImDoing = false;
#endif
        string buildPath = System.IO.Path.GetFullPath(Application.dataPath + "/../Unity-iPhone");
        Directory.CreateDirectory(buildPath);
#if UNITY_5
        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.iOS, BuildOptions.Development);
#else
        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.iPhone, BuildOptions.Development);
#endif
    }
}
