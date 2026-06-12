# Baut Ember als eigenstaendige Single-File-Exe nach .\dist
# Voraussetzung: .NET 8 SDK (winget install Microsoft.DotNet.SDK.8)
$ErrorActionPreference = 'Stop'

dotnet publish "$PSScriptRoot\src\Ember\Ember.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$PSScriptRoot\dist"

Write-Host ""
Write-Host "Fertig: $PSScriptRoot\dist\Ember.exe" -ForegroundColor Green
