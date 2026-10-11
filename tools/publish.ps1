<#
.SYNOPSIS
  Baut den Dienst samt ONNX-Plugin und Modellen in einen Ordner, der auf den Zielrechner kopiert werden kann.

.PARAMETER Output
  Zielordner. Standard: artifacts\Kennzeichenerkennung im Repository.
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\Kennzeichenerkennung'),
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

dotnet publish (Join-Path $root 'src\Kennzeichen.Service\Kennzeichen.Service.csproj') -c $Configuration -r win-x64 --self-contained false -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Veröffentlichen des Dienstes fehlgeschlagen.' }

$plugin = Join-Path $Output 'plugins\Kennzeichen.Recognizers.Onnx'
dotnet publish (Join-Path $root 'plugins\Kennzeichen.Recognizers.Onnx\Kennzeichen.Recognizers.Onnx.csproj') -c $Configuration -r win-x64 --self-contained false -o $plugin
if ($LASTEXITCODE -ne 0) { throw 'Veröffentlichen des Plugins fehlgeschlagen.' }

& (Join-Path $PSScriptRoot 'download-models.ps1') -Destination (Join-Path $Output 'models')

Write-Host "Fertig: $Output"
