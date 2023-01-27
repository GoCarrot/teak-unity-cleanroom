#define USE_UNITY_IAP

using UnityEngine;
using UnityEngine.UI;

#if USE_UNITY_IAP && (UNITY_FACEBOOK || !UNITY_WEBGL)
using UnityEngine.Purchasing;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

#if !TEAK_NOT_AVAILABLE
using MiniJSON.Teak;
#endif

#if UNITY_FACEBOOK
using Facebook.Unity;
#endif

[RequireComponent(typeof(TeakInterface))]
public partial class TestDriver : MonoBehaviour
#if USE_UNITY_IAP && (UNITY_FACEBOOK || !UNITY_WEBGL)
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
    Button userProfileTestButton;

#if UNITY_IOS
    string pushToken;
#endif

#if USE_UNITY_IAP && (UNITY_FACEBOOK || !UNITY_WEBGL)
    IStoreController storeController;
#endif

    string testPurchaseSku = "io.teak.app.sku.dollar";

#if UNITY_ANDROID
    int AndroidAPILevel {
        get {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION")) {
             return version.GetStatic<int>("SDK_INT");
           }
        }
    }
#endif

    private static readonly string TestListVersionKey = "TestListVersion";
    int TestListVersion {
        get {
            return 1;
        }
    }

    private static readonly string TestListCurrentTestKey = "TestListCurrentTest";

    List<Test> testList;
    IEnumerator<Test> testEnumerator;

    // IL2CPP builds incorrectly report these as unused?
#pragma warning disable
    Dictionary<string, object> testContext;
    Dictionary<string, object> globalContext = new Dictionary<string, object>();
#pragma warning restore

    private string _launchedFromDeepLinkPath;
    string LaunchedFromDeepLinkPath {
        get {
            return _launchedFromDeepLinkPath;
        }

        set {
            _launchedFromDeepLinkPath = DateTime.Now.ToString("hh:mm:ss") + " " + value;
            StartCoroutine(Coroutine.DoDuringFixedUpdate(() => {
                this.SetupUI();
            }));
        }
    }

    void Awake() {
#if !TEAK_NOT_AVAILABLE
        Teak.Instance.RegisterRoute("/test/:data", "Test", "Deep link for automated tests", (Dictionary<string, object> parameters) => {
            this.LaunchedFromDeepLinkPath = parameters["__incoming_url"] as string;
            Debug.Log(this.LaunchedFromDeepLinkPath);

            if (this.testEnumerator != null) {
                this.testEnumerator.Current.DeepLink(parameters);
            }

            // Throw a test exception
            if (!this.DeepLinkTestExceptionThrown) {
                throw new ArgumentException("Test exception");
            }
        });

        this.SetupStorePlugin();
#endif // TEAK_NOT_AVAILABLE
    }

    void Start() {
        // Don't turn off the screen while running the test suite
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

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
        Teak.Instance.OnLogEvent += OnLogEvent;
        Teak.Instance.OnForegroundNotification += OnForegroundNotification;
        Teak.Instance.OnCallbackError += OnCallbackError;

#if TEAK_4_2_OR_NEWER
        Teak.Instance.OnUserData += OnUserData;
#endif
#endif // TEAK_NOT_AVAILABLE
    }

    void OnValidate() {
        if (this.testColor.Length != 2) {
            Array.Resize(ref this.testColor, 2);
        }
    }

    void OnPushTokenChanged(string pushToken) {
#if UNITY_IOS
        this.pushToken = pushToken;
#endif
        if (this.testEnumerator != null) {
            this.testEnumerator.Current.PushTokenChanged(pushToken);
        }
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
        TeakLogEvent teakLogEvent = new TeakLogEvent(logEvent);
        if (this.testEnumerator != null) {
            this.testEnumerator.Current.LogEvent(teakLogEvent);
        }

        if (teakLogEvent.LogLevel == TeakLogEvent.Level.ERROR) {
            Debug.LogError(teakLogEvent.ToString());
        }

        if ("test.delegate".Equals(teakLogEvent.EventType)) {
            this.LaunchedFromDeepLinkPath = teakLogEvent.EventData["url"] as string;
        }
    }

    bool DeepLinkTestExceptionThrown { get; set; }
    void OnCallbackError(string callback, Exception exception, Dictionary<string, object> data) {
        if (!this.DeepLinkTestExceptionThrown) {
            this.DeepLinkTestExceptionThrown = true;
#   if UNITY_WEBGL
            Debug.ClearDeveloperConsole();
#   endif
        }
    }

#if TEAK_4_2_OR_NEWER
    void OnUserData(Teak.UserData userData) {
        Debug.Log("[OnUserData]: " + Json.Serialize(userData.ToDictionary()));
    }
