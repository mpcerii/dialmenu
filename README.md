<div align="center">

# DialMenu

**Der Lautstärkeregler deiner Tastatur wird zum Menü.**

Ein radiales Menü für Windows, das den Drehknopf einer mechanischen Tastatur
in einen vollwertigen Programm- und Makro-Starter verwandelt.

![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-1f6feb?style=flat-square)
![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512bd4?style=flat-square)
![Lizenz](https://img.shields.io/badge/Lizenz-MIT-3fb950?style=flat-square)

</div>

---

## Worum es geht

Viele mechanische Tastaturen haben einen Drehknopf, der fest auf Lautstärke
verdrahtet ist. DialMenu fängt diese Signale ab, bevor Windows sie sieht, und
öffnet stattdessen ein Menü: **drehen wählt, drücken führt aus.**

Die Tastatur selbst wird dabei nicht verändert. Kein Flashen, keine Firmware,
keine Treiber. DialMenu hört nur mit — steck die Tastatur ab oder beende das
Programm, und alles ist wieder beim Alten.

```
   Drehen ──▶ HID-Report ──▶ DialMenu ──▶ Aktion
                              │
                              └──▶ Windows sieht keine Lautstärke mehr
```

## Funktionen

- **Radiales Menü im Glas-Look** — elf Segmente, monochrom, mit leuchtendem
  Auswahlrahmen. Skaliert von 70 % bis 180 %, ohne abgeschnitten zu werden.
- **Beliebig viele Befehle** — nicht auf elf festgelegt. Der Ring wächst mit.
- **Programme, Ordner, Webseiten** — inklusive Auswahl aus den tatsächlich
  installierten Programmen statt Pfad-Tippen.
- **Tastenkombinationen** — `Ctrl+Alt+M`, `Win+D`, `Alt+F4` und alles dazwischen.
- **Makros** — Tastenkombinationen, Text und Pausen zu einer Sequenz verketten.
- **Mehrere Profile** — ein Ring für Arbeit, einer fürs Spielen.
- **Mehrere Geräte** — jeder Drehknopf mit eigenem Verhalten.
- **Freie Platzierung** — vier Ecken, sechs Kantenpositionen, Bildschirmmitte
  oder der Monitor unter dem Mauszeiger.
- **Autostart** — optional, mit Prüfung, ob der Eintrag wirklich greift.

## Installation

Es gibt kein Setup. DialMenu ist eine einzelne `.exe`.

1. [Neueste Version herunterladen](../../releases/latest)
2. `DialMenu.exe` in einen Ordner deiner Wahl legen
3. Starten — das Einstellungsfenster öffnet sich
4. Unter **Geräte** prüfen, ob deine Tastatur erkannt wurde
5. Unter **Befehle** zusammenstellen, was im Ring stehen soll

Ab Windows 10, .NET Framework 4.8 (auf Windows 11 vorinstalliert).

## Erste Schritte

**Welche Tastaturen funktionieren?**
Jede, deren Drehknopf Standard-Lautstärketasten sendet. Das trifft auf die
meisten zu. DialMenu zeigt unter **Geräte** alle erkannten Eingabegeräte an.

**Der Knopf reagiert nicht.**
Unter **Geräte** nachsehen, ob das Gerät aktiviert ist. Der Diagnosebereich
zeigt die letzten rohen Signale — wenn dort beim Drehen nichts erscheint,
sendet die Tastatur etwas anderes als erwartet.

**Wie komme ich an die Einstellungen, wenn das Menü den Knopf belegt?**
Über das Symbol im Infobereich. Ein Klick darauf öffnet die Einstellungen.

## Konfiguration

Alles liegt in `%APPDATA%\DialMenu\config.json` — lesbar und versionierbar.
Ein Profil lässt sich kopieren und teilen.

## Selbst bauen

Voraussetzungen: Windows mit .NET Framework 4.8, MSBuild (in Visual Studio
oder den Build Tools enthalten).

```powershell
.\build.ps1
```

Die fertige Datei liegt danach unter `bin\Release\DialMenu.exe`.

## Wie es funktioniert

Zwei Mechanismen greifen ineinander:

**Raw Input** liest die rohen HID-Pakete des Geräts mit. Daraus ergibt sich,
welcher Rastschritt gedreht und wann gedrückt wurde — unabhängig davon, welche
Tasten Windows daraus macht.

**Ein Tastatur-Hook** fängt die daraus entstehenden Lautstärketasten ab. Er
lässt sie nur dann durch, wenn sie *nicht* unmittelbar von unserem Gerät kamen.
Die Lautstärketasten anderer Programme und Tastaturen bleiben unberührt.

Selbst erzeugte Tastendrücke (etwa für „Lauter" als Befehl) werden markiert und
nie abgefangen — sonst würde sich das Programm selbst blockieren.

## Grenzen

- **Windows only.** Die Mechanismen sind Windows-spezifisch.
- **Nur Geräte mit Standard-Media-Tasten.** Exotische Firmware, die eigene
  Protokolle spricht, lässt sich nicht abfangen.
- **Ein Rastschritt weckt das Menü, er wählt noch nicht.** Sonst wäre der erste
  Schritt doppelt belegt.
- **Die Systemlautstärke entfällt am Knopf**, solange DialMenu läuft. Über die
  Lautstärketasten der Tastatur geht sie weiterhin.

## Mitmachen

Fehlerberichte und Vorschläge sind willkommen. Wenn du ein Gerät hast, das
nicht erkannt wird, hilft ein Auszug aus dem Diagnosebereich unter **Geräte**.

## Lizenz

MIT — siehe [LICENSE](LICENSE).
