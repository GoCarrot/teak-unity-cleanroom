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

    static string errorText = null;

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

            Prepare();
            onRewardCalled = true;
            return CheckStatus();
        }

        public bool OnLaunchedFromNotification(TeakNotification notification)
        {
            if(!this.CreativeId.Equals(notification.CreativeName, System.StringComparison.Ordinal))
            {
                errorText = "Expected '" + this.CreativeId + "' got:\n" + Json.Serialize(notification.CreativeName);
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
        Debug.Log("[Teak Unity Cleanroom] Lifecycle: OnApplicationPause(" + isPaused + ")");

        if(isPaused)
        {
            // Pause
        }
        else
        {
            // Resume
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
                    StartCoroutine(TeakNotification.ScheduleNotification(currentTest.CreativeId, currentTest.Name, 5, (TeakNotification.Reply reply) => {
                        teakScheduledNotification = reply.Notifications[0].ScheduleId;

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
            if(GUILayout.Button("Cancel Test: " + teakScheduledNotification, GUILayout.Height(buttonHeight)))
            {
                StartCoroutine(TeakNotification.CancelScheduledNotification(teakScheduledNotification, (TeakNotification.Reply reply) => {
                    teakScheduledNotification = null;
                }));
            }
        }

        if(GUILayout.Button("Cancel All Notifications", GUILayout.Height(buttonHeight)))
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
