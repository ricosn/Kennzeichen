# Kennzeichen

Kfz-Kennzeichenerkennung als Windows-Dienst. Der Dienst holt Bilder von einer Netzwerkkamera (RTSP oder
HTTP-Einzelbild), lässt sie von einem austauschbaren Erkenner-Plugin auswerten und schreibt die Ergebnisse ins
Log (Ereignisanzeige) und in eine JSON-Datei pro Tag.

```
Kamera ──► IFrameSource ──► RecognitionWorker ──► IPlateRecognizer (Plugin) ──► IResultSink
           RTSP / Snapshot   Filter, Wiederholungen       z. B. "Onnx"              Log, JSON-Datei
```

## Aufbau

| Projekt | Inhalt |
|---|---|
| `src/Kennzeichen.Abstractions` | Schnittstellen `IFrameSource`, `IPlateRecognizer`, `IPlateRecognizerFactory`, `IResultSink` |
| `src/Kennzeichen.Service` | Windows-Dienst (.NET 10 Worker Service), Plugin-Lader, Filter, Ausgaben |
| `src/Kennzeichen.Sources.OpenCv` | Kamera-Anbindung über OpenCvSharp: RTSP-Stream und HTTP-Snapshot |
| `plugins/Kennzeichen.Recognizers.Onnx` | Erster Erkenner: YOLOv9-Kennzeichendetektor + fast-plate-ocr über ONNX Runtime (CPU) |
| `tests/Kennzeichen.Tests` | Unit-Tests und ein Vergleich mit der Python-Referenzimplementierung |
| `tools/` | Modelle laden, veröffentlichen, Dienst registrieren (PowerShell) |

## Schnellstart (Windows)

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
# 1. Bauen, Plugin und Modelle in einen Ordner legen
./tools/publish.ps1 -Output C:\Kennzeichenerkennung

# 2. Kamera in C:\Kennzeichenerkennung\appsettings.json eintragen (siehe unten)

# 3. Zum Ausprobieren im Vordergrund starten
C:\Kennzeichenerkennung\Kennzeichen.Service.exe

