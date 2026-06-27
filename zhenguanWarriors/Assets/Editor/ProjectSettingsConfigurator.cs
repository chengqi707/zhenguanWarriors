using UnityEditor;
using UnityEngine;

namespace ZhenguanWarriors.Editor
{
    /// <summary>
    /// 自动校正 Android 发布所需的 PlayerSettings。
    /// 在 Unity Editor 启动时执行一次，确保 IL2CPP + ARM64、包名、SDK 版本等关键配置正确。
    /// </summary>
    public static class ProjectSettingsConfigurator
    {
        private const string AndroidBundleId = "com.chengqi.zhenguanwarriors";
        private const AndroidSdkVersions MinSdk = AndroidSdkVersions.AndroidApiLevel24;
        private const AndroidSdkVersions TargetSdk = AndroidSdkVersions.AndroidApiLevel33;

        [InitializeOnLoadMethod]
        private static void ApplyOnEditorLoad()
        {
            // 延迟一帧，避免 Editor 启动时设置 API 不可用
            EditorApplication.delayCall += ApplyAndroidSettings;
        }

        [MenuItem("贞观勇士/校正 Android 构建设置")]
        public static void ApplyAndroidSettings()
        {
            bool changed = false;

            // 包名
            string currentId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            if (currentId != AndroidBundleId)
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidBundleId);
                Debug.Log($"[构建设置] 包名已校正: {AndroidBundleId}");
                changed = true;
            }

            // 脚本后端：IL2CPP
            var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android);
            if (backend != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                Debug.Log("[构建设置] Scripting Backend 已切换为 IL2CPP");
                changed = true;
            }

            // 目标架构：ARM64
            var arch = PlayerSettings.Android.targetArchitectures;
            if (arch != AndroidArchitecture.ARM64)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                Debug.Log("[构建设置] Android 目标架构已设为 ARM64");
                changed = true;
            }

            // SDK 版本
            if (PlayerSettings.Android.minSdkVersion != MinSdk)
            {
                PlayerSettings.Android.minSdkVersion = MinSdk;
                Debug.Log($"[构建设置] minSdkVersion 已设为 {MinSdk}");
                changed = true;
            }
            if (PlayerSettings.Android.targetSdkVersion != TargetSdk)
            {
                PlayerSettings.Android.targetSdkVersion = TargetSdk;
                Debug.Log($"[构建设置] targetSdkVersion 已设为 {TargetSdk}");
                changed = true;
            }

            // 禁用 Unity 启动屏（若许可证允许）与 Logo
            if (PlayerSettings.SplashScreen.showUnityLogo)
            {
                PlayerSettings.SplashScreen.showUnityLogo = false;
                Debug.Log("[构建设置] Unity Splash Logo 已禁用");
                changed = true;
            }
            if (PlayerSettings.SplashScreen.show)
            {
                PlayerSettings.SplashScreen.show = false;
                Debug.Log("[构建设置] Unity Splash Screen 已禁用（如许可证不允许则会被忽略）");
                changed = true;
            }

            // 方向锁定为横屏
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                Debug.Log("[构建设置] 默认方向已锁定为横屏");
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[构建设置] Android 构建设置已保存");
            }
            else
            {
                Debug.Log("[构建设置] Android 构建设置无需更改");
            }
        }
    }
}
