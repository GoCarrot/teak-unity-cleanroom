using System.IO;
using UnityEditor;

class BuildPlayer
{
    static string[] scenes = { "Assets/MainScene.unity" };

    static void Android()
    {
        string buildPath = "teak-unity-cleanroom.apk";

        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.Android, BuildOptions.Development);
    }

    static void iOS()
    {
        string buildPath = "iOSBuild";
        Directory.CreateDirectory(buildPath);

        BuildPipeline.BuildPlayer(scenes, buildPath, BuildTarget.iPhone, BuildOptions.Development);
    }
}
