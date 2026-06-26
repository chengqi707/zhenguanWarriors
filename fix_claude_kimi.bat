@echo off
chcp 65001 >nul
title Claude + Kimi 环境配置修复器

echo ========================================
echo   Claude Code + Kimi API 配置修复
echo ========================================
echo.

:: 1. 清除旧的 DeepSeek 配置（系统环境变量）
echo [1/4] 尝试删除旧的 ANTHROPIC_AUTH_TOKEN 环境变量...
setx ANTHROPIC_AUTH_TOKEN "" >nul 2>&1
setx ANTHROPIC_AUTH_TOKEN /M "" >nul 2>&1
reg delete "HKCU\Environment" /v ANTHROPIC_AUTH_TOKEN /f >nul 2>&1
reg delete "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v ANTHROPIC_AUTH_TOKEN /f >nul 2>&1
echo     已清除 ANTHROPIC_AUTH_TOKEN（需重新打开终端生效）

:: 2. 设置 Kimi Base URL
echo [2/4] 设置 Kimi API 端点...
setx ANTHROPIC_BASE_URL "https://api.kimi.com/coding/" >nul 2>&1
echo     ANTHROPIC_BASE_URL = https://api.kimi.com/coding/

:: 3. 提示用户设置 API Key
echo.
echo [3/4] 检查 API Key...
if "%ANTHROPIC_API_KEY%"=="" (
    echo [注意] 未检测到 ANTHROPIC_API_KEY 环境变量！
    echo.
    echo 请执行以下命令之一：
    echo   方法1（临时，仅当前窗口）：
    echo     set ANTHROPIC_API_KEY=sk-kimi-xxx
    echo.
    echo   方法2（永久，写入系统）：
    echo     setx ANTHROPIC_API_KEY "sk-kimi-xxx"
    echo.
    pause
    exit /b 1
) else (
    echo     API Key 已设置 ✓
)

:: 4. 清理 Claude 的持久化配置（缓存中的 DeepSeek URL）
echo [4/4] 清理 Claude 缓存中的 DeepSeek 配置...
set "CLD_LOCAL=%LOCALAPPDATA%\Claude"
set "CLD_ROAM=%APPDATA%\Claude"
set "CLD_USER=%USERPROFILE%\.claude.json"

if exist "%CLD_LOCAL%" (
    rd /s /q "%CLD_LOCAL%" 2>nul
    echo     已清理 %LOCALAPPDATA%\Claude
)
if exist "%CLD_ROAM%" (
    rd /s /q "%CLD_ROAM%" 2>nul
    echo     已清理 %APPDATA%\Claude
)
if exist "%CLD_USER%" (
    del /q "%CLD_USER%" 2>nul
    echo     已删除 %USERPROFILE%\.claude.json
)

echo.
echo ========================================
echo   配置完成！
echo ========================================
echo.
echo 请执行以下步骤：
echo.
echo 1. [必须] 关闭所有终端/PowerShell 窗口
echo 2. 重新打开 PowerShell 或 CMD
echo 3. 运行：claude
echo 4. 在 Claude 中输入：/status
echo 5. 确认显示：Anthropic base URL: https://api.kimi.com/coding/
echo.
echo 如果提示输入 API Key，请粘贴您的 Kimi API Key
echo.
pause
