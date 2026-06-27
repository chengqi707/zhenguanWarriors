using System;
using ZhenguanWarriors.Utils;

namespace ZhenguanWarriors.Core.Ads
{
    /// <summary>
    /// 广告 Stub：无 SDK 或编辑器环境下直接回调成功，保证开发和无网环境不卡流程
    /// </summary>
    public class StubAdProvider : IAdProvider
    {
        public void Initialize(Action<string> onComplete)
        {
            GameLogger.LogInfo(LogCategory.Ad, "广告 Stub 初始化完成");
            onComplete?.Invoke(string.Empty);
        }

        public bool IsReady(string placementId) => true;

        public void ShowRewardedAd(string placementId, Action onRewarded, Action onFailedOrSkipped)
        {
            GameLogger.LogInfoFormat(LogCategory.Ad, "Stub 播放广告|placement={0}", placementId);
            onRewarded?.Invoke();
        }
    }
}
