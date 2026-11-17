param(
    [string]$OutputName = 'TaskbarController.exe',
    [switch]$SkipShortcuts
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputPath = Join-Path $PSScriptRoot $OutputName
$iconPath = Join-Path $PSScriptRoot 'TaskbarController.ico'

& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    "/win32icon:$iconPath" "/resource:$iconPath,TaskbarController.ico" `
    "/out:$outputPath" "$PSScriptRoot\TaskbarController.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }

if ($SkipShortcuts) { return }

$shell = New-Object -ComObject WScript.Shell
$shortcuts = [ordered]@{
    'Start Automatic' = '--auto'
    'Toggle Taskbar' = '--toggle'
    'Stop and Restore' = '--exit'
}
foreach ($entry in $shortcuts.GetEnumerator()) {
    $shortcut = $shell.CreateShortcut((Join-Path $PSScriptRoot ($entry.Key + '.lnk')))
    $shortcut.TargetPath = $outputPath
    $shortcut.Arguments = $entry.Value
    $shortcut.WorkingDirectory = $PSScriptRoot
    $shortcut.IconLocation = "$iconPath,0"
    $shortcut.Save()
}
