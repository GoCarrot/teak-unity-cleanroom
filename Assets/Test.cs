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
        Failed
    }

    public string Name { get; set; }
    public TestState Status { get; set; }

    // State, this could probably be done better
    private TestState began, reward, deepLink, launchedFromNotification, foregroundNotification,
        logEvent;
    private TestState[] AllStates {
        get {
            return new TestState[] {
                this.began,
                this.reward,
                this.deepLink,
                this.launchedFromNotification,
                this.foregroundNotification,
                this.logEvent
            };
        }
    }

#if !TEAK_NOT_AVAILABLE

    // Teak Events
    public Action<Test, TeakReward, Action<bool>> OnReward { get; set; }
    public Action<Test, Dictionary<string, object>, Action<bool>> OnDeepLink { get; set; }
    public Action<Test, TeakNotification, Action<bool>> OnLaunchedFromNotification { get; set; }
    public Action<Test, TeakNotification, Action<bool>> OnForegroundNotification { get; set; }
    public Action<Test, TeakLogEvent, Action<bool>> OnLogEvent { get; set; }

    // Test Lifecycle
    public Action<Test, Action<bool>> OnBegin { get; set; }
    public Action<Test> OnComplete { get; set; }

    /////
    // Test lifecycle
    private void ResetTest() {
        this.Status = TestState.Pending;

        this.began = TestState.Pending;
        this.reward = (this.OnReward == null ? TestState.Passed : TestState.Pending);
        this.deepLink = (this.OnDeepLink == null ? TestState.Passed : TestState.Pending);
        this.launchedFromNotification = (this.OnLaunchedFromNotification == null ? TestState.Passed : TestState.Pending);
        this.foregroundNotification = (this.OnForegroundNotification == null ? TestState.Passed : TestState.Pending);
        this.logEvent = (this.OnLogEvent == null ? TestState.Passed : TestState.Pending);
    }

    public void Begin() {
        this.ResetTest();
        this.EvaluatePredicate(this.OnBegin, (TestState state) => {
            this.began = state;
        });
    }

    private void Complete() {
        if (this.OnComplete != null) {
            this.OnComplete(this);
        }
    }

    /////
    // Helpers
    private void EvaluatePredicate(Action<Test, Action<bool>> predicate, Action<TestState> state) {
        if (predicate != null) {
            predicate(this, (bool passed) => {
                state(passed ? TestState.Passed : TestState.Failed);
                this.ProcessState();
            });
        } else {
            state(TestState.Passed);
            this.ProcessState();
        }
    }

    private void EvaluatePredicate<T>(Action<Test, T, Action<bool>> predicate, T val, Action<TestState> state) {
        if (predicate != null) {
            predicate(this, val, (bool passed) => {
                state(passed ? TestState.Passed : TestState.Failed);
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
            bool allPassed = this.AllStates.All(state => state == TestState.Passed);
            this.Status = allPassed ? TestState.Passed : TestState.Failed;
            this.Complete();
        }
    }

    /////
    // Teak hooks
    public void Reward(TeakReward reward) {
        this.EvaluatePredicate(this.OnReward, reward, (TestState state) => {
            this.reward = state;
        });
    }

    public void DeepLink(Dictionary<string, object> parameters) {
        this.EvaluatePredicate(this.OnDeepLink, parameters, (TestState state) => {
            this.deepLink = state;
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

    public void LogEvent(TeakLogEvent logEvent) {
        this.EvaluatePredicate(this.OnLogEvent, logEvent, (TestState state) => {
            this.logEvent = state;
        });
    }
#endif
}
