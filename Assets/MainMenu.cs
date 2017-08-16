#if UNITY_3_5 || UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7
#else
#  define UNITY_5
#endif

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

#if !TEAK_NOT_AVAILABLE
    void Awake()
    {
        Teak.Instance.RegisterRoute("/store/:sku", "Store", "Open the store to an SKU", (Dictionary<string, object> parameters) => {
            Debug.Log("Got store deep link: " + Json.Serialize(parameters));
        });
    }

    void Start()
    {
        teakUserId = SystemInfo.deviceUniqueIdentifier;
        teakSdkVersion = "Teak SDK Version: " + Teak.Version;

        Teak.Instance.IdentifyUser(teakUserId);

        Teak.Instance.OnLaunchedFromNotification += OnLaunchedFromNotification;
        Teak.Instance.OnReward += OnReward;
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
    }

    void OnReward(Dictionary<string, object> notificationPayload)
    {
        Debug.Log("OnReward: " + Json.Serialize(notificationPayload));
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

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, Screen.height - 20));

        GUILayout.Label(teakSdkVersion);
        GUILayout.Label(teakUserId);
        GUILayout.Label(teakDeepLinkLaunch);

#if UNITY_IOS
        if(pushTokenString != null)
        {
            GUILayout.Label("Push Token: " + pushTokenString);
        }
        else
        {
            if(GUILayout.Button("Request Push Notifications", GUILayout.Height(buttonHeight)))
            {
#if UNITY_5
                UnityEngine.iOS.NotificationServices.RegisterForNotifications(UnityEngine.iOS.NotificationType.Alert |  UnityEngine.iOS.NotificationType.Badge |  UnityEngine.iOS.NotificationType.Sound);
#else
                NotificationServices.RegisterForRemoteNotificationTypes(RemoteNotificationType.Alert |  RemoteNotificationType.Badge |  RemoteNotificationType.Sound);
#endif
            }
        }
#endif

        if(teakScheduledNotification == null)
        {
            if(GUILayout.Button("Simple Notification", GUILayout.Height(buttonHeight)))
            {
                StartCoroutine(TeakNotification.ScheduleNotification("test_none", "Simple push notification", 10, (string scheduleId) => {
                    teakScheduledNotification = scheduleId;
                }));
            }

            if(GUILayout.Button("Deep Link", GUILayout.Height(buttonHeight)))
            {
                StartCoroutine(TeakNotification.ScheduleNotification("test_deeplink", "Push notification with deep link", 10, (string scheduleId) => {
                    teakScheduledNotification = scheduleId;
                }));
            }

            if(GUILayout.Button("Reward", GUILayout.Height(buttonHeight)))
            {
                StartCoroutine(TeakNotification.ScheduleNotification("test_reward", "Push notification with reward", 10, (string scheduleId) => {
                    teakScheduledNotification = scheduleId;
                }));
            }

            if(GUILayout.Button("Reward + Deep Link", GUILayout.Height(buttonHeight)))
            {
                StartCoroutine(TeakNotification.ScheduleNotification("test_rewarddeeplink", "Push notification with reward and deep link", 10, (string scheduleId) => {
                    teakScheduledNotification = scheduleId;
                }));
            }
        }
        else
        {
            if(GUILayout.Button("Cancel Notification " + teakScheduledNotification, GUILayout.Height(buttonHeight)))
            {
                StartCoroutine(TeakNotification.CancelScheduledNotification(teakScheduledNotification, (string scheduleId) => {
                    teakScheduledNotification = null;
                }));
            }
        }

        GUILayout.EndArea();
    }
#endif
}
