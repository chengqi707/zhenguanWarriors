@echo off
chcp 65001 >nul
title Claude + Kimi 启动器

echo ========================================
echo   Claude Code + Kimi K2.6 启动器
echo ========================================
echo.

:: 设置 Kimi API 端点（覆盖 DeepSeek 配置）
set "ANTHROPIC_BASE_URL=https://api.kimi.com/coding/"
set "CLAUDE_CODE_AUTO_COMPACT_WINDOW=262144"

:: 检查是否设置了 API Key
if "%ANTHROPIC_API_KEY%"=="" (
    echo [错误] 未检测到 ANTHROPIC_API_KEY 环境变量！
    echo.
    echo 请设置您的 Kimi API Key：
    echo   方法1：在系统环境变量中添加 ANTHROPIC_API_KEY
    echo   方法2：临时设置：set ANTHROPIC_API_KEY=sk-kimi-xxx
    echo.
    pause
    exit /b 1
)

echo [1/2] 环境变量已设置：
echo     ANTHROPIC_BASE_URL = %ANTHROPIC_BASE_URL%
echo     API Key            = %ANTHROPIC_API_KEY:~0,15%... (已截断)
echo.

echo [2/2] 启动 Claude Code...
echo.
claude
