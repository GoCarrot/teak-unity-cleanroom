#if UNITY_3_5 || UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
#else
#  define UNITY_5
#endif

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#if !TEAK_NOT_AVAILABLE
using MiniJSON.Teak;
#endif

public class MainMenu : MonoBehaviour
{
    public int buttonHeight = 250;

#if UNITY_IOS
    string pushTokenString = null;
#endif
    string teakUserId = null;
    string teakSdkVersion = null;
    string teakDeepLinkLaunch = null;
    string teakScheduledNotification = null;

    static string rewardJson = null;

    const string TeakUserIdKey = "Teak.UserId";

    class Test
    {
        public string Name { get; set; }
        public string CreativeId { get; set; }
        public string VerifyDeepLink { get; set; }
        public string VerifyReward { get; set; }
        public int Status { get; set; }
        public bool NoAutoBackground { get; set; }

        bool onDeepLinkCalled = false;
        bool onRewardCalled = false;
        bool onLaunchCalled = false;

        void Prepare()
        {
            if(string.IsNullOrEmpty(this.VerifyReward)) onRewardCalled = true;
            if(string.IsNullOrEmpty(this.VerifyDeepLink)) onDeepLinkCalled = true;
        }

        bool CheckStatus()
        {
            if(onLaunchCalled && onDeepLinkCalled && onRewardCalled)
            {
                if(this.Status == 0) this.Status = 1;
                return true;
            }
            return false;
        }

        public bool OnDeepLink(Dictionary<string, object> parameters)
        {
            if(!string.IsNullOrEmpty(this.VerifyDeepLink) &&
                !this.VerifyDeepLink.Equals(parameters["data"] as string, System.StringComparison.Ordinal))
            {
                rewardJson = "Expected '" + this.VerifyDeepLink + "' contents:\n" + Json.Serialize(parameters);
                this.Status = 2;
            }

            Prepare();
            onDeepLinkCalled = true;
            return CheckStatus();
        }

        public bool OnReward(Dictionary<string, object> parameters)
        {
            Prepare();
            onRewardCalled = true;
            return CheckStatus();
        }

        public bool OnLaunchedFromNotification(Dictionary<string, object> parameters)
        {
            Prepare();
            onLaunchCalled = true;
            return CheckStatus();
        }
    }

    List<Test> masterTestList = new List<Test>
    {
        new Test { Name = "Simple Notification", CreativeId = "test_none" },
        new Test { Name = "Deep Link", CreativeId = "test_deeplink", VerifyDeepLink = "link-only" },
        new Test { Name = "Reward", CreativeId = "test_reward" },
        new Test { Name = "Reward + Deep Link", CreativeId = "test_rewarddeeplink", VerifyDeepLink = "with-reward" }
    };

    List<Test> testList;
    IEnumerator<Test> testEnumerator;

    List<GUIStyle> statusStyle;

    void SetUpTests()
    {
        testList = new List<Test>(masterTestList);
        testEnumerator = testList.GetEnumerator();
        testEnumerator.MoveNext();
    }

#if !TEAK_NOT_AVAILABLE
    void Awake()
    {
        Teak.Instance.RegisterRoute("/store/:sku", "Store", "Open the store to an SKU", (Dictionary<string, object> parameters) => {
            Debug.Log("Got store deep link: " + Json.Serialize(parameters));
        });

        Teak.Instance.RegisterRoute("/test/:data", "Test", "Deep link for semi-automated tests", (Dictionary<string, object> parameters) => {
            if(testEnumerator != null && testEnumerator.Current.OnDeepLink(parameters))
            {
                if(!testEnumerator.MoveNext()) testEnumerator = null;
            }
        });

        rewardJson = null;
    }

    void Start()
    {
        if (!PlayerPrefs.HasKey(TeakUserIdKey))
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            char[] stringChars = new char[8];
            System.Random random = new System.Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            PlayerPrefs.SetString(TeakUserIdKey, new string(stringChars));
            PlayerPrefs.Save();
        }
        teakUserId = PlayerPrefs.GetString(TeakUserIdKey);
        teakSdkVersion = "Teak SDK Version: " + Teak.Version;

        Teak.Instance.IdentifyUser(teakUserId);

        Teak.Instance.OnLaunchedFromNotification += OnLaunchedFromNotification;
        Teak.Instance.OnReward += OnReward;

