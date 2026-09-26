# Sends dev commands to a running game with Debug.DevCommands enabled and prints the new log lines.
# Example: ./scripts/dev.ps1 -Commands "click Host Button","capture" -Wait 5
param([string[]]$Commands, [int]$Wait = 3,
      [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Gamble With Your Friends")
$log = Join-Path $GameDir "BepInEx\LogOutput.log"
$before = (Get-Content $log).Count
foreach ($c in $Commands) {
    Set-Content (Join-Path $GameDir "BepInEx\gwyfvr-command.txt") $c
    Start-Sleep $Wait
}
Get-Content $log | Select-Object -Skip $before
