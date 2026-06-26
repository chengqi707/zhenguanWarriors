using UnityEngine;

namespace ZhenguanWarriors.Core.Audio
{
    /// <summary>
    /// 音频管理器——BGM/SFX播放框架
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private bool _bgmEnabled = true;
        private bool _sfxEnabled = true;

        // ===== 音频资源路径（占位） =====
        // 正式资源放在 Assets/Resources/Audio/BGM/ 和 Assets/Resources/Audio/SFX/ 下
        public static class BgmClips
        {
            public const string Title = "Audio/BGM/title";          // 主菜单
            public const string Battle = "Audio/BGM/battle";        // 战斗
            public const string Boss = "Audio/BGM/boss";            // Boss战
            public const string Victory = "Audio/BGM/victory";      // 胜利
            public const string Defeat = "Audio/BGM/defeat";        // 失败
            public const string Story = "Audio/BGM/story";          // 剧情
        }

        public static class SfxClips
        {
            public const string Attack = "Audio/SFX/attack";        // 攻击
            public const string Hit = "Audio/SFX/hit";              // 受击
            public const string Crit = "Audio/SFX/crit";            // 暴击
            public const string Skill = "Audio/SFX/skill";          // 计策
            public const string Heal = "Audio/SFX/heal";            // 医疗
            public const string LevelUp = "Audio/SFX/levelup";      // 升级
            public const string Click = "Audio/SFX/click";          // 点击
            public const string Death = "Audio/SFX/death";          // 阵亡
            public const string Duel = "Audio/SFX/duel";            // 单挑
        }

        void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.volume = 0.5f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.volume = 0.7f;

            Debug.Log("[音频] 音频管理器初始化完成（占位模式）");
        }

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("AudioManager");
                    _instance = go.AddComponent<AudioManager>();
                }
                return _instance;
            }
        }

        // ===== BGM =====

        public void PlayBGM(string clipPath)
        {
            if (!_bgmEnabled) return;
            var clip = Resources.Load<AudioClip>(clipPath);
            if (clip != null)
            {
                if (_bgmSource.clip != clip)
                {
                    _bgmSource.clip = clip;
                    _bgmSource.Play();
                }
                else if (!_bgmSource.isPlaying)
                {
                    _bgmSource.Play();
                }
            }
            else
            {
                Debug.LogWarning($"[音频] BGM资源缺失: {clipPath}");
            }
        }

        public void StopBGM()
        {
            _bgmSource.Stop();
        }

        public void SetBGMEnabled(bool enabled)
        {
            _bgmEnabled = enabled;
            if (!enabled) _bgmSource.Stop();
        }

        // ===== SFX =====

        public void PlaySFX(string clipPath)
        {
            if (!_sfxEnabled) return;
            var clip = Resources.Load<AudioClip>(clipPath);
            if (clip != null)
            {
                _sfxSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning($"[音频] SFX资源缺失: {clipPath}");
            }
        }

        public void SetSFXEnabled(bool enabled)
        {
            _sfxEnabled = enabled;
        }

        // ===== 快捷方法 =====

        public static void PlayBgm(string clipPath) => Instance.PlayBGM(clipPath);
        public static void PlaySfx(string clipPath) => Instance.PlaySFX(clipPath);
        public static void StopBgm() => Instance.StopBGM();
    }
}
