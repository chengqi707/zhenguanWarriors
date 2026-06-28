using UnityEditor;
using UnityEngine;

namespace ZhenguanWarriors.Editor
{
    /// <summary>
    /// 音频资源导入后处理：Android 平台自动使用 Vorbis 压缩，降低 APK 体积
    /// </summary>
    public class AudioImportPostprocessor : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/"))
                return;

            var importer = assetImporter as AudioImporter;
            if (importer == null) return;

            // 默认强制单声道对 BGM 不适用，仅对 SFX 启用单声道
            bool isSfx = assetPath.Contains("/SFX/");

            var defaultSettings = importer.defaultSampleSettings;
            defaultSettings.loadType = isSfx ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
            defaultSettings.compressionFormat = AudioCompressionFormat.Vorbis;
            defaultSettings.quality = isSfx ? 0.5f : 0.7f;
            defaultSettings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            defaultSettings.preloadAudioData = isSfx;
            importer.defaultSampleSettings = defaultSettings;

            // Android 覆盖
            var androidSettings = importer.GetOverrideSampleSettings("Android");
            androidSettings.loadType = isSfx ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
            androidSettings.compressionFormat = AudioCompressionFormat.Vorbis;
            androidSettings.quality = isSfx ? 0.5f : 0.7f;
            androidSettings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            androidSettings.preloadAudioData = isSfx;
            importer.SetOverrideSampleSettings("Android", androidSettings);

            importer.forceToMono = isSfx;

            Debug.Log($"[AudioImportPostprocessor] 已配置音频压缩|路径={assetPath}|平台=Android|格式=Vorbis|质量={(isSfx ? 0.5f : 0.7f)}");
        }
    }
}
