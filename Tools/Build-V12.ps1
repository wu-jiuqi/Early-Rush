param([string]$UnityPath = 'D:\unity\Editor\Unity.exe', [switch]$ValidateOnly, [switch]$Quick)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "找不到 Unity，请通过 -UnityPath 指定 Unity 6000.0.26f1。" }
$logDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory 'v12-build.log'
$method = if ($Quick) { 'BuildEarlyRushV12.ValidateQuick' } elseif ($ValidateOnly) { 'BuildEarlyRushV12.Validate' } else { 'BuildEarlyRushV12.Build' }
$arguments = '-batchmode -nographics -quit -projectPath "{0}" -executeMethod {1} -logFile "{2}"' -f $projectRoot, $method, $logPath
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Unity 构建或验证失败，查看日志：$logPath" }
Write-Output "完成。日志：$logPath"
Write-Output (Join-Path $projectRoot 'Builds\EarlyRush-V12')
