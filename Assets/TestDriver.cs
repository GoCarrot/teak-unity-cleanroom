using UnityEngine;
using UnityEngine.UI;

using System;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(TeakInterface))]
public class TestDriver : MonoBehaviour {
    public GameObject buttonPrefab;
    public GameObject textPrefab;
    public Color[] testColor = new Color[2];

    public GameObject buttonContainer;
    public GameObject sdkVersionText;
    public GameObject userIdText;
    public GameObject bundleIdText;

    TeakInterface teakInterface;
    string scheduledNotificationId;
    string pushToken;

    List<Test> masterTestList = new List<Test> {
        new Test { Name = "Simple Notification", CreativeId = "test_none" },
        new Test { Name = "Deep Link", CreativeId = "test_deeplink", VerifyDeepLink = "link-only" },
        new Test { Name = "Reward", CreativeId = "test_reward", VerifyReward = "coins" },
        new Test { Name = "Reward + Deep Link", CreativeId = "test_rewarddeeplink", VerifyDeepLink = "with-reward", VerifyReward = "coins" }
    };

    List<Test> testList;
    IEnumerator<Test> testEnumerator;

    void Awake() {
        this.teakInterface = GetComponent<TeakInterface>();
        this.teakInterface.OnPushTokenChanged += OnPushTokenChanged;

        Teak.Instance.RegisterRoute("/test/:data", "Test", "Deep link for semi-automated tests", (Dictionary<string, object> parameters) => {
            if (this.testEnumerator != null && this.testEnumerator.Current.OnDeepLink(parameters)) {
                if (!this.testEnumerator.MoveNext()) testEnumerator = null;
            }
        });
    }

    void Start() {
        this.testList = new List<Test>(this.masterTestList);
        this.testEnumerator = this.testList.GetEnumerator();
        this.testEnumerator.MoveNext();

        this.SetupUI();

        Teak.Instance.OnLaunchedFromNotification += OnLaunchedFromNotification;
        Teak.Instance.OnReward += OnReward;

        this.TestThingsThatShouldBeTestedInBetterWays();
    }

    void OnValidate() {
        if (this.testColor.Length != 2) {
            Array.Resize(ref this.testColor, 2);
        }
    }

    void OnPushTokenChanged(string pushToken) {
        this.pushToken = pushToken;
        this.SetupUI();
    }

    void OnLaunchedFromNotification(TeakNotification notification) {
        this.scheduledNotificationId = null;

        if (this.testEnumerator != null && this.testEnumerator.Current.OnLaunchedFromNotification(notification)) {
            if (!this.testEnumerator.MoveNext()) this.testEnumerator = null;
        }
    }

    void OnReward(TeakReward reward) {
        if (this.testEnumerator != null && this.testEnumerator.Current.OnReward(reward)) {
            if (!this.testEnumerator.MoveNext()) this.testEnumerator = null;
        }
    }

    private void TestThingsThatShouldBeTestedInBetterWays() {
        // Ensure the Prime31 and OpenIAB purchase methods are exposed on Android
#if UNITY_ANDROID
        AndroidJavaClass teak = new AndroidJavaClass("io.teak.sdk.Teak");
        teak.CallStatic("prime31PurchaseSucceeded", "{}");
        teak.CallStatic("openIABPurchaseSucceeded", "{\"originalJson\":\"{}\"}");
        teak.CallStatic("pluginPurchaseFailed", 42, "cleanroom");
#endif

        Teak.Instance.TrackEvent("some_boolean", null, null);

        Teak.Instance.SetBadgeCount(42);
    }

    private void SetupUI() {
        this.sdkVersionText.GetComponent<Text>().text = "Teak SDK Version: " + Teak.Version;
        this.userIdText.GetComponent<Text>().text = this.teakInterface.TeakUserId;
        this.bundleIdText.GetComponent<Text>().text = Application.identifier;

        // Clear
        foreach (Transform child in this.buttonContainer.transform) {
            Destroy(child.gameObject);
        }

        // If no push token, show button to register (on iOS)
        if (this.pushToken == null) {
#if UNITY_IOS
            Button button = this.CreateButton("Request Push Permissions");
            button.onClick.AddListener(() => {
                UnityEngine.iOS.NotificationServices.RegisterForNotifications(UnityEngine.iOS.NotificationType.Alert |  UnityEngine.iOS.NotificationType.Badge |  UnityEngine.iOS.NotificationType.Sound);
            });
#endif
        }

        // Completed tests
        foreach (Test test in this.testList) {
            if (test.Status > 0) {
                Text text = this.CreateText(test.Name);
                int colorIndex = test.Status - 1 < 0 ? 0 : test.Status;
                Debug.Log(colorIndex);
                text.color = this.testColor[colorIndex];
            }
        }

        // In-progress test notification
        if (this.testEnumerator != null) {
            Test currentTest = this.testEnumerator.Current;
            if (this.scheduledNotificationId == null) {
                Button button = this.CreateButton(currentTest.Name);
                button.onClick.AddListener(() => {
                    StartCoroutine(TeakNotification.ScheduleNotification(currentTest.CreativeId, currentTest.Name, 5, (TeakNotification.Reply reply) => {
                        this.scheduledNotificationId = reply.Notifications[0].ScheduleId;

                        if (reply.Notifications[0].ScheduleId == null) {
                            this.RecordErrorForTest(currentTest, "ScheduleId was null");
                        }

                        if (reply.Status == TeakNotification.Reply.ReplyStatus.Ok && !currentTest.NoAutoBackground) {
                            Utils.BackgroundApp();
                        }
                    }));
                });
            } else {
                Button button = this.CreateButton("Cancel: " + currentTest.Name);
                button.onClick.AddListener(() => {
                    StartCoroutine(TeakNotification.CancelScheduledNotification(this.scheduledNotificationId, (TeakNotification.Reply reply) => {
                        if (reply.Notifications[0].ScheduleId == null) {
                            this.RecordErrorForTest(currentTest, "ScheduleId was null");
                        }
                        this.scheduledNotificationId = null;
                    }));
                });
            }
        }
    }

    private void RecordErrorForTest(Test test, string error) {
        // TODO
    }

    private Text CreateText(string textString) {
        GameObject go = this.InstantiateInContainer(this.textPrefab);
        Text text = go.GetComponent<Text>();
        text.text = textString;
        return text;
    }

    private Button CreateButton(string label) {
        GameObject go = this.InstantiateInContainer(this.buttonPrefab);
        Text[] buttonText = go.GetComponentsInChildren<Text>();
        buttonText[0].text = label;

        return go.GetComponent<Button>();
    }

    private GameObject InstantiateInContainer(GameObject prefab) {
        GameObject go = Instantiate(prefab) as GameObject;
        go.transform.parent = this.buttonContainer.transform;
        return go;
    }
}
