# Compile "Travail comme Papa" en un seul exécutable dans .\publish
# Usage : clic droit > Exécuter avec PowerShell, ou  ./build.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

dotnet publish "$root\TravailCommePapa\TravailCommePapa.csproj" `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "$root\publish"

Write-Host ""
Write-Host "OK : $root\publish\TravailCommePapa.exe" -ForegroundColor Green
