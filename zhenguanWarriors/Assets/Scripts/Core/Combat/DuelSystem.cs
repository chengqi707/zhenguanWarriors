using System;
using ZhenguanWarriors.Core.Battle;

namespace ZhenguanWarriors.Core.Combat
{
    /// <summary>
    /// 单挑回合行动
    /// </summary>
    public enum DuelAction
    {
        Attack,     // 攻击——普通输出
        Defend,     // 防御——减伤50%，下回合反击+20%
        Special     // 必杀——消耗1必杀槽，伤害×2但命中-20%
    }

    /// <summary>
    /// 单挑结果
    /// </summary>
    public enum DuelResult
    {
        Win,        // 胜
        Lose,       // 败
        Draw        // 平
    }

    /// <summary>
    /// 单挑系统——简化版3回合迷你对决
    /// </summary>
    public class DuelSystem
    {
        public BattleUnit Player { get; private set; }
        public BattleUnit Enemy { get; private set; }
        public int Round { get; private set; } = 0;
        public int MaxRounds { get; private set; } = 3;

        // 单挑中临时HP（不用真实HP）
        public int PlayerDuelHp { get; private set; }
        public int EnemyDuelHp { get; private set; }

        // 必杀槽
        public int PlayerSpecialGauge { get; private set; } = 1;
        public int EnemySpecialGauge { get; private set; } = 1;

        // 上一回合防御标记（下回合反击加成）
        public bool PlayerDefendedLast { get; private set; }
        public bool EnemyDefendedLast { get; private set; }

        public bool IsFinished => Round >= MaxRounds || PlayerDuelHp <= 0 || EnemyDuelHp <= 0;

        public event Action<string> OnDuelLog;

        public DuelSystem(BattleUnit player, BattleUnit enemy)
        {
            Player = player;
            Enemy = enemy;
            // 单挑HP = 真实HP上限的50%（模拟3-5回合对决）
            PlayerDuelHp = (int)(player.MaxHp * 0.5f);
            EnemyDuelHp = (int)(enemy.MaxHp * 0.5f);
        }

        /// <summary>
        /// 执行一回合单挑
        /// </summary>
        public void ExecuteRound(DuelAction playerAction, DuelAction enemyAction)
        {
            if (IsFinished) return;
            Round++;

            string log = $"===== 单挑第 {Round} 回合 =====\n";

            // 计算双方伤害（属性不能传ref，用局部变量中转）
            int playerGauge = PlayerSpecialGauge;
            int enemyGauge = EnemySpecialGauge;
            int playerDamage = CalcDuelDamage(Player, Enemy, playerAction, enemyAction,
                PlayerDefendedLast, ref playerGauge);
            int enemyDamage = CalcDuelDamage(Enemy, Player, enemyAction, playerAction,
                EnemyDefendedLast, ref enemyGauge);
            PlayerSpecialGauge = playerGauge;
            EnemySpecialGauge = enemyGauge;

            // 防御减伤
            if (enemyAction == DuelAction.Defend)
                playerDamage = (int)(playerDamage * 0.5f);
            if (playerAction == DuelAction.Defend)
                enemyDamage = (int)(enemyDamage * 0.5f);

            // 应用伤害
            EnemyDuelHp -= playerDamage;
            PlayerDuelHp -= enemyDamage;

            // 必杀槽积累
            if (enemyDamage > 0) PlayerSpecialGauge = Math.Min(3, PlayerSpecialGauge + 1);
            if (playerDamage > 0) EnemySpecialGauge = Math.Min(3, EnemySpecialGauge + 1);

            // 记录防御状态
            PlayerDefendedLast = playerAction == DuelAction.Defend;
            EnemyDefendedLast = enemyAction == DuelAction.Defend;

            log += $"{Player.Name} {ActionName(playerAction)} → {Enemy.Name} 受到 {playerDamage} 伤害\n";
            log += $"{Enemy.Name} {ActionName(enemyAction)} → {Player.Name} 受到 {enemyDamage} 伤害\n";
            log += $"单挑HP：{Player.Name} {PlayerDuelHp} / {Enemy.Name} {EnemyDuelHp}";

            OnDuelLog?.Invoke(log);
        }

