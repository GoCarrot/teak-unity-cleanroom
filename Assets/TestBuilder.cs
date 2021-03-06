#if !TEAK_NOT_AVAILABLE
using MiniJSON.Teak;
#endif

using UnityEngine;

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

class TestBuilder {
    // private static List<Test> allBuiltTests = new List<Test>();
    private Test test;
    private TestDriver testDriver;

    public static TestBuilder Build(string testName, TestDriver testDriver) {
        return new TestBuilder(testName, testDriver);
    }

    public static implicit operator Test(TestBuilder builder) {
        return builder.test;
    }

    private TestBuilder(string testName,  TestDriver testDriver) {
        this.testDriver = testDriver;

        this.test = new Test();
        this.test.Name = testName;
        this.test.ExcludedPlatforms = new HashSet<RuntimePlatform>();
        test.OnResult = this.GetOnResultWithAdditionalAction(null);
    }

    public TestBuilder ExcludePlatform(RuntimePlatform platform) {
        this.test.ExcludedPlatforms.Add(platform);
        return this;
    }

    public TestBuilder ExcludeWebGL() {
        return this.ExcludePlatform(RuntimePlatform.WebGLPlayer);
    }

    public TestBuilder OnlyIOS() {
        return this.ExcludePlatform(RuntimePlatform.Android).ExcludePlatform(RuntimePlatform.WebGLPlayer);
    }

    public TestBuilder WhenStarted(Action<Action<Test.TestState>> action) {
        test.OnBegin = action;
        return this;
    }

    public TestBuilder ScheduleNotification(string creativeId) {
#if !TEAK_NOT_AVAILABLE
        test.OnBegin = this.ScheduleNotification(creativeId, false);
        test.OnForegroundNotification = this.ValidateNotification(creativeId);
#endif
        return this;
    }

    public TestBuilder ScheduleBackgroundNotification(string creativeId) {
#if !TEAK_NOT_AVAILABLE
        test.OnBegin = this.ScheduleNotification(creativeId, true);
        test.OnLaunchedFromNotification = this.ValidateNotification(creativeId);
#endif
        return this;
    }

    public TestBuilder ExpectReward() {
        return this.ExpectReward(null);
    }

    public TestBuilder ExpectReward(string rewardItem) {
        // TODO: rewardItem
#if !TEAK_NOT_AVAILABLE
        test.OnReward = (TeakReward reward, Action<Test.TestState> state) => {
            state(string.IsNullOrEmpty(reward.RewardId) ? Test.TestState.Failed : Test.TestState.Passed);
        };
#endif
        return this;
    }

    public TestBuilder ExpectDeepLink() {
        return this.ExpectDeepLink((Dictionary<string, object> parameters, Action<Test.TestState> state) => {
            state(Test.TestState.Passed);
        });
    }

    public TestBuilder ExpectDeepLink(Action<Dictionary<string, object>, Action<Test.TestState>> callback) {
#if !TEAK_NOT_AVAILABLE
        test.OnDeepLink = callback;
#endif
        return this;
    }

    public TestBuilder ExpectPushToken() {
#if UNITY_IOS
        if (UnityEngine.iOS.NotificationServices.deviceToken == null) {
            test.OnPushTokenChanged = (string pushToken, Action<Test.TestState> state) => {
                state(Test.TestState.Passed);
            };
        }
#endif
        return this;
    }

#if !TEAK_NOT_AVAILABLE && TEAK_2_2_OR_NEWER
    public TestBuilder ExpectLogEvent(Action<TeakLogEvent, Action<Test.TestState>> action) {
        test.OnLogEvent = action;
        return this;
    }
#endif

    public TestBuilder BeforeFinished(Action<Action<Test.TestState>> action) {
        test.OnComplete = action;
        return this;
    }

    public TestBuilder WhenFinished(Action done) {
        test.OnResult = this.GetOnResultWithAdditionalAction(done);
        return this;
    }

    private Action GetOnResultWithAdditionalAction(Action done) {
        return () => {
            if (done != null) {
                done();
            }
            testDriver.OnTestBuilderTestDone();
        };
    }
#if !TEAK_NOT_AVAILABLE
    private Action<Action<Test.TestState>> ScheduleNotification(string creativeId, bool backgroundApp) {
        return (Action<Test.TestState> state) => {
            testDriver.StartCoroutine(TeakNotification.ScheduleNotification(creativeId, this.test.Name, 5, (TeakNotification.Reply reply) => {
                // if (reply.Notifications[0].ScheduleId == null)
                // {
                //     errorText = "ScheduleId was null";
                // }
                state(reply.Status == TeakNotification.Reply.ReplyStatus.Ok ? Test.TestState.Passed : Test.TestState.Failed);

                if (backgroundApp) {
                    Utils.BackgroundApp();
                }
            }));
        };
    }

    private Action<TeakNotification, Action<Test.TestState>> ValidateNotification(string creativeId) {
        return (TeakNotification notification, Action<Test.TestState> state) => {
            bool notificationValid = true;
            notificationValid &= creativeId.Equals(notification.CreativeId, System.StringComparison.Ordinal);
#if TEAK_3_2_OR_NEWER
#   if UNITY_IOS
            notificationValid &= "ios_push".Equals(notification.ChannelName, System.StringComparison.Ordinal);
#   elif UNITY_ANDROID
            notificationValid &= "android_push".Equals(notification.ChannelName, System.StringComparison.Ordinal);
#   endif
#endif // TEAK_3_2_OR_NEWER
            state(notificationValid ? Test.TestState.Passed : Test.TestState.Failed);
        };
    }
#endif
}