        statusStyle = new List<GUIStyle>
        {
            new GUIStyle { fontSize = 50, wordWrap = true, normal = new GUIStyleState { textColor = Color.yellow } },
            new GUIStyle { fontSize = 50, wordWrap = true, normal = new GUIStyleState { textColor = Color.green } },
            new GUIStyle { fontSize = 50, wordWrap = true, normal = new GUIStyleState { textColor = Color.red } }
        };
        SetUpTests();
    }

    void OnApplicationPause(bool isPaused)
    {
        if(isPaused)
        {
            // Pause
        }
        else
        {
            // Resume
        }
    }

    void OnLaunchedFromNotification(Dictionary<string, object> notificationPayload)
    {
        Debug.Log("OnLaunchedFromNotification: " + Json.Serialize(notificationPayload));
        teakScheduledNotification = null; // To get the UI back

        // Testing automation
        if(testEnumerator != null &&testEnumerator.Current.OnLaunchedFromNotification(notificationPayload))
        {
            if(!testEnumerator.MoveNext()) testEnumerator = null;
        }
    }

    // To use this callback example, simply register it during Start() like so:
    //     Teak.Instance.OnReward += OnReward;
    // You can register as many listeners to an event as you like.
    void OnReward(Dictionary<string, object> rewardPayload)
    {
        switch (rewardPayload["status"] as string) {
            case "grant_reward": {
                // The user has been issued this reward by Teak
                Dictionary<string, object> rewards = rewardPayload["reward"] as Dictionary<string, object>;
                foreach(KeyValuePair<string, object> entry in rewards)
                {
                    Debug.Log("OnReward -- Give the user " + entry.Value + " instances of " + entry.Key);
                }
            }
            break;

            case "self_click": {
                // The user has attempted to claim a reward from their own social post
            }
            break;

            case "already_clicked": {
                // The user has already been issued this reward
            }
            break;

            case "too_many_clicks": {
                // The reward has already been claimed its maximum number of times globally
            }
            break;

            case "exceed_max_clicks_for_day": {
                // The user has already claimed their maximum number of rewards of this type for the day
            }
            break;

            case "expired": {
                // This reward has expired and is no longer valid
            }
            break;

            case "invalid_post": {
                //Teak does not recognize this reward id
            }
            break;
        }

        // Display JSON
        rewardJson = Json.Serialize(rewardPayload);

        // Testing automation
        if(testEnumerator != null)
        {
            if(testEnumerator.Current.OnReward(rewardPayload))
            {
                if(!testEnumerator.MoveNext())
                {
                    testEnumerator = null;
                }
            }
        }
    }

#if UNITY_IOS
    void FixedUpdate()
    {
        if(pushTokenString == null)
        {
#if UNITY_5
            byte[] token = UnityEngine.iOS.NotificationServices.deviceToken;
#else
            byte[] token = NotificationServices.deviceToken;
#endif
            if(token != null)
            {
                // Teak will take care of storing this automatically
                pushTokenString = System.BitConverter.ToString(token).Replace("-", "").ToLower();
            }
        }
    }
#endif

#if UNITY_IOS
    [DllImport ("__Internal")]
    private static extern float TeakIntegrationTestSuspend();
#endif

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

        GUILayout.Label(teakSdkVersion);
        GUILayout.Label(teakUserId);
        GUILayout.Label(teakDeepLinkLaunch);

        GUILayout.Label(Application.identifier, statusStyle[0]);
#if UNITY_IOS

        if(pushTokenString != null)
        {
            GUILayout.Label("Push Token: " + pushTokenString);
        }
        else
        {
            if(GUILayout.Button("Request Push Permissions", GUILayout.Height(buttonHeight)))
            {
#if UNITY_5
                UnityEngine.iOS.NotificationServices.RegisterForNotifications(UnityEngine.iOS.NotificationType.Alert |  UnityEngine.iOS.NotificationType.Badge |  UnityEngine.iOS.NotificationType.Sound);
#else
                NotificationServices.RegisterForRemoteNotificationTypes(RemoteNotificationType.Alert |  RemoteNotificationType.Badge |  RemoteNotificationType.Sound);
#endif
            }
        }
#endif

        foreach(Test test in testList)
        {
            if(test.Status > 0)
            {
                GUILayout.Label(test.Name, statusStyle[test.Status]);
            }
        }

        if(teakScheduledNotification == null)
        {
            if(testEnumerator != null)
            {
                Test currentTest = testEnumerator.Current;
                if(GUILayout.Button(currentTest.Name, GUILayout.Height(buttonHeight)))
                {
                    StartCoroutine(TeakNotification.ScheduleNotification(currentTest.CreativeId, currentTest.Name, 5, (string scheduleId) => {
                        teakScheduledNotification = scheduleId;

                        if(!currentTest.NoAutoBackground)
                        {
                            BackgroundApp();
                        }
                    }));
                }
            }
        }
        else
        {
            if(GUILayout.Button("Cancel Test: " + teakScheduledNotification, GUILayout.Height(buttonHeight)))
            {
                StartCoroutine(TeakNotification.CancelScheduledNotification(teakScheduledNotification, (string scheduleId) => {
                    teakScheduledNotification = null;
                }));
            }
        }

        if(rewardJson != null)
        {
            GUILayout.Label(rewardJson, statusStyle[0]);
        }

        GUILayout.EndArea();
    }

    void BackgroundApp()
    {
#if UNITY_EDITOR
#elif UNITY_IOS
        TeakIntegrationTestSuspend();
#elif UNITY_ANDROID
        using(AndroidJavaClass intentCls = new AndroidJavaClass("android.content.Intent"))
        {
            using(AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", intent.GetStatic<string>("ACTION_MAIN"));
                intent.Call<AndroidJavaObject>("addCategory", intent.GetStatic<string>("CATEGORY_HOME"));

                using(AndroidJavaClass jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using(AndroidJavaObject jo = jc.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        jo.Call("startActivity", intent);
                    }
                }
            }
        }
#endif
    }
#endif
}
