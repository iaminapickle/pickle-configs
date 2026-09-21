# Fresh Windows box -> configured. Run from an elevated PowerShell:
#
#   irm https://raw.githubusercontent.com/iaminapickle/pickle-configs/main/bootstrap.ps1 | iex
#
# STATUS: scaffold. Package install and a PowerShell profile aren't ported
# yet; SSH keys, git identity, and GlazeWM + Zebar already work.

$ErrorActionPreference = 'Stop'

function Log($msg) { Write-Host ":: $msg" -ForegroundColor Magenta }

if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
    throw "winget not found. Install 'App Installer' from the Microsoft Store first."
}

Log "installing chezmoi and age"
winget install --silent --accept-package-agreements --accept-source-agreements twpayne.chezmoi
winget install --silent --accept-package-agreements --accept-source-agreements FiloSottile.age

$keyFile = Join-Path $env:USERPROFILE ".config\chezmoi\key.txt"
if (-not (Test-Path $keyFile)) {
    Write-Warning "No age identity at $keyFile -- encrypted files will be skipped."
    Write-Warning "Copy it over, then run: chezmoi apply"
}

Log "initialising chezmoi"
chezmoi init --apply https://github.com/iaminapickle/pickle-configs.git

Log "done."