#endif
#endif // TEAK_NOT_AVAILABLE

    private void SetupUI() {
#if !TEAK_NOT_AVAILABLE
        this.sdkVersionText.GetComponent<Text>().text = "Teak SDK Version: " + Teak.Version;
        this.userIdText.GetComponent<Text>().text = this.teakInterface.TeakUserId;
        this.bundleIdText.GetComponent<Text>().text = Application.identifier + " (" + Teak.AppId + ")";

        // Clear
        foreach (Transform child in this.buttonContainer.transform) {
            Destroy(child.gameObject);
        }

        // Completed tests
        foreach (Test test in this.testList) {
            if (test.Status > 0) {
                string textString = test.Name;
                if (test.Status == Test.TestState.Running) textString = "Running: " + textString;
                Text text = this.CreateText(textString);
                int colorIndex = (test.Status == Test.TestState.Failed) ? 1 : 0;
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

        // Purchase testing
#if false && UNITY_WEBGL
        {
            Button button = this.CreateButton("Test Purchase");
            button.onClick.AddListener(() => {
                FB.Canvas.PayWithProductId(
                    this.testPurchaseSku,
                    "purchaseiap",
                    null,
                    null,
                    (IPayResult result) => {
                        if(!string.IsNullOrEmpty(result.Error)) {
                            Debug.LogError(result.Error);
                        } else {
                            Teak.Instance.ReportCanvasPurchase(result.RawResult);

                            StartCoroutine(Coroutine.Do(() => {
                                Debug.Log("[TestDriver] Consuming " + result.ResultDictionary["purchase_token"]);
                                FB.API(
                                    result.ResultDictionary["purchase_token"] + "/consume",
                                    HttpMethod.POST,
                                    (IGraphResult graphResult) => {
                                        if(!string.IsNullOrEmpty(graphResult.Error)) {
                                            Debug.LogError(graphResult.Error);
                                        } else {
                                            Debug.Log(graphResult.RawResult);
                                        }
                                    }
                                );
                            }));
                        }
                    }
                );
            });
        }
#elif USE_PRIME31
        {
            Button button = this.CreateButton("Test Purchase");
            button.onClick.AddListener(() => {
                Prime31.GoogleIAB.purchaseProduct(this.testPurchaseSku);
            });
        }
#elif USE_UNITY_IAP && (UNITY_FACEBOOK || !UNITY_WEBGL)
        {
            Button button = this.CreateButton("Test Purchase");
            button.onClick.AddListener(() => {
                UnityEngine.Purchasing.Product product = this.storeController.products.WithID(this.testPurchaseSku);
                if (product != null && product.availableToPurchase) {
                    this.storeController.InitiatePurchase(product);
                } else {
                    Debug.LogError("THE THING WAS ALREADY PURCHASED IT THINKS!");
                }
            });
        }
#endif // USE_PRIME31

        // TestExceptionReporting
        {
            Button button = this.CreateButton("TestExceptionReporting");
            button.onClick.AddListener(() => {
                this.teakInterface.TestExceptionReporting();
            });
        }

#if UNITY_ANDROID
        // TestExceptionReporting
        {
            Button button = this.CreateButton("Clear All System Notifications");
            button.onClick.AddListener(() => {
                Utils.ClearAllNotifications();
            });
        }

        // Android 13 RegisterForNotifications
        {
            Button button = this.CreateButton("Teak.RegisterForNotifications");
            button.onClick.AddListener(() => {
                Teak.Instance.RegisterForNotifications();
            });
        }
#endif

#if UNITY_FACEBOOK
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
                FacebookDelegate<ILoginResult>  handler = (ILoginResult result) => {
                    SetupUI();

                    Debug.Log("[TestDriver] Token: " + Facebook.Unity.AccessToken.CurrentAccessToken.TokenString);
                };
#if UNITY_IOS
                // The Null is nonce
                FB.Mobile.LoginWithTrackingPreference(LoginTracking.LIMITED, perms, null, handler);
#else
                FB.LogInWithReadPermissions(perms, handler);
#endif
            });
        }
#endif // UNITY_FACEBOOK

#endif // TEAK_NOT_AVAILABLE

        // Deep Link Path
        if (this.LaunchedFromDeepLinkPath != null) {
            Text text = CreateText(this.LaunchedFromDeepLinkPath);
            text.resizeTextForBestFit = true;
            text.alignment = TextAnchor.UpperLeft;
        }
    }

