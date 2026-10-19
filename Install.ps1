param([switch]$StartWithWindows,[switch]$NoLaunch)
$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot 'DockBot.exe'
if(!(Test-Path -LiteralPath $source)){$source=Join-Path $PSScriptRoot 'bin\DockBot.exe'}
if(!(Test-Path -LiteralPath $source)){throw 'DockBot.exe not found. Extract the release or build first.'}
$destination=Join-Path $env:LOCALAPPDATA 'Programs\DockBot'
New-Item -ItemType Directory -Path $destination -Force|Out-Null
$target=Join-Path $destination 'DockBot.exe'
$legacyData=Join-Path $env:LOCALAPPDATA 'FluidDock'
$shortcutShell=New-Object -ComObject WScript.Shell
$startupPath=Join-Path ([Environment]::GetFolderPath('Startup')) 'DockBot.lnk'
$profileArguments='';$previousExecutable=''
foreach($existing in @($startupPath,(Join-Path ([Environment]::GetFolderPath('Programs')) 'DockBot.lnk'))){if(Test-Path -LiteralPath $existing){$oldLink=$shortcutShell.CreateShortcut($existing);if($oldLink.TargetPath -like '*\DockBot.exe'){$previousExecutable=$oldLink.TargetPath};if($oldLink.Arguments -match '^--profile-dir "[^"]+"$'){$profileArguments=$oldLink.Arguments};[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($oldLink)}}
foreach($process in Get-Process DockBot,FluidDock -ErrorAction SilentlyContinue){
    if($process.Path -in @($target,$previousExecutable,(Join-Path $legacyData 'DockBot.exe'),(Join-Path $legacyData 'FluidDock.exe'))){
        [void]$process.CloseMainWindow();if(!$process.WaitForExit(3000)){$process.Kill();$process.WaitForExit()}
    }
}
if([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($target)){Copy-Item -LiteralPath $source -Destination $target -Force}
$startMenu=Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\DockBot.lnk'
$link=$shortcutShell.CreateShortcut($startMenu)
$link.TargetPath=$target
$link.Arguments=$profileArguments
$link.WorkingDirectory=$destination
$link.Save()
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)
if($StartWithWindows){
    # Use the application's recovery and atomic-write code, preserving every preference.
    $configure=Start-Process -FilePath $target -ArgumentList ($profileArguments+' --enable-startup-only') -Wait -PassThru -WindowStyle Hidden
    if($configure.ExitCode -ne 0){throw 'Startup preference could not be saved; existing data was preserved.'}
    $startupPath=Join-Path ([Environment]::GetFolderPath('Startup')) 'DockBot.lnk'
    $startup=$shortcutShell.CreateShortcut($startupPath)
    $startup.TargetPath=$target
    $startup.Arguments=$profileArguments
    $startup.WorkingDirectory=$destination
    $startup.Save()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($startup)
}
elseif(Test-Path -LiteralPath $startupPath){$startup=$shortcutShell.CreateShortcut($startupPath);$startup.TargetPath=$target;$startup.Arguments=$profileArguments;$startup.WorkingDirectory=$destination;$startup.Save();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($startup)}
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutShell)
if(!$NoLaunch){$launch=@{FilePath=$target;WindowStyle='Hidden'};if($profileArguments){$launch.ArgumentList=$profileArguments};Start-Process @launch}
Write-Output ('Installed: '+$target)
