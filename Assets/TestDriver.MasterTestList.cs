// using UnityEngine;

using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
                        Teak.Instance.RegisterForProvisionalNotifications();
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
                    .ExpectLogEvent(this.ReIdentifyWithEmailLogPredicate)
                    .ExpectUserData(this.ReIdentifyWithEmailUserDataPredicate),

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
                    .ExpectLogEvent(this.ReIdentifyWithEmailLogPredicate)
                    .ExpectUserData(this.ReIdentifyWithEmailUserDataPredicate),

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

#if UNITY_IOS
                // A link opened from outside the app, the way Safari or another app would. The host
                // runner (script/ios-suite) sees the marker and opens the URL.
                TestBuilder.Build("Open Teak link from outside the app", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.testContext["externalLinkToken"] = "external-" + Guid.NewGuid().ToString("N");
                        Debug.Log("[Test Driver] external open teak" + Teak.AppId + ":///test/" + this.testContext["externalLinkToken"]);
                        state(Test.TestState.Passed);
                    })
                    .ExpectDeepLink((Dictionary<string, object> parameters, Action<Test.TestState> state) => {
                        object data;
                        state(parameters.TryGetValue("data", out data) && this.testContext["externalLinkToken"].Equals(data) ?
                            Test.TestState.Passed : Test.TestState.Pending);
                    }),
#endif

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

#if UNITY_IOS
                // The Emoji test schedules for "now". A schedule that lands before the server has this
                // device's push token is accepted and then never sent, so wait for the token first.
                TestBuilder.Build("Wait For Push Token", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.StartCoroutine(this.WaitForPushTokenAtServer(state));
                    }),
#endif

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
                    }).ExpectPostLaunchSummary((object obj, Action<Test.TestState> state) => {
#if TEAK_4_1_OR_NEWER
                        TeakPostLaunchSummary summary = obj as TeakPostLaunchSummary;
                        if(summary.ScheduleName.Contains("test_emoji_log_exception")) {
                            state(Test.TestState.Passed);
                        } else {
                            state(Test.TestState.Failed);
                        }
#endif
                    }).ExpectReward(),
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
                        StartCoroutine(this.TestNumericAttribute("automated_test_number", randomDouble, state));
                    }),

                TestBuilder.Build("String Attributes (15+ seconds)", this)
                    .WhenStarted((Action<Test.TestState> state) => {
                        string randomString = Utils.RandomNonConfusingCharacterString(20);
                        StartCoroutine(this.TestStringAttribute("automated_test_string", randomString, (str) => {
                            state(randomString.Equals(str) ? Test.TestState.Passed : Test.TestState.Failed);
                        }));
                    }),

#if !UNITY_EDITOR
                // Each /me/profile payload carries profile_seq, one more than the last. Two values set in
                // separate batch windows: the second is set only once the first has gone out.
                TestBuilder.Build("Profile Sequence Increments", this)
                    .ExcludeWebGL()
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.SetProfileSeqValue("profileSeqFirstValue", UnityEngine.Random.Range(100000, 1000000));
                        state(Test.TestState.Passed);
                    })
                    .ExpectLogEvent(this.ProfileSeqLogPredicate),
#endif

                // A comma-decimal culture must not change what Teak reads or writes. The reward arrives the
                // way native delivers it, as a JSON string into the SDK's own parse path.
                TestBuilder.Build("Decimal Parse and Send in de-DE", this)
                    .ExcludeWebGL()
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.testContext["originalCulture"] = CultureInfo.CurrentCulture;
                        try {
                            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                        } catch (Exception e) {
                            this.FailCurrentTest("couldn't switch to de-DE: " + e.Message);
                            return;
                        }
                        string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                        Debug.Log("[TestDriver] Culture " + CultureInfo.CurrentCulture.Name + ", decimal separator '" + separator + "'");
                        if (separator != ",") {
                            this.FailCurrentTest("de-DE didn't take, decimal separator is '" + separator + "'");
                            return;
                        }

                        Teak.Instance.SendMessage("RewardClaimAttempt", DecimalRewardJson);
