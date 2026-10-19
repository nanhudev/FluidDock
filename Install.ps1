param([switch]$StartWithWindows,[switch]$NoLaunch)
$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'DockBot.exe'
if(!(Test-Path -LiteralPath $source)){$source=Join-Path $PSScriptRoot 'bin\DockBot.exe'}
if(!(Test-Path -LiteralPath $source)){throw 'DockBot.exe not found. Extract the release or build first.'}
$destination=Join-Path $env:LOCALAPPDATA 'Programs\DockBot'
New-Item -ItemType Directory -Path $destination -Force|Out-Null
$target=Join-Path $destination 'DockBot.exe'
if([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($target)){Copy-Item -LiteralPath $source -Destination $target -Force}
$shortcutShell=New-Object -ComObject WScript.Shell
$startMenu=Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\DockBot.lnk'
$link=$shortcutShell.CreateShortcut($startMenu)
$link.TargetPath=$target
$link.WorkingDirectory=$destination
$link.Save()
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)
if($StartWithWindows){
    $startupPath=Join-Path ([Environment]::GetFolderPath('Startup')) 'DockBot.lnk'
    $startup=$shortcutShell.CreateShortcut($startupPath)
    $startup.TargetPath=$target
    $startup.WorkingDirectory=$destination
    $startup.Save()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($startup)
    $legacyData=Join-Path $env:LOCALAPPDATA 'FluidDock'
    $dataFolder=if(Test-Path -LiteralPath (Join-Path $legacyData 'apps.json')){$legacyData}else{Join-Path $env:LOCALAPPDATA 'DockBot'}
    New-Item -ItemType Directory -Path $dataFolder -Force|Out-Null
    $preferencesPath=Join-Path $dataFolder 'preferences.json'
    $preferences=if(Test-Path -LiteralPath $preferencesPath){Get-Content -LiteralPath $preferencesPath -Raw|ConvertFrom-Json}else{[pscustomobject]@{}}
    $preferences|Add-Member -NotePropertyName StartWithWindows -NotePropertyValue $true -Force
    $preferences|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $preferencesPath -Encoding utf8
}
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutShell)
if(!$NoLaunch){Start-Process -FilePath $target -WindowStyle Hidden}
Write-Output ('Installed: '+$target)
