<#
.SYNOPSIS
  Registriert den veröffentlichten Dienst in Windows und richtet automatischen Neustart bei Fehlern ein.
  Als Administrator ausführen. Vorgehen nach
  https://learn.microsoft.com/dotnet/core/extensions/windows-service

.PARAMETER InstallDirectory
  Ordner mit Kennzeichen.Service.exe (Ausgabe von tools\publish.ps1).
#>
param(
    [Parameter(Mandatory)] [string]$InstallDirectory,
    [string]$ServiceName = 'Kennzeichenerkennung'
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path (Resolve-Path $InstallDirectory) 'Kennzeichen.Service.exe'
if (-not (Test-Path $exe)) { throw "Nicht gefunden: $exe" }

# Quelle für die Ereignisanzeige anlegen (Name wie Logging:EventLog:SourceName in appsettings.json).
if (-not [System.Diagnostics.EventLog]::SourceExists($ServiceName)) {
    [System.Diagnostics.EventLog]::CreateEventSource($ServiceName, 'Application')
}

sc.exe create $ServiceName binpath= "`"$exe`"" start= delayed-auto
if ($LASTEXITCODE -ne 0) { throw 'sc.exe create fehlgeschlagen.' }

sc.exe description $ServiceName 'Kfz-Kennzeichenerkennung von Netzwerkkamera'

# Nach einem Absturz nach 60 s neu starten; Fehlerzähler täglich zurücksetzen.
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

Write-Host "Dienst '$ServiceName' angelegt. Starten mit: sc.exe start $ServiceName"
