param([string]$BaseUrl = 'https://dockbot.bubbleapp.cn')
$ErrorActionPreference = 'Stop'
if ($BaseUrl -notmatch '^https://[a-zA-Z0-9.-]+(?:/[a-zA-Z0-9_-]+)*$') { throw 'Expected an HTTPS website origin with optional path.' }
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'dist/website'
New-Item -ItemType Directory -Path (Join-Path $output 'assets') -Force | Out-Null
Copy-Item -Path (Join-Path $root 'website/*') -Destination $output -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'assets/buddy.svg') -Destination (Join-Path $output 'assets/buddy.svg') -Force
foreach ($name in @('overview.png', 'setup.png', 'cleanup.png')) {
    Copy-Item -LiteralPath (Join-Path $root "docs/media/$name") -Destination (Join-Path $output "assets/$name") -Force
}
foreach ($name in @('index.html', 'robots.txt', 'sitemap.xml', 'llms.txt')) {
    $file = Join-Path $output $name
    $text = [IO.File]::ReadAllText($file).Replace('https://dockbot.bubbleapp.cn', $BaseUrl)
    if ($name -eq 'index.html') { $text = $text.Replace('href="/"', ('href="' + $BaseUrl + '/"')) }
    [IO.File]::WriteAllText($file, $text, (New-Object Text.UTF8Encoding($false)))
}
Write-Output "Website built: $output"
