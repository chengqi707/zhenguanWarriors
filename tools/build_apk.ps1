# 贞观勇士 - 一键导出 Android release APK
# 用法：关闭 Tuanjie/Unity Editor 后，在项目根目录执行：
#   powershell -ExecutionPolicy Bypass -File tools/build_apk.ps1
# 输出：zhenguanWarriors/Build/zhenguanWarriors_v0.11.apk

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptDir
$projectDir = Join-Path $projectRoot "zhenguanWarriors"
$editor = "C:\Program Files\Tuanjie\Hub\Editor\2022.3.62t8\Editor\Tuanjie.exe"
$logFile = Join-Path $projectRoot "build_apk.log"

if (!(Test-Path $editor)) {
    Write-Error "未找到 Tuanjie 编辑器: $editor"
    exit 1
}

# 清理旧日志
if (Test-Path $logFile) { Remove-Item $logFile -Force }

Write-Host "开始构建 Android release APK..."
Write-Host "项目路径: $projectDir"
Write-Host "日志文件: $logFile"

$proc = Start-Process -FilePath $editor `
    -ArgumentList @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", "$projectDir",
        "-executeMethod", "BuildScript.BuildAPK",
        "-logFile", "$logFile"
    ) `
    -NoNewWindow -Wait -PassThru

if ($proc.ExitCode -ne 0) {
    Write-Error "APK 构建失败，退出码: $($proc.ExitCode)。请查看日志: $logFile"
    exit $proc.ExitCode
}

Write-Host "APK 构建完成。输出目录: $(Join-Path $projectDir 'Build')"
