#if UNITY_PURCHASING
using UnityEngine;
using UnityEngine.Purchasing;

using System;
using System.Collections;
using System.Collections.Generic;

#if !TEAK_NOT_AVAILABLE
using MiniJSON.Teak;
#endif // !TEAK_NOT_AVAILABLE

public class TeakStoreListener_v1_20 : IStoreListener {
    public static readonly string Version = "0.0.1";

    public IStoreListener AttachedStoreListener { get; private set; }

    protected bool ForwardEventsToTeak { get; set; }

    public TeakStoreListener_v1_20(IStoreListener hostedListener) {
        if (hostedListener == null) throw new ArgumentNullException("hostedListener");
        this.AttachedStoreListener = hostedListener;
        this.ForwardEventsToTeak = false;

#if !TEAK_NOT_AVAILABLE
        // Check that Teak version is 1.0.0, otherwise the ProGuard mapping will be incorrect
        if (!"1.0.0".Equals(Teak.Version)) throw new NotSupportedException("This version of the TeakStoreListener will only work with Teak SDK 1.0.0.");
#endif // !TEAK_NOT_AVAILABLE
    }

#region IStoreListener
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions) {
#if !UNITY_EDITOR && UNITY_ANDROID
        IGooglePlayStoreExtensions googlePlayStoreExtensions = extensions.GetExtension<IGooglePlayStoreExtensions>();
        try {
            this.ForwardEventsToTeak = (googlePlayStoreExtensions != null &&
                                        googlePlayStoreExtensions.GetProductJSONDictionary() != null &&
                                        googlePlayStoreExtensions.GetProductJSONDictionary().Count > 0);
        } finally {
        }

        if (this.ForwardEventsToTeak) {
            Debug.Log("[TeakStoreListener] Running on Google Play Store.");
        }
#endif // #UNITY_ANDROID
        this.AttachedStoreListener.OnInitialized(controller, extensions);
    }

    public void OnInitializeFailed(InitializationFailureReason error) {
        this.AttachedStoreListener.OnInitializeFailed(error);
    }

    public void OnPurchaseFailed(Product item, PurchaseFailureReason r) {
#if !UNITY_EDITOR && UNITY_ANDROID
        try {
            if (this.ForwardEventsToTeak) {
                AndroidJavaClass teak = new AndroidJavaClass("io.teak.sdk.Teak");
                teak.CallStatic("pluginPurchaseFailed", (int) r, "unityiap");
            }
        } finally {
        }
#endif // #UNITY_ANDROID
        this.AttachedStoreListener.OnPurchaseFailed(item, r);
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e) {
#if !UNITY_EDITOR && UNITY_ANDROID && !TEAK_NOT_AVAILABLE
        try {
            if (this.ForwardEventsToTeak) {
                Debug.Log(e.purchasedProduct.receipt);
                Dictionary<string, object> receipt = Json.Deserialize(e.purchasedProduct.receipt) as Dictionary<string,object>;
                Dictionary<string, object> receiptPayload = Json.Deserialize(receipt["Payload"] as string) as Dictionary<string,object>;
                Dictionary<string, object> receiptPayloadJson = Json.Deserialize(receiptPayload["json"] as string) as Dictionary<string,object>;
                string receiptPayloadJsonString = Json.Serialize(receiptPayloadJson);

                AndroidJavaClass teak = new AndroidJavaClass("io.teak.sdk.Teak");
                AndroidJavaObject teakInstanceSingleton = teak.GetStatic<AndroidJavaObject>("Instance");
                using (AndroidJavaObject extras = new AndroidJavaObject("java.util.HashMap")) {
                    IntPtr putMethod = AndroidJNIHelper.GetMethodID(extras.GetRawClass(), "put",
                        "(Ljava/lang/String;Ljava/lang/Object;)Ljava/lang/Object;");
                    AndroidJNI.CallObjectMethod(extras.GetRawObject(), putMethod,
                        AndroidJNIHelper.CreateJNIArgArray(new object[] { "iap_plugin", "unityiap" }));

                    // io.teak.sdk.TeakInstance -> io.teak.sdk.q:
                    //     321:326:void purchaseSucceeded(java.lang.String,java.util.Map) -> a

                    IntPtr purchaseSucceededMethod = AndroidJNIHelper.GetMethodID(AndroidJNI.FindClass("io/teak/sdk/q"), "a",
                        "(Ljava/lang/String;Ljava/util/Map;)V");
                    AndroidJNI.CallVoidMethod(teakInstanceSingleton.GetRawObject(), purchaseSucceededMethod,
                        AndroidJNIHelper.CreateJNIArgArray(new object[] { receiptPayloadJsonString, extras }));
                }
            }
        } finally {
        }
#endif // UNITY_ANDROID && !TEAK_NOT_AVAILABLE
        return this.AttachedStoreListener.ProcessPurchase(e);
    }
#endregion IStoreListener
}
#endif // UNITY_PURCHASING
