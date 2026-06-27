#if UNITY_ADS
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Advertisement;
using ZhenguanWarriors.Utils;

namespace ZhenguanWarriors.Core.Ads
{
    /// <summary>
    /// Unity Ads 激励视频实现（仅在安装 com.unity.ads 包且定义 UNITY_ADS 时编译）
    /// </summary>
    public class UnityAdsProvider : IAdProvider,
        IUnityAdsInitializationListener,
        IUnityAdsLoadListener,
        IUnityAdsShowListener
    {
        // 实际发布前需要在 Unity Project Settings / Services 中配置游戏 ID
        private const string ANDROID_GAME_ID = "";
        private const string IOS_GAME_ID = "";
        private const bool TEST_MODE = true;

        private readonly HashSet<string> _loadedPlacements = new();
        private Action<string> _onInitComplete;
        private Action _onRewarded;
        private Action _onFailedOrSkipped;

        public void Initialize(Action<string> onComplete)
        {
            _onInitComplete = onComplete;

#if UNITY_ANDROID
            string gameId = ANDROID_GAME_ID;
#elif UNITY_IOS
            string gameId = IOS_GAME_ID;
#else
            string gameId = "";
#endif
            if (string.IsNullOrEmpty(gameId))
            {
                GameLogger.LogWarning(LogCategory.Ad, "UnityAds 未配置游戏 ID，将回退到 Stub");
                onComplete?.Invoke("未配置游戏 ID");
                return;
            }

            try
            {
                Advertisement.Initialize(gameId, TEST_MODE, this);
            }
            catch (Exception e)
            {
                GameLogger.LogErrorFormat(LogCategory.Ad, "UnityAds 初始化异常|原因={0}", e.Message);
                onComplete?.Invoke(e.Message);
            }
        }

        public bool IsReady(string placementId)
        {
            return _loadedPlacements.Contains(placementId);
        }

        public void ShowRewardedAd(string placementId, Action onRewarded, Action onFailedOrSkipped)
        {
            _onRewarded = onRewarded;
            _onFailedOrSkipped = onFailedOrSkipped;

            if (!_loadedPlacements.Contains(placementId))
            {
                GameLogger.LogWarningFormat(LogCategory.Ad, "UnityAds 广告未加载|placement={0}", placementId);
                onFailedOrSkipped?.Invoke();
                return;
            }

            try
            {
                Advertisement.Show(placementId, this);
            }
            catch (Exception e)
            {
                GameLogger.LogErrorFormat(LogCategory.Ad, "UnityAds 播放异常|placement={0}|原因={1}", placementId, e.Message);
                onFailedOrSkipped?.Invoke();
            }
        }

        private void LoadAllPlacements()
        {
            foreach (AdPlacementType type in Enum.GetValues(typeof(AdPlacementType)))
            {
                string id = type.ToPlacementId();
                _loadedPlacements.Remove(id);
                try { Advertisement.Load(id, this); }
                catch (Exception e)
                {
                    GameLogger.LogErrorFormat(LogCategory.Ad, "UnityAds 加载异常|placement={0}|原因={1}", id, e.Message);
                }
            }
        }

        // ----- IUnityAdsInitializationListener -----
        public void OnInitializationComplete()
        {
            GameLogger.LogInfo(LogCategory.Ad, "UnityAds 初始化完成");
            _onInitComplete?.Invoke(string.Empty);
            LoadAllPlacements();
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            GameLogger.LogErrorFormat(LogCategory.Ad, "UnityAds 初始化失败|error={0}|message={1}", error, message);
            _onInitComplete?.Invoke(message);
        }

        // ----- IUnityAdsLoadListener -----
        public void OnUnityAdsAdLoaded(string placementId)
        {
            GameLogger.LogInfoFormat(LogCategory.Ad, "UnityAds 广告加载成功|placement={0}", placementId);
            _loadedPlacements.Add(placementId);
        }

        public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
        {
            GameLogger.LogWarningFormat(LogCategory.Ad, "UnityAds 广告加载失败|placement={0}|原因={1}", placementId, message);
            _loadedPlacements.Remove(placementId);
        }

        // ----- IUnityAdsShowListener -----
        public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
        {
            GameLogger.LogWarningFormat(LogCategory.Ad, "UnityAds 播放失败|placement={0}|原因={1}", placementId, message);
            _onFailedOrSkipped?.Invoke();
        }

        public void OnUnityAdsShowStart(string placementId) { }
        public void OnUnityAdsShowClick(string placementId) { }

        public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
        {
            if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
            {
                GameLogger.LogInfoFormat(LogCategory.Ad, "UnityAds 播放完成|placement={0}", placementId);
                _onRewarded?.Invoke();
            }
            else
            {
                GameLogger.LogInfoFormat(LogCategory.Ad, "UnityAds 未完整观看|placement={0}|state={1}", placementId, showCompletionState);
                _onFailedOrSkipped?.Invoke();
            }
        }
    }
}
#endif
