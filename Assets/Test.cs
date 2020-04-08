#if !TEAK_NOT_AVAILABLE
using MiniJSON.Teak;
#endif

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

class Test {
    public enum TestState {
        Pending,
        Passed,
        Failed,
        Running
    }

    public string Name { get; set; }
    public TestState Status { get; set; }

    // State, this could probably be done better
    private TestState began, reward, deepLink, launchedFromNotification, foregroundNotification,
#if TEAK_2_2_OR_NEWER
        logEvent,
#endif
        pushTokenChanged;
    private TestState[] AllStates {
        get {
            return new TestState[] {
                this.began,
                this.reward,
                this.deepLink,
                this.launchedFromNotification,
                this.foregroundNotification,
#if TEAK_2_2_OR_NEWER
                this.logEvent,
#endif
                this.pushTokenChanged
            };
        }
    }

    // Teak Events
#if !TEAK_NOT_AVAILABLE
    public Action<TeakReward, Action<TestState>> OnReward { get; set; }
    public Action<Dictionary<string, object>, Action<TestState>> OnDeepLink { get; set; }
    public Action<TeakNotification, Action<TestState>> OnLaunchedFromNotification { get; set; }
    public Action<TeakNotification, Action<TestState>> OnForegroundNotification { get; set; }

#   if TEAK_2_2_OR_NEWER
    public Action<TeakLogEvent, Action<TestState>> OnLogEvent { get; set; }
#   endif
#endif

    // Test Lifecycle
    public Action<Action<TestState>> OnBegin { get; set; }
    public Action<Action<TestState>> OnComplete { get; set; }
    public Action OnResult { get; set; }
    public Action<string, Action<TestState>> OnPushTokenChanged { get; set; }

    /////
    // Test lifecycle
    private void ResetTest() {
        this.Status = TestState.Pending;

        this.began = TestState.Pending;
#if !TEAK_NOT_AVAILABLE
        this.reward = (this.OnReward == null ? TestState.Passed : TestState.Pending);
        this.deepLink = (this.OnDeepLink == null ? TestState.Passed : TestState.Pending);
        this.launchedFromNotification = (this.OnLaunchedFromNotification == null ? TestState.Passed : TestState.Pending);

#if UNITY_WEBGL
        this.foregroundNotification = TestState.Passed;

#   if TEAK_2_2_OR_NEWER
        this.logEvent = TestState.Passed;
#   endif
#else
        this.foregroundNotification = (this.OnForegroundNotification == null ? TestState.Passed : TestState.Pending);

#   if TEAK_2_2_OR_NEWER
        this.logEvent = (this.OnLogEvent == null ? TestState.Passed : TestState.Pending);
#   endif // TEAK_2_2_OR_NEWER
#endif // UNITY_WEBGL
#endif // !TEAK_NOT_AVAILABLE
        this.pushTokenChanged = (this.OnPushTokenChanged == null ? TestState.Passed : TestState.Pending);
    }

    public void Begin() {
        this.ResetTest();
        this.Status = TestState.Running;
        this.EvaluatePredicate(this.OnBegin, (TestState state) => {
            this.began = state;
        });
    }

    /////
    // Helpers
    private void EvaluatePredicate(Action<Action<TestState>> predicate, Action<TestState> state) {
        if (predicate != null) {
            predicate((TestState newState) => {
                state(newState);
                this.ProcessState();
            });
        } else {
            state(TestState.Passed);
            this.ProcessState();
        }
    }

    private void EvaluatePredicate<T>(Action<T, Action<TestState>> predicate, T val, Action<TestState> state) {
        if (predicate != null) {
            predicate(val, (TestState newState) => {
                state(newState);
                this.ProcessState();
            });
        } else {
            state(TestState.Passed);
            this.ProcessState();
        }
    }

    private void ProcessState() {
        bool allRun = this.AllStates.All(state => state != TestState.Pending);
        if (allRun) {
            TestState onCompleteState = TestState.Passed;
            if (this.OnComplete != null) {
                this.OnComplete((TestState state) => {
                    onCompleteState = state;
                });
            }

            bool allPassed = this.AllStates.All(state => state == TestState.Passed) &&
                (onCompleteState == TestState.Passed);

            this.Status = allPassed ? TestState.Passed : TestState.Failed;

            if (this.OnResult != null) {
                this.OnResult();
            }
        }
    }

    /////
    // Teak hooks
#if !TEAK_NOT_AVAILABLE
    public void DeepLink(Dictionary<string, object> parameters) {
        this.EvaluatePredicate(this.OnDeepLink, parameters, (TestState state) => {
            this.deepLink = state;
        });
    }

    public void Reward(TeakReward reward) {
        this.EvaluatePredicate(this.OnReward, reward, (TestState state) => {
            this.reward = state;
        });
    }

    public void LaunchedFromNotification(TeakNotification notification) {
        this.EvaluatePredicate(this.OnLaunchedFromNotification, notification, (TestState state) => {
            this.launchedFromNotification = state;
        });
    }

    public void ForegroundNotification(TeakNotification notification) {
        this.EvaluatePredicate(this.OnForegroundNotification, notification, (TestState state) => {
            this.foregroundNotification = state;
        });
    }
#if TEAK_2_2_OR_NEWER
    public void LogEvent(TeakLogEvent logEvent) {
        this.EvaluatePredicate(this.OnLogEvent, logEvent, (TestState state) => {
            // Only assign when it changes from Pending to not pending
            this.logEvent = this.logEvent == TestState.Pending ? state : this.logEvent;
        });
    }
#endif
#endif

    public void PushTokenChanged(string pushToken) {
        this.EvaluatePredicate(this.OnPushTokenChanged, pushToken, (TestState state) => {
            this.pushTokenChanged = state;
        });
    }
}
