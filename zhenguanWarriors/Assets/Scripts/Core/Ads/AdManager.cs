using System;
using UnityEngine;
using ZhenguanWarriors.Core.Save;
using ZhenguanWarriors.Utils;
using ZhenguanWarriors.View.BattleView;

namespace ZhenguanWarriors.Core.Ads
{
    /// <summary>
    /// 广告管理器单例：统一管理广告 provider、频次控制与统一播放入口
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        [Header("频次控制")]
        [SerializeField] private float _adCooldownSeconds = 5f;
        [SerializeField] private int _dailyLimit = 1;
        [SerializeField] private int _dailyRewardGold = 500;

        public int DailyRewardGold => _dailyRewardGold;

        private IAdProvider _provider;
        private bool _isInitializing;
        private DateTime _lastAdTime = DateTime.MinValue;

        // 每局可用性（由 BattleTestController 在战斗开始时调用 ResetBattleAvailability 重置）
        public bool DoubleRewardAvailableThisBattle { get; private set; } = true;
        public bool DefeatReviveAvailableThisBattle { get; private set; } = true;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            SelectAndInitProvider();
        }

        /// <summary>选择并初始化广告 provider</summary>
        private void SelectAndInitProvider()
        {
            if (_provider != null || _isInitializing) return;
            _isInitializing = true;

#if UNITY_ADS
            // 编辑器内仍优先使用 Stub，避免误触发真实广告
            if (Application.isEditor)
            {
                _provider = new StubAdProvider();
            }
            else
            {
                var unityProvider = new UnityAdsProvider();
                unityProvider.Initialize(error =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        GameLogger.LogWarningFormat(LogCategory.Ad, "UnityAds 初始化失败，回退 Stub|原因={0}", error);
                        _provider = new StubAdProvider();
                    }
                    else
                    {
                        _provider = unityProvider;
                    }
                    _isInitializing = false;
                });
                return;
            }
#else
            _provider = new StubAdProvider();
#endif
            _provider.Initialize(_ => _isInitializing = false);
        }

        /// <summary>新战斗开始时调用，重置每局限次</summary>
        public void ResetBattleAvailability()
        {
            DoubleRewardAvailableThisBattle = true;
            DefeatReviveAvailableThisBattle = true;
        }

        /// <summary>指定点位在当前状态下是否可播</summary>
        public bool IsReady(AdPlacementType placement)
        {
            if (_provider == null) return false;
            if (!IsCooldownReady()) return false;

            return placement switch
            {
                AdPlacementType.DailyExtraReward => IsDailyRewardReadyInternal(),
                _ => _provider.IsReady(placement.ToPlacementId())
            };
        }

        /// <summary>战后双倍奖励是否可观看</summary>
        public bool CanShowDoubleRewards()
        {
            return DoubleRewardAvailableThisBattle && IsReady(AdPlacementType.DoubleRewards);
        }

        /// <summary>战败复活是否可观看</summary>
        public bool CanShowDefeatRevive()
        {
            return DefeatReviveAvailableThisBattle && IsReady(AdPlacementType.DefeatRevive);
        }

        /// <summary>每日额外奖励是否可观看</summary>
        public bool CanShowDailyExtraReward()
        {
            return IsReady(AdPlacementType.DailyExtraReward);
        }

        /// <summary>统一播放入口（战斗中禁止调用）</summary>
        public void ShowAd(AdPlacementType placement, Action onRewarded, Action onFailedOrSkipped = null)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentPage == GamePage.Battle)
            {
                GameLogger.LogWarning(LogCategory.Ad, "战斗中禁止播放广告");
                onFailedOrSkipped?.Invoke();
                return;
            }

            // 未初始化完成时先尝试初始化
            if (_provider == null)
            {
                if (!_isInitializing)
                    SelectAndInitProvider();

                // 使用 Stub 兜底，确保不卡流程
                GameLogger.LogWarning(LogCategory.Ad, "广告 provider 未就绪，使用 Stub 兜底");
                new StubAdProvider().ShowRewardedAd(placement.ToPlacementId(),
                    () => OnRewardGranted(placement, onRewarded),
                    onFailedOrSkipped);
                return;
            }

            string placementId = placement.ToPlacementId();

            if (!_provider.IsReady(placementId))
            {
                GameLogger.LogWarningFormat(LogCategory.Ad, "广告未就绪，使用 Stub 兜底|placement={0}", placementId);
                new StubAdProvider().ShowRewardedAd(placementId,
                    () => OnRewardGranted(placement, onRewarded),
                    onFailedOrSkipped);
                return;
            }

            _provider.ShowRewardedAd(placementId,
                () => OnRewardGranted(placement, onRewarded),
                () => OnAdFailedOrSkipped(placement, onFailedOrSkipped));
        }

        /// <summary>发放每日奖励并持久化</summary>
        public void GrantDailyReward()
        {
            var save = GameState.CurrentSave;
            if (save == null) return;

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (save.lastDailyAdDate != today)
            {
                save.lastDailyAdDate = today;
                save.dailyAdWatchCount = 0;
            }
            save.dailyAdWatchCount++;
            save.gold += _dailyRewardGold;
            SaveManager.AutoSave(save);
        }

        /// <summary>获取今日剩余可观看次数</summary>
        public int GetDailyRemainingCount()
        {
            var save = GameState.CurrentSave;
            if (save == null) return 0;
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (save.lastDailyAdDate != today)
                return _dailyLimit;
            return Mathf.Max(0, _dailyLimit - save.dailyAdWatchCount);
        }

        private bool IsCooldownReady()
        {
            if (_lastAdTime == DateTime.MinValue) return true;
            return (DateTime.Now - _lastAdTime).TotalSeconds >= _adCooldownSeconds;
        }

        private bool IsDailyRewardReadyInternal()
        {
            return GetDailyRemainingCount() > 0;
        }

        private void OnRewardGranted(AdPlacementType placement, Action userCallback)
        {
            _lastAdTime = DateTime.Now;

            switch (placement)
            {
                case AdPlacementType.DoubleRewards:
                    DoubleRewardAvailableThisBattle = false;
                    break;
                case AdPlacementType.DefeatRevive:
                    DefeatReviveAvailableThisBattle = false;
                    break;
                case AdPlacementType.DailyExtraReward:
                    GrantDailyReward();
                    break;
            }

            GameLogger.LogInfoFormat(LogCategory.Ad, "广告奖励发放|placement={0}", placement);
            userCallback?.Invoke();
        }

        private void OnAdFailedOrSkipped(AdPlacementType placement, Action userCallback)
        {
            GameLogger.LogWarningFormat(LogCategory.Ad, "广告失败或跳过|placement={0}", placement);
            userCallback?.Invoke();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
