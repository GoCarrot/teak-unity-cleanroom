﻿using UnityEngine;
using UnityEngine.UI;

#if UNITY_PURCHASING && (UNITY_FACEBOOK || !UNITY_WEBGL)
using UnityEngine.Purchasing;
#endif

using System;
using System.Collections;
using System.Collections.Generic;

#if !TEAK_NOT_AVAILABLE
using Facebook.Unity;
using MiniJSON.Teak;
#endif

[RequireComponent(typeof(TeakInterface))]
public class TestDriver : MonoBehaviour
#if UNITY_PURCHASING && (UNITY_FACEBOOK || !UNITY_WEBGL)
, IStoreListener
#endif
{
    public GameObject buttonPrefab;
    public GameObject textPrefab;
    public Color[] testColor = new Color[2];

    public GameObject buttonContainer;
    public GameObject sdkVersionText;
    public GameObject userIdText;
    public GameObject bundleIdText;

    TeakInterface teakInterface;
    string pushToken;
    Button userProfileTestButton;

#if UNITY_PURCHASING && (UNITY_FACEBOOK || !UNITY_WEBGL)
    IStoreController storeController;
#endif

#if (UNITY_PURCHASING && (UNITY_FACEBOOK || !UNITY_WEBGL)) || USE_PRIME31
    string testPurchaseSku = "io.teak.app.sku.dollar";
#endif

    private static readonly string TestListVersionKey = "TestListVersion";
    int TestListVersion {
        get {
            return 1;
        }
    }

    private static readonly string TestListCurrentTestKey = "TestListCurrentTest";
    List<Test> MasterTestList {
        get {
            return new List<Test> {
                new Test {
                    Name = "Simple Notification",
                    OnDeepLink = (Test test, Dictionary<string, object> parameters, Action<bool> passed) => {
                        Debug.Log("DEEP LINK CALLED");
                        passed(true);
                    },
                    OnReward = (Test test, TeakReward reward, Action<bool> passed) => {
                        Debug.Log("ON REWARD CALLED");
                        passed(true);
                    },
                    OnComplete = (Test test) => {
                        Debug.Log("TEST COMPLETE");
                        StartCoroutine(Coroutine.DoDuringFixedUpdate(() => {
                            this.SetupUI();
                        }));
                    }
                },
                // new Test { Name = "Simple Notification", CreativeId = "test_none" },
                // new Test { Name = "Deep Link", CreativeId = "test_deeplink", VerifyDeepLink = "link-only" },
                // new Test { Name = "Reward", CreativeId = "test_reward", VerifyReward = "coins" },
                // new Test { Name = "Reward + Deep Link", CreativeId = "test_rewarddeeplink", VerifyDeepLink = "with-reward", VerifyReward = "coins" },
                // new Test { Name = "Foreground Notification", CreativeId = "test_none", NoAutoBackground = true }
            };
        }
    }

    List<Test> testList;
    IEnumerator<Test> testEnumerator;

    void Awake() {
        // Facebook
#if !TEAK_NOT_AVAILABLE
        if (!FB.IsInitialized) {
            FB.Init(() => {
                if (FB.IsInitialized) {
                    FB.ActivateApp();
                }
            });
        } else {
            FB.ActivateApp();
        }

        Teak.Instance.RegisterRoute("/test/:data", "Test", "Deep link for automated tests", (Dictionary<string, object> parameters) => {
            if (this.testEnumerator != null) {
                this.testEnumerator.Current.DeepLink(parameters);
            }
        });

        this.SetupStorePlugin();
#endif // TEAK_NOT_AVAILABLE
    }

    void Start() {
        this.teakInterface = GetComponent<TeakInterface>();
        this.teakInterface.OnPushTokenChanged += OnPushTokenChanged;

        this.ResetTests();

        if (PlayerPrefs.GetInt(TestListVersionKey, 0) == this.TestListVersion &&
            PlayerPrefs.GetString(TestListCurrentTestKey, null) != null) {

            while (this.testEnumerator != null &&
                   !string.Equals(this.testEnumerator.Current.Name, PlayerPrefs.GetString(TestListCurrentTestKey))) {
                // TODO: At some point should maybe serialize the tests out to JSON, with Status and debug info
                //       but for now just set it as successful.
                this.testEnumerator.Current.Status = Test.TestState.Passed;
                this.AdvanceTests(true);
            }
            this.SetupUI();
        }

#if !TEAK_NOT_AVAILABLE
        Teak.Instance.OnLaunchedFromNotification += OnLaunchedFromNotification;
        Teak.Instance.OnReward += OnReward;
#if TEAK_2_2_OR_NEWER
        Teak.Instance.OnLogEvent += OnLogEvent;
        Teak.Instance.OnForegroundNotification += OnForegroundNotification;
#endif
#endif // TEAK_NOT_AVAILABLE

        this.TestThingsThatShouldBeTestedInBetterWays();

#if UNITY_ANDROID && !UNITY_EDITOR
        // For 2.1.1, test to make certain that io_teak_enable_caching is definitely
        // removed and disabled (even if the XML persists)
        AndroidJavaClass httpResponseCache = new AndroidJavaClass("android.net.http.HttpResponseCache");
        AndroidJavaObject installedCache = httpResponseCache.CallStatic<AndroidJavaObject>("getInstalled");
        if (installedCache != null) {
            Debug.LogError("Android Cache is installed!");
        }
#endif
    }

    void OnValidate() {
        if (this.testColor.Length != 2) {
            Array.Resize(ref this.testColor, 2);
        }
    }

    void OnPushTokenChanged(string pushToken) {
        this.pushToken = pushToken;
        StartCoroutine(Coroutine.DoDuringFixedUpdate(() => {
            this.SetupUI();
        }));
    }

#if !TEAK_NOT_AVAILABLE
    void OnLaunchedFromNotification(TeakNotification notification) {
        if (this.testEnumerator != null) {
            this.testEnumerator.Current.LaunchedFromNotification(notification);
        }
    }

    void OnForegroundNotification(TeakNotification notification) {
        if (this.testEnumerator != null) {
            this.testEnumerator.Current.ForegroundNotification(notification);
        }
    }

    void OnReward(TeakReward reward) {
        if (this.testEnumerator != null) {
            this.testEnumerator.Current.Reward(reward);
        }
    }

    void OnLogEvent(Dictionary<string, object> logEvent) {
        if (this.testEnumerator != null) {
            this.testEnumerator.Current.LogEvent(new TeakLogEvent(logEvent));
        }
    }
#endif // TEAK_NOT_AVAILABLE

    private void TestThingsThatShouldBeTestedInBetterWays() {
        // Ensure the plugin purchase methods are exposed on Android
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass teak = new AndroidJavaClass("io.teak.sdk.Teak");
        teak.CallStatic("pluginPurchaseSucceeded", "{}", "test");
        teak.CallStatic("pluginPurchaseFailed", 42, "cleanroom");
#endif
    }

    private void SetupUI() {
#if !TEAK_NOT_AVAILABLE
        this.sdkVersionText.GetComponent<Text>().text = "Teak SDK Version: " + Teak.Version;
        this.userIdText.GetComponent<Text>().text = this.teakInterface.TeakUserId;
        this.bundleIdText.GetComponent<Text>().text = Application.identifier + " (" + Teak.AppId + ")";

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
                int colorIndex = 0; // HAX: test.Status - 1 < 0 ? 0 : test.Status - 1;
                text.color = this.testColor[colorIndex];
                // if (test.ErrorText != null) {
                //     Text errorText = this.CreateText(test.ErrorText);
                //     errorText.fontStyle = FontStyle.Italic;
                //     errorText.color = Color.red;
                // }
            }
        }

        // In-progress test notification
        if (this.testEnumerator == null) {
            // Reset test
            Button button = this.CreateButton("Reset Tests");
            button.onClick.AddListener(() => {
                this.ResetTests();
            });
        }

