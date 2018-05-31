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
    private int buttonHeightInPx = Screen.height / 5;

#if UNITY_IOS
    string pushTokenString = null;
#endif
    string teakUserId = null;
    string teakSdkVersion = null;
    string teakDeepLinkLaunch = null;
    string teakScheduledNotification = null;

    static string errorText = null;

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
                (!parameters.ContainsKey("data") ||
                !this.VerifyDeepLink.Equals(parameters["data"] as string, System.StringComparison.Ordinal)))
            {
#if !TEAK_NOT_AVAILABLE
                errorText = "Expected '" + this.VerifyDeepLink + "' contents:\n" + Json.Serialize(parameters);
#endif
                this.Status = 2;
            }

            Prepare();
            onDeepLinkCalled = true;
            return CheckStatus();
        }

#if !TEAK_NOT_AVAILABLE
        public bool OnReward(TeakReward reward)
        {
            if(!this.CreativeId.Equals(reward.CreativeId, System.StringComparison.Ordinal))
            {
                errorText = "Expected '" + this.CreativeId + "' contents: " + reward.CreativeId;
                this.Status = 2;
            }

            if (string.IsNullOrEmpty(reward.RewardId)) {
                errorText = "RewardId was null or empty";
                this.Status = 2;
            }

            Prepare();
            onRewardCalled = true;
            return CheckStatus();
        }

        public bool OnLaunchedFromNotification(TeakNotification notification)
        {
            if(!this.CreativeId.Equals(notification.CreativeId, System.StringComparison.Ordinal))
            {
                errorText = "Expected '" + this.CreativeId + "' got:\n" + Json.Serialize(notification.CreativeId);
                this.Status = 2;
            }
            else if(!string.IsNullOrEmpty(this.VerifyReward) && !notification.Incentivized)
            {
                errorText = "Expected 'incentivized'";
                this.Status = 2;
            }

            Prepare();
            onLaunchCalled = true;
            return CheckStatus();
        }