#if UNITY_EDITOR
                        // Schedule is a no-op in the editor, so check the serializer it would hand to native.
                        string sent = MiniJSON.Teak.Json.Serialize(this.DecimalPersonalizationData);
                        Debug.Log("[TestDriver] Personalization data serialized as " + sent);
                        state(sent == "{\"" + DecimalPersonalizationKey + "\":0.1}" ? Test.TestState.Passed : Test.TestState.Failed);
#else
                        this.StartCoroutine(Teak.Notification.Schedule("test_none", 600, this.DecimalPersonalizationData, (Teak.Notification.Reply reply) => {
                            if (reply.Error || reply.ScheduleIds == null || reply.ScheduleIds.Count == 0) {
                                this.FailCurrentTest("schedule with decimal personalization failed: " + reply);
                                return;
                            }
                            // Nothing should fire from this later in the suite.
                            this.StartCoroutine(TeakNotification.CancelScheduledNotification(reply.ScheduleIds[0], (TeakNotification.Reply cancelReply) => {}));
                            state(Test.TestState.Passed);
                        }));
#endif
                    })
                    .ExpectReward((TeakReward reward, Action<Test.TestState> state) => {
                        object small = null, large = null;
                        if (reward.Reward != null) {
                            reward.Reward.TryGetValue("coins", out small);
                            reward.Reward.TryGetValue("gems", out large);
                        }
                        Debug.Log("[TestDriver] Decimal reward read coins=" + DescribeNumber(small) + " gems=" + DescribeNumber(large));
                        state(small is double && (double) small == 0.1 && large is double && (double) large == 1000.0 ?
                              Test.TestState.Passed : Test.TestState.Failed);
                    })
#if !UNITY_EDITOR
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        // iOS logs the endpoint without ".json", Android with it.
                        Dictionary<string, object> payload = RequestPayload(logEvent, "/me/local_notify", "/me/local_notify.json");
                        if (payload == null) {
                            state(Test.TestState.Pending);
                            return;
                        }
                        Dictionary<string, object> personalization = payload.GetValueOrDefault("personalization_data") as Dictionary<string, object>;
                        object sent = personalization == null ? null : personalization.GetValueOrDefault(DecimalPersonalizationKey);
                        Debug.Log("[TestDriver] local_notify sent " + DecimalPersonalizationKey + "=" + DescribeNumber(sent));
                        state(sent is double && (double) sent == 0.1 ? Test.TestState.Passed : Test.TestState.Failed);
                    })
