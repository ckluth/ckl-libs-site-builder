param(
    [Parameter(Mandatory = $true)][string]$RepoRoot,
    [Parameter(Mandatory = $true)][string]$NotesOut
)

# -------------------------------------------------------
# resolve-release.ps1 - building block for release.cmd.
# Resolves the release version from the main project .csproj and
# extracts the matching CHANGELOG.md section into a notes file.
# Prints ONLY the version to stdout on success; all diagnostics go
# to stderr. Exits non-zero (with nothing on stdout) on any failure.
# Per ADR-0023.
# -------------------------------------------------------

$ErrorActionPreference = 'Stop'

function Fail([string]$msg) {
    [Console]::Error.WriteLine("[ERROR] $msg")
    exit 1
}

# --- locate the main project .csproj (exclude the *.Tests.csproj) ---
$csprojs = Get-ChildItem -Path $RepoRoot -Recurse -Filter *.csproj -File |
    Where-Object { $_.Name -notlike '*.Tests.csproj' -and $_.FullName -notmatch '\\(bin|obj)\\' }
if (-not $csprojs -or $csprojs.Count -eq 0) { Fail "No main .csproj found under '$RepoRoot'." }
if ($csprojs.Count -gt 1) { Fail "Multiple main .csproj found: $($csprojs.FullName -join ', ')." }
$csproj = $csprojs[0].FullName

# --- read <Version> ---
$xml = [xml](Get-Content -Raw -LiteralPath $csproj)
$version = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { Fail "No <Version> element in '$csproj' - nothing to release." }
$version = ([string]$version).Trim()

# --- verify a matching CHANGELOG.md section exists ---
$changelog = Join-Path $RepoRoot 'CHANGELOG.md'
if (-not (Test-Path -LiteralPath $changelog)) { Fail "No CHANGELOG.md at '$changelog'." }
$lines = Get-Content -LiteralPath $changelog

$heading = "^##\s*\[" + [regex]::Escape($version) + "\]"
$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match $heading) { $start = $i; break }
}
if ($start -lt 0) {
    Fail "No '## [$version]' section in CHANGELOG.md (only [Unreleased], or a version mismatch). Move Unreleased into a dated [$version] section first."
}

# --- collect the section body up to the next '## ' heading ---
$body = New-Object System.Collections.Generic.List[string]
for ($j = $start + 1; $j -lt $lines.Count; $j++) {
    if ($lines[$j] -match '^##\s') { break }
    [void]$body.Add($lines[$j])
}
$notes = ($body -join "`n").Trim()
if (-not $notes) { $notes = "Release v$version." }
Set-Content -LiteralPath $NotesOut -Value $notes -Encoding UTF8

# --- success: only the version on stdout ---
[Console]::Out.WriteLine($version)
exit 0
