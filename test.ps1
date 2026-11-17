param([switch]$ExerciseTaskbar, [switch]$StartAutomatic)
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot 'TaskbarController.exe'
[void][System.Reflection.Assembly]::LoadFrom($executable)
[OledTaskbar.Native]::SelfTest()
[OledTaskbar.Native]::Diagnose()
if ($ExerciseTaskbar) {
    $initialHidden = [OledTaskbar.Native]::IsHidden()
    try {
        [OledTaskbar.Native]::SetHidden(-not $initialHidden)
        if ([OledTaskbar.Native]::IsHidden() -eq $initialHidden) { throw 'Taskbar round-trip test failed' }
    }
    finally { [OledTaskbar.Native]::SetHidden($initialHidden) }
    if ([OledTaskbar.Native]::IsHidden() -ne $initialHidden) { throw 'Initial state was not restored' }
    'PASS: taskbar switched and original state restored.'
}
if ($StartAutomatic) {
    Start-Process -FilePath $executable -ArgumentList '--auto' -WindowStyle Hidden
    Start-Sleep -Seconds 6
    [OledTaskbar.Native]::Diagnose()
    Get-Process -Name TaskbarController | Select-Object Id, ProcessName, WorkingSet64
}