#if USE_PRIME31
        // Purchase
        {
            Button button = this.CreateButton("Test Purchase");
            button.onClick.AddListener(() => {
                Prime31.GoogleIAB.purchaseProduct(this.testPurchaseSku);
            });
        }
#elif UNITY_PURCHASING && (UNITY_FACEBOOK || !UNITY_WEBGL)
        // Purchase
        {
            Button button = this.CreateButton("Test Purchase");
            button.onClick.AddListener(() => {
                Product product = this.storeController.products.WithID(this.testPurchaseSku);
                if (product != null && product.availableToPurchase) {
                    this.storeController.InitiatePurchase(product);
                } else {
                    Debug.LogError("THE THING WAS ALREADY PURCHASED IT THINKS!");
                }
            });
        }
#endif // USE_PRIME31

        // TrackEvent
#if TEAK_2_1_OR_NEWER
        {
            Button button = this.CreateButton("IncrementEvent Spin");
            button.onClick.AddListener(() => {
                Teak.Instance.IncrementEvent("spin", "slot", "asshole_cats", 1);
                Teak.Instance.IncrementEvent("coin_sink", "slot", "asshole_cats", 50000);
            });
        }
#endif

        // TestExceptionReporting
        {
            Button button = this.CreateButton("TestExceptionReporting");
            button.onClick.AddListener(() => {
                this.teakInterface.TestExceptionReporting();
            });
        }

        // Facebook Login/Logout
        if (FB.IsLoggedIn) {
            Button button = this.CreateButton("Facebook Logout");
            button.onClick.AddListener(() => {
                FB.LogOut();
                SetupUI();
            });
        } else {
            Button button = this.CreateButton("Facebook Login");
            button.onClick.AddListener(() => {
                var perms = new List<string>(){"public_profile", "email"};
                FB.LogInWithReadPermissions(perms, (ILoginResult result) => {
                    SetupUI();
                });
            });
        }
