@echo off
chcp 65001 >nul
title Unity 重叠日志全自动采集工具

echo ========================================
echo   Unity 重叠日志全自动采集工具
echo ========================================
echo.

:: 配置路径
set "APK_PATH=C:\chengqi\Android\AndroidProject\zhenguanWarriors\zhenguanWarriors.apk"
set "PACKAGE_NAME=com.yourcompany.zhenguan"
set "ACTIVITY=com.unity3d.player.UnityPlayerActivity"
set "LOG_DEST=C:\chengqi\Android\AndroidProject\zhenguanWarriors\overlap_log.txt"
set "REMOTE_LOG_PATH=/sdcard/Android/data/%PACKAGE_NAME%/files/overlap_log.txt"
set "ALT_REMOTE_PATH=/sdcard/Download/overlap_log.txt"

:: 检查 adb
echo [1/6] 检查 ADB 连接...
adb devices -l | findstr "device" >nul
if errorlevel 1 (
    echo [错误] 未检测到设备！请连接手机并启用 USB 调试。
    pause
    exit /b 1
)
echo     设备已连接 ✓

:: 检查 APK 是否存在
echo [2/6] 检查 APK 文件...
if not exist "%APK_PATH%" (
    echo [错误] 未找到 APK: %APK_PATH%
    echo 请先在 Unity Hub 中构建 APK。
    pause
    exit /b 1
)
echo     APK 文件存在 ✓

:: 安装 APK
echo [3/6] 安装 APK（如果已安装会覆盖）...
adb install -r "%APK_PATH%" >nul 2>&1
if errorlevel 1 (
    echo [警告] 安装可能失败，尝试直接启动...（如果已安装则忽略）
) else (
    echo     安装成功 ✓
)

:: 清除旧日志（防止读取到旧数据）
echo [4/6] 清除旧日志...
adb shell "rm -f %REMOTE_LOG_PATH% 2>/dev/null; rm -f %ALT_REMOTE_PATH% 2>/dev/null" >nul 2>&1

:: 启动游戏
echo [5/6] 启动游戏...
adb shell am start -n %PACKAGE_NAME%/%ACTIVITY% >nul 2>&1
echo     游戏已启动 ✓

echo.
echo ========================================
echo   请在手机上操作游戏！
echo   进入战斗，让敌方回合运行几个回合。
echo ========================================
echo.
pause

:: 拉取日志
echo [6/6] 拉取日志文件...
adb shell "ls %REMOTE_LOG_PATH% 2>/dev/null" >nul 2>&1
if not errorlevel 1 (
    adb pull "%REMOTE_LOG_PATH%" "%LOG_DEST%" >nul 2>&1
    echo     从 data 目录拉取成功 ✓
) else (
    adb shell "ls %ALT_REMOTE_PATH% 2>/dev/null" >nul 2>&1
    if not errorlevel 1 (
        adb pull "%ALT_REMOTE_PATH%" "%LOG_DEST%" >nul 2>&1
        echo     从 Download 目录拉取成功 ✓
    ) else (
        echo [警告] 未找到日志文件，可能路径不同。
        echo 尝试搜索所有 overlap_log.txt...
        adb shell "find /sdcard -name 'overlap_log.txt' 2>/dev/null" > found.txt
        type found.txt
        del found.txt
    )
)

:: 分析日志
if exist "%LOG_DEST%" (
    echo.
    echo ========================================
    echo   日志分析结果
    echo ========================================
    echo.
    for /f %%a in ('findstr /c /i "重叠" "%LOG_DEST%" ^| find /c /v ""') do set OVERLAP_COUNT=%%a
    for /f %%a in ('findstr /c /i "AI-Decide" "%LOG_DEST%" ^| find /c /v ""') do set AI_COUNT=%%a
    for /f %%a in ('findstr /c /i "PathFinder" "%LOG_DEST%" ^| find /c /v ""') do set PATH_COUNT=%%a
    for /f %%a in ('findstr /c /i "无法" "%LOG_DEST%" ^| find /c /v ""') do set BLOCK_COUNT=%%a
    
    echo 重叠相关日志: %OVERLAP_COUNT%
    echo AI 决策日志: %AI_COUNT%
    echo 寻路警告: %PATH_COUNT%
    echo 移动阻挡: %BLOCK_COUNT%
    echo.
    echo [日志文件]: %LOG_DEST%
    echo.
    echo --- 重叠日志摘要 ---
    findstr /n /i "重叠" "%LOG_DEST%" | findstr /v "重叠检测.*无重叠"
    echo.
    echo 请把 %LOG_DEST% 文件发送给 AI 分析。
) else (
    echo [错误] 未能获取日志文件。
)

pause
