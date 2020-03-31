using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public partial class TestDriver : MonoBehaviour {
    List<Test> MasterTestList {
        get {
            return new List<Test> {
#if !TEAK_NOT_AVAILABLE
#if TEAK_2_2_OR_NEWER
                TestBuilder.Build("Reward Link", this)
                    .ExpectDeepLink()
                    .ExpectReward()
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        // Make sure it has app_version and app_version_name (Android only)
                        if ("request.send".Equals(logEvent.EventType) &&
                            "rewards.gocarrot.com".Equals(logEvent.EventData["hostname"] as string)) {
                            Dictionary<string, object> payload = logEvent.EventData["payload"] as Dictionary<string, object>;
                            if (payload.ContainsKey("app_version")
#if UNITY_ANDROID
                                && payload.ContainsKey("app_version_name")
#endif
                                ) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        } else {
                            state(Test.TestState.Pending);
                        }
                    }),

#endif // TEAK_2_2_OR_NEWER

#if UNITY_IOS
                TestBuilder.Build("Push Notification Permission", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        if (this.pushToken == null) {
                            UnityEngine.iOS.NotificationServices.RegisterForNotifications(UnityEngine.iOS.NotificationType.Alert |  UnityEngine.iOS.NotificationType.Badge |  UnityEngine.iOS.NotificationType.Sound);
                        }
                        state(Test.TestState.Passed);
                    })
                    .ExpectPushToken(),
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
                // Make sure that these methods don't get obfuscated
                TestBuilder.Build("Android Plugin Purchase Methods Exposed", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        bool success = true;
                        AndroidJavaClass teak = new AndroidJavaClass("io.teak.sdk.Teak");

                        IntPtr pluginPurchaseSucceeded = AndroidJNI.GetStaticMethodID(
                            teak.GetRawClass(),
                            "pluginPurchaseSucceeded",
                            "(Ljava/lang/String;Ljava/lang/String;)V");
                        success &= pluginPurchaseSucceeded != IntPtr.Zero;

                        IntPtr pluginPurchaseFailed = AndroidJNI.GetStaticMethodID(
                            teak.GetRawClass(),
                            "pluginPurchaseFailed",
                            "(ILjava/lang/String;)V");
                        success &= pluginPurchaseFailed != IntPtr.Zero;

                        state(success ? Test.TestState.Passed : Test.TestState.Failed);
                    }),

                // For 2.1.1, test to make certain that io_teak_enable_caching is definitely
                // removed and disabled (even if the XML persists)
                TestBuilder.Build("Android io_teak_enable_caching is disabled", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        AndroidJavaClass httpResponseCache = new AndroidJavaClass("android.net.http.HttpResponseCache");
                        AndroidJavaObject installedCache = httpResponseCache.CallStatic<AndroidJavaObject>("getInstalled");
                        state(installedCache == null ? Test.TestState.Passed : Test.TestState.Failed);
                    }),
#endif