        /// <summary>
        /// 判定最终结果
        /// </summary>
        public DuelResult GetResult()
        {
            if (PlayerDuelHp <= 0 && EnemyDuelHp > 0) return DuelResult.Lose;
            if (EnemyDuelHp <= 0 && PlayerDuelHp > 0) return DuelResult.Win;
            if (PlayerDuelHp <= 0 && EnemyDuelHp <= 0) return DuelResult.Draw;

            // 按剩余HP比例判定
            float playerRatio = (float)PlayerDuelHp / (Player.MaxHp * 0.5f);
            float enemyRatio = (float)EnemyDuelHp / (Enemy.MaxHp * 0.5f);

            if (playerRatio > enemyRatio + 0.15f) return DuelResult.Win;
            if (enemyRatio > playerRatio + 0.15f) return DuelResult.Lose;
            return DuelResult.Draw;
        }

        /// <summary>
        /// 应用单挑结果到真实单位
        /// </summary>
        public string ApplyResult()
        {
            var result = GetResult();
            return result switch
            {
                DuelResult.Win => ApplyWin(Player, Enemy),
                DuelResult.Lose => ApplyWin(Enemy, Player),
                DuelResult.Draw => ApplyDraw(),
                _ => "单挑结果异常"
            };
        }

        private string ApplyWin(BattleUnit winner, BattleUnit loser)
        {
            int damage = (int)(loser.MaxHp * 0.3f);
            loser.TakeDamage(damage);
            return $"{winner.Name} 单挑获胜！{loser.Name} 部队受到 {damage} 点伤害！";
        }

        private string ApplyDraw()
        {
            int pDmg = (int)(Player.MaxHp * 0.15f);
            int eDmg = (int)(Enemy.MaxHp * 0.15f);
            Player.TakeDamage(pDmg);
            Enemy.TakeDamage(eDmg);
            return $"单挑平局！双方各受 {pDmg}/{eDmg} 点伤害。";
        }

        // ========== 内部计算 ==========

        private int CalcDuelDamage(BattleUnit attacker, BattleUnit defender,
            DuelAction action, DuelAction defenderAction,
            bool defendedLast, ref int specialGauge)
        {
            // 基础伤害 = 武力差 + 随机
            int baseDmg = Math.Max(5, attacker.Strength - defender.Strength / 2);
            baseDmg += UnityEngine.Random.Range(-5, 6);

            // ★ 被动：单挑达人（伤害+20%）
            if (attacker.HasPassiveType("duel_win_pct"))
            {
                baseDmg = (int)(baseDmg * 1.2f);
            }

            switch (action)
            {
                case DuelAction.Attack:
                    if (defendedLast) baseDmg = (int)(baseDmg * 1.2f); // 防御后反击
                    break;

                case DuelAction.Special:
                    if (specialGauge >= 1)
                    {
                        specialGauge--;
                        baseDmg = (int)(baseDmg * 2f);
                        // 必杀命中-20%：模拟为50%概率伤害减半
                        if (UnityEngine.Random.Range(0, 100) < 20)
                        {
                            baseDmg /= 2;
                        }
                    }
                    else
                    {
                        baseDmg = (int)(baseDmg * 0.5f); // 没必杀槽硬放
                    }
                    break;

                case DuelAction.Defend:
                    baseDmg = 0; // 防御不造成伤害
                    break;
            }

            return Math.Max(0, baseDmg);
        }

        private string ActionName(DuelAction a) => a switch
        {
            DuelAction.Attack => "攻击",
            DuelAction.Defend => "防御",
            DuelAction.Special => "必杀",
            _ => "未知"
        };

        /// <summary>
        /// 是否可以发起单挑
        /// </summary>
        public static bool CanDuel(BattleUnit attacker, BattleUnit defender)
        {
            if (!attacker.IsAlive || !defender.IsAlive) return false;
            // 相邻
            if (attacker.Position.Distance(defender.Position) > 1) return false;
            // 武力差 ≤ 25
            if (Math.Abs(attacker.Strength - defender.Strength) > 25) return false;
            return true;
        }
    }
}
