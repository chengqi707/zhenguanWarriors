using System;

namespace ZhenguanWarriors.Utils
{
    /// <summary>
    /// 日志分类，用于按模块过滤
    /// </summary>
    [Flags]
    public enum LogCategory
    {
        System = 1 << 0,    // 系统初始化、组件缺失、空引用等
        Battle = 1 << 1,    // 战斗流程、移动、攻击、胜负
        AI = 1 << 2,        // AI 决策、寻路、撤退
        Save = 1 << 3,      // 存档、读档、版本迁移
        Audio = 1 << 4,     // 音频播放、资源缺失
        UI = 1 << 5,        // UI 交互、缩放、页面切换
        Level = 1 << 6,     // 关卡加载、JSON 导出
        Network = 1 << 7,   // 网络请求（预留）

        All = System | Battle | AI | Save | Audio | UI | Level | Network
    }
}
