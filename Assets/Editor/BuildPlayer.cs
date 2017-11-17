#if UNITY_3_5 || UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
#else
#  define UNITY_5
#endif

using System.IO;
using UnityEditor;
using UnityEngine;

class BuildPlayer
{
    static string[] scenes = { "Assets/MainScene.unity" };

    static void SetBundleId()
    {
        string appId = "com.teakio.pushtest";

        string[] args = System.Environment.GetCommandLineArgs();
        int argIdx = System.Array.IndexOf(args, "BuildPlayer.SetBundleId");
        if(argIdx > -1 && args.Length > argIdx)
        {
            appId = args[argIdx + 1];
        }

        System.Console.WriteLine("Setting App Identifier to " + appId);

#if UNITY_5
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, appId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, appId);
#else
        PlayerSettings.bundleIdentifier = appId;
#endif
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

        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.Android, BuildOptions.Development);
    }

    static void iOS()
    {
#if !TEAK_NOT_AVAILABLE
        TeakSettings.JustShutUpIKnowWhatImDoing = false;
#endif
        string buildPath = System.IO.Path.GetFullPath(Application.dataPath + "/../iOSBuild");
        Directory.CreateDirectory(buildPath);
#if UNITY_5
        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.iOS, BuildOptions.Development);
#else
        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.iPhone, BuildOptions.Development);
#endif
    }
}
