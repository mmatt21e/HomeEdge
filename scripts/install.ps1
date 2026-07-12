<#
.SYNOPSIS
    HomeStock installer / upgrader for Windows. Downloads a released, self-contained build
    (no .NET SDK required), installs it, and optionally registers a Windows service.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File install.ps1
    powershell -ExecutionPolicy Bypass -File install.ps1 -Dir 'C:\HomeStock' -Port 8080 -Service

.PARAMETER Version   Release tag to install (default: latest)
.PARAMETER Dir       Install directory (default: %LOCALAPPDATA%\HomeStock)
.PARAMETER Port      HTTP port (default: 8080)
.PARAMETER Repo      Source repo (default: mmatt21e/HomeEdge)
.PARAMETER Service   Register + start a Windows service (requires an elevated shell)
.PARAMETER AdminPassword  First-run admin password
#>
param(
    [string]$Version = 'latest',
    [string]$Dir = "$env:LOCALAPPDATA\HomeStock",
    [int]$Port = 8080,
    [string]$Repo = 'mmatt21e/HomeEdge',
    [switch]$Service,
    [string]$AdminPassword = ''
)

$ErrorActionPreference = 'Stop'

# Detect architecture -> RID
$arch = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$rid = "win-$arch"

# Resolve version
if ($Version -eq 'latest') {
    Write-Host "Resolving latest release of $Repo ..."
    $rel = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/latest" -Headers @{ 'User-Agent' = 'homestock-installer' }
    $tag = $rel.tag_name
} else {
    $tag = $Version
}
$ver = $tag.TrimStart('v')
$asset = "homestock-$ver-$rid.zip"
$url = "https://github.com/$Repo/releases/download/$tag/$asset"

Write-Host "Installing HomeStock $tag ($rid) to $Dir"
New-Item -ItemType Directory -Force -Path "$Dir\data" | Out-Null

$tmp = Join-Path $env:TEMP $asset
Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $tmp -Headers @{ 'User-Agent' = 'homestock-installer' }

# Verify checksum if available
try {
    $sumsUrl = "https://github.com/$Repo/releases/download/$tag/SHA256SUMS-$ver.txt"
    $sums = (Invoke-WebRequest -Uri $sumsUrl -Headers @{ 'User-Agent' = 'homestock-installer' }).Content
    $expected = ($sums -split "`n" | Where-Object { $_ -match [regex]::Escape($asset) }) -split '\s+' | Select-Object -First 1
    $actual = (Get-FileHash $tmp -Algorithm SHA256).Hash.ToLower()
    if ($expected -and ($expected.ToLower() -ne $actual)) { throw "Checksum mismatch for $asset" }
    if ($expected) { Write-Host "Checksum OK" }
} catch { Write-Host "Checksum verification skipped: $_" }

# Swap app directory
if (Test-Path "$Dir\app") { Remove-Item "$Dir\app" -Recurse -Force }
Expand-Archive -Path $tmp -DestinationPath "$Dir\app" -Force
Remove-Item $tmp -Force

$exe = "$Dir\app\HomeStock.Web.exe"
$connEnv = "Data Source=$Dir\data\homestock.db;Cache=Shared"

if ($Service) {
    Write-Host "Registering Windows service (requires elevation) ..."
    $bin = "`"$exe`""
    sc.exe create HomeStock binPath= $bin start= auto | Out-Null
    # Service environment is best configured via a wrapper; for simplicity set machine env vars.
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', "http://0.0.0.0:$Port", 'Machine')
    [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', $connEnv, 'Machine')
    [Environment]::SetEnvironmentVariable('Storage__AttachmentsPath', "$Dir\data\attachments", 'Machine')
    if ($AdminPassword) { [Environment]::SetEnvironmentVariable('Seed__AdminPassword', $AdminPassword, 'Machine') }
    Start-Service HomeStock
    Write-Host "Service started. Open http://<this-host-ip>:$Port"
} else {
    Write-Host ""
    Write-Host "Run it with:"
    Write-Host "  `$env:ASPNETCORE_URLS='http://0.0.0.0:$Port'"
    Write-Host "  `$env:ConnectionStrings__DefaultConnection='$connEnv'"
    Write-Host "  `$env:Storage__AttachmentsPath='$Dir\data\attachments'"
    if ($AdminPassword) { Write-Host "  `$env:Seed__AdminPassword='$AdminPassword'" }
    Write-Host "  & '$exe'"
    Write-Host ""
    Write-Host "Then open http://<this-host-ip>:$Port"
}
