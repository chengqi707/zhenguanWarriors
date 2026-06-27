using System;

namespace ZhenguanWarriors.Core.Ads
{
    /// <summary>
    /// 广告提供方抽象接口——便于在 Unity Ads / TapTap / 微信广告之间切换
    /// </summary>
    public interface IAdProvider
    {
        /// <summary>初始化广告 SDK</summary>
        /// <param name="onComplete">初始化完成回调，error 为空表示成功</param>
        void Initialize(Action<string> onComplete);

        /// <summary>指定点位是否已准备好可播放</summary>
        bool IsReady(string placementId);

        /// <summary>播放激励视频</summary>
        /// <param name="placementId">点位 ID</param>
        /// <param name="onRewarded">玩家完整观看，应发放奖励</param>
        /// <param name="onFailedOrSkipped">失败、跳过或关闭，不发放奖励</param>
        void ShowRewardedAd(string placementId, Action onRewarded, Action onFailedOrSkipped);
    }
}
