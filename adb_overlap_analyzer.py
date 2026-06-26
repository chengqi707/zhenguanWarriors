#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ADB日志捕获与重叠分析工具

使用方法：
1. 确保手机已通过 adb 连接，APK 正在运行
2. 运行此脚本：
   python3 adb_overlap_analyzer.py
3. 脚本会自动启动 adb logcat 捕获 Unity 日志
4. 当检测到重叠时，会自动输出分析结果

或者按特定次数运行后保存日志：
   python3 adb_overlap_analyzer.py --save logs.txt --duration 60
"""

import subprocess
import sys
import re
import os
import time
import json
from datetime import datetime
from collections import defaultdict


class OverlapLogAnalyzer:
    """Unity 日志捕获与重叠分析器"""

    # 关键词过滤
    KEYWORDS = [
        "[重叠检测]",
        "[重叠]",
        "[MoveUnitAnimation]",
        "[AI-Decide]",
        "[PathFinder]",
        "[CheckMoveTowards]",
        "[CheckRetreat]",
        "[AttackUnit]",
        "[ExecuteAIAttack]",
        "[ResolveUnitOverlaps]",
        "[CreateEnemyUnits]",
        "[OnPhaseChanged]",
        "[StartBattle]",
        "[EnemyAI]",
        "⚠",
        "同一位置",
        "被 ... 占据",
        "无法移动",
        "不可通行",
    ]

    # 重叠日志正则模式
    OVERLAP_PATTERN = re.compile(
        r'\[重叠检测\]\s+检查点:\s+(.+?)\s+—\s+发现\s+(\d+)\s+处重叠!'
    )
    OVERLAP_CELL_PATTERN = re.compile(
        r'\[重叠\]\s+格子\(([-\d]+),\s*([-\d]+)\)\s+被\s+(\d+)\s+个单位占据:\s+(.+)'
    )
    MOVE_MID_PATTERN = re.compile(
        r'\[MoveUnitAnimation\]\s+(.+?)\s+路径中间格\(([-\d]+),\s*([-\d]+)\)被\s+(.+?)\s+占据!'
    )
    AI_DECIDE_PATTERN = re.compile(
        r'\[AI-Decide\]\s+(.+?)\s+选择\s+(.+?)\s+→\s+目标格\(([-\d]+),\s*([-\d]+)\)\s+\|\s+原因:\s+(.+)'
    )
    PATHFINDER_WARN_PATTERN = re.compile(
        r'\[PathFinder\]\s+目标格\(([-\d]+),\s*([-\d]+)\)不可通行！'
    )
    ATTACK_SAME_POS_PATTERN = re.compile(
        r'\[AttackUnit\]\s+⚠\s+(.+?)\s+和\s+(.+?)\s+在同一位置\(([-\d]+),\s*([-\d]+)\)！'
    )
    MOVE_BLOCK_PATTERN = re.compile(
        r'\[MoveUnitAnimation\]\s+(.+?)\s+目标格\(([-\d]+),\s*([-\d]+)\)被\s+(.+?)\s+\[(.+?)\]\s+占据，无法移动！'
    )

    def __init__(self, output_file=None, duration=None, print_all=False):
        self.output_file = output_file
        self.duration = duration
        self.print_all = print_all
        self.start_time = time.time()
        self.overlap_events = []  # 记录所有重叠事件
        self.unit_positions = defaultdict(list)  # 记录单位位置变化
        self.ai_decisions = []  # 记录AI决策
        self.blocked_moves = []  # 记录被阻挡的移动
        self.path_warnings = []  # 记录PathFinder警告
        self.log_buffer = []  # 原始日志缓存

    def run(self):
        """启动 adb logcat 并分析日志"""
        print("=" * 60)
        print("  Unity 重叠日志捕获与分析工具")
        print("=" * 60)
        print(f"  启动时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"  保存文件: {self.output_file or '不保存'}")
        print(f"  运行时长: {self.duration or '直到手动停止'} 秒")
        print("-" * 60)
        print("  等待 adb logcat 输出...")
        print("  提示：确保手机已连接，APK 正在运行")
        print("  按 Ctrl+C 停止捕获")
        print("=" * 60)

        # 先清除旧日志
        subprocess.run(["adb", "logcat", "-c"], capture_output=True)
        time.sleep(0.5)

        # 启动 adb logcat，只捕获 Unity 标签
        cmd = ["adb", "logcat", "-s", "Unity", "-v", "time", "threadtime"]
        try:
            process = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1,
                encoding='utf-8',
                errors='ignore'
            )
        except FileNotFoundError:
            print("\n[错误] adb 命令未找到！")
            print("请确保 Android SDK 的 platform-tools 已加入 PATH 环境变量。")
            print("或者手动设置 adb 路径：")
            print(f"  set PATH=%PATH%;C:\\Program Files\\Tuanjie\\Hub\\Editor\\2022.3.62t8\\Editor\\Data\\PlaybackEngines\\AndroidPlayer\\SDK\\platform-tools")
            return

        try:
            while True:
                if self.duration and (time.time() - self.start_time) > self.duration:
                    print(f"\n[完成] 已运行 {self.duration} 秒，停止捕获。")
                    break

                line = process.stdout.readline()
                if not line:
                    continue

                line = line.rstrip('\n')
                self.log_buffer.append(line)
                self._analyze_line(line)

        except KeyboardInterrupt:
            print("\n[停止] 用户中断捕获。")
        finally:
            process.terminate()
            self._print_summary()
            if self.output_file:
                self._save_logs()

    def _analyze_line(self, line):
        """分析单行日志"""
        # 只处理包含关键词的日志
        if not any(kw in line for kw in self.KEYWORDS):
            if self.print_all:
                print(f"[RAW] {line}")
            return

        print(f"[LOG] {line}")

        # 匹配重叠检测事件
        m = self.OVERLAP_PATTERN.search(line)
        if m:
            checkpoint, count = m.group(1), int(m.group(2))
            self.overlap_events.append({
                "time": time.time() - self.start_time,
                "checkpoint": checkpoint,
                "count": count,
                "cells": []
            })
            print(f"  → [重叠事件] 检查点 '{checkpoint}' 发现 {count} 处重叠!")
            return

        # 匹配具体重叠格子
        m = self.OVERLAP_CELL_PATTERN.search(line)
        if m:
            q, r, count, units = m.group(1), m.group(2), int(m.group(3)), m.group(4)
            if self.overlap_events:
                self.overlap_events[-1]["cells"].append({
                    "q": int(q), "r": int(r), "count": count, "units": units
                })
            print(f"  → [重叠格子] ({q},{r}) 被 {count} 个单位占据: {units}")
            return

        # 匹配移动中间格被阻挡
        m = self.MOVE_MID_PATTERN.search(line)
        if m:
            unit_name, q, r, blocker = m.group(1), int(m.group(2)), int(m.group(3)), m.group(4)
            self.blocked_moves.append({
                "time": time.time() - self.start_time,
                "unit": unit_name, "q": q, "r": r, "blocker": blocker,
                "type": "mid_cell"
            })
            print(f"  → [移动阻挡] {unit_name} 路径中间格({q},{r})被 {blocker} 占据")
            return

        # 匹配目标格被阻挡（无法移动）
        m = self.MOVE_BLOCK_PATTERN.search(line)
        if m:
            unit_name, q, r, blocker, faction = m.group(1), int(m.group(2)), int(m.group(3)), m.group(4), m.group(5)
            self.blocked_moves.append({
                "time": time.time() - self.start_time,
                "unit": unit_name, "q": q, "r": r, "blocker": blocker,
                "type": "target_cell"
            })
            print(f"  → [移动阻挡] {unit_name} 目标格({q},{r})被 {blocker}[{faction}] 占据，无法移动")
            return

        # 匹配AI决策
        m = self.AI_DECIDE_PATTERN.search(line)
        if m:
            unit, action, q, r, reason = m.group(1), m.group(2), int(m.group(3)), int(m.group(4)), m.group(5)
            self.ai_decisions.append({
                "time": time.time() - self.start_time,
                "unit": unit, "action": action, "q": q, "r": r, "reason": reason
            })
            print(f"  → [AI决策] {unit} 选择{action} → ({q},{r}) | {reason}")
            return

        # 匹配PathFinder警告
        m = self.PATHFINDER_WARN_PATTERN.search(line)
        if m:
            q, r = int(m.group(1)), int(m.group(2))
            self.path_warnings.append({
                "time": time.time() - self.start_time,
                "q": q, "r": r
            })
            print(f"  → [寻路警告] 目标格({q},{r})不可通行!")
            return

        # 匹配攻击同位置
        m = self.ATTACK_SAME_POS_PATTERN.search(line)
        if m:
            unit1, unit2, q, r = m.group(1), m.group(2), int(m.group(3)), int(m.group(4))
            self.overlap_events.append({
                "time": time.time() - self.start_time,
                "checkpoint": f"AttackUnit-{unit1}攻击{unit2}",
                "count": 1,
                "cells": [{"q": q, "r": r, "count": 2, "units": f"{unit1}, {unit2}"}]
            })
            print(f"  → [严重] 攻击发生时 {unit1} 和 {unit2} 在同一位置({q},{r})!")
            return

    def _print_summary(self):
        """打印分析摘要"""
        elapsed = time.time() - self.start_time
        print("\n" + "=" * 60)
        print("  分析摘要")
        print("=" * 60)
        print(f"  总运行时间: {elapsed:.1f} 秒")
        print(f"  捕获日志行数: {len(self.log_buffer)}")
        print(f"  重叠事件数: {len(self.overlap_events)}")
        print(f"  移动阻挡次数: {len(self.blocked_moves)}")
        print(f"  AI决策记录数: {len(self.ai_decisions)}")
        print(f"  寻路警告次数: {len(self.path_warnings)}")
        print("-" * 60)

        if not self.overlap_events:
            print("  ✅ 未检测到任何重叠事件")
            return

        # 按检查点统计
        checkpoint_counts = defaultdict(int)
        for event in self.overlap_events:
            checkpoint_counts[event["checkpoint"]] += 1

        print("  重叠事件按检查点分布:")
        for cp, count in sorted(checkpoint_counts.items(), key=lambda x: -x[1]):
            print(f"    - {cp}: {count} 次")

        print("-" * 60)
        print("  重叠详情:")
        for i, event in enumerate(self.overlap_events, 1):
            print(f"    事件 #{i} @ {event['time']:.1f}s - 检查点: {event['checkpoint']}")
            for cell in event.get("cells", []):
                print(f"      格子({cell['q']},{cell['r']}) - {cell['count']} 单位: {cell['units']}")

        print("-" * 60)
        print("  重叠根因分析:")
        self._analyze_root_causes()

    def _analyze_root_causes(self):
        """基于收集的数据分析重叠根因"""
        # 检查是否有AI决策后直接重叠
        ai_related = 0
        for event in self.overlap_events:
            cp = event["checkpoint"]
            if "EnemyAI" in cp or "MoveUnitAnimation" in cp or "ExecuteAIAttack" in cp:
                ai_related += 1

        if ai_related > 0:
            print(f"    - 发现 {ai_related} 个重叠事件与AI行动相关，可能是AI移动到被占据格子导致")

        # 检查是否有寻路警告后重叠
        if self.path_warnings:
            print(f"    - 发现 {len(self.path_warnings)} 次寻路警告（目标格不可通行），但AI可能仍尝试移动")

        # 检查被阻挡的移动
        if self.blocked_moves:
            print(f"    - 发现 {len(self.blocked_moves)} 次移动被阻挡（被正确拦截），但可能有漏网之鱼")

        # 检查攻击时同位置
        attack_same = [e for e in self.overlap_events if "AttackUnit" in e["checkpoint"]]
        if attack_same:
            print(f"    - 发现 {len(attack_same)} 次攻击时双方处于同一位置，这是严重bug！")

        print("-" * 60)
        print("  建议修复方案:")
        print("    1. 在AI移动前，确保目标格完全空置（不是仅检查友方）")
        print("    2. FindPath 应考虑 occupiedCells，避免路径穿过其他单位")
        print("    3. 在ResolveUnitOverlaps中扩大搜索半径或确保总能找到空位")
        print("    4. EnemyTurn开始时也调用ResolveUnitOverlaps")

    def _save_logs(self):
        """保存日志到文件"""
        print(f"\n[保存] 正在保存日志到 {self.output_file} ...")
        with open(self.output_file, 'w', encoding='utf-8') as f:
            f.write(f"# Unity 重叠日志捕获\n")
            f.write(f"# 捕获时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
            f.write(f"# 运行时长: {time.time() - self.start_time:.1f} 秒\n")
            f.write(f"# 总行数: {len(self.log_buffer)}\n")
            f.write("=" * 60 + "\n\n")
            for line in self.log_buffer:
                f.write(line + "\n")

        # 同时保存分析结果JSON
        json_file = self.output_file.replace('.txt', '_analysis.json')
        with open(json_file, 'w', encoding='utf-8') as f:
            json.dump({
                "overlap_events": self.overlap_events,
                "blocked_moves": self.blocked_moves,
                "ai_decisions": self.ai_decisions,
                "path_warnings": self.path_warnings,
                "stats": {
                    "total_lines": len(self.log_buffer),
                    "overlap_count": len(self.overlap_events),
                    "blocked_count": len(self.blocked_moves),
                    "ai_decision_count": len(self.ai_decisions),
                    "path_warning_count": len(self.path_warnings)
                }
            }, f, ensure_ascii=False, indent=2)

        print(f"[保存] 日志已保存到: {self.output_file}")
        print(f"[保存] 分析结果已保存到: {json_file}")


def main():
    import argparse
    parser = argparse.ArgumentParser(description='Unity 重叠日志捕获与分析工具')
    parser.add_argument('--save', '-s', default=None, help='保存日志到文件路径')
    parser.add_argument('--duration', '-d', type=int, default=None, help='运行时长（秒）')
    parser.add_argument('--all', '-a', action='store_true', help='打印所有Unity日志（不只是关键词）')
    args = parser.parse_args()

    analyzer = OverlapLogAnalyzer(
        output_file=args.save,
        duration=args.duration,
        print_all=args.all
    )
    analyzer.run()


if __name__ == '__main__':
    main()
