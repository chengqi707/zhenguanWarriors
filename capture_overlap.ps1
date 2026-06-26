# Unity Overlap Log Capture & Analysis Tool
# Usage: Right-click -> Run with PowerShell

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = "overlap_logs_$timestamp.txt"
$filterFile = "overlap_filtered_$timestamp.txt"

$keywords = @(
    "[overlap]",
    "[AI-Decide]",
    "[PathFinder]",
    "[MoveUnitAnimation]",
    "[AttackUnit]",
    "[ExecuteAIAttack]",
    "[ResolveUnitOverlaps]",
    "[CreateEnemyUnits]",
    "[OnPhaseChanged]",
    "[StartBattle]",
    "[EnemyAI]",
    "[CheckMoveTowards]",
    "[CheckRetreat]"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Unity Overlap Log Capture Tool" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Log file: $logFile"
Write-Host "Press Ctrl+C to stop capture"
Write-Host ""

adb logcat -c 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] adb command failed!" -ForegroundColor Red
    Write-Host "Make sure Android SDK platform-tools is in PATH."
    Read-Host "Press Enter to exit"
    exit 1
}

Start-Sleep -Seconds 1

Write-Host "[1/2] Capturing logs... Please play the game on your phone." -ForegroundColor Green
Write-Host ""

$job = Start-Job -ScriptBlock {
    param($logFile)
    adb logcat -s Unity -v threadtime > $logFile
} -ArgumentList $logFile

try {
    adb logcat -s Unity -v threadtime | ForEach-Object {
        $line = $_
        foreach ($kw in $using:keywords) {
            if ($line -like "*$kw*") {
                Write-Host $line -ForegroundColor Yellow
                break
            }
        }
    }
} catch {
    Write-Host "`nCapture stopped by user." -ForegroundColor Yellow
}

Stop-Job $job -ErrorAction SilentlyContinue
Remove-Job $job -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "[2/2] Analyzing logs..." -ForegroundColor Green
Start-Sleep -Seconds 2

$logLines = Get-Content $logFile -ErrorAction SilentlyContinue
$filtered = $logLines | Where-Object {
    $line = $_
    foreach ($kw in $keywords) {
        if ($line -like "*$kw*") { return $true }
    }
    return $false
}
$filtered | Set-Content $filterFile

$totalLines = $logLines.Count
$filterLines = $filtered.Count
$overlapCount = ($logLines | Select-String "\[overlap\]").Count

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total lines:      $totalLines"
Write-Host "Filtered lines:   $filterLines"
Write-Host "Overlap events:   $overlapCount"
if ($overlapCount -eq 0) {
    Write-Host "Result: No overlap detected. Game running normally." -ForegroundColor Green
} else {
    Write-Host "Result: $overlapCount overlap events found!" -ForegroundColor Red
    Write-Host "Check file: $filterFile"
}
Write-Host "Full log: $logFile"
Write-Host ""
Read-Host "Press Enter to exit"
