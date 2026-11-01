param([int]$Attempt=1)
$ErrorActionPreference='Stop'
$taskRoot='D:\CodexWorkspace\2026-10-31\task-4'
$exePath=$taskRoot+'\artifacts\v4-two-part-verified\EarlyRush-V4-Windows\EarlyRush.exe'
$suffix=if($Attempt -gt 1){[string]$Attempt}else{''}
$outputPath=$taskRoot+'\qa-v4-merged-smoke'+$suffix
$logPath=$taskRoot+'\artifacts\player-v4-merged-smoke'+$suffix+'.log'
if(Get-Process EarlyRush -ErrorAction SilentlyContinue){throw 'Inspect existing Player before another smoke'}
if((Test-Path -LiteralPath $outputPath) -or (Test-Path -LiteralPath $logPath)){throw 'Inspect prior smoke result before another execution'}
New-Item -ItemType Directory -Path $outputPath | Out-Null
$p=Start-Process -FilePath $exePath -ArgumentList @('-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-force-d3d11','--qa',('--qa-output='+$outputPath),'-logFile',$logPath) -WindowStyle Normal -PassThru
$watch=[Diagnostics.Stopwatch]::StartNew()
$ready=$false
while(-not $p.HasExited -and $watch.Elapsed.TotalSeconds -lt 50){
    if(Test-Path -LiteralPath $logPath){
        $stream=[IO.File]::Open($logPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
        try{$reader=[IO.StreamReader]::new($stream);$content=$reader.ReadToEnd()}finally{$stream.Dispose()}
        $passCount=[regex]::Matches($content,'RUSH_QA_CHECK PASS').Count
        if($passCount -ge 24 -and $content.Contains('Dash accelerates and consumes cooldown')){$ready=$true;break}
    }
    Start-Sleep -Milliseconds 300
    $p.Refresh()
}
if(-not $ready){throw 'Startup smoke did not reach required basic checks; inspect current process and log'}
$closeAccepted=$p.CloseMainWindow()
if(-not $closeAccepted){throw 'Window did not accept normal close; do not force terminate'}
if(-not $p.WaitForExit(15000)){throw 'Normal window close still pending; inspect process before retry'}
@{normalCloseAccepted=$closeAccepted;normalCloseExitCode=$p.ExitCode;elapsedSeconds=$watch.Elapsed.TotalSeconds;player=$exePath;playerLog=$logPath;output=$outputPath}|ConvertTo-Json -Depth 3|Set-Content -LiteralPath ($taskRoot+'\artifacts\v4-merged-smoke-exit.json') -Encoding UTF8
Write-Output ('SMOKE_EXIT='+$p.ExitCode)
if($p.ExitCode -ne 0){exit $p.ExitCode}
