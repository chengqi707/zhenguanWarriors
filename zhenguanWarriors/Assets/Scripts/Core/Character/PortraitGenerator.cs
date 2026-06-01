using System.Collections.Generic;
using UnityEngine;
using ZhenguanWarriors.Core.UI;

namespace ZhenguanWarriors.Core.Character
{
    /// <summary>
    /// 角色立绘占位生成器——用角色缩写+兵种色生成头像Texture2D
    /// 后续可替换为AI生成的正式立绘
    /// </summary>
    public static class PortraitGenerator
    {
        private static Dictionary<string, Texture2D> _cache = new();

        /// <summary>获取角色头像（带缓存）</summary>
        public static Texture2D GetPortrait(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return GenerateDefault();
            if (_cache.TryGetValue(charId, out var cached)) return cached;

            var charData = CharacterDatabase.Get(charId);
            if (charData == null) return GenerateDefault();

            Texture2D tex = GeneratePortrait(charData.Name, GetClassColor(charData.UnitClass));
            _cache[charId] = tex;
            return tex;
        }

        /// <summary>直接生成头像（根据名字和颜色）</summary>
        public static Texture2D GeneratePortrait(string name, Color bgColor)
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            float cx = size / 2f, cy = size / 2f, r = size / 2f - 2;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // 圆形裁剪
                    if (dist <= r)
                    {
                        // 边缘渐变
                        float edge = Mathf.Clamp01((r - dist) / 3f);
                        pixels[y * size + x] = Color.Lerp(Color.black, bgColor, 0.7f + 0.3f * edge);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D GenerateDefault()
        {
            return GeneratePortrait("??", Color.gray);
        }

        private static Color GetClassColor(ClassType cls) => cls switch
        {
            ClassType.Infantry => Theme.ClassInfantry,
            ClassType.HeavyInfantry => Theme.ClassHeavyInf,
            ClassType.Cavalry => Theme.ClassCavalry,
            ClassType.Archer => Theme.ClassArcher,
            ClassType.Siege => Theme.ClassSiege,
            ClassType.Strategist => Theme.ClassStrategist,
            _ => Color.gray
        };

        /// <summary>获取头像上显示的文字（名字前两个字）</summary>
        public static string GetPortraitText(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            if (name.Length >= 2) return name.Substring(0, 2);
            return name;
        }

        /// <summary>清空头像缓存</summary>
        public static void ClearCache()
        {
            foreach (var tex in _cache.Values)
                Object.Destroy(tex);
            _cache.Clear();
        }
    }
}
