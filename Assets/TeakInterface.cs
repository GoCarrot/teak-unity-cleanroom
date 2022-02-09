using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#if !TEAK_NOT_AVAILABLE
using MiniJSON.Teak;
#endif

public class TeakInterface : MonoBehaviour {
    public string TeakUserId { get; private set; }

    // Can filter logs for "Launch Matrix" and just see these events, without stack trace
    void LogLaunchMatrixEvent(string logMessage) {
        UnityEngine.Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[✅] Event: {0}", logMessage);
    }

    // Suppress the "is never used and will always have its default value" warning
#pragma warning disable
    public event Action<string> OnPushTokenChanged;
#pragma warning restore

#if !TEAK_NOT_AVAILABLE
#region Unity
    void Awake() {
        Debug.Log("[Teak Unity Cleanroom] Lifecycle: Awake");

        // Register Deep Link routes with Teak
        // Make sure your calls to Teak.Instance.RegisterRoute are in Awake()
        Teak.Instance.RegisterRoute("/store/:sku", "Store", "Open the store to an SKU", (Dictionary<string, object> parameters) => {
            Debug.Log("[Teak Unity Cleanroom] Got store deep link: " + Json.Serialize(parameters));
        });

        Teak.Instance.RegisterRoute("/slots/:sku", "Testing", "Used for launch matrix testing", (Dictionary<string, object> parameters) => {
            LogLaunchMatrixEvent("Deep Link");
        });
    }

    void Start() {
        Debug.Log("[Teak Unity Cleanroom] Lifecycle: Start");

#if UNITY_EDITOR
        this.TeakUserId = "unity-editor";
#elif UNITY_WEBGL
        this.TeakUserId = "unity-webgl-" + Facebook.Unity.AccessToken.CurrentAccessToken.UserId;
#else
        Dictionary<string, object> deviceConfiguration = Teak.Instance.GetDeviceConfiguration();
        this.TeakUserId = "unity-" + (deviceConfiguration["deviceModel"] as string).ToLower();
#endif

        // Assign Teak Callbacks
        Teak.Instance.OnLaunchedFromNotification += OnLaunchedFromNotification;
        Teak.Instance.OnReward += OnReward;
        Teak.Instance.OnLogEvent += OnLogEvent;
        Teak.Instance.OnLaunchedFromLink += OnLaunchedFromLink;

#if TEAK_4_1_OR_NEWER
        Teak.Instance.OnPostLaunchSummary += OnPostLaunchSummary;
#endif

        // Print out notification state
        Debug.Log("[Teak Unity Cleanroom] Notification State: " + Teak.Instance.PushNotificationState);
        if (Teak.Instance.PushNotificationState == Teak.NotificationState.Disabled) {
            Debug.Log("Notifications are disabled!");
        }

        // Do *not* provide email address in the first identify user call
        // the tests will re-identify providing email shortly into the test suite
#if TEAK_4_1_OR_NEWER
        Teak.UserConfiguration userConfiguration = new Teak.UserConfiguration {};

        Teak.Instance.IdentifyUser(this.TeakUserId, userConfiguration);

        // Just ensure this works
        Teak.Instance.RefreshPushTokenIfAuthorized();
#else
        Teak.Instance.IdentifyUser(this.TeakUserId);
#endif

        // Add Prime31 event listeners
#if USE_PRIME31 && UNITY_ANDROID
        Prime31.GoogleIABManager.purchaseFailedEvent += Teak.Instance.Prime31PurchaseFailed;
        Prime31.GoogleIABManager.purchaseSucceededEvent += Teak.Instance.Prime31PurchaseSucceded;
#endif
    }

    void OnApplicationPause(bool isPaused) {
        Debug.Log("[Teak Unity Cleanroom] Lifecycle: OnApplicationPause(" + isPaused + ")");
    }
#endregion

#region Teak
    void OnLaunchedFromNotification(TeakNotification notification) {
        Debug.Log("[Teak Unity Cleanroom] OnLaunchedFromNotification: " + notification.ToString());
        LogLaunchMatrixEvent("Notification");
    }

    void OnLaunchedFromLink(Dictionary<string, object> json) {
        Debug.Log("[Teak Unity Cleanroom] OnLaunchedFromLink: " + Json.Serialize(json));
        LogLaunchMatrixEvent("Link Launch");
    }

#if TEAK_4_1_OR_NEWER
    void OnPostLaunchSummary(TeakPostLaunchSummary postLaunchSummary) {
        Debug.Log("[Teak Unity Cleanroom] OnPostLaunchSummary: " + postLaunchSummary.ToString());

        // PostLaunchSummary should always happen last, so this will separate out tests
        LogLaunchMatrixEvent("-----");
    }
#endif

    // To use this callback example, simply register it during Start() like so:
    //     Teak.Instance.OnReward += OnReward;
    // You can register as many listeners to an event as you like.
    void OnReward(TeakReward reward) {
        Debug.Log("[Teak Unity Cleanroom] OnReward: " + reward.ToString());
        LogLaunchMatrixEvent("Reward");

        switch (reward.Status) {
            case TeakReward.RewardStatus.GrantReward: {
                // The user has been issued this reward by Teak
                foreach (KeyValuePair<string, object> entry in reward.Reward) {
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
    }

    void OnLogEvent(Dictionary<string, object> logData) {
        // Debug.Log(new TeakLogEvent(logData));
    }
#endregion

    public IEnumerator GetUserJson(Action<Dictionary<string, object>> action) {
        if (Teak.AppId == null) {
            Teak.AppId = "613659812345256";
        }

        string url = "https://gocarrot.com/games/" + Teak.AppId + "/users.json";

        WWWForm form = new WWWForm();
        form.AddField("do_not_track_event", "true");
        form.AddField("api_key", this.TeakUserId);
        using (UnityWebRequest w = UnityWebRequest.Post(url, form)) {
#if UNITY_2017_2_OR_NEWER
            yield return w.SendWebRequest();
#else
            yield return w.Send();
#endif
            while (!w.downloadHandler.isDone) {
                yield return new WaitForEndOfFrame();
            }
            Dictionary<string, object> json = Json.Deserialize(w.downloadHandler.text) as Dictionary<string, object>;
            Dictionary<string, object> userProfile = json["user_profile"] as Dictionary<string, object>;
            Dictionary<string, object> context = Json.Deserialize(userProfile["context"] as string) as Dictionary<string, object>;
            userProfile["context"] = context;
            action(json);
        }
    }

#if UNITY_IOS
    string pushTokenString = null;
    void FixedUpdate() {
        if (this.pushTokenString == null) {
            byte[] token = UnityEngine.iOS.NotificationServices.deviceToken;
            if (token != null) {
                // Teak will take care of storing this automatically
                this.pushTokenString = System.BitConverter.ToString(token).Replace("-", "").ToLower();

                // Inform the TestDriver
                if (this.OnPushTokenChanged != null) {
                    this.OnPushTokenChanged(this.pushTokenString);
                }
            }
        }
    }
#endif

    public void TestExceptionReporting()
    {
#if UNITY_EDITOR
#elif UNITY_ANDROID
        AndroidJavaClass teakUnity = new AndroidJavaClass("io.teak.sdk.wrapper.unity.TeakUnity");
        teakUnity.CallStatic("testExceptionReporting");
#elif UNITY_IOS
#endif
    }
#endif // TEAK_NOT_AVAILABLE
}
