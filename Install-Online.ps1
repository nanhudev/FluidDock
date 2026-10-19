$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$release=Invoke-RestMethod -Uri 'https://raw.githubusercontent.com/nanhudev/dockbot/main/release.json'
if($release.tag_name -notmatch '^v\d+\.\d+\.\d+(-[a-z0-9.]+)?$'){throw 'Invalid DockBot release manifest.'}
$downloadRoot='https://github.com/nanhudev/dockbot/releases/download/'+$release.tag_name
$legacy=Join-Path $env:LOCALAPPDATA 'FluidDock'
$destination=Join-Path $env:LOCALAPPDATA 'Programs\DockBot'
New-Item -ItemType Directory -Path $destination -Force|Out-Null
$temporary=Join-Path $destination 'DockBot.exe.download'
Invoke-WebRequest -UseBasicParsing -Uri ($downloadRoot+'/DockBot.exe') -OutFile $temporary
$response=(Invoke-WebRequest -UseBasicParsing -Uri ($downloadRoot+'/DockBot.exe.sha256')).Content
$text=if($response -is [byte[]]){[Text.Encoding]::UTF8.GetString($response)}else{[string]$response}
$expected=$text.Trim().Split(' ')[0]
$algorithm=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($temporary)
try{$actual=[BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$algorithm.Dispose()}
if($expected -notmatch '^[a-fA-F0-9]{64}$' -or $actual -ne $expected){throw 'The download checksum did not match. Please retry.'}
$executable=Join-Path $destination 'DockBot.exe'
$shell=New-Object -ComObject WScript.Shell
$shortcutPath=Join-Path ([Environment]::GetFolderPath('Programs')) 'DockBot.lnk'
$startupPath=Join-Path ([Environment]::GetFolderPath('Startup')) 'DockBot.lnk'
$profileArguments='';$previousExecutable=''
foreach($existing in @($startupPath,$shortcutPath)){
    if(Test-Path -LiteralPath $existing){$oldLink=$shell.CreateShortcut($existing);if($oldLink.TargetPath -like '*\DockBot.exe'){$previousExecutable=$oldLink.TargetPath};if($oldLink.Arguments -match '^--profile-dir "[^"]+"$'){$profileArguments=$oldLink.Arguments};[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($oldLink)}
}
foreach($process in Get-Process DockBot,FluidDock -ErrorAction SilentlyContinue){
    try{if($process.Path -in @($executable,$previousExecutable,(Join-Path $legacy 'DockBot.exe'),(Join-Path $legacy 'FluidDock.exe'))){[void]$process.CloseMainWindow();if(!$process.WaitForExit(3000)){$process.Kill();$process.WaitForExit()}}}catch{}
}
Move-Item -LiteralPath $temporary -Destination $executable -Force
Unblock-File -LiteralPath $executable
$shortcut=$shell.CreateShortcut($shortcutPath);$shortcut.TargetPath=$executable;$shortcut.Arguments=$profileArguments;$shortcut.WorkingDirectory=$destination;$shortcut.Save()
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
$startupPath=Join-Path ([Environment]::GetFolderPath('Startup')) 'DockBot.lnk'
if(Test-Path -LiteralPath $startupPath){$startup=$shell.CreateShortcut($startupPath);$startup.TargetPath=$executable;$startup.Arguments=$profileArguments;$startup.WorkingDirectory=$destination;$startup.Save();[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($startup)}
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
$launch=@{FilePath=$executable;WorkingDirectory=$destination;WindowStyle='Hidden'};if($profileArguments){$launch.ArgumentList=$profileArguments};Start-Process @launch
Write-Host ('DockBot '+$release.tag_name+' is ready.')
