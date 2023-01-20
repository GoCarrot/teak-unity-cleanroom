#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
 
public class PythonEnvBuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 1;
    public void OnPreprocessBuild(BuildReport report)
    {
        // /Library/Frameworks/Python.framework/Versions/2.7/bin/python
        System.Environment.SetEnvironmentVariable("EMSDK_PYTHON", "/usr/local/bin/python3");
    }
}
#endif
