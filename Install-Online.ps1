$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$headers=@{'User-Agent'='dockbot-installer'}
$releases=Invoke-RestMethod -Uri 'https://api.github.com/repos/nanhudev/dockbot/releases' -Headers $headers
$release=$releases|Where-Object {!$_.draft -and ($_.assets.name -contains 'DockBot.exe') -and ($_.assets.name -contains 'DockBot.exe.sha256')}|Select-Object -First 1
if(!$release){throw 'No packaged DockBot release is available.'}
$legacy=Join-Path $env:LOCALAPPDATA 'FluidDock'
$destination=Join-Path $env:LOCALAPPDATA 'Programs\DockBot'
New-Item -ItemType Directory -Path $destination -Force|Out-Null
$temporary=Join-Path $destination 'DockBot.exe.download'
$asset=$release.assets|Where-Object name -eq 'DockBot.exe'|Select-Object -First 1
$checksum=$release.assets|Where-Object name -eq 'DockBot.exe.sha256'|Select-Object -First 1
Invoke-WebRequest -UseBasicParsing -Uri $asset.browser_download_url -OutFile $temporary
$response=(Invoke-WebRequest -UseBasicParsing -Uri $checksum.browser_download_url).Content
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
