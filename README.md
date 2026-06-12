# Ember — CodeBurn für Windows

Ember bringt die [CodeBurn](https://github.com/getagentseal/codeburn)-Kostenübersicht in den
Windows-11-System-Tray — als Pendant zur macOS-Menubar-App. Ein kleines Icon in der Taskleiste
zeigt die heutigen AI-Coding-Kosten; ein Klick öffnet ein Flyout mit Tages- und Monatsübersicht,
Top-Modellen und Top-Projekten.

## Installation (Anwender)

Eine Zeile in PowerShell — von GitHub:

```powershell
irm https://raw.githubusercontent.com/MAITMarcoplatzer/Ember/main/install.ps1 | iex
```

oder MAIT-intern:

```powershell
irm https://git.mait.de/AI.Network/Ember/raw/branch/main/install.ps1 | iex
```

Das Skript

1. installiert `codeburn` per npm, falls es fehlt (Node.js 20+ wird vorausgesetzt),
2. lädt die aktuelle `Ember.exe` aus dem neuesten Release nach `%LOCALAPPDATA%\Ember`,
3. erstellt eine Startmenü-Verknüpfung und aktiviert den Autostart,
4. startet die App.

Deinstallation: `uninstall.ps1` aus dem Repo ausführen.

> **Hinweis für MAIT-verwaltete Geräte:** Die zentral verwaltete Defender-ASR-Regel
> "Block executable files unless they meet a prevalence, age, or trusted list criterion"
> (`01443614-CD74-433A-B99E-2ECDC07BFC25`) blockiert unsignierte Exe-Dateien. Für den
> Rollout muss die IT die Exe entweder mit dem Firmen-Codesigning-Zertifikat signieren,
> per Intune als Managed-Installer-App verteilen oder eine ASR-Ausnahme für
> `%LOCALAPPDATA%\Ember\Ember.exe` pflegen. Für Entwicklungs-/Testzwecke läuft die App
> alternativ über den signierten .NET-Host: `dotnet Ember.dll` (framework-abhängiger Build).

## Bedienung

| Aktion | Ergebnis |
|---|---|
| Linksklick auf das Tray-Icon | Flyout mit Heute/Monat, Top-Modellen, Top-Projekten |
| Rechtsklick | Menü: Aktualisieren, Dashboard öffnen, Autostart, Beenden |
| "Dashboard öffnen" | startet das volle CodeBurn-TUI (`codeburn report`) im Terminal |

Die Daten werden alle 30 Sekunden aktualisiert. Währung und Beträge kommen direkt aus
CodeBurn — `codeburn currency EUR` stellt die Anzeige auf Euro um.

## Architektur

- **.NET 8 / WPF**, Single-File-Exe, keine weiteren Laufzeitabhängigkeiten
- Datenbezug ausschließlich über die CodeBurn-CLI: `codeburn today|month --format json`
  (keine eigene Preislogik, automatisch kompatibel mit CodeBurn-Updates)
- Tray-Icon wird zur Laufzeit mit dem aktuellen Tagesbetrag gerendert (GDI+)
- Flyout folgt dem Windows-Theme (hell/dunkel) und nutzt abgerundete Win11-Ecken (DWM)
- Autostart über `HKCU\...\CurrentVersion\Run`

```
src/Ember/
  App.xaml(.cs)          Einstieg, Tray-Icon, Kontextmenü, 30s-Refresh
  FlyoutWindow.xaml(.cs) Flyout-UI (Heute/Monat, Balken-Breakdowns)
  CodeburnClient.cs      CLI-Aufruf + JSON-Parsing
  TrayIconFactory.cs     dynamisches Kosten-Icon
  MoneyFormat.cs         Währungsformatierung (de-DE)
  Theme.cs               Hell/Dunkel-Erkennung
  Autostart.cs           Run-Key-Verwaltung
```

## Entwicklung

```powershell
winget install Microsoft.DotNet.SDK.8
.\build.ps1          # erzeugt dist\Ember.exe
```

Release veröffentlichen: Tag erstellen, `build.ps1` ausführen, `dist\Ember.exe` als
Asset an das Release hängen (Gitea und/oder GitHub) — `install.ps1` probiert beide
Quellen in dieser Reihenfolge: MAIT-Gitea, dann GitHub.

Das Projekt wird parallel gepflegt auf [git.mait.de/AI.Network/Ember](https://git.mait.de/AI.Network/Ember)
(MAIT-intern) und [github.com/MAITMarcoplatzer/Ember](https://github.com/MAITMarcoplatzer/Ember) (öffentlich).

## Roadmap

- [ ] 7-Tage-Zeitraum (sobald über die CLI abfragbar)
- [ ] Budget + Monatsend-Prognose im Flyout
- [ ] Sparkline der letzten 14 Tage
- [ ] Einstellungsdialog (Icon-Stil, Intervall)
- [ ] winget-Paket / Upstream-Contribution als `windows/` im CodeBurn-Repo
