param([string]$Unity="D:\unity\Editor\Unity.exe")
$ErrorActionPreference="Stop"
$Project=(Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Artifacts=Join-Path (Split-Path $Project) "artifacts"
New-Item -ItemType Directory -Force $Artifacts | Out-Null
if(!(Test-Path $Unity)){throw "Unity editor not found: $Unity"}
$Log=Join-Path $Artifacts "v7-unity-build.log"
# Run independently of old v6 output. Unity exits nonzero or missing receipt aborts packaging.
$p=Start-Process -FilePath $Unity -ArgumentList @("-batchmode","-quit","-projectPath",('"'+$Project+'"'),"-executeMethod","BuildEarlyRush.Build","-logFile",('"'+$Log+'"')) -PassThru -Wait
if($p.ExitCode -ne 0){throw "Unity build exit $($p.ExitCode). See $Log"}
if(!(Select-String -Path $Log -SimpleMatch "EARLY_RUSH_BUILD: Succeeded")){throw "No successful build receipt"}
Write-Host "Built v7. Run visible Player regression and inspect screenshots before packaging/releasing."
