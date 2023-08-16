// using UnityEngine;

using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using TeakCleanroomExtensions;

public partial class TestDriver : UnityEngine.MonoBehaviour {
    public static bool ShouldBeAbleToOpenNotificationSettings {
        get {
            Regex rx = new Regex("(iOS|iPadOS||Android OS) (\\d+)\\.?(\\d+)?\\.?(\\d+)?",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Debug.Log("Reported OS Version: " + UnityEngine.SystemInfo.operatingSystem);
            MatchCollection matches = rx.Matches(UnityEngine.SystemInfo.operatingSystem);

            foreach (Match match in matches) {
                GroupCollection groups = match.Groups;
                string os = groups[1].Value;
                int major, minor = 0, patch = 0;
                Int32.TryParse(groups[2].Value, out major);
                Int32.TryParse(groups[3].Value, out minor);
                Int32.TryParse(groups[4].Value, out patch);

                Debug.Log("OS: {0}, version {1}.{2}.{3}", os, major, minor, patch);

                bool shouldOpen = false;
                if ((os == "iOS" || os == "iPadOS") && (major > 15 || (major == 15 && minor >= 4))) {
                    shouldOpen = true;
                } else if (os == "Android OS" && major >= 8) {
                    shouldOpen = true;
                }

                return shouldOpen;
            }

            return false;
        }
    }

    List<Test> MasterTestList {
        get {
            return new List<Test> {
#if !TEAK_NOT_AVAILABLE

#   if TEAK_4_2_OR_NEWER
                TestBuilder.Build("Wait For PostLaunchSummary", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        Teak.Instance.IdentifyUser(this.teakInterface.TeakUserId, new Teak.UserConfiguration {});
                        state(Test.TestState.Passed);
                    })
                    .ExpectUserData((object obj, Action<Test.TestState> state) => {
                        state(Test.TestState.Passed);
                    }),
#   endif

//                 TestBuilder.Build("Reward Link", this)
//                     .ExpectDeepLink()
//                     .ExpectReward()
// #if !UNITY_WEBGL
//                     .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
//                         // Make sure it has app_version and app_version_name (Android only)
//                         if ("request.send".Equals(logEvent.EventType) &&
//                             "rewards.gocarrot.com".Equals(logEvent.EventData["hostname"] as string)) {
//                             Dictionary<string, object> payload = logEvent.EventData["payload"] as Dictionary<string, object>;
//                             if (payload.ContainsKey("app_version")
// #   if UNITY_ANDROID
//                                 && payload.ContainsKey("app_version_name")
// #   endif
//                                 ) {
//                                 state(Test.TestState.Passed);
//                             } else {
//                                 state(Test.TestState.Failed);
//                             }
//                         } else {
//                             state(Test.TestState.Pending);
//                         }
//                     })
// #endif // !UNITY_WEBGL
//                     .BeforeFinished((Action<Test.TestState> state) => {
//                         state(this.DeepLinkTestExceptionThrown ? Test.TestState.Passed : Test.TestState.Failed);
//                     }),

#if UNITY_IOS || UNITY_ANDROID
                TestBuilder.Build("Push Notification Permission", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.RegisterForNotifications(granted => {
                            state(granted ? Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),
#endif

                // Re-identify the user, providing email. This was an NSNull crash
                // in the 4.1.x family
                TestBuilder.Build("Re-Identify User Providing Email", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.testContext["userEmail"] = "ops@teak.io";
                        Teak.Instance.IdentifyUser(this.teakInterface.TeakUserId, this.testContext["userEmail"] as string);
                        state(Test.TestState.Passed);
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("request.send".Equals(logEvent.EventType) &&
                            (logEvent.EventData["endpoint"] as string).EndsWith("/users.json") &&
                            ((logEvent.EventData["payload"] as Dictionary<string, object>)["email"] != null)) {
                            Debug.Log(logEvent);
                            Dictionary<string, object> payload = logEvent.EventData["payload"] as Dictionary<string, object>;
                            Debug.Log(this.teakInterface.TeakUserId.Equals(payload["api_key"] as string) ? "Api Key match" : "Api Key mismatch");
                            Debug.Log(payload.ContainsKey("email") ? "Email present" : "Email absent");
                            Debug.Log((this.testContext["userEmail"] as string).Equals(payload["email"] as string) ? "Email match" : "Email mismatch");
                            Debug.Log((bool)payload["do_not_track_event"] ? "do not track" : "tracked");


                            if (this.teakInterface.TeakUserId.Equals(payload["api_key"] as string) &&
                                payload.ContainsKey("email") &&
                                (this.testContext["userEmail"] as string).Equals(payload["email"] as string) &&
                                ((bool)payload["do_not_track_event"])) {
                                Debug.Log("Re-Identify Passed");
                                state(Test.TestState.Passed);
                            } else {
                                Debug.Log("Re-Identify Failed");
                                state(Test.TestState.Failed);
                            }
                        } else {
                            Debug.Log("Re-Identify Pending");
                            state(Test.TestState.Pending);
                        }
                    })
                    .ExpectUserData((object obj, Action<Test.TestState> state) => {
#if TEAK_4_2_OR_NEWER
                        Teak.UserData userData = obj as Teak.UserData;
                        Debug.Log(userData.EmailStatus.State);
                        if(
                            (userData.EmailStatus.State == Teak.Channel.State.Available || userData.EmailStatus.State == Teak.Channel.State.OptIn)
                            && userData.PushStatus.State == Teak.Channel.State.OptIn
#if TEAK_4_3_OR_NEWER
                            && userData.EmailStatus["teak"] == Teak.Channel.State.OptIn
                            && userData.PushStatus["teak"] == Teak.Channel.State.OptIn
#endif // TEAK_4_3_OR_NEWER
                        ) {
                            state(Test.TestState.Passed);
                        } else {
                            state(Test.TestState.Failed);
                        }
#endif
                    }),

                TestBuilder.Build("Delete Email", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        Teak.Instance.DeleteEmail();
                        StartCoroutine(Coroutine.DoAfterSeconds(0.1f,() => {
                            state(Test.TestState.Passed);
                        }));
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("request.reply".Equals(logEvent.EventType) &&
                            ((logEvent.EventData["endpoint"] as string).EndsWith("/me/email.json") || (logEvent.EventData["endpoint"] as string).EndsWith("/me/email"))) {
                            Debug.Log(logEvent);
                            state(Test.TestState.Passed);
                         } else {
                            state(Test.TestState.Pending);
                        }
                    }),

                // Verify that we can set the same player email address after a deleteEmail call.
                TestBuilder.Build("Re-Identify User Providing Email Take 2", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.testContext["userEmail"] = "ops@teak.io";
                        Teak.Instance.IdentifyUser(this.teakInterface.TeakUserId, this.testContext["userEmail"] as string);
                        state(Test.TestState.Passed);
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("request.send".Equals(logEvent.EventType) &&
                            (logEvent.EventData["endpoint"] as string).EndsWith("/users.json") &&
                            ((logEvent.EventData["payload"] as Dictionary<string, object>)["email"] != null)) {
                            Debug.Log(logEvent);
                            Dictionary<string, object> payload = logEvent.EventData["payload"] as Dictionary<string, object>;
                            Debug.Log(this.teakInterface.TeakUserId.Equals(payload["api_key"] as string) ? "Api Key match" : "Api Key mismatch");
                            Debug.Log(payload.ContainsKey("email") ? "Email present" : "Email absent");
                            Debug.Log((this.testContext["userEmail"] as string).Equals(payload["email"] as string) ? "Email match" : "Email mismatch");
                            Debug.Log((bool)payload["do_not_track_event"] ? "do not track" : "tracked");


                            if (this.teakInterface.TeakUserId.Equals(payload["api_key"] as string) &&
                                payload.ContainsKey("email") &&
                                (this.testContext["userEmail"] as string).Equals(payload["email"] as string) &&
                                ((bool)payload["do_not_track_event"])) {
                                Debug.Log("Re-Identify Passed");
                                state(Test.TestState.Passed);
                            } else {
                                Debug.Log("Re-Identify Failed");
                                state(Test.TestState.Failed);
                            }
                        } else {
                            Debug.Log("Re-Identify Pending");
                            state(Test.TestState.Pending);
                        }
                    })
                    .ExpectUserData((object obj, Action<Test.TestState> state) => {
#if TEAK_4_2_OR_NEWER
                        Teak.UserData userData = obj as Teak.UserData;
                        if(
                            (userData.EmailStatus.State == Teak.Channel.State.Available || userData.EmailStatus.State == Teak.Channel.State.OptIn)
                            && userData.PushStatus.State == Teak.Channel.State.OptIn
#if TEAK_4_3_OR_NEWER
                            && userData.EmailStatus["teak"] == Teak.Channel.State.OptIn
                            && userData.PushStatus["teak"] == Teak.Channel.State.OptIn
#endif // TEAK_4_3_OR_NEWER
                        ) {
                            state(Test.TestState.Passed);
                        } else {
                            state(Test.TestState.Failed);
                        }
#endif
                    }),

#if UNITY_ANDROID && !UNITY_EDITOR
                // For 2.1.1, test to make certain that io_teak_enable_caching is definitely
                // removed and disabled (even if the XML persists)
                TestBuilder.Build("Android io_teak_enable_caching is disabled", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        UnityEngine.AndroidJavaClass httpResponseCache = new UnityEngine.AndroidJavaClass("android.net.http.HttpResponseCache");
                        UnityEngine.AndroidJavaObject installedCache = httpResponseCache.CallStatic<UnityEngine.AndroidJavaObject>("getInstalled");
                        state(installedCache == null ? Test.TestState.Passed : Test.TestState.Failed);
                    }),
#endif

#if TEAK_4_2_OR_NEWER
                TestBuilder.Build("Handle Deep Link Path", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        state(Teak.Instance.HandleDeepLinkPath("/test/asdf") ?
                            Test.TestState.Passed : Test.TestState.Failed);
                    })
                    .ExpectDeepLink(),

                TestBuilder.Build("CanOpenNotificationSettings", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        state(Teak.Instance.CanOpenNotificationSettings == ShouldBeAbleToOpenNotificationSettings ?
                            Test.TestState.Passed : Test.TestState.Failed);
                    }),

