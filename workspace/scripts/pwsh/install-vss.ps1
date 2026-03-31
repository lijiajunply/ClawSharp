# .workspace/scripts/pwsh/install-vss.ps1
# 自动下载并安装 sqlite-vss 扩展二进制文件 (Windows)

$Version = "v0.1.2"
$TargetDir = "ClawSharp.CLI/bin/Debug/net10.0"
$Platform = "loadable-windows-x86_64"

$Url = "https://github.com/asg017/sqlite-vss/releases/download/$Version/$Platform.zip"
$TempFile = ".workspace/vss_temp.zip"

Write-Host "=== Installing sqlite-vss for Windows ==="
if (-not (Test-Path $TargetDir)) { New-Item -ItemType Directory -Path $TargetDir }

Write-Host "Downloading from: $Url"
Invoke-WebRequest -Uri $Url -OutFile $TempFile

Write-Host "Extracting to: $TargetDir"
Expand-Archive -Path $TempFile -DestinationPath $TargetDir -Force
Remove-Item $TempFile

Write-Host "`n✓ sqlite-vss successfully installed to $TargetDir" -ForegroundColor Green
