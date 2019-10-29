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
        test.OnResult = this.GetOnResultWithAdditionalAction(null);
    }

    public TestBuilder WhenStarted(Action<Action<Test.TestState>> action) {
        test.OnBegin = action;
        return this;
    }

    public TestBuilder ScheduleNotification(string creativeId) {
#if !TEAK_NOT_AVAILABLE
        test.OnBegin = this.ScheduleNotification(creativeId, false);
        test.OnForegroundNotification = this.ValidateCreativeId(creativeId);
#endif
        return this;
    }

    public TestBuilder ScheduleBackgroundNotification(string creativeId) {
#if !TEAK_NOT_AVAILABLE
        test.OnBegin = this.ScheduleNotification(creativeId, true);
        test.OnLaunchedFromNotification = this.ValidateCreativeId(creativeId);
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
        return this.ExpectDeepLink(null);
    }

    public TestBuilder ExpectDeepLink(string dataContents) {
        // TODO: dataContents
#if !TEAK_NOT_AVAILABLE
        test.OnDeepLink = (Dictionary<string, object> parameters, Action<Test.TestState> state) => {
            state(Test.TestState.Passed);
        };
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

    private Action<TeakNotification, Action<Test.TestState>> ValidateCreativeId(string creativeId) {
        return (TeakNotification notification, Action<Test.TestState> state) => {
            state(creativeId.Equals(notification.CreativeId, System.StringComparison.Ordinal) ? Test.TestState.Passed : Test.TestState.Failed);
        };
    }
#endif
}
