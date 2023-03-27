using System;

namespace TeakCleanroomExtensions {
    public static class Debug {
        public static void Log(object message, params object[] vargs) {
            if (vargs.Length == 0) {
                UnityEngine.Debug.Log(message);
            } else if (vargs.Length == 1 && vargs[0] is UnityEngine.Object) {
                UnityEngine.Debug.Log(message, vargs[0] as UnityEngine.Object);
            } else {
                UnityEngine.Debug.Log(string.Format(message.ToString(), vargs));
            }
        }
    }
}
