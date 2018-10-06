using UnityEngine;

class Utils {
#if UNITY_IOS
    [DllImport ("__Internal")]
    private static extern float TeakIntegrationTestSuspend();
#endif

    public static void BackgroundApp() {
#if UNITY_EDITOR
#elif UNITY_IOS
        TeakIntegrationTestSuspend();
#elif UNITY_ANDROID
        using (AndroidJavaClass intentCls = new AndroidJavaClass("android.content.Intent")) {
            using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent")) {
                intent.Call<AndroidJavaObject>("setAction", intent.GetStatic<string>("ACTION_MAIN"));
                intent.Call<AndroidJavaObject>("addCategory", intent.GetStatic<string>("CATEGORY_HOME"));

                using(AndroidJavaClass jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) {
                    using(AndroidJavaObject jo = jc.GetStatic<AndroidJavaObject>("currentActivity")) {
                        jo.Call("startActivity", intent);
                    }
                }
            }
        }
#endif
    }

    public static string RandomNonConfusingCharacterString(int length) {
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghkmnpqrstuvwxyz23456789";
        char[] stringChars = new char[length];
        System.Random random = new System.Random();

        for (int i = 0; i < stringChars.Length; i++) {
            stringChars[i] = chars[random.Next(chars.Length)];
        }

        return new string(stringChars);
    }
}
