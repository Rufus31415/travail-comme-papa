# Compile "Travail comme Papa" en un seul exécutable autonome (runtime .NET inclus) dans .\publish
# Usage : clic droit > Exécuter avec PowerShell, ou  ./build.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

dotnet publish "$root\TravailCommePapa\TravailCommePapa.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -o "$root\publish"

Write-Host ""
Write-Host "OK : $root\publish\TravailCommePapa.exe" -ForegroundColor Green
