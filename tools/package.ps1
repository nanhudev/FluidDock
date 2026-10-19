param([string]$Version='0.1.0')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'build.ps1') -Test
$stage=Join-Path $root ('dist\FluidDock-'+$Version+'-windows')
if(Test-Path -LiteralPath $stage){throw 'Release staging directory already exists. Choose a new version or remove it explicitly.'}
New-Item -ItemType Directory -Path $stage -Force|Out-Null
foreach($name in @('README.md','LICENSE','Install.ps1')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $stage}
Copy-Item -LiteralPath (Join-Path $root 'bin\FluidDock.exe') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $stage 'docs') -Recurse
$zip=$stage+'.zip'
Compress-Archive -LiteralPath $stage -DestinationPath $zip
Get-FileHash -LiteralPath $zip -Algorithm SHA256|ForEach-Object {$_.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($zip)}|Set-Content -LiteralPath ($zip+'.sha256') -Encoding ascii
Write-Output $zip
