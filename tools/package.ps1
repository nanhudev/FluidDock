param([string]$Version='0.1.1',[switch]$Refresh)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'build.ps1') -Test
if($Version -notmatch '^\d+\.\d+\.\d+(-[a-z0-9.]+)?$'){throw 'Invalid release version.'}
$stage=Join-Path $root ('dist\staging-'+[Guid]::NewGuid().ToString('N')+'\DockBot-'+$Version+'-windows')
if((Test-Path -LiteralPath $stage) -and !$Refresh){throw 'Release staging directory already exists. Use -Refresh to overwrite generated release files.'}
New-Item -ItemType Directory -Path $stage -Force|Out-Null
foreach($name in @('README.md','LICENSE','Install.ps1')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $stage}
Copy-Item -LiteralPath (Join-Path $root 'bin\DockBot.exe') -Destination $stage -Force
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $stage 'docs') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'assets') -Destination (Join-Path $stage 'assets') -Recurse -Force
$zip=Join-Path $root ('dist\DockBot-'+$Version+'-windows.zip')
Compress-Archive -LiteralPath $stage -DestinationPath $zip -Force:$Refresh
$algorithm=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($zip)
try{$hash=[BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$algorithm.Dispose()}
($hash+'  '+[IO.Path]::GetFileName($zip))|Set-Content -LiteralPath ($zip+'.sha256') -Encoding ascii
Write-Output $zip
