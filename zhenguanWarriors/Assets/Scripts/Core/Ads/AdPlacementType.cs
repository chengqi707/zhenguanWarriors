namespace ZhenguanWarriors.Core.Ads
{
    /// <summary>
    /// 激励视频点位类型
    /// </summary>
    public enum AdPlacementType
    {
        /// <summary>战后结算页双倍奖励</summary>
        DoubleRewards,

        /// <summary>战败后复活再战</summary>
        DefeatRevive,

        /// <summary>主菜单每日额外奖励</summary>
        DailyExtraReward
    }

    /// <summary>
    /// 点位扩展方法
    /// </summary>
    public static class AdPlacementTypeExtensions
    {
        /// <summary>获取 Unity Ads 等 SDK 使用的 placementId</summary>
        public static string ToPlacementId(this AdPlacementType type)
        {
            return type switch
            {
                AdPlacementType.DoubleRewards => "Rewarded_DoubleRewards",
                AdPlacementType.DefeatRevive => "Rewarded_DefeatRevive",
                AdPlacementType.DailyExtraReward => "Rewarded_DailyExtraReward",
                _ => "Rewarded_Default"
            };
        }
    }
}
