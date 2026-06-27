# 贞观勇士 - 命令行编译检查脚本（解决 bash 中路径斜杠被吃的问题）
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectDir = Join-Path $scriptDir "zhenguanWarriors"
$solution = Join-Path $projectDir "zhenguanWarriors.sln"
$msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

if (!(Test-Path $solution)) {
    Write-Error "未找到解决方案文件: $solution"
    exit 1
}

if (!(Test-Path $msbuild)) {
    Write-Error "未找到 MSBuild: $msbuild"
    exit 1
}

Write-Host "正在编译: $solution"
& $msbuild $solution /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /restore /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "编译失败，退出码: $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "编译通过"