                TestBuilder.Build("Set Channel (Email) to OptOut", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.SetChannelState(Teak.Channel.Type.Email, Teak.Channel.State.OptOut, (reply) => {
                            state(reply.State == Teak.Channel.State.OptOut ? Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),

                TestBuilder.Build("Set Channel (Email) to OptIn", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.SetChannelState(Teak.Channel.Type.Email, Teak.Channel.State.Available, (reply) => {
                            state(reply.State == Teak.Channel.State.OptIn || reply.State == Teak.Channel.State.Available ?
                                      Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),

#if TEAK_4_3_OR_NEWER
                TestBuilder.Build("Set Category State (Email) to OptOut", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.SetCategoryState(Teak.Channel.Type.Email, "teak", Teak.Channel.State.OptOut, (reply) => {
                            state(!reply.Error && reply.State == Teak.Channel.State.OptOut && reply.Channel == Teak.Channel.Type.Email && reply.Category == "teak" ?
                                Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),

                TestBuilder.Build("Set Category State (Email) to OptIn", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.SetCategoryState(Teak.Channel.Type.Email, "teak", Teak.Channel.State.OptIn, (reply) => {
                            state(!reply.Error && reply.State == Teak.Channel.State.OptIn && reply.Channel == Teak.Channel.Type.Email && reply.Category == "teak" ?
                                Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),

                TestBuilder.Build("Set Does Not Exist Category State (Email) to OptOut", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.SetCategoryState(Teak.Channel.Type.Email, "never never exist", Teak.Channel.State.OptOut, (reply) => {
                            if(reply.Error && reply.Errors["category"][0].Contains("Unknown category")) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    }),
#endif // TEAK_4_3_OR_NEWER

                TestBuilder.Build("Set Channel (Platform Push) to OptOut", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.SetChannelState(Teak.Channel.Type.PlatformPush, Teak.Channel.State.OptOut, (reply) => {
                            state(reply.State == Teak.Channel.State.OptOut ? Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),

                TestBuilder.Build("Set Channel (Platform Push) to OptIn", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.SetChannelState(Teak.Channel.Type.PlatformPush, Teak.Channel.State.Available, (reply) => {
                            state(reply.State == Teak.Channel.State.OptIn || reply.State == Teak.Channel.State.Available ?
                                      Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),

                TestBuilder.Build("Delete Email", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        Teak.Instance.DeleteEmail();
                        StartCoroutine(Coroutine.DoAfterSeconds(0.1f,() => {
                            state(Test.TestState.Passed);
                        }));
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("request.reply".Equals(logEvent.EventType) &&
                            ((logEvent.EventData["endpoint"] as string).EndsWith("/me/email.json") || (logEvent.EventData["endpoint"] as string).EndsWith("/me/email"))) {
                            Debug.Log(logEvent);
                            state(Test.TestState.Passed);
                         } else {
                            state(Test.TestState.Pending);
                        }
                    }),

                TestBuilder.Build("Set Channel (Email) to OptOut No Email", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.SetChannelState(Teak.Channel.Type.Email, Teak.Channel.State.OptOut, (reply) => {
                            if(reply.Error && reply.Errors["email"][0].Contains("not reachable")) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    }),

#if TEAK_4_3_OR_NEWER
                TestBuilder.Build("Set Category State (Email) to OptOut No Email", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Teak.Instance.SetCategoryState(Teak.Channel.Type.Email, "teak", Teak.Channel.State.OptOut, (reply) => {
                            if(reply.Error && reply.Errors["email"][0].Contains("not reachable")) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    }),
#endif // TEAK_4_3_OR_NEWER
#endif // TEAK_4_2_OR_NEWER

#if !UNITY_WEBGL
                TestBuilder.Build("Notification with Emoji", this)
                    .WhenStarted((Action<Test.TestState> state) => {

                        // Have to use "WhenStarted" because ScheduleNotification will wait for a received log event
                        this.StartCoroutine(TeakNotification.ScheduleNotification("test_emoji_log_exception", "test_emoji_log_exception", 0, (TeakNotification.Reply reply) => {
                            if (reply.Status == TeakNotification.Reply.ReplyStatus.Ok) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
#if UNITY_IOS
                        // On iOS there should be no errors
                        if ("notification.foreground".Equals(logEvent.EventType) || "notification.received".Equals(logEvent.EventType)) {
                            state(Test.TestState.Passed);
                            return;
                        }
#elif UNITY_ANDROID // UNITY_IOS
                        // Should be fine on API Level 22+
                        if (this.AndroidAPILevel > 21 && "notification.received".Equals(logEvent.EventType)) {
                            state(Test.TestState.Passed);
                            return;
                        } else if("error.loghandler".Equals(logEvent.EventType)) {
                            // On API < 22 it should report the parse error
                            state(Test.TestState.Passed);
                            return;
                        }
#endif
                        state(Test.TestState.Pending);
                    }),
#endif // !UNITY_WEBGL

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
                                    List<string> canceledIds = new List<string>();
                                    if (cancelReply.Notifications != null) {
                                        canceledIds = cancelReply.Notifications.Select(e => e.ScheduleId).ToList();
                                    }
                                    Debug.Log("Cancel All Notifications");
                                    Debug.Log(scheduledId);
                                    foreach(string id in canceledIds) {
                                        Debug.Log(id);
                                    }
                                    state(canceledIds.Contains(scheduledId) ? Test.TestState.Passed : Test.TestState.Failed);
                                }));
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    }),

                TestBuilder.Build("Report errors for invalid scheduling", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.StartCoroutine(Teak.Notification.Schedule("test_never_ever_create_this_notification", 5, null, (Teak.Notification.Reply reply) => {
                            Debug.Log("Response for Report errors fro invalid scheduling received");
                            Debug.Log(reply);
                            if (!reply.Error || !reply.Errors["identifier"][0].Contains("dashboard")) {
                                state(Test.TestState.Failed);
                            } else {
                                state(Test.TestState.Passed);
                            }
                        }));
                    }),

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

                TestBuilder.Build("Notification with Non-Teak Deep Link (backgrounded)", this)
#if !TEAK_4_1_OR_NEWER
                    .OnlyIOS()
#else
                    .ExcludeWebGL()
#endif
                    .ScheduleBackgroundNotification("test_nonteak_deeplink")
#if UNITY_IOS
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("test.delegate".Equals(logEvent.EventType)) {
                            state(Test.TestState.Passed);
                        } else {
                            state(Test.TestState.Pending);
                        }
                    })
#endif
                    .ExpectPostLaunchSummary((object obj, Action<Test.TestState> state) => {
#if TEAK_4_1_OR_NEWER
                        TeakPostLaunchSummary summary = obj as TeakPostLaunchSummary;
                        if(summary.LaunchLink.StartsWith("nonteak://") && summary.ScheduleName.Contains("test_nonteak_deeplink")) {
                            state(Test.TestState.Passed);
                        } else {
                            state(Test.TestState.Failed);
                        }
#endif
                    }),

#if TEAK_4_3_OR_NEWER && !UNITY_WEBGL
                    TestBuilder.Build("Session Stop on Backgrounding", this)
                        .WhenStarted((Action<Test.TestState> state) => {
                            this.globalContext["templatedString"] = Utils.RandomNonConfusingCharacterString(20);
                            this.StartCoroutine(Teak.Notification.Schedule("test_incentivized_templated", 5, new Dictionary<string, object> { {"templatedString", this.globalContext["templatedString"] } }, (Teak.Notification.Reply reply) => {
                                if (!reply.Error) {
                                    Utils.BackgroundApp();
                                    state(Test.TestState.Passed);
                                } else {
                                    state(Test.TestState.Failed);
                                }
                            }));
                        })
                        .ExpectPostLaunchSummary((object obj, Action<Test.TestState> state) => {
#if TEAK_4_1_OR_NEWER
                            TeakPostLaunchSummary summary = obj as TeakPostLaunchSummary;
                            if((summary.DeepLink as string).EndsWith(this.globalContext["templatedString"] as string)) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
#endif
                        })
                        .ExpectReward()
                        .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                            if ("request.send".Equals(logEvent.EventType) &&
                            (logEvent.EventData["endpoint"] as string).EndsWith("/session_stop")) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Pending);
                            }
                        }),

                    TestBuilder.Build("Session Resume on Returning", this)
                        .WhenStarted((Action<Test.TestState> state) => {
                            if(Teak.Instance.CanOpenNotificationSettings) {
                                Teak.Instance.OpenNotificationSettings();
                            } else if (Teak.Instance.CanOpenSettingsAppToThisAppsSettings) {
                                Teak.Instance.OpenSettingsAppToThisAppsSettings();
                            } else {
                                Utils.BackgroundApp();
                            }
                            state(Test.TestState.Passed);
                        })
                        .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                            if ("request.send".Equals(logEvent.EventType) &&
                            (logEvent.EventData["endpoint"] as string).EndsWith("/session_resume")) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Pending);
                            }
                        }),
#endif

                TestBuilder.Build("Store Current Deep Link Path", this)
                    .ExcludeWebGL()
                    .WhenStarted((Action<Test.TestState> state) => {
                        // We wait for a few seconds to allow the new attribution to kick in.
                        // This is really a hack, and in no way good.
                        StartCoroutine(Coroutine.DoAfterSeconds(5.0f,() => {
                            this.LaunchedFromDeepLinkPath = "fake_deep_link_path";
                            Debug.Log("Storing deep link: " + this.LaunchedFromDeepLinkPath);
                            this.globalContext["lastDeepLink"] = this.LaunchedFromDeepLinkPath;
                            state(Test.TestState.Passed);
                        }));
                    }),

                // This should be the last test in the list, just to keep it easy
                TestBuilder.Build("Re-Identify User with New User Id", this)
                    .ExcludeWebGL()
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.testContext["updatedUserId"] = "re-identify-test-" + this.teakInterface.TeakUserId;
                        Teak.Instance.IdentifyUser(this.testContext["updatedUserId"] as string, null, null);
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
                        Teak.Instance.IdentifyUser(this.teakInterface.TeakUserId, null, null);
                    }),

                TestBuilder.Build("Ensure re-identifying the user didn't re-run deep links", this)
                    .ExcludeWebGL()
                    .WhenStarted((Action<Test.TestState> state) => {
                        StartCoroutine(Coroutine.DoAfterSeconds(5.0f, () => {
                            Debug.Log("Current deep link: " + this.LaunchedFromDeepLinkPath);
                            if (this.LaunchedFromDeepLinkPath.Equals(this.globalContext["lastDeepLink"] as string)) {
                                state(Test.TestState.Passed);
                            } else {
                                state(Test.TestState.Failed);
                            }
                        }));
                    }),

                TestBuilder.Build("Logout", this)
                    .ExcludeWebGL()
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
                    })
#endif // !TEAK_NOT_AVAILABLE
            };
        }
    }
}