# 4. Als Dienst registrieren (PowerShell als Administrator)
./tools/install-service.ps1 -InstallDirectory C:\Kennzeichenerkennung
sc.exe start Kennzeichenerkennung
```

Ergebnisse landen in `C:\Kennzeichenerkennung\results\kennzeichen-JJJJ-MM-TT.json`, ein JSON-Array pro Tag, z. B.:

```json
[
  {
    "sourceId": "einfahrt",
    "timestamp": "2026-10-11T14:03:02.31+00:00",
    "recognizerName": "Onnx",
    "reading": {
      "text": "5AU 5341",
      "textConfidence": 0.9998,
      "detectionConfidence": 0.8428,
      "box": { "x": 307, "y": 187, "width": 80, "height": 23 },
      "region": "Czech Republic",
      "compactText": "5AU5341"
    }
  }
]
```

## Konfiguration (`appsettings.json`)

### Kamera (Axis)

```json
"Camera": {
  "Type": "Rtsp",
  "Name": "einfahrt",
  "Url": "rtsp://192.168.0.90/axis-media/media.amp",
  "Username": "benutzer",
  "Password": "passwort",
  "IntervalMilliseconds": 250,
  "ReconnectDelaySeconds": 5,
  "HardwareDecoding": true
}
```

- `Rtsp`: Axis dokumentiert `rtsp://<user>:<pass>@<ip>/axis-media/media.amp`, mit `?camera=2` für einen anderen Kanal
  ([Axis: Video streaming – getting started](https://developer.axis.com/video-streaming-and-recording/video-streaming/getting-started/)).
  Benutzer und Passwort werden aus `Username`/`Password` in die URL eingesetzt. Es wird immer das neueste Bild
  ausgewertet, höchstens eines pro `IntervalMilliseconds`.
- `Snapshot`: fragt alle `IntervalMilliseconds` ein JPEG per HTTP ab (Basic- oder Digest-Anmeldung). Die passende
  Einzelbild-URL steht in der Dokumentation des Kameramodells.
- `Folder`: liest Bilddateien (`.jpg`, `.jpeg`, `.png`, `.bmp`) aus dem Ordner in `Url`, erst alle vorhandenen, dann jede
  neu hinzugefügte. Zum Testen ohne Kamera; jedes Bild wird einzeln gemeldet. Beispiel ohne Änderung der Datei:
  `Kennzeichen.Service.exe --Camera:Type=Folder --Camera:Url=testbilder`

Zugangsdaten können statt in der Datei auch als Umgebungsvariablen gesetzt werden: `Camera__Username`, `Camera__Password`.

#### CPU-Last beim RTSP-Stream

Jedes Bild des Streams muss dekodiert werden, auch wenn nur alle `IntervalMilliseconds` eines ausgewertet wird. Der Aufwand
wächst mit Auflösung und Bildrate. Was hilft:

1. **Weniger Bilder und Pixel von der Kamera anfordern.** Axis-Kameras nehmen das in der URL an, z. B.
   `rtsp://192.168.0.90/axis-media/media.amp?resolution=1280x720&fps=10`. `fps` ist die Bildrate (0 = unbegrenzt),
   `resolution` ist modellabhängig ([VAPIX Video Streaming API, Abschnitt 3.4](https://wiki.2n.com/hip/inte/files/latest/en/25340932/25340931/1/1519853908027/vapix_video_streaming_52937_en_1307.pdf),
   Axis-Dokument von 2013, auf aktuellem Modell prüfen). Das Kennzeichen sollte im Bild weiterhin gut lesbar sein.
2. `HardwareDecoding` (Standard `true`): dekodiert über die Grafikeinheit (unter Windows D3D11), falls OpenCV eine findet;
   sonst in Software. Welche Art aktiv ist, steht beim Verbinden im Log („Hardware-Dekodierung: D3D11“ oder „None“).
3. `IntervalMilliseconds` erhöhen: Jede Erkennung kostet Rechenzeit; 500 statt 250 halbiert die Zahl der Erkennungen.

Der Dienst rechnet nur die Bilder in BGR um, die er auch auswertet, und ONNX Runtime wartet nicht mehr aktiv auf Arbeit
(`AllowSpinning`, Standard `false`; laut [ONNX-Runtime-Doku](https://onnxruntime.ai/docs/performance/tune-performance/threading.html)
kostet Spinning „more CPU cycles“).

### Erkenner

```json
"Recognizer": {
  "Name": "Onnx",
  "PluginDirectory": "plugins",
  "Settings": {
    "DetectorModel": "models/yolo-v9-t-384-license-plates-end2end.onnx",
    "OcrModel": "models/cct_xs_v2_global.onnx",
    "OcrConfig": "models/cct_xs_v2_global_plate_config.yaml",
    "DetectionThreshold": 0.25,
    "InsertSpaces": true,
    "SpaceGapFactor": 2.5,
    "IntraOpNumThreads": 0,
    "AllowSpinning": false
  }
}
```

**Leerzeichen** (`InsertSpaces`, Standard `true`): Der Erkenner sucht die einzelnen Zeichen im Kennzeichenausschnitt und setzt
ein Leerzeichen, wo der Abstand mehr als `SpaceGapFactor` (Standard 2,5) mal so groß ist wie der mittlere Abstand, z. B.
`M AB 1234`. Plaketten zwischen Bezirk und Buchstaben werden dabei übergangen, weil sie niedriger sind als die Zeichen.
Zwischen Buchstaben und Ziffern steht immer ein Leerzeichen. Stimmt die Zahl der gefundenen Zeichen nicht mit dem gelesenen
Text überein (bei kleinen Kennzeichen zerfallen Zeichen oder wachsen zusammen), sucht der Erkenner die Lücken in der Schrift
(ab 0,35 Zeichenhöhen) und ordnet sie über ihre Lage einer Textstelle zu; ein zusätzliches Leerzeichen setzt er dabei nur
innerhalb einer Buchstabengruppe, nie zwischen Ziffern. Findet er keine solche Lücke, bleibt es beim Leerzeichen vor den Ziffern (`MAB 1234`). Für Vergleiche und den Wiederholungsfilter zählt der Text ohne Leerzeichen (`compactText`).

Alternativ das europäische OCR-Modell: `european_mobile_vit_v2_ocr.onnx` mit `european_mobile_vit_v2_ocr_config.yaml`.

### Ergebnisse

| Schlüssel | Bedeutung |
|---|---|
| `MinTextConfidence` | Mittlere Zeichen-Konfidenz, ab der gemeldet wird (Standard 0,5) |
| `MinTextLength` | Mindestlänge des Textes (Standard 3) |
| `SuppressRepeatSeconds` | Dasselbe Kennzeichen derselben Kamera wird erst wieder gemeldet, wenn es so lange nicht gesehen wurde |
| `JsonDirectory` | Ordner für die JSON-Dateien (eine pro Tag); leer schaltet die Dateiausgabe ab |

## Eigenen Erkenner einbauen

1. Klassenbibliothek (`net10.0`) anlegen, `<EnableDynamicLoading>true</EnableDynamicLoading>` setzen.
2. `Kennzeichen.Abstractions` mit `<Private>false</Private>` und `<ExcludeAssets>runtime</ExcludeAssets>` referenzieren.
3. `IPlateRecognizerFactory` (öffentlich, parameterloser Konstruktor) und `IPlateRecognizer` implementieren.
4. Ausgabe nach `plugins/<Projektname>/` kopieren und in `Recognizer:Name` den `Name` der Factory eintragen.

Jedes Plugin wird in einem eigenen `AssemblyLoadContext` geladen und darf eigene Versionen seiner Abhängigkeiten
mitbringen. Nur `Kennzeichen.Abstractions` und `Microsoft.Extensions.*` kommen vom Dienst
([Microsoft: Create a .NET application with plugins](https://learn.microsoft.com/dotnet/core/tutorials/creating-app-with-plugin-support)).
Plugins laufen im Dienstprozess und sind keine Sicherheitsgrenze; nur vertrauenswürdige Plugins einsetzen.

## Modelle und Lizenzen

Die Modelle werden mit `tools/download-models.ps1` geladen und per SHA-256 geprüft; sie liegen nicht im Repository.

| Teil | Quelle | Lizenz |
|---|---|---|
| Detektor `yolo-v9-t-384-license-plates-end2end` | [open-image-models](https://github.com/ankandrew/open-image-models) | Repository MIT. Lizenz der Gewichte dort nicht separat angegeben; das ursprüngliche [YOLOv9](https://github.com/WongKinYiu/yolov9) steht unter GPL-3.0. **Vor kommerzieller Nutzung klären.** |
| OCR `cct_xs_v2_global`, `european_mobile_vit_v2_ocr` | [fast-plate-ocr](https://github.com/ankandrew/fast-plate-ocr) | Repository MIT; Lizenz der Gewichte nicht separat angegeben |
| ONNX Runtime | [microsoft/onnxruntime](https://github.com/microsoft/onnxruntime) | MIT |
| OpenCvSharp | [shimat/opencvsharp](https://github.com/shimat/opencvsharp) | Apache-2.0 |
| YamlDotNet | [aaubry/YamlDotNet](https://github.com/aaubry/YamlDotNet) | MIT |

Bekannte Grenzen der OCR-Modelle (aus ihren Konfigurationsdateien): Das Alphabet umfasst nur `0-9` und `A-Z`, also keine
Umlaute; das globale Modell liest bis zu 10, das europäische bis zu 9 Zeichen. Leerzeichen kennen die Modelle nicht; der
Erkenner setzt sie selbst (siehe unten). Wie zuverlässig deutsche Kennzeichen unter Ihren Bedingungen erkannt werden, muss mit eigenen Aufnahmen
gemessen werden.

## Entwickeln

```bash
dotnet build
pwsh ./tools/download-models.ps1   # für die Integrationstests
dotnet test
```

Die Integrationstests vergleichen Position und Text auf `tests/Kennzeichen.Tests/TestData/fast-alpr-test_image.png`
mit der Python-Referenz (fast-alpr 0.4.0). Fehlen die Modelle in `models/`, werden sie übersprungen.
