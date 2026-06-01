using System;

namespace ZhenguanWarriors.Core.Battle
{
    /// <summary>
    /// 天气类型
    /// </summary>
    public enum WeatherType
    {
        Sunny,      // 晴——无影响
        Rain,       // 雨——火攻无效，移动+1，弓兵命中-10%
        Snow,       // 雪——移动+1，火攻-50%
        Fog,        // 雾——视野-2，远程命中-20%
        Windy,      // 大风——火攻沿风向扩散，弓兵命中±10%
    }

    /// <summary>
    /// 风向
    /// </summary>
    public enum WindDirection
    {
        None,   // 无风
        North,  // 北
        South,  // 南
        East,   // 东
        West,   // 西
    }

    /// <summary>
    /// 天气系统——管理全局天气与风向
    /// </summary>
    public class WeatherSystem
    {
        public WeatherType CurrentWeather { get; private set; } = WeatherType.Sunny;
        public WindDirection Wind { get; private set; } = WindDirection.None;

        // 事件
        public Action<WeatherType> OnWeatherChanged;
        public Action<WindDirection> OnWindChanged;

        public WeatherSystem(WeatherType initial = WeatherType.Sunny, WindDirection wind = WindDirection.None)
        {
            CurrentWeather = initial;
            Wind = wind;
        }

        public void SetWeather(WeatherType weather)
        {
            if (CurrentWeather != weather)
            {
                CurrentWeather = weather;
                OnWeatherChanged?.Invoke(weather);
            }
        }

        public void SetWind(WindDirection wind)
        {
            if (Wind != wind)
            {
                Wind = wind;
                OnWindChanged?.Invoke(wind);
            }
        }

        /// <summary>火攻是否有效</summary>
        public bool IsFireAttackEffective => CurrentWeather != WeatherType.Rain;

        /// <summary>火攻伤害倍率</summary>
        public float FireDamageMultiplier => CurrentWeather switch
        {
            WeatherType.Rain => 0f,      // 雨：完全无效
            WeatherType.Snow => 0.5f,    // 雪：减半
            WeatherType.Windy => 1.2f,   // 大风：增强
            _ => 1.0f
        };

        /// <summary>获取风向对应的六边形方向偏移</summary>
        public HexCoord GetWindOffset()
        {
            return Wind switch
            {
                WindDirection.North => new HexCoord(0, -1),
                WindDirection.South => new HexCoord(0, 1),
                WindDirection.East  => new HexCoord(1, 0),
                WindDirection.West  => new HexCoord(-1, 0),
                _ => new HexCoord(0, 0)
            };
        }

        /// <summary>弓兵命中修正</summary>
        public int ArcherHitModifier => CurrentWeather switch
        {
            WeatherType.Rain => -10,
            WeatherType.Fog  => -20,
            WeatherType.Windy => 0,   // 顺逆风单独计算
            _ => 0
        };

        /// <summary>全兵种移动力额外消耗（雨天）</summary>
        public int ExtraMoveCost => CurrentWeather switch
        {
            WeatherType.Rain => 1,
            WeatherType.Snow => 1,
            _ => 0
        };
    }
}