#endif // TEAK_NOT_AVAILABLE
    }

#if !TEAK_NOT_AVAILABLE
    private IEnumerator TestNumericAttribute(string key, double value, Action<double> action) {
        Teak.Instance.SetNumericAttribute(key, value);
        yield return new WaitForSeconds(10.0f);
        yield return this.teakInterface.GetUserJson((json) => {
            Dictionary<string, object> userProfile = json["user_profile"] as Dictionary<string, object>;
            Dictionary<string, object> numericAttributes = userProfile["number_attributes"] as Dictionary<string, object>;
            Debug.Log("Got back: " + numericAttributes[key]);
            action((double) numericAttributes[key]);
        });
    }

    private IEnumerator TestStringAttribute(string key, string value, Action<string> action) {
        Teak.Instance.SetStringAttribute(key, value);
        yield return new WaitForSeconds(10.0f);
        yield return this.teakInterface.GetUserJson((json) => {
            Dictionary<string, object> userProfile = json["user_profile"] as Dictionary<string, object>;
            Dictionary<string, object> stringAttributes = userProfile["string_attributes"] as Dictionary<string, object>;
            Debug.Log("Got back: " + stringAttributes[key]);
            action(stringAttributes[key] as string);
        });
    }
#endif // TEAK_NOT_AVAILABLE

    ///// Test Helpers
    private void AdvanceTests() {
        this.AdvanceTests(false);
    }

    private void AdvanceTests(bool doNotSerialize) {
        if (this.testEnumerator == null) {
            this.testEnumerator = this.testList.GetEnumerator();
            this.testEnumerator.MoveNext();
        } else if (!this.testEnumerator.MoveNext()) {
            this.testEnumerator = null;
        }

        if (this.testEnumerator != null) {
            this.testEnumerator.Current.Begin();
        }

        if (!doNotSerialize) {
            PlayerPrefs.SetInt(TestListVersionKey, this.TestListVersion);

            if (this.testEnumerator != null) {
                Test currentTest = this.testEnumerator.Current;
                PlayerPrefs.SetString(TestListCurrentTestKey, currentTest.Name);
            } else {
                PlayerPrefs.SetString(TestListCurrentTestKey, null);
            }
            PlayerPrefs.Save();
        }
    }

    private void ResetTests() {
        this.testList = this.MasterTestList;
        this.testEnumerator = null;
        this.AdvanceTests();
        this.SetupUI();
    }

    ////// UI Helpers
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
        go.transform.SetParent(this.buttonContainer.transform, false);
        return go;
    }

    ///// Store plugin heleprs and events
    private void SetupStorePlugin() {
#if USE_PRIME31
        // Prime31 Events
        Prime31.GoogleIABManager.billingSupportedEvent += OnBillingSupported;
        Prime31.GoogleIABManager.queryInventorySucceededEvent += OnQueryInventorySucceeded;
        Prime31.GoogleIABManager.purchaseSucceededEvent += OnPurchaseSucceeded;

        // TODO: Get public key from an autogenerated file
        Prime31.GoogleIAB.init("MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEApVAJ0RF8TMjQaT27Y6guGfxAO/IeYPv/0HyM8AVBzwdC8+XYVwjAP0FWUOdgg4jyFsB7d2HDmdt1HFAa0H6HkyEla2qi4d3iqn86nYD/b2izOp8wWso2C6D0ZsC+TgvmfFHC8LrhcFUInwtmhRJVye7vfC2Rvf6mValhjVUvf0MOnUTg7RTbubZWJr3rvYZftvFYb/0Al5pYqYO7Sls19ctVmARbUL2hQxckqcujbVHo5tV7EHAau+PYFgOL7zH3kSEEzqYIo7LvyVeO+DXyFL8ct7i/W8W4rBMDJwWYBCRndIyoS5NePFH6MrkGNz22WB9NnMU0ytIGbOb/kyGU6wIDAQAB", true);
#elif UNITY_PURCHASING && (UNITY_FACEBOOK || !UNITY_WEBGL)

#if AMAZON
        Debug.Log("Initializing Unity IAP for: Amazon");
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.AmazonAppStore));
#else
        Debug.Log("Initializing Unity IAP for: Google Play");
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.GooglePlay));
#endif

        builder.AddProduct(this.testPurchaseSku, ProductType.Consumable, new IDs {
            { this.testPurchaseSku, GooglePlay.Name },
            { this.testPurchaseSku, AmazonApps.Name },
            { this.testPurchaseSku, AppleAppStore.Name }
        });