#endif
                    .WhenFinished(() => {
                        object original;
                        if (this.testContext.TryGetValue("originalCulture", out original)) {
                            CultureInfo.CurrentCulture = original as CultureInfo;
                        }
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

#if UNITY_ANDROID && !UNITY_EDITOR
                this.RedisplayedAnimatedNotificationTest(),
#endif

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

                    TestBuilder.Build("Notification Send on Backgrounding", this)
                        .WhenStarted((Action<Test.TestState> state) => {
                            Utils.BackgroundApp();
                            state(Test.TestState.Passed);
                        })
                        .WhenBackgrounded((Action<Test.TestState> state) => {
                            this.globalContext["templatedString"] = Utils.RandomNonConfusingCharacterString(20);
                            this.StartCoroutine(Teak.Notification.Schedule("test_incentivized_templated", 5, new Dictionary<string, object> { {"templatedString", this.globalContext["templatedString"] } }, (Teak.Notification.Reply reply) => {

                            }));
                            state(Test.TestState.Passed);
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
                        .ExpectReward(),

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

                // Intentionally trigger an SDK-internal exception so the native SDK catches it
                // via its own handler and reports it to Sentry/Raven with the run's accumulated
                // breadcrumbs attached. Placed late so plenty of breadcrumbs exist by now, which
                // is what makes the resulting Sentry report a meaningful end-to-end check.
                //
                // Verifying the breadcrumbs actually land on the report is a manual Sentry-dashboard
                // step (see C-840) -- nothing in game code can read the outbound Raven payload.
                //
                // Both platforms emit an observable "exception" log event we assert on for teeth:
                // Android log.exception() and iOS reportWithHelper: (the caught-exception path, since
                // C-843) both ship event_type "exception" with type = the exception name -- which is
                // "ReportTestException" on both (getSimpleName() / NSException.name), so the assert
                // keys identically with no per-platform split. Heads up: the harness has no per-test
                // timeout, so if that event never arrives this test HANGS rather than fails -- the
                // on-device event-arrival check is the load-bearing ship-time QA gate (C-840).
                TestBuilder.Build("Trigger SDK Exception (Sentry breadcrumbs)", this)
                    .ExcludeWebGL()
                    .WhenStarted((Action<Test.TestState> state) => {
                        this.teakInterface.TestExceptionReporting();
                        state(Test.TestState.Passed);
                    })
                    .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                        if ("exception".Equals(logEvent.EventType) &&
                            logEvent.EventData != null &&
                            logEvent.EventData.ContainsKey("type") &&
                            "ReportTestException".Equals(logEvent.EventData["type"] as string)) {
                            state(Test.TestState.Passed);
                        } else {
                            state(Test.TestState.Pending);
                        }
                    }),

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
                        // A users.json still in flight for the previous id can land here first, so only
                        // the request for the new id decides; a true miss falls to the watchdog.
                        Dictionary<string, object> payload = UsersJsonRequestPayload(logEvent);
                        if (payload != null && (this.testContext["updatedUserId"] as string).Equals(payload.GetValueOrDefault("api_key") as string)) {
                            Debug.Log(logEvent);
                            state(Test.TestState.Passed);
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

#if !TEAK_NOT_AVAILABLE
    /// <summary>The payload of a users.json request.send log event, or null for any other event.</summary>
    static Dictionary<string, object> UsersJsonRequestPayload(TeakLogEvent logEvent) {
        return RequestPayload(logEvent, "/users.json");
    }

    /// <summary>The payload of a request.send log event for the given endpoint, or null for any other event.</summary>
    static Dictionary<string, object> RequestPayload(TeakLogEvent logEvent, params string[] endpoints) {
        if (!"request.send".Equals(logEvent.EventType) || logEvent.EventData == null) {
            return null;
        }
        string endpoint = logEvent.EventData.GetValueOrDefault("endpoint") as string;
        if (endpoint == null || !endpoints.Any(e => endpoint.EndsWith(e))) {
            return null;
        }
        return logEvent.EventData.GetValueOrDefault("payload") as Dictionary<string, object>;
    }

    static string DescribeNumber(object value) {
        return value == null ? "(missing)" : Convert.ToString(value, CultureInfo.InvariantCulture) + " (" + value.GetType().Name + ")";
    }

    /// <summary>Fails the running test now with a reason, rather than at the watchdog.</summary>
    void FailCurrentTest(string reason) {
        if (this.testEnumerator != null) {
            this.testEnumerator.Current.ForceFailure(reason);
        }
    }

#if !UNITY_EDITOR
    // Must exist on the dashboard: Android drops attributes it doesn't already know. The values are
    // whole numbers, so they read back exactly after native's JSON round trip.
    const string ProfileSeqAttribute = "automated_test_number";

    void SetProfileSeqValue(string contextKey, double value) {
        this.testContext[contextKey] = value;
        Teak.Instance.SetNumericAttribute(ProfileSeqAttribute, value);
    }

    // Decides only on the sends carrying this test's values. Earlier tests' profile sends and retries
    // stay Pending.
    void ProfileSeqLogPredicate(TeakLogEvent logEvent, Action<Test.TestState> state) {
        Dictionary<string, object> payload = RequestPayload(logEvent, "/me/profile");
        Dictionary<string, object> numbers = payload == null ? null : payload.GetValueOrDefault("number_attributes") as Dictionary<string, object>;
        object raw = numbers == null ? null : numbers.GetValueOrDefault(ProfileSeqAttribute);
        if (raw == null) {
            state(Test.TestState.Pending);
            return;
        }

        double value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
        bool isFirst = value == (double) this.testContext["profileSeqFirstValue"];
        bool isSecond = this.testContext.ContainsKey("profileSeqSecondValue") &&
                        value == (double) this.testContext["profileSeqSecondValue"];
        if (!isFirst && !isSecond) {
            state(Test.TestState.Pending);
            return;
        }

        object seqRaw = payload.GetValueOrDefault("profile_seq");
        Debug.Log("[TestDriver] /me/profile " + ProfileSeqAttribute + "=" + DescribeNumber(raw) + " profile_seq=" + DescribeNumber(seqRaw));
        if (seqRaw == null) {
            UnityEngine.Debug.LogError("[TestDriver] /me/profile carried no profile_seq");
            state(Test.TestState.Failed);
            return;
        }
        long seq = Convert.ToInt64(seqRaw, CultureInfo.InvariantCulture);

        if (isFirst) {
            // A retry of the first send logs again with the same payload; only the first one counts.
            if (!this.testContext.ContainsKey("profileSeqFirst")) {
                this.testContext["profileSeqFirst"] = seq;
                this.SetProfileSeqValue("profileSeqSecondValue", value + 1);
            }
            state(Test.TestState.Pending);
            return;
        }

        long expected = (long) this.testContext["profileSeqFirst"] + 1;
        Debug.Log("[TestDriver] profile_seq " + seq + ", expected " + expected);
        if (seq == 1 && expected != 1 && !this.testContext.ContainsKey("profileSeqReanchored")) {
            // An identify reply in between gives the player a new profile, whose count starts over.
            // Start again from this send; a count stuck at 1 still fails on the next pair.
            Debug.Log("[TestDriver] profile_seq restarted at 1, re-anchoring on this send");
            this.testContext["profileSeqReanchored"] = true;
            this.testContext["profileSeqFirstValue"] = value;
            this.testContext["profileSeqFirst"] = seq;
            this.SetProfileSeqValue("profileSeqSecondValue", value + 1);
            state(Test.TestState.Pending);
            return;
        }
        state(seq == expected ? Test.TestState.Passed : Test.TestState.Failed);
    }
#endif // !UNITY_EDITOR

#if UNITY_ANDROID && !UNITY_EDITOR
    const string RedisplayTestName = "Re-displayed Animated Notification Opens Game";

    // Android 12+ re-posts an animated notification after the screen turns off, and a tap on the
    // re-posted one has to open the game. script/android-redisplay turns the screen off, waits for the
    // re-display and taps it; this side posts the notification and checks the attribution.
    Test RedisplayedAnimatedNotificationTest() {
        string skipReason = null;
        if (!this.adbHostAttached) {
            skipReason = "no adb host (run script/android-redisplay)";
        } else if (this.AndroidAPILevel < 31) {
            skipReason = "Android API " + this.AndroidAPILevel + ", re-display needs 31+";
        }
        if (skipReason != null) {
            return TestBuilder.Build(RedisplayTestName, this)
                .WhenStarted((Action<Test.TestState> state) => {
                    Debug.Log("[Test Driver] skipped " + RedisplayTestName + ": " + skipReason);
                    state(Test.TestState.Passed);
                });
        }

        return TestBuilder.Build(RedisplayTestName, this)
            .WhenStarted((Action<Test.TestState> state) => {
                // Teak parses teakNotifId as a number.
                string teakNotifId = ((long) UnityEngine.Random.Range(100000000, int.MaxValue)).ToString(CultureInfo.InvariantCulture);
                this.testContext["redisplayTeakNotifId"] = teakNotifId;
                Utils.ClearAllNotifications();
                Debug.Log("[Test Driver] host redisplay " + teakNotifId);
                try {
                    PostAnimatedNotificationOnceBackgrounded(teakNotifId);
                } catch (Exception e) {
                    // A minified host that renamed Teak's internals lands here.
                    UnityEngine.Debug.LogError("[Test Driver] couldn't post the animated notification: " + e);
                    state(Test.TestState.Failed);
                    return;
                }
                Utils.BackgroundApp();
                state(Test.TestState.Passed);
            })
            .ExpectLogEvent((TeakLogEvent logEvent, Action<Test.TestState> state) => {
                Dictionary<string, object> payload = UsersJsonRequestPayload(logEvent);
                object attributed = payload == null ? null : payload.GetValueOrDefault("teak_notif_id");
                if (attributed == null) {
                    state(Test.TestState.Pending);
                    return;
                }
                string expected = this.testContext["redisplayTeakNotifId"] as string;
                Debug.Log("[Test Driver] users.json teak_notif_id=" + attributed + ", expected " + expected);
                state(expected.Equals(Convert.ToString(attributed, CultureInfo.InvariantCulture)) ?
                      Test.TestState.Passed : Test.TestState.Failed);
            });
    }

    // Same shape as teak-android's example "Simulate Animated": a four-frame sprite sheet in the big
    // view. A foregrounded game suppresses display, so it posts a few seconds after backgrounding.
    // Both views carry the id as text, so the host finds the notification collapsed or expanded. A
    // frame lasts 2s because uiautomator can only dump the shade while it's idle between frames.
    static void PostAnimatedNotificationOnceBackgrounded(string teakNotifId) {
        string text = "Cleanroom animated " + teakNotifId;
        string display = MiniJSON.Teak.Json.Serialize(new Dictionary<string, object> {
            { "contentView", "teak_notif_no_title" },
            { "teak_notif_no_title", new Dictionary<string, object> { { "left_image", "BUILTIN_APP_ICON" }, { "text", text } } },
            { "bigContentView", "teak_big_notif_animated" },
            { "teak_big_notif_animated", new Dictionary<string, object> {
                { "text", text },
                { "view_animator", new Dictionary<string, object> {
                    { "sprite_sheet", "assets:///cleanroom-animated.png" },
                    { "display_ms", 2000 },
                    { "width", 128 },
                    { "height", 128 }
                } }
            } }
        });

        using (UnityEngine.AndroidJavaClass unityPlayer = new UnityEngine.AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (UnityEngine.AndroidJavaObject activity = unityPlayer.GetStatic<UnityEngine.AndroidJavaObject>("currentActivity")) {
            UnityEngine.AndroidJavaObject context = activity.Call<UnityEngine.AndroidJavaObject>("getApplicationContext");
            UnityEngine.AndroidJavaObject intent = new UnityEngine.AndroidJavaObject("android.content.Intent");
            intent.Call<UnityEngine.AndroidJavaObject>("putExtra", "teakNotifId", teakNotifId);
            intent.Call<UnityEngine.AndroidJavaObject>("putExtra", "version", "1");
            intent.Call<UnityEngine.AndroidJavaObject>("putExtra", "message", text);
            intent.Call<UnityEngine.AndroidJavaObject>("putExtra", "display", display);

            // Resolve the classes now, on the Unity thread, so a renamed class fails the test here.
            string received;
            using (UnityEngine.AndroidJavaClass eventClass = new UnityEngine.AndroidJavaClass("io.teak.sdk.event.PushNotificationEvent")) {
                received = eventClass.GetStatic<string>("Received");
            }
            UnityEngine.AndroidJavaObject pushEvent = new UnityEngine.AndroidJavaObject("io.teak.sdk.event.PushNotificationEvent", received, context, intent);

            using (UnityEngine.AndroidJavaObject looper = new UnityEngine.AndroidJavaClass("android.os.Looper").CallStatic<UnityEngine.AndroidJavaObject>("getMainLooper"))
            using (UnityEngine.AndroidJavaObject handler = new UnityEngine.AndroidJavaObject("android.os.Handler", looper)) {
                handler.Call<bool>("postDelayed", new PostTeakEvent(pushEvent), 3000L);
            }
        }
    }

    class PostTeakEvent : UnityEngine.AndroidJavaProxy {
        readonly UnityEngine.AndroidJavaObject teakEvent;

        public PostTeakEvent(UnityEngine.AndroidJavaObject teakEvent) : base("java.lang.Runnable") {
            this.teakEvent = teakEvent;
        }

        public void run() {
            using (UnityEngine.AndroidJavaClass teakEventClass = new UnityEngine.AndroidJavaClass("io.teak.sdk.TeakEvent")) {
                teakEventClass.CallStatic<bool>("postEvent", this.teakEvent);
            }
        }
    }
#endif // UNITY_ANDROID && !UNITY_EDITOR

    const string DecimalPersonalizationKey = "cleanroom_decimal";

    Dictionary<string, object> DecimalPersonalizationData {
        get { return new Dictionary<string, object> { { DecimalPersonalizationKey, 0.1 } }; }
    }

    // Shaped like the claim reply native hands to Teak.RewardClaimAttempt, with decimal quantities.
    const string DecimalRewardJson = "{\"teakRewardId\":\"cleanroom-decimal-reward\",\"status\":\"grant_reward\"," +
        "\"teakScheduleName\":\"cleanroom_decimal\",\"teakCreativeName\":\"cleanroom_decimal\",\"teakChannelName\":\"android_push\"," +
        "\"reward\":{\"coins\":0.1,\"gems\":1e3}}";

    // Identify requests that don't carry an email (e.g. the earlier push-token refresh) aren't the
    // one under test, so they stay Pending rather than throwing on a missing key.
    void ReIdentifyWithEmailLogPredicate(TeakLogEvent logEvent, Action<Test.TestState> state) {
        Dictionary<string, object> payload = UsersJsonRequestPayload(logEvent);
        if (payload == null || payload.GetValueOrDefault("email") == null) {
            state(Test.TestState.Pending);
            return;
        }

        Debug.Log(logEvent);
        object doNotTrack = payload.GetValueOrDefault("do_not_track_event");
        bool passed = this.teakInterface.TeakUserId.Equals(payload.GetValueOrDefault("api_key") as string) &&
                      (this.testContext["userEmail"] as string).Equals(payload["email"] as string) &&
                      doNotTrack is bool && (bool)doNotTrack;
        Debug.Log(passed ? "Re-Identify Passed" : "Re-Identify Failed");
        state(passed ? Test.TestState.Passed : Test.TestState.Failed);
    }

    // A user data reply from before the email was registered can arrive first, so wait for one that
    // shows the email channel before judging it.
    void ReIdentifyWithEmailUserDataPredicate(object obj, Action<Test.TestState> state) {
#if TEAK_4_2_OR_NEWER
        Teak.UserData userData = obj as Teak.UserData;
        Debug.Log(userData.EmailStatus.State);
        if (userData.EmailStatus.State != Teak.Channel.State.Available && userData.EmailStatus.State != Teak.Channel.State.OptIn) {
            state(Test.TestState.Pending);
            return;
        }

        bool passed = userData.PushStatus.State == Teak.Channel.State.OptIn
#if TEAK_4_3_OR_NEWER
                      && userData.EmailStatus["teak"] == Teak.Channel.State.OptIn
                      && userData.PushStatus["teak"] == Teak.Channel.State.OptIn
#endif // TEAK_4_3_OR_NEWER
                      ;
        state(passed ? Test.TestState.Passed : Test.TestState.Failed);
#endif // TEAK_4_2_OR_NEWER
    }
#endif // !TEAK_NOT_AVAILABLE
}
