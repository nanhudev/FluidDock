param([switch]$StartWithWindows,[switch]$NoLaunch)
$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'FluidDock.exe'
if(!(Test-Path -LiteralPath $source)){$source=Join-Path $PSScriptRoot 'bin\FluidDock.exe'}
if(!(Test-Path -LiteralPath $source)){throw 'FluidDock.exe not found. Extract the release or build first.'}
$destination=Join-Path $env:LOCALAPPDATA 'Programs\FluidDock'
New-Item -ItemType Directory -Path $destination -Force|Out-Null
$target=Join-Path $destination 'FluidDock.exe'
if([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($target)){Copy-Item -LiteralPath $source -Destination $target -Force}
$shortcutShell=New-Object -ComObject WScript.Shell
$startMenu=Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\FluidDock.lnk'
$link=$shortcutShell.CreateShortcut($startMenu)
$link.TargetPath=$target
$link.WorkingDirectory=$destination
$link.Save()
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)
if($StartWithWindows){
    $startupPath=Join-Path ([Environment]::GetFolderPath('Startup')) 'Fluid Dock.lnk'
    $startup=$shortcutShell.CreateShortcut($startupPath)
    $startup.TargetPath=$target
    $startup.WorkingDirectory=$destination
    $startup.Save()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($startup)
    $dataFolder=Join-Path $env:LOCALAPPDATA 'FluidDock'
    New-Item -ItemType Directory -Path $dataFolder -Force|Out-Null
    $preferencesPath=Join-Path $dataFolder 'preferences.json'
    $preferences=if(Test-Path -LiteralPath $preferencesPath){Get-Content -LiteralPath $preferencesPath -Raw|ConvertFrom-Json}else{[pscustomobject]@{}}
    $preferences|Add-Member -NotePropertyName StartWithWindows -NotePropertyValue $true -Force
    $preferences|ConvertTo-Json -Depth 10|Set-Content -LiteralPath $preferencesPath -Encoding utf8
}
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutShell)
if(!$NoLaunch){Start-Process -FilePath $target -WindowStyle Hidden}
Write-Output ('Installed: '+$target)
