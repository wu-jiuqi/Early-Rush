$ErrorActionPreference='Stop'
$root='D:\EarlyRush\Early-Rush'
Wait-Process -Id 30964 -ErrorAction SilentlyContinue
$steps=@(
 @('V4LayoutContractValidation.ValidateDynamicNpcOwnership','v14-v4-dynamic-npc-ownership-2'),
 @('V4InteriorReplayValidation.MetroTasks','v14-v4-metro-tasks'),
 @('V4InteriorReplayValidation.OfficeTasks','v14-v4-office-tasks')
)
foreach($step in $steps){
 $a=@('-batchmode','-nographics','-quit','-projectPath',$root,'-executeMethod',$step[0],'-logFile',"$root\Logs\$($step[1]).log")
 $started=[DateTime]::UtcNow
 $proc=Start-Process 'D:\Unity\Editor\Unity.exe' -ArgumentList ($a|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru
 $proc.WaitForExit();$proc.Refresh()
 [pscustomobject]@{step=$step[0];started=$started.ToString('o');finished=[DateTime]::UtcNow.ToString('o');exitCode=$proc.ExitCode}|ConvertTo-Json -Compress|Add-Content -LiteralPath "$root\Logs\v14-v4-remaining-results.jsonl"
}