#if TEAK_2_2_OR_NEWER
                TestBuilder.Build("Notification with Emoji", this)
                    .WhenStarted((Action<Test.TestState> state) => {

                        // Have to use "WhenStarted" because ScheduleNotification will wait for a received log event
                        this.StartCoroutine(TeakNotification.ScheduleNotification("test_emoji_log_exception", "test_emoji_log_exception", 10, (TeakNotification.Reply reply) => {
                            if (reply.Status == TeakNotification.Reply.ReplyStatus.Ok) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {

#   if UNITY_IOS
                        // On iOS there should be no errors
#       if TEAK_3_1_OR_NEWER
                        if ("notification.foreground".Equals(logEvent.EventType) || "notification.received".Equals(logEvent.EventType)) {
#       else
                        if ("notification.received".Equals(logEvent.EventType) || "notification.received".Equals(logEvent.EventType)) {
#       endif // TEAK_3_1_OR_NEWER
                            state(Test.TestState.Passed);
                            return;
                        }
#   elif UNITY_ANDROID
                        // Should be fine on API Level 22+
                        if (this.AndroidAPILevel > 21 && "notification.received".Equals(logEvent.EventType)) {
                            state(Test.TestState.Passed);
                            return;
                        } else if("error.loghandler".Equals(logEvent.EventType)) {
                            // On API < 22 it should report the parse error
                            state(Test.TestState.Passed);
                            return;
                        }
#   endif // UNITY_IOS
                        state(Test.TestState.Pending);
                    }),
#else // TEAK_2_2_OR_NEWER
                TestBuilder.Build("Simple Notification", this)
                    .ScheduleNotification("test_none"),
#endif // TEAK_2_2_OR_NEWER

                TestBuilder.Build("Cancel Notification", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.StartCoroutine(TeakNotification.ScheduleNotification("test_none", "test_none", 10, (TeakNotification.Reply reply) => {
                            if (reply.Status == TeakNotification.Reply.ReplyStatus.Ok) {
                                string scheduledId = reply.Notifications[0].ScheduleId;
                                this.StartCoroutine(TeakNotification.CancelScheduledNotification(scheduledId, (TeakNotification.Reply cancelReply) => {
                                    string canceledId = null;
                                    if (cancelReply.Notifications != null) {
                                        canceledId = cancelReply.Notifications[0].ScheduleId;
                                    }
                                    state(scheduledId.Equals(canceledId) ? Test.TestState.Passed : Test.TestState.Failed);
                                }));
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    }),

                TestBuilder.Build("Cancel All Notifications", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.StartCoroutine(TeakNotification.ScheduleNotification("test_none", "test_none", 10, (TeakNotification.Reply reply) => {
                            if (reply.Status == TeakNotification.Reply.ReplyStatus.Ok) {
                                string scheduledId = reply.Notifications[0].ScheduleId;
                                this.StartCoroutine(TeakNotification.CancelAllScheduledNotifications((TeakNotification.Reply cancelReply) => {
                                    string canceledId = null;
                                    Debug.Log("LOOKING FOR: " + scheduledId);
                                    if (cancelReply.Notifications != null) {
                                        canceledId = cancelReply.Notifications[0].ScheduleId;
                                        Debug.Log("FOUND: " + cancelReply.Notifications);
                                        Debug.Log("FOUND: " + cancelReply.Notifications[0]);
                                    }
                                    state(scheduledId.Equals(canceledId) ? Test.TestState.Passed : Test.TestState.Failed);
                                }));
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    }),

#if UNITY_IOS && TEAK_2_2_OR_NEWER
                TestBuilder.Build("Notification with Non-Teak Deep Link (backgrounded)", this)
                    .ScheduleBackgroundNotification("test_nonteak_deeplink")
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("test.delegate".Equals(logEvent.EventType)) {
                            state(Test.TestState.Passed);
                        } else {
                            state(Test.TestState.Pending);
                        }
                    }),
#endif // UNITY_IOS && TEAK_2_2_OR_NEWER

                TestBuilder.Build("Numeric Attributes (15+ seconds)", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        double randomDouble = UnityEngine.Random.value;
                        StartCoroutine(this.TestNumericAttribute("automated_test_number", randomDouble, (n) => {
                            state(n == randomDouble ? Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),

                TestBuilder.Build("String Attributes (15+ seconds)", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        string randomString = Utils.RandomNonConfusingCharacterString(20);
                        StartCoroutine(this.TestStringAttribute("automated_test_string", randomString, (str) => {
                            state(randomString.Equals(str) ? Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),

#if TEAK_2_2_OR_NEWER
                TestBuilder.Build("Store Current Deep Link Path", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        Debug.Log("Last deep link: " + this.LaunchedFromDeepLinkPath);
                        this.globalContext["lastDeepLink"] = this.LaunchedFromDeepLinkPath;
                        state(Test.TestState.Passed);
                    }),
#endif // TEAK_2_2_OR_NEWER

#if TEAK_3_2_OR_NEWER
                TestBuilder.Build("Logout", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        Teak.Instance.Logout();
                        state(Test.TestState.Passed);
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("session.state".Equals(logEvent.EventType) &&
                            "Expired".Equals(logEvent.EventData["state"] as string)) {
                            state(Test.TestState.Passed);
                        } else {
                            state(Test.TestState.Pending);
                        }
                    }),
#endif // TEAK_3_2_OR_NEWER

#if TEAK_2_3_OR_NEWER
                TestBuilder.Build("Re-Identify User Providing Email", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.testContext["userEmail"] = "bogus@teak.io";
                        Teak.Instance.IdentifyUser(this.teakInterface.TeakUserId, this.testContext["userEmail"] as string);
                        state(Test.TestState.Passed);
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("request.send".Equals(logEvent.EventType) &&
                            (logEvent.EventData["endpoint"] as string).EndsWith("/users.json")) {
                            Debug.Log(logEvent);
                            Dictionary<string, object> payload = logEvent.EventData["payload"] as Dictionary<string, object>;

                            if (this.teakInterface.TeakUserId.Equals(payload["api_key"] as string) &&
                                (this.testContext["userEmail"] as string).Equals(payload["email"] as string) &&
                                ((bool)payload["do_not_track_event"])) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        } else {
                            state(Test.TestState.Pending);
                        }
                    }),
#endif // TEAK_2_3_OR_NEWER

#if TEAK_2_2_OR_NEWER
                // This should be the last test in the list, just to keep it easy
                TestBuilder.Build("Re-Identify User with New User Id", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.testContext["updatedUserId"] = "re-identify-test-" + this.teakInterface.TeakUserId;
                        Teak.Instance.IdentifyUser(this.testContext["updatedUserId"] as string);
                        state(Test.TestState.Passed);
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("request.send".Equals(logEvent.EventType) &&
                            (logEvent.EventData["endpoint"] as string).EndsWith("/users.json")) {
                            Debug.Log(logEvent);
                            Dictionary<string, object> payload = logEvent.EventData["payload"] as Dictionary<string, object>;
                            if ((this.testContext["updatedUserId"] as string).Equals(payload["api_key"] as string)) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        } else {
                            state(Test.TestState.Pending);
                        }
                    })
                    .WhenFinished(() => {
                        Teak.Instance.IdentifyUser(this.teakInterface.TeakUserId);
                    }),
#endif // TEAK_2_2_OR_NEWER

#if TEAK_2_2_OR_NEWER
                TestBuilder.Build("Ensure re-identifying the user didn't re-run deep links", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Coroutine.DoAfterSeconds(5.0f, () => {
                            Debug.Log("Current deep link: " + this.LaunchedFromDeepLinkPath);
                            if (this.LaunchedFromDeepLinkPath.Equals(this.globalContext["lastDeepLink"] as string)) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    })
#endif // TEAK_2_2_OR_NEWER

#endif // !TEAK_NOT_AVAILABLE
            };
        }
    }
}