#endif
    }

    List<Test> masterTestList = new List<Test>
    {
        new Test { Name = "Simple Notification", CreativeId = "test_none" },
        new Test { Name = "Deep Link", CreativeId = "test_deeplink", VerifyDeepLink = "link-only" },
        new Test { Name = "Reward", CreativeId = "test_reward", VerifyReward = "coins" },
        new Test { Name = "Reward + Deep Link", CreativeId = "test_rewarddeeplink", VerifyDeepLink = "with-reward", VerifyReward = "coins" }
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
        // Ensure the Prime31 and OpenIAB purchase methods are exposed on Android
#if UNITY_ANDROID
        AndroidJavaClass teak = new AndroidJavaClass("io.teak.sdk.Teak");
        teak.CallStatic("prime31PurchaseSucceeded", "{}");
        teak.CallStatic("openIABPurchaseSucceeded", "{\"originalJson\":\"{}\"}");
        teak.CallStatic("pluginPurchaseFailed", 42);
#endif

        Debug.Log("[Teak Unity Cleanroom] Lifecycle: Awake");

        Teak.Instance.RegisterRoute("/store/:sku", "Store", "Open the store to an SKU", (Dictionary<string, object> parameters) => {
            Debug.Log("[Teak Unity Cleanroom] Got store deep link: " + Json.Serialize(parameters));
        });

        Teak.Instance.RegisterRoute("/test/:data", "Test", "Deep link for semi-automated tests", (Dictionary<string, object> parameters) => {
            if(testEnumerator != null && testEnumerator.Current.OnDeepLink(parameters))
            {
                if(!testEnumerator.MoveNext()) testEnumerator = null;
            }
        });
    }

    void Start()
    {
        Debug.Log("[Teak Unity Cleanroom] Lifecycle: Start");

        Dictionary<string, object> deviceConfiguration = Teak.Instance.GetDeviceConfiguration();
        teakUserId = "unity-" + (deviceConfiguration["deviceModel"] as string).ToLower();
        teakSdkVersion = "Teak SDK Version: " + Teak.Version;

        Teak.Instance.IdentifyUser(teakUserId);

        Teak.Instance.TrackEvent("some_boolean", null, null);

        Teak.Instance.OnLaunchedFromNotification += OnLaunchedFromNotification;
        Teak.Instance.OnReward += OnReward;

        int fontSize = 50;
        statusStyle = new List<GUIStyle>
        {
            new GUIStyle { fontSize = fontSize, wordWrap = true, normal = new GUIStyleState { textColor = Color.yellow } },
            new GUIStyle { fontSize = fontSize, wordWrap = true, normal = new GUIStyleState { textColor = Color.green } },
            new GUIStyle { fontSize = fontSize, wordWrap = true, normal = new GUIStyleState { textColor = Color.red } }
        };
        SetUpTests();
    }

    void OnApplicationPause(bool isPaused)
    {
        Debug.Log("[Teak Unity Cleanroom] Lifecycle: OnApplicationPause(" + isPaused + ")");

        if(isPaused)
        {
            // Pause
        }
        else
        {
            // Resume

            // Badge count test
            Teak.Instance.SetBadgeCount(42);
        }
    }

    void OnLaunchedFromNotification(TeakNotification notification)
    {
        Debug.Log("[Teak Unity Cleanroom] OnLaunchedFromNotification: " + notification.ToString());
        teakScheduledNotification = null; // To get the UI back

        // Testing automation
        if(testEnumerator != null &&testEnumerator.Current.OnLaunchedFromNotification(notification))
        {
            if(!testEnumerator.MoveNext()) testEnumerator = null;
        }
    }

    // To use this callback example, simply register it during Start() like so:
    //     Teak.Instance.OnReward += OnReward;
    // You can register as many listeners to an event as you like.
    void OnReward(TeakReward reward)
    {
        switch (reward.Status) {
            case TeakReward.RewardStatus.GrantReward: {
                // The user has been issued this reward by Teak
                foreach(KeyValuePair<string, object> entry in reward.Reward)
                {
                    Debug.Log("[Teak Unity Cleanroom] OnReward -- Give the user " + entry.Value + " instances of " + entry.Key);
                }
            }
            break;

            case TeakReward.RewardStatus.SelfClick: {
                // The user has attempted to claim a reward from their own social post
            }
            break;

            case TeakReward.RewardStatus.AlreadyClicked: {
                // The user has already been issued this reward
            }
            break;

            case TeakReward.RewardStatus.TooManyClicks: {
                // The reward has already been claimed its maximum number of times globally
            }
            break;

            case TeakReward.RewardStatus.ExceedMaxClicksForDay: {
                // The user has already claimed their maximum number of rewards of this type for the day
            }
            break;

            case TeakReward.RewardStatus.Expired: {
                // This reward has expired and is no longer valid
            }
            break;

            case TeakReward.RewardStatus.InvalidPost: {
                //Teak does not recognize this reward id
            }
            break;
        }

        // Testing automation
        if(testEnumerator != null)
        {
            if(testEnumerator.Current.OnReward(reward))
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
            byte[] token = null; //UnityEngine.iOS.NotificationServices.deviceToken;
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

        GUILayout.Label(teakSdkVersion, statusStyle[0]);
        GUILayout.Label(teakUserId, statusStyle[0]);
        GUILayout.Label(teakDeepLinkLaunch, statusStyle[0]);

        GUILayout.Label(Application.identifier, statusStyle[0]);
#if UNITY_IOS

        if(pushTokenString != null)
        {
            GUILayout.Label("Push Token: " + pushTokenString);
        }
        else
        {
            if(GUILayout.Button("Request Push Permissions", new GUILayoutOption[] { GUILayout.Height(buttonHeightInPx) }))
            {
#if UNITY_5
                //UnityEngine.iOS.NotificationServices.RegisterForNotifications(UnityEngine.iOS.NotificationType.Alert |  UnityEngine.iOS.NotificationType.Badge |  UnityEngine.iOS.NotificationType.Sound);
#else
                NotificationServices.RegisterForRemoteNotificationTypes(RemoteNotificationType.Alert |  RemoteNotificationType.Badge |  RemoteNotificationType.Sound);
#endif
            }
        }
#endif

        if(GUILayout.Button("User Profile Test", new GUILayoutOption[] { GUILayout.Height(buttonHeightInPx / 1.5f) }))
        {
            Teak.Instance.SetNumericAttribute("coins", (double) Random.Range(0.0f, 1000000.0f));
            Teak.Instance.SetStringAttribute("last_slot", RandomNonConfusingCharacterString(10));
        }

        if(!Teak.Instance.AreNotificationsEnabled())
        {
            if(GUILayout.Button("Open Settings App", new GUILayoutOption[] { GUILayout.Height(buttonHeightInPx / 1.5f) }))
            {
                Teak.Instance.OpenSettingsAppToThisAppsSettings();
            }
        }

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
                if(GUILayout.Button(currentTest.Name, new GUILayoutOption[] { GUILayout.Height(buttonHeightInPx) }))
                {
                    StartCoroutine(TeakNotification.ScheduleNotification(currentTest.CreativeId, currentTest.Name, 5, (TeakNotification.Reply reply) => {
                        teakScheduledNotification = reply.Notifications[0].ScheduleId;

                        if (reply.Notifications[0].ScheduleId == null)
                        {
                            errorText = "ScheduleId was null";
                        }

                        if(reply.Status == TeakNotification.Reply.ReplyStatus.Ok && !currentTest.NoAutoBackground)
                        {
                            BackgroundApp();
                        }
                    }));
                }
            }
        }
        else
        {
            if(GUILayout.Button("Cancel Test: " + teakScheduledNotification, new GUILayoutOption[] { GUILayout.Height(buttonHeightInPx) }))
            {
                StartCoroutine(TeakNotification.CancelScheduledNotification(teakScheduledNotification, (TeakNotification.Reply reply) => {
                    if (reply.Notifications[0].ScheduleId == null)
                    {
                        errorText = "ScheduleId was null";
                    }

                    teakScheduledNotification = null;
                }));
            }
        }

        if(GUILayout.Button("Cancel All Notifications", new GUILayoutOption[] { GUILayout.Height(buttonHeightInPx) }))
        {
            StartCoroutine(TeakNotification.CancelAllScheduledNotifications((TeakNotification.Reply reply) => {
                errorText = reply.Notifications == null ? reply.Status.ToString() : reply.Notifications.ToString();
            }));
        }

        if(errorText != null)
        {
            GUILayout.Label(errorText, statusStyle[2]);
        }

        GUILayout.EndArea();
    }

    string RandomNonConfusingCharacterString(int length)
    {
        var chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghkmnpqrstuvwxyz23456789";
        char[] stringChars = new char[length];
        System.Random random = new System.Random();

        for (int i = 0; i < stringChars.Length; i++)
        {
            stringChars[i] = chars[random.Next(chars.Length)];
        }

        return new string(stringChars);
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
