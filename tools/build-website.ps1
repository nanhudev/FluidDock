$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'dist/website'
New-Item -ItemType Directory -Path (Join-Path $output 'assets') -Force | Out-Null
Copy-Item -Path (Join-Path $root 'website/*') -Destination $output -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'assets/buddy.svg') -Destination (Join-Path $output 'assets/buddy.svg') -Force
foreach ($name in @('overview.png', 'setup.png', 'cleanup.png')) {
    Copy-Item -LiteralPath (Join-Path $root "docs/media/$name") -Destination (Join-Path $output "assets/$name") -Force
}
Write-Output "Website built: $output"
