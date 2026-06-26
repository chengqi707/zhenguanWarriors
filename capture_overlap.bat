@echo off
chcp 65001 >nul
title ADB Overlap Log Capture

set TIMESTAMP=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%
set TIMESTAMP=%TIMESTAMP: =0%
set LOG_FILE=overlap_logs_%TIMESTAMP%.txt
set FILTER_FILE=overlap_filtered_%TIMESTAMP%.txt

echo ========================================
echo   Unity Overlap Log Capture Tool
echo ========================================
echo.
echo Log file: %LOG_FILE%
echo Press Ctrl+C then Y to stop
echo.

adb logcat -c 2>nul
if errorlevel 1 (
    echo [ERROR] adb command failed!
    echo Make sure:
    echo   - Phone is connected via USB with debugging enabled
    echo   - Android SDK platform-tools is in PATH
    pause
    exit /b 1
)

timeout /t 1 /nobreak >nul

echo [1/2] Capturing logs... Please play the game on your phone.
echo.

start /b adb logcat -s Unity -v threadtime > "%LOG_FILE%" 2>nul

adb logcat -s Unity -v threadtime | findstr /C:"[overlap]" /C:"[AI-Decide]" /C:"[PathFinder]" /C:"[MoveUnitAnimation]" /C:"[AttackUnit]" /C:"[ExecuteAIAttack]" /C:"[ResolveUnitOverlaps]" /C:"[CreateEnemyUnits]" /C:"[OnPhaseChanged]" /C:"[StartBattle]" /C:"[EnemyAI]" /C:"[CheckMoveTowards]" /C:"[CheckRetreat]"

echo.
echo [2/2] Capture stopped. Analyzing...
timeout /t 2 /nobreak >nul

findstr /C:"[overlap]" /C:"[AI-Decide]" /C:"[PathFinder]" /C:"[MoveUnitAnimation]" /C:"[AttackUnit]" /C:"[ExecuteAIAttack]" /C:"[ResolveUnitOverlaps]" /C:"[CreateEnemyUnits]" /C:"[OnPhaseChanged]" /C:"[StartBattle]" /C:"[EnemyAI]" /C:"[CheckMoveTowards]" /C:"[CheckRetreat]" "%LOG_FILE%" > "%FILTER_FILE%" 2>nul

for /f %%a in ('type "%LOG_FILE%" ^| find /c /v ""') do set TOTAL_LINES=%%a
for /f %%a in ('type "%FILTER_FILE%" ^| find /c /v ""') do set FILTER_LINES=%%a
for /f %%a in ('findstr /C:"[overlap]" "%LOG_FILE%" ^| find /c /v ""') do set OVERLAP_COUNT=%%a

echo.
echo ========================================
echo   Summary
echo ========================================
echo Total lines:   %TOTAL_LINES%
echo Filtered lines: %FILTER_LINES%
echo Overlap events: %OVERLAP_COUNT%
if %OVERLAP_COUNT%==0 (
    echo Result: No overlap detected. Game running normally.
) else (
    echo Result: %OVERLAP_COUNT% overlap events found!
    echo Check file: %FILTER_FILE%
)
echo Full log: %LOG_FILE%
echo.
pause
