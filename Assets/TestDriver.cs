#define USE_UNITY_IAP

// Which Unity IAP purchase path this build compiles. v5 is Google Play only; Amazon v5 builds
// purchase through the Appstore SDK.
#if USE_UNITY_IAP && !UNITY_PURCHASING_V5 && (UNITY_FACEBOOK || !UNITY_WEBGL)
#define TEAK_IAP_V4
#elif USE_UNITY_IAP && UNITY_PURCHASING_V5 && UNITY_ANDROID && !AMAZON
#define TEAK_IAP_V5
#endif

using UnityEngine;
using UnityEngine.UI;

#if TEAK_IAP_V4 || TEAK_IAP_V5
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
#if TEAK_IAP_V4
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

#if TEAK_IAP_V4
    IStoreController storeController;
#elif TEAK_IAP_V5
    StoreController storeController;
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

    // Per-test watchdog: a test whose completion callback never fires would otherwise stall the
    // whole suite indefinitely. Force-fail after a time budget so the run advances. The longest
    // legitimate test waits ~15s (numeric/string attributes), so 90s is a wide margin.
    // NB: qualify UnityEngine.Coroutine — the unqualified `Coroutine` is this project's helper class.
    private const float TestTimeoutSeconds = 90.0f;
    private UnityEngine.Coroutine testTimeoutCoroutine;

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

#if !TEAK_NOT_AVAILABLE
    TeakPostLaunchSummary PostLaunchSummary {
        get; set;
    }
#endif

    void Awake() {
#if !TEAK_NOT_AVAILABLE
        Debug.Log("[Teak Unity Cleanroom] TestDriver Lifecycle: Awake");
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
        Debug.Log("[Teak Unity Cleanroom] TestDriver Lifecycle: Start");
        // Don't turn off the screen while running the test suite
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        this.teakInterface = GetComponent<TeakInterface>();

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
        Teak.Instance.OnPostLaunchSummary += OnPostLaunchSummary;
#endif
#endif // TEAK_NOT_AVAILABLE
    }

    void OnValidate() {
        if (this.testColor.Length != 2) {
            Array.Resize(ref this.testColor, 2);
        }
    }

    void OnApplicationPause(bool isPaused) {
        if(isPaused && this.testEnumerator != null) {
            this.testEnumerator.Current.Background();
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
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Test Driver] log {0} -> {1}", teakLogEvent.EventType,
                        this.testEnumerator != null ? this.testEnumerator.Current.Name : "(no test)");
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
        if (this.testEnumerator != null) {
            this.testEnumerator.Current.UserData(userData);
        }
        Debug.Log("[OnUserData]: " + Json.Serialize(userData.ToDictionary()));
        Debug.Log("Player's push channel state is: " + userData.PushStatus.StateName);
        Debug.Log("Player's email channel state is: " + userData.EmailStatus.StateName);
        foreach(Teak.Channel.Category category in Teak.Channel.Categories) {
            Debug.Log("Player is " + (userData.PushStatus[category.Id] == Teak.Channel.State.OptIn ? "opted-in to" : " opted-out of") + category.Name + " for push.");
            Debug.Log("Player is " + (userData.EmailStatus[category.Id] == Teak.Channel.State.OptIn ? "opted-in to" : "opted-out of") + category.Name + " for email.");
        }
    }
# endif

