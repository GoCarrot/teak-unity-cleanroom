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

    static void Android()
    {
        string buildPath = System.IO.Path.GetFullPath(Application.dataPath + "/../teak-unity-cleanroom.apk");

        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.Android, BuildOptions.Development);
    }

    static void iOS()
    {
        string buildPath = System.IO.Path.GetFullPath(Application.dataPath + "/../iOSBuild");
        Directory.CreateDirectory(buildPath);
#if UNITY_5
        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.iOS, BuildOptions.Development);
#else
        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.iPhone, BuildOptions.Development);
#endif
    }
}
