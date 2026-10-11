<#
.SYNOPSIS
  Lädt die ONNX-Modelle für den Erkenner "Onnx" herunter und prüft ihre SHA-256-Prüfsummen.

.DESCRIPTION
  Die Modelle werden nicht im Repository gespeichert, weil die Lizenz der Modellgewichte nicht
  abschließend geklärt ist (siehe README). Quellen:
  - Detektor: https://github.com/ankandrew/open-image-models (Release "assets")
  - OCR:      https://github.com/ankandrew/fast-plate-ocr (Modelle im Release "arg-plates" von ankandrew/cnn-ocr-lp)

.PARAMETER Destination
  Zielordner. Standard: models\ im Repository.
#>
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\models')
)

$ErrorActionPreference = 'Stop'

$models = @(
    @{ Url = 'https://github.com/ankandrew/open-image-models/releases/download/assets/yolo-v9-t-384-license-plates-end2end.onnx'
       Sha256 = '888397b96d761c89db40bc9c305838e8652660f5e282c2cadebbe8d2951a77a8' }
    @{ Url = 'https://github.com/ankandrew/cnn-ocr-lp/releases/download/arg-plates/cct_xs_v2_global.onnx'
       Sha256 = '8031afb5fdc6b4d80462c9d542f1284ebd2cfddf5dbacd62609848d7e2855f44' }
    @{ Url = 'https://github.com/ankandrew/cnn-ocr-lp/releases/download/arg-plates/cct_xs_v2_global_plate_config.yaml'
       Sha256 = '0335c74a305173bb6f393efed0fde03cadeaa0b649ed8e19f431016d8232d0a6' }
    @{ Url = 'https://github.com/ankandrew/cnn-ocr-lp/releases/download/arg-plates/european_mobile_vit_v2_ocr.onnx'
       Sha256 = '5f388f57ddec318d38d17e420d292f5a049595bec93f111838903f6617f6943f' }
    @{ Url = 'https://github.com/ankandrew/cnn-ocr-lp/releases/download/arg-plates/european_mobile_vit_v2_ocr_config.yaml'
       Sha256 = '202b90f2a1f3a0d486b8f1fbd5cdc2f19f4f4881a094f086b983976ed3108aaf' }
)

New-Item -ItemType Directory -Force -Path $Destination | Out-Null

foreach ($model in $models) {
    $file = Join-Path $Destination ([IO.Path]::GetFileName($model.Url))
    if (-not (Test-Path $file) -or (Get-FileHash $file -Algorithm SHA256).Hash -ne $model.Sha256) {
        Write-Host "Lade $($model.Url)"
        Invoke-WebRequest -Uri $model.Url -OutFile $file -UseBasicParsing
    }

    $hash = (Get-FileHash $file -Algorithm SHA256).Hash
    if ($hash -ne $model.Sha256) {
        throw "Prüfsumme stimmt nicht für $file (erwartet $($model.Sha256), erhalten $hash)."
    }

    Write-Host "OK  $file"
}