#if !TEAK_NOT_AVAILABLE
    private IEnumerator TestNumericAttribute(string key, double value, Action<double> action) {
        Teak.Instance.SetNumericAttribute(key, value);
        yield return new WaitForSeconds(15.0f);
        yield return this.teakInterface.GetUserJson((json) => {
            Dictionary<string, object> userProfile = json["user_profile"] as Dictionary<string, object>;
            Dictionary<string, object> numericAttributes = userProfile["number_attributes"] as Dictionary<string, object>;
            action((double) numericAttributes[key]);
        });
    }

    private IEnumerator TestStringAttribute(string key, string value, Action<string> action) {
        Teak.Instance.SetStringAttribute(key, value);
        yield return new WaitForSeconds(15.0f);
        yield return this.teakInterface.GetUserJson((json) => {
            Dictionary<string, object> userProfile = json["user_profile"] as Dictionary<string, object>;
            Dictionary<string, object> stringAttributes = userProfile["string_attributes"] as Dictionary<string, object>;
            action(stringAttributes[key] as string);
        });
    }
#endif // TEAK_NOT_AVAILABLE

    ///// Test Helpers
    public void OnTestBuilderTestDone() {
        this.AdvanceTests();
        StartCoroutine(Coroutine.DoDuringFixedUpdate(() => {
            this.SetupUI();
        }));
    }

    private void AdvanceTests() {
        this.AdvanceTests(false);
    }

    private void AdvanceTests(bool doNotSerialize) {
        if (this.testEnumerator == null) {
            this.testEnumerator = this.testList.GetEnumerator();
        }

        this.testContext = new Dictionary<string, object>();

        if (this.testEnumerator.MoveNext()) {
            this.testEnumerator.Current.Begin();
        } else {
            this.testEnumerator = null;
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
        this.testList = this.MasterTestList.FindAll(e => !e.ExcludedPlatforms.Contains(Application.platform));
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
#if !TEAK_NOT_AVAILABLE
#if USE_PRIME31
        // Prime31 Events
        Prime31.GoogleIABManager.billingSupportedEvent += OnBillingSupported;
        Prime31.GoogleIABManager.queryInventorySucceededEvent += OnQueryInventorySucceeded;
        Prime31.GoogleIABManager.purchaseSucceededEvent += OnPurchaseSucceeded;

        // TODO: Get public key from an autogenerated file
        Prime31.GoogleIAB.init("MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEApVAJ0RF8TMjQaT27Y6guGfxAO/IeYPv/0HyM8AVBzwdC8+XYVwjAP0FWUOdgg4jyFsB7d2HDmdt1HFAa0H6HkyEla2qi4d3iqn86nYD/b2izOp8wWso2C6D0ZsC+TgvmfFHC8LrhcFUInwtmhRJVye7vfC2Rvf6mValhjVUvf0MOnUTg7RTbubZWJr3rvYZftvFYb/0Al5pYqYO7Sls19ctVmARbUL2hQxckqcujbVHo5tV7EHAau+PYFgOL7zH3kSEEzqYIo7LvyVeO+DXyFL8ct7i/W8W4rBMDJwWYBCRndIyoS5NePFH6MrkGNz22WB9NnMU0ytIGbOb/kyGU6wIDAQAB", true);
#elif USE_UNITY_IAP && (UNITY_FACEBOOK || !UNITY_WEBGL)

#if AMAZON
        Debug.Log("[TestDriver] Initializing Unity IAP for: Amazon");
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.AmazonAppStore));
#else
        Debug.Log("[TestDriver] Initializing Unity IAP for: Google Play");
        ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.GooglePlay));
#endif

        builder.AddProduct(this.testPurchaseSku, ProductType.Consumable, new IDs {
            { this.testPurchaseSku, GooglePlay.Name },
            { this.testPurchaseSku, AmazonApps.Name },
            { this.testPurchaseSku, AppleAppStore.Name }
        });

        Debug.Log("[TestDriver] Initializing UnityPurchasing...");
        UnityPurchasing.Initialize(this, builder);

#endif // USE_PRIME31
#endif // !TEAK_NOT_AVAILABLE
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


#if USE_UNITY_IAP && (UNITY_FACEBOOK || !UNITY_WEBGL)
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions) {
        Debug.Log("[TestDriver-UnityIAP] OnInitialized");

        this.storeController = controller;

        // Try and clear this pending-purchase issue
        UnityEngine.Purchasing.Product product = this.storeController.products.WithID(this.testPurchaseSku);
        if (product != null) {
            Debug.Log("[TestDriver-UnityIAP] Confirming Pending Purchase: " + product);
            this.storeController.ConfirmPendingPurchase(product);
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error) {
        Debug.Log("[TestDriver-UnityIAP] OnInitializeFailed: " + error);
    }

    public void OnPurchaseFailed(UnityEngine.Purchasing.Product item, PurchaseFailureReason r) {
        Debug.Log("[TestDriver-UnityIAP] OnPurchaseFailed: " + r);
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e) {
        Debug.Log("[TestDriver-UnityIAP] ProcessPurchase: " + e.purchasedProduct.definition.id);
        return PurchaseProcessingResult.Complete;
    }
#endif // USE_UNITY_IAP && (UNITY_FACEBOOK || !UNITY_WEBGL)
}