#if TEAK_4_1_OR_NEWER

    void OnPostLaunchSummary(TeakPostLaunchSummary postLaunchSummary) {
        this.PostLaunchSummary = postLaunchSummary;
        if(this.testEnumerator != null) {
            this.testEnumerator.Current.PostLaunchSummary(postLaunchSummary);
        }
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
#elif TEAK_IAP_V4
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
#elif TEAK_IAP_V5
        {
            Button button = this.CreateButton("Test Purchase");
            button.onClick.AddListener(() => {
                UnityEngine.Purchasing.Product product = this.storeController?.GetProductById(this.testPurchaseSku);
                if (product != null) {
                    this.storeController.PurchaseProduct(product);
                } else {
                    Debug.LogError("[TestDriver-UnityIAP] Product not fetched: " + this.testPurchaseSku);
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
    // The server keeps numeric attributes as DECIMAL(36,9), so a double comes back rounded to nine
    // decimal places. Compare within a tolerance that covers that rounding.
    private const double NumericAttributeTolerance = 1e-8;

    private IEnumerator TestNumericAttribute(string key, double value, Action<Test.TestState> state) {
        Teak.Instance.SetNumericAttribute(key, value);
        yield return new WaitForSeconds(15.0f);
        yield return this.teakInterface.GetUserJson((json) => {
            Dictionary<string, object> userProfile = json["user_profile"] as Dictionary<string, object>;
            object attributes;
            userProfile.TryGetValue("number_attributes", out attributes);
            Dictionary<string, object> numericAttributes = attributes as Dictionary<string, object>;

            object raw = null;
            if (numericAttributes == null || !numericAttributes.TryGetValue(key, out raw) || raw == null) {
                Debug.LogError("[TestDriver] Numeric attribute " + key + " not in number_attributes, sent=" + value.ToString("R"));
                state(Test.TestState.Failed);
                return;
            }

            double readBack;
            try {
                readBack = Convert.ToDouble(raw);
            } catch (Exception e) {
                Debug.LogError("[TestDriver] Numeric attribute " + key + " is not a number: " + raw + " (" + raw.GetType() + ") " + e.Message);
                state(Test.TestState.Failed);
                return;
            }

            bool matches = Math.Abs(readBack - value) <= NumericAttributeTolerance;
            Debug.Log("[TestDriver] Numeric attribute " + key + " sent=" + value.ToString("R") + " read back=" + readBack.ToString("R") + " (" + raw.GetType() + ") " + (matches ? "within" : "outside") + " tolerance " + NumericAttributeTolerance);
            state(matches ? Test.TestState.Passed : Test.TestState.Failed);
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

        if (this.testTimeoutCoroutine != null) {
            this.StopCoroutine(this.testTimeoutCoroutine);
            this.testTimeoutCoroutine = null;
        }

        if (this.testEnumerator.MoveNext()) {
            Test current = this.testEnumerator.Current;
            CrashIfRequested(current.Name);
            current.Begin();
            this.testTimeoutCoroutine = this.StartCoroutine(this.TestWatchdog(current));
        } else {
            this.testEnumerator = null;
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Test Driver] suite complete");
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

    // Force-fails the current test if it doesn't finish within TestTimeoutSeconds, so a never-firing
    // completion callback can't stall the suite. No-ops if the test already completed or the suite
    // has advanced to a different test.
    private IEnumerator TestWatchdog(Test test) {
        yield return new WaitForSeconds(TestTimeoutSeconds);
        if (this.testEnumerator != null && this.testEnumerator.Current == test && !test.IsComplete) {
            Debug.LogError("[Test Driver] Test timed out after " + TestTimeoutSeconds + "s, forcing failure: " + test.Name);
            test.ForceTimeout();
        }
    }

    // Debug builds only: hard-crash the player when the named test is about to begin, so a test
    // runner's crash detection can be exercised on demand.
    private const string CrashAtEnvironmentVariable = "TEAK_CLEANROOM_CRASH_AT";
    private static void CrashIfRequested(string testName) {
        if (!Debug.isDebugBuild) {
            return;
        }
        if (testName.Equals(Environment.GetEnvironmentVariable(CrashAtEnvironmentVariable))) {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Test Driver] crashing on request before {0}", testName);
            UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.AccessViolation);
        }
    }

    private void ResetTests() {
        Debug.Log("[Test Driver] Resetting tests...");
        this.testList = this.MasterTestList.FindAll(e => !e.ExcludedPlatforms.Contains(Application.platform));
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Test Driver] tests: {0}", this.testList.Count);
        foreach (Test test in this.testList) {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Test Driver] test {0}", test.Name);
        }
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
#elif TEAK_IAP_V4

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
#elif TEAK_IAP_V5
        SetupUnityIAPv5();

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


#if TEAK_IAP_V4
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

    public void OnInitializeFailed(InitializationFailureReason error, String message) {
        Debug.Log("[TestDriver-UnityIAP] OnInitializeFailed: " + error);
    }

    public void OnPurchaseFailed(UnityEngine.Purchasing.Product item, PurchaseFailureReason r) {
        Debug.Log("[TestDriver-UnityIAP] OnPurchaseFailed: " + r);
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e) {
        Debug.Log("[TestDriver-UnityIAP] ProcessPurchase: " + e.purchasedProduct.definition.id);
        return PurchaseProcessingResult.Complete;
    }
#endif // TEAK_IAP_V4

#if TEAK_IAP_V5
    private async void SetupUnityIAPv5() {
        Debug.Log("[TestDriver] Initializing Unity IAP v5 for: Google Play");
        this.storeController = UnityIAPServices.StoreController();

        this.storeController.OnProductsFetched += (List<UnityEngine.Purchasing.Product> products) => {
            Debug.Log("[TestDriver-UnityIAP] OnProductsFetched: " + products.Count);
            // After the products, so a fetched pending order resolves against a known product.
            this.storeController.FetchPurchases();
        };
        this.storeController.OnProductsFetchFailed += (ProductFetchFailed failure) => {
            Debug.Log("[TestDriver-UnityIAP] OnProductsFetchFailed: " + failure.FailureReason);
        };
        // Consumable: confirm every pending order, which consumes it (v4's ProcessPurchase -> Complete).
        this.storeController.OnPurchasePending += (PendingOrder order) => {
            foreach (CartItem item in order.CartOrdered.Items()) {
                Debug.Log("[TestDriver-UnityIAP] ProcessPurchase: " + item.Product.definition.id);
            }
            this.storeController.ConfirmPurchase(order);
        };
        this.storeController.OnPurchaseConfirmed += (Order order) => {
            Debug.Log("[TestDriver-UnityIAP] OnPurchaseConfirmed");
        };
        this.storeController.OnPurchaseFailed += (FailedOrder order) => {
            Debug.Log("[TestDriver-UnityIAP] OnPurchaseFailed: " + order.FailureReason + " " + order.Details);
        };
        // Fetched pending orders also arrive through OnPurchasePending, so a purchase that
        // completed while the app wasn't running gets confirmed at launch.
        this.storeController.OnPurchasesFetched += (Orders orders) => {
            Debug.Log("[TestDriver-UnityIAP] OnPurchasesFetched: " + orders.PendingOrders.Count + " pending, " + orders.ConfirmedOrders.Count + " confirmed");
        };
        this.storeController.OnPurchasesFetchFailed += (PurchasesFetchFailureDescription failure) => {
            Debug.Log("[TestDriver-UnityIAP] OnPurchasesFetchFailed: " + failure.FailureReason + " " + failure.Message);
        };

        try {
            await this.storeController.Connect();
            Debug.Log("[TestDriver-UnityIAP] OnInitialized");
            this.storeController.FetchProducts(new List<ProductDefinition> {
                new ProductDefinition(this.testPurchaseSku, ProductType.Consumable)
            });
        } catch (Exception e) {
            Debug.Log("[TestDriver-UnityIAP] OnInitializeFailed: " + e.Message);
        }
    }
#endif // TEAK_IAP_V5
}
