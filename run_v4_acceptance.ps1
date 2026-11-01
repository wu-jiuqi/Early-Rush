param([Parameter(Mandatory=$true)][int]$Build,[Parameter(Mandatory=$true)][int]$RouteRun)
$ErrorActionPreference='Stop'
$taskRoot='D:\CodexWorkspace\2026-10-31\task-4'
$manifest=$taskRoot+'\artifacts\v4-build'+$Build+'-tested.json'
$exePath=$taskRoot+'\artifacts\EarlyRush-V4-Windows\EarlyRush.exe'
if(-not(Test-Path -LiteralPath $manifest)){throw 'Freeze the successful build before QA'}
if(Get-Process EarlyRush,Unity -ErrorAction SilentlyContinue){throw 'Another Player or Editor is still running'}
$runs=@(@{Name=('combat'+$Build);Flag='--combat-qa';OutputFlag='--combat-qa-output='},@{Name=('impact'+$Build);Flag='--impact-qa';OutputFlag='--impact-qa-output='},@{Name=('route'+$RouteRun);Flag='--qa';OutputFlag='--qa-output='})
foreach($run in $runs){
    $run.Directory=$taskRoot+'\qa-v4-'+$run.Name
    $run.Log=$taskRoot+'\artifacts\v4-'+$run.Name+'-player.log'
    if((Test-Path -LiteralPath $run.Directory) -or (Test-Path -LiteralPath $run.Log)){throw 'Inspect existing QA artifacts before rerunning'}
}
foreach($run in $runs){New-Item -ItemType Directory -Path $run.Directory|Out-Null;Copy-Item -LiteralPath $manifest -Destination ($run.Directory+'\tested-build.json')}
$exitPath=$taskRoot+'\artifacts\v4-final-player-exits.json'
if(Test-Path -LiteralPath $exitPath){
    $archive=$taskRoot+'\artifacts\v4-before-build'+$Build+'-player-exits.json'
    if(Test-Path -LiteralPath $archive){throw 'Exit archive already exists'}
    Copy-Item -LiteralPath $exitPath -Destination $archive
}
$records=@()
foreach($run in $runs){
    $p=Start-Process -FilePath $exePath -ArgumentList @('-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-force-d3d11',$run.Flag,($run.OutputFlag+$run.Directory),'-logFile',$run.Log) -WindowStyle Normal -PassThru
    $p.WaitForExit()
    $records+=@{name=$run.Name;exitCode=$p.ExitCode;log=$run.Log}
    $records|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $exitPath -Encoding UTF8
    Write-Output ($run.Name+'_EXIT='+$p.ExitCode)
    if($p.ExitCode -ne 0){exit $p.ExitCode}
}
