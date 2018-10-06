using MiniJSON.Teak;

using System.Collections;
using System.Collections.Generic;

class Test {
    public string Name { get; set; }
    public string CreativeId { get; set; }
    public string VerifyDeepLink { get; set; }
    public string VerifyReward { get; set; }
    public int Status { get; set; }
    public bool NoAutoBackground { get; set; }
    public string ErrorText { get; set; }

    bool onDeepLinkCalled = false;
    bool onRewardCalled = false;
    bool onLaunchCalled = false;

    void Prepare() {
        if (string.IsNullOrEmpty(this.VerifyReward)) onRewardCalled = true;
        if (string.IsNullOrEmpty(this.VerifyDeepLink)) onDeepLinkCalled = true;
    }

    bool CheckStatus() {
        if (this.onLaunchCalled && this.onDeepLinkCalled && this.onRewardCalled) {
            if (this.Status == 0) this.Status = 1;
            return true;
        }
        return false;
    }

    public bool OnDeepLink(Dictionary<string, object> parameters) {
        if (!string.IsNullOrEmpty(this.VerifyDeepLink) &&
            (!parameters.ContainsKey("data") ||
            !this.VerifyDeepLink.Equals(parameters["data"] as string, System.StringComparison.Ordinal))) {
#if !TEAK_NOT_AVAILABLE
            this.ErrorText = "Expected '" + this.VerifyDeepLink + "' contents:\n" + Json.Serialize(parameters);
#endif
            this.Status = 2;
        }

        Prepare();
        this.onDeepLinkCalled = true;
        return CheckStatus();
    }

#if !TEAK_NOT_AVAILABLE
    public bool OnReward(TeakReward reward) {
        if (!this.CreativeId.Equals(reward.CreativeId, System.StringComparison.Ordinal)) {
            this.ErrorText = "Expected '" + this.CreativeId + "' contents: " + reward.CreativeId;
            this.Status = 2;
        }

        if (string.IsNullOrEmpty(reward.RewardId)) {
            this.ErrorText = "RewardId was null or empty";
            this.Status = 2;
        }

        Prepare();
        this.onRewardCalled = true;
        return CheckStatus();
    }

    public bool OnLaunchedFromNotification(TeakNotification notification) {
        if (!this.CreativeId.Equals(notification.CreativeId, System.StringComparison.Ordinal)) {
            this.ErrorText = "Expected '" + this.CreativeId + "' got:\n" + Json.Serialize(notification.CreativeId);
            this.Status = 2;
        }
        else if (!string.IsNullOrEmpty(this.VerifyReward) && !notification.Incentivized) {
            this.ErrorText = "Expected 'incentivized'";
            this.Status = 2;
        }

        Prepare();
        this.onLaunchCalled = true;
        return CheckStatus();
    }
#endif
}
