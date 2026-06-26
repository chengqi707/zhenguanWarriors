@echo off
chcp 65001 >nul
title ADB Log Capture - Simple Mode

echo ========================================
echo  Unity 日志捕获工具（简单版）
echo ========================================
echo.
echo 用法：运行后，在手机上操作游戏，
echo       按任意键停止并分析日志。
echo.

set "LOG_FILE=unity_log.txt"

echo [1] 清除旧日志...
adb logcat -c >nul 2>&1
echo [2] 开始捕获，请在手机上操作游戏...
echo     按任意键停止捕获...

:: 在后台捕获日志到文件
start /b adb logcat -s Unity -v threadtime > "%LOG_FILE%" 2>nul

:: 等待用户按键
pause >nul

:: 终止后台 adb logcat（结束进程）
taskkill /f /im adb.exe 2>nul

timeout /t 2 /nobreak >nul

echo.
echo [3] 日志已保存到: %LOG_FILE%

:: 检查文件是否存在且有内容
if not exist "%LOG_FILE%" (
    echo [ERROR] 日志文件未生成！
    pause
    exit /b 1
)

for %%F in ("%LOG_FILE%") do set FILESIZE=%%~zF
if %FILESIZE%==0 (
    echo [WARNING] 日志文件为空，可能设备未输出日志。
    pause
    exit /b 1
)

echo [4] 搜索重叠相关日志...
findstr /N /I /C:"[overlap]" /C:"[AI-Decide]" /C:"[PathFinder]" /C:"[MoveUnitAnimation]" /C:"[AttackUnit]" /C:"[ExecuteAIAttack]" /C:"[ResolveUnitOverlaps]" /C:"[CreateEnemyUnits]" /C:"[OnPhaseChanged]" /C:"[StartBattle]" /C:"[EnemyAI]" /C:"[CheckMoveTowards]" /C:"[CheckRetreat]" "%LOG_FILE%"

echo.
echo [5] 分析完成。完整日志: %LOG_FILE%
pause
