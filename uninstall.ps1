# Entfernt Ember vollstaendig (App, Verknuepfung, Autostart)
$ErrorActionPreference = 'SilentlyContinue'

Get-Process Ember | Stop-Process -Force
Start-Sleep -Milliseconds 300
Remove-Item -Recurse -Force (Join-Path $env:LOCALAPPDATA 'Ember')
Remove-Item -Force (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Ember.lnk')
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Ember'

Write-Host "Ember wurde entfernt." -ForegroundColor Green