#if TEAK_2_0_OR_NEWER
        Debug.Log("Initializing UnityPurchasing...");
        UnityPurchasing.Initialize(new TeakStoreListener(this), builder);
#endif // #TEAK_2_0_OR_NEWER

#endif // USE_PRIME31
    }

#if USE_PRIME31
    void OnBillingSupported() {
        // TODO: Get SKUs from an autogenerated file
        Prime31.GoogleIAB.queryInventory(new string[] { this.testPurchaseSku });
    }

    void OnQueryInventorySucceeded(List<Prime31.GooglePurchase> purchases, List<Prime31.GoogleSkuInfo> skuInfo) {
        foreach (Prime31.GooglePurchase purchase in purchases) {
            Prime31.GoogleIAB.consumeProduct(purchase.productId);
        }
    }

    void OnPurchaseSucceeded(Prime31.GooglePurchase purchase) {
        Prime31.GoogleIAB.consumeProduct(purchase.productId);
    }
#endif // USE_PRIME31


#if UNITY_PURCHASING && (UNITY_FACEBOOK || !UNITY_WEBGL)
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions) {
        Debug.Log("[TestDriver-UnityIAP] OnInitialized");

        this.storeController = controller;

        // Try and clear this pending-purchase issue
        Product product = this.storeController.products.WithID(this.testPurchaseSku);
        if (product != null) {
            Debug.Log("[TestDriver-UnityIAP] Confirming Pending Purchase: " + product);
            this.storeController.ConfirmPendingPurchase(product);
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error) {
        Debug.Log("[TestDriver-UnityIAP] OnInitializeFailed: " + error);
    }

    public void OnPurchaseFailed(Product item, PurchaseFailureReason r) {
        Debug.Log("[TestDriver-UnityIAP] OnPurchaseFailed: " + r);
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e) {
        Debug.Log("[TestDriver-UnityIAP] OnPurchaseFailed: " + e.purchasedProduct.definition.id);
        return PurchaseProcessingResult.Complete;
    }
#endif // UNITY_PURCHASING && (UNITY_FACEBOOK || !UNITY_WEBGL)
}
