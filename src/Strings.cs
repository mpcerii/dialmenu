using System;
using System.Collections.Generic;
using System.Globalization;

namespace DialMenu
{
    // Sprachen. Die Oberflaeche ist deutsch geschrieben; hier liegen die
    // Texte, die uebersetzt werden. Fehlt ein Eintrag, faellt die Anzeige
    // auf den Schluessel zurueck - nie auf eine leere Flaeche.
    public static class Strings
    {
        public static string Current = "de";

        static readonly Dictionary<string, Dictionary<string, string>> Tables =
            new Dictionary<string, Dictionary<string, string>>();

        static Strings()
        {
            Dictionary<string, string> de = new Dictionary<string, string>();
            de["app.settings"] = "Einstellungen";
            de["app.subtitle"] = "Der Lautstärkeregler deiner Tastatur wird zum Menü.";
            de["app.windowTitle"] = "DialMenu – Einstellungen";
            de["app.preview"] = "Im Overlay ansehen";
            de["app.closeNote"] = "Schließen legt das Fenster in den Infobereich.";
            de["app.trayPaused"] = "DialMenu – pausiert";
            de["app.traySettings"] = "Einstellungen";
            de["app.trayAbout"] = "Über DialMenu";
            de["app.trayFolder"] = "Konfigurationsordner öffnen";
            de["app.trayQuit"] = "Beenden";
            de["app.settingsError"] = "Die Einstellungen konnten nicht geöffnet werden.";

            de["nav.commands"] = "Befehle";
            de["nav.profiles"] = "Profile";
            de["nav.devices"] = "Geräte";
            de["nav.appearance"] = "Darstellung";
            de["nav.behavior"] = "Verhalten";
            de["nav.about"] = "Über";

            de["common.save"] = "Speichern";
            de["common.cancel"] = "Abbrechen";
            de["common.add"] = "Hinzufügen";
            de["common.choose"] = "Datei wählen…";
            de["common.folder"] = "Ordner wählen…";
            de["common.search"] = "Suchen";
            de["common.close"] = "Schließen";
            de["common.none"] = "Keine";
            de["common.apply"] = "Übernehmen";
            de["common.rename"] = "Umbenennen";
            de["common.remove"] = "Entfernen";
            de["common.delete"] = "Löschen";
            de["common.duplicate"] = "Duplizieren";
            de["common.activate"] = "Aktivieren";
            de["common.open"] = "Öffnen";
            de["common.reset"] = "Zurücksetzen";

            de["error.pageTitle"] = "Diese Seite ließ sich nicht aufbauen";
            de["error.pageHead"] = "Fehler beim Aufbau von „{0}“";
            de["error.hint"] = "Der Fehler steht im Protokoll. Über „Über“ lässt es sich öffnen.";

            de["icon.title"] = "Symbol wählen";
            de["icon.subtitle"] = "Ohne Auswahl bestimmt DialMenu das Symbol nach Art und Ziel.";
            de["icon.auto"] = "Automatisch";
            de["icon.autoTip"] = "Symbol wieder nach Art und Ziel bestimmen lassen.";
            de["icon.empty"] = "Keine Symbole verfügbar.";
            de["icon.automatic"] = "(automatisch)";

            de["commands.title"] = "Befehle";
            de["commands.subtitle"] = "Die Reihenfolge links entspricht der Drehrichtung am Regler.";
            de["commands.inMenu"] = "Im Menü";
            de["commands.profile"] = "Profil";
            de["commands.edit"] = "Befehl bearbeiten";
            de["commands.name"] = "Bezeichnung im Menü";
            de["commands.kind"] = "Art";
            de["commands.target"] = "Ziel";
            de["commands.args"] = "Argumente (optional)";
            de["commands.hint"] = "Kurzhinweis im Menü (optional)";
            de["commands.icon"] = "Symbol";
            de["commands.iconAuto"] = "Automatisch nach Art und Ziel";
            de["commands.iconPick"] = "Symbol wählen…";
            de["commands.iconReset"] = "Zurücksetzen";
            de["commands.add"] = "Befehl hinzufügen";
            de["commands.duplicate"] = "Duplizieren";
            de["commands.remove"] = "Entfernen";
            de["commands.up"] = "Nach oben";
            de["commands.down"] = "Nach unten";
            de["commands.empty"] = "Wähle links einen Befehl aus, um ihn zu bearbeiten.";
            de["commands.emptyTitle"] = "Kein Befehl ausgewählt";
            de["commands.emptyHint"] =
                "Wähle links einen Befehl aus oder lege mit „Befehl hinzufügen“ einen neuen an.";
            de["commands.steps"] = "Schritte bearbeiten…";
            de["commands.order"] = "Reihenfolge";
            de["commands.countEmpty"] = "leer";
            de["commands.countOne"] = "1 Befehl";
            de["commands.countMany"] = "{0} Befehle";
            de["commands.unnamed"] = "Ohne Namen";
            de["commands.command"] = "Befehl";
            de["commands.iconChosen"] = "Gewählt: {0}";
            de["commands.iconNote"] = "Ohne Auswahl bestimmt DialMenu das Symbol selbst.";
            de["commands.macroSection"] = "Ablauf";
            de["commands.macroNone"] = "Noch keine Schritte hinterlegt.";
            de["commands.stepsOne"] = "1 Schritt";
            de["commands.stepsMany"] = "{0} Schritte";
            de["commands.stepHotkey"] = "Tastenkombination";
            de["commands.stepText"] = "Text";
            de["commands.stepDelay"] = "Pause";
            de["commands.stepProgram"] = "Programm";
            de["commands.checkCaption"] = "Bitte prüfen";
            de["commands.deleteTitle"] = "Befehl entfernen";
            de["commands.deleteQuestion"] = "„{0}“ aus dem Menü entfernen?";
            de["commands.copySuffix"] = " (Kopie)";
            de["commands.newCommand"] = "Neuer Befehl";
            de["commands.defaultLabel"] = "Neue Aktion";
            de["commands.browseFileTitle"] = "Programm oder Datei wählen";
            de["commands.browseFilter"] =
                "Programme (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|Alle Dateien (*.*)|*.*";
            de["commands.browseFolder"] = "Ordner wählen";
            de["commands.preview"] = "So sieht der Ring aus";
            de["commands.previewEmpty"] = "Noch leer";
            de["commands.previewMore"] = "+{0} weitere";

            de["commands.target.program"] =
                "Vollständiger Pfad oder Kurzname, zum Beispiel notepad.exe.";
            de["commands.target.url"] =
                "Adresse, Ordner oder Datei – zum Beispiel https://youtube.com, C:\\Bilder oder ms-settings:";
            de["commands.target.shell"] = "Wird ohne sichtbares Fenster ausgeführt.";
            de["commands.target.hotkey"] = "Zum Beispiel Strg+Alt+M, Win+D oder F11.";
            de["commands.target.media"] = "playpause, next, prev oder stop";
            de["commands.target.volume"] = "up, down oder mute – die Argumente geben die Schritte an.";
            de["commands.target.system"] =
                "lock, sleep, showdesktop, taskmgr, shutdown, restart oder logoff";
            de["commands.target.activate"] =
                "Teil des Fenstertitels oder Programmname, zum Beispiel spotify";
            de["commands.target.discord"] =
                "Ziel: toggle (Discords eigene Taste, Anzeige stimmt), mute oder unmute " +
                "(systemweit, ohne Anzeige in Discord) sowie deafen oder toggledeafen. " +
                "In den Argumenten steht optional die Tastenkombination aus Discord; " +
                "ohne Angabe gilt Strg+Shift+M.";
            de["commands.target.macro"] =
                "Ein Makro braucht kein Ziel – die Ablaufschritte zählen.";

            de["kind.program"] = "Programm starten";
            de["kind.url"] = "Webseite oder Ordner öffnen";
            de["kind.shell"] = "Befehl ausführen";
            de["kind.hotkey"] = "Tastenkombination senden";
            de["kind.media"] = "Mediensteuerung";
            de["kind.volume"] = "Lautstärke";
            de["kind.system"] = "Systemfunktion";
            de["kind.activate"] = "Fenster in den Vordergrund";
            de["kind.discord"] = "Discord";
            de["kind.macro"] = "Makro";

            de["describe.emptyAction"] = "Leere Aktion";
            de["describe.program"] = "Programm: {0}";
            de["describe.programNone"] = "Programm ohne Ziel";
            de["describe.url"] = "URL: {0}";
            de["describe.urlNone"] = "URL ohne Ziel";
            de["describe.shell"] = "Befehl: {0}";
            de["describe.shellNone"] = "Befehl ohne Inhalt";
            de["describe.hotkey"] = "Tastenkombination: {0}";
            de["describe.hotkeyNone"] = "Tastenkombination ohne Taste";
            de["describe.media"] = "Medien: {0}";
            de["describe.volume"] = "Lautstärke: {0}";
            de["describe.volumeMute"] = "Lautstärke: Stumm schalten";
            de["describe.volumeSteps"] = "Lautstärke: {0} ({1} {2})";
            de["describe.louder"] = "lauter";
            de["describe.quieter"] = "leiser";
            de["describe.stepOne"] = "Schritt";
            de["describe.stepMany"] = "Schritte";
            de["describe.system"] = "System: {0}";
            de["describe.activate"] = "Fenster aktivieren: {0}";
            de["describe.activateNone"] = "Fenster ohne Titel";
            de["describe.discord"] = "Discord: {0}";
            de["describe.discordToggle"] = "Discord: Stummschalten umschalten ({0})";
            de["describe.discordMute"] = "Discord: Mikrofon stumm (systemweit)";
            de["describe.discordUnmute"] = "Discord: Mikrofon laut (systemweit)";
            de["describe.discordDeafen"] = "Discord: Taubschalten umschalten ({0})";
            de["describe.macro"] = "Makro ({0} {1})";
            de["describe.noKind"] = "Aktion ohne Art";
            de["describe.unknownKind"] = "Unbekannt: {0}";
            de["describe.unknown"] = "unbekannt";

            de["media.next"] = "Nächster Titel";
            de["media.prev"] = "Vorheriger Titel";
            de["media.stop"] = "Stop";

            de["system.lock"] = "Sperren";
            de["system.sleep"] = "Standby";
            de["system.hibernate"] = "Ruhezustand";
            de["system.showdesktop"] = "Desktop zeigen";
            de["system.taskmgr"] = "Task-Manager";
            de["system.shutdown"] = "Herunterfahren";
            de["system.restart"] = "Neustart";
            de["system.logoff"] = "Abmelden";
            de["system.screenoff"] = "Monitor aus";
            de["system.alttab"] = "Fenster wechseln";
            de["system.taskview"] = "Aufgabenansicht";
            de["system.virtualdesktop"] = "Nächster Desktop";
            de["system.snip"] = "Screenshot-Ausschnitt";
            de["system.emojis"] = "Emoji-Tafel";
            de["system.clipboard"] = "Zwischenablage";
            de["system.magnifier"] = "Bildschirmlupe";
            de["system.explorer"] = "Datei-Explorer";
            de["system.startmenu"] = "Startmenü";

            de["validate.empty"] = "Aktion ist leer.";
            de["validate.program"] = "Programm: Bitte einen Programmnamen oder Pfad angeben.";
            de["validate.url"] = "URL: Bitte ein Ziel angeben (z. B. https://example.com).";
            de["validate.shell"] = "Befehl: Bitte einen Shell-Befehl angeben.";
            de["validate.hotkey"] =
                "Tastenkombination: Bitte eine Kombination angeben (z. B. Ctrl+Alt+M).";
            de["validate.hotkeyInvalid"] =
                "Tastenkombination „{0}“ konnte nicht gelesen werden. Beispiel: Ctrl+Alt+M oder F11.";
            de["validate.mediaMissing"] =
                "Medien: Bitte eine Aktion wählen (playpause, next, prev, stop).";
            de["validate.mediaUnknown"] =
                "Medien: Unbekannte Aktion „{0}“. Erlaubt: playpause, next, prev, stop.";
            de["validate.volumeMissing"] = "Lautstärke: Bitte up, down oder mute angeben.";
            de["validate.volumeUnknown"] =
                "Lautstärke: Unbekannte Richtung „{0}“. Erlaubt: up, down, mute.";
            de["validate.volumeNaN"] = "Lautstärke: Die Schrittanzahl „{0}“ ist keine Zahl.";
            de["validate.volumeRange"] =
                "Lautstärke: Die Schrittanzahl muss zwischen 1 und 50 liegen.";
            de["validate.systemMissing"] =
                "System: Bitte einen Befehl angeben (z. B. lock, sleep, taskmgr).";
            de["validate.systemUnknown"] =
                "System: Unbekannter Befehl „{0}“. Erlaubt: lock, sleep, hibernate, showdesktop, " +
                "taskmgr, shutdown, restart, logoff, screenoff, alttab, taskview, virtualdesktop, " +
                "snip, emojis, clipboard, magnifier, explorer, startmenu.";
            de["validate.activate"] =
                "Fenster: Bitte einen Fenstertitel oder Prozessnamen angeben.";
            de["validate.discordUnknown"] =
                "Discord: Unbekannte Aktion „{0}“. Erlaubt: toggle, mute, unmute, deafen, " +
                "undeafen, toggledeafen.";
            de["validate.discordHotkey"] =
                "Discord: Die Tastenkombination „{0}“ konnte nicht gelesen werden. " +
                "Beispiel: Ctrl+Shift+M.";
            de["validate.macroEmpty"] = "Makro: Das Makro enthält keine Schritte.";
            de["validate.stepEmpty"] = "Makro: Schritt {0} ist leer.";
            de["validate.stepHotkeyMissing"] =
                "Makro: Schritt {0} (Tastenkombination) hat keine Taste.";
            de["validate.stepHotkeyInvalid"] =
                "Makro: Schritt {0} (Tastenkombination) ist ungültig: „{1}“.";
            de["validate.stepTextEmpty"] = "Makro: Schritt {0} (Text) ist leer.";
            de["validate.stepProgramMissing"] = "Makro: Schritt {0} (Programm) hat kein Ziel.";
            de["validate.stepDelayInvalid"] =
                "Makro: Schritt {0} (Wartezeit) ist keine gültige Millisekundenzahl.";
            de["validate.stepUnknownType"] =
                "Makro: Schritt {0} hat einen unbekannten Typ „{1}“. " +
                "Erlaubt: hotkey, text, delay, program, wait.";
            de["validate.stepNegativeDelay"] =
                "Makro: Schritt {0} hat eine negative Verzögerung.";
            de["validate.kindMissing"] = "Aktion: Die Art der Aktion fehlt.";
            de["validate.kindUnknown"] = "Aktion: Unbekannte Art „{0}“.";

            de["profiles.title"] = "Profile";
            de["profiles.subtitle"] =
                "Jedes Profil hat einen eigenen Ring. Das aktive Profil bestimmt, was der Regler zeigt.";
            de["profiles.new"] = "Neues Profil";
            de["profiles.namePrefix"] = "Profil {0}";
            de["profiles.active"] = "Aktiv";
            de["profiles.renameTitle"] = "Profil umbenennen";
            de["profiles.deleteTitle"] = "Profil löschen";
            de["profiles.deleteQuestion"] = "Profil „{0}“ mit allen Befehlen löschen?";
            de["profiles.name"] = "Name";
            de["profiles.defaultName"] = "Hauptprofil";

            de["devices.title"] = "Geräte";
            de["devices.subtitle"] =
                "DialMenu liest die Rohdaten des Drehreglers über HID und ordnet sie dem passenden Gerät zu.";
            de["devices.searching"] = "Suche läuft …";
            de["devices.attached"] = "Angeschlossen";
            de["devices.configured"] = "Eingerichtet";
            de["devices.addStandard"] = "Standard-Gerät hinzufügen (ISY IGK-6500)";
            de["devices.configuredNote"] = "Nur eingerichtete und aktivierte Geräte lösen das Menü aus.";
            de["devices.learnTitle"] = "Tastatur automatisch erkennen";
            de["devices.learnNote"] = "DialMenu kann den Drehregler selbst finden, ohne dass du Kennungen eintragen musst. Du drehst dabei nur am Regler und drückst ihn einmal.";
            de["devices.learnStart"] = "Erkennung starten";
            de["devices.learnStop"] = "Erkennung abbrechen";
            de["devices.learnWaiting"] = "Dreh den Regler dreimal nach rechts, dreimal nach links und drücke ihn dann einmal. Bitte währenddessen keine andere Taste drücken.";
            de["devices.learnDone"] = "Erkannt und eingerichtet:";
            de["devices.learnDoneHint"] = "Fertig. Du kannst den Regler jetzt benutzen.";
            de["devices.learnFailed"] = "Das Muster wurde nicht erkannt. Bitte noch einmal starten.";
            de["devices.learnNoId"] = "Das Gerät meldet keine eigene Kennung und lässt sich nicht zuverlässig wiedererkennen:";
            de["devices.foundNone"] = "Kein passendes Eingabegerät gefunden.";
            de["devices.foundOne"] = "1 Gerät gefunden";
            de["devices.foundMany"] = "{0} Geräte gefunden";
            de["devices.searchFailed"] = "Suche nicht möglich: {0}";
            de["devices.hint"] = "Regler anschließen und die Seite erneut öffnen, um erneut zu suchen.";
            de["devices.rescan"] = "Erneut suchen";
            de["devices.headerName"] = "Gerät";
            de["devices.headerVendor"] = "Hersteller";
            de["devices.headerProduct"] = "Produkt";
            de["devices.unnamed"] = "Unbenanntes Gerät";
            de["devices.configuredState"] = "Eingerichtet";
            de["devices.notSelectable"] = "Nicht wählbar";
            de["devices.notSelectableTip"] =
                "Dieses Gerät meldet keine eigene Kennung. Es lässt sich nicht zuverlässig wiedererkennen.";
            de["devices.noMatch"] = "kein Muster";
            de["devices.attachedState"] = "  ·  angeschlossen";
            de["devices.detachedState"] = "  ·  nicht angeschlossen";
            de["devices.enabled"] = "Aktiv";
            de["devices.none"] = "Noch kein Gerät eingerichtet.";
            de["devices.standardAlready"] = "Das Standard-Gerät ist bereits eingerichtet.";

            de["appearance.title"] = "Darstellung";
            de["appearance.subtitle"] = "Wo und wie der Ring erscheint.";
            de["appearance.position"] = "Position";
            de["appearance.scale"] = "Größe";
            de["appearance.mode"] = "Helligkeit";
            de["appearance.modeAuto"] = "Wie Windows";
            de["appearance.modeDark"] = "Dunkel";
            de["appearance.modeLight"] = "Hell";
            de["appearance.ring"] = "Ring";
            de["appearance.labels"] = "Beschriftungen im Ring anzeigen";
            de["appearance.positionNote"] =
                "„Am Mauszeiger“ folgt dem Bildschirm, auf dem sich der Zeiger gerade befindet.";
            de["appearance.labelsNote"] =
                "Ohne Beschriftungen bleibt nur das Symbol sichtbar – ruhiger, aber weniger eindeutig.";
            de["appearance.brightnessNote"] =
                "„Automatisch“ folgt der Einstellung von Windows und wechselt mit ihr.";
            de["appearance.ringNote"] =
                "Jede Kachel zeigt das Thema in der Helligkeit, die oben gewählt ist.";

            de["appearance.pos.bottomRight"] = "Unten rechts";
            de["appearance.pos.bottomLeft"] = "Unten links";
            de["appearance.pos.topRight"] = "Oben rechts";
            de["appearance.pos.topLeft"] = "Oben links";
            de["appearance.pos.rightTop"] = "Rechts oben";
            de["appearance.pos.rightCenter"] = "Rechts mitte";
            de["appearance.pos.rightBottom"] = "Rechts unten";
            de["appearance.pos.leftTop"] = "Links oben";
            de["appearance.pos.leftCenter"] = "Links mitte";
            de["appearance.pos.leftBottom"] = "Links unten";
            de["appearance.pos.topCenter"] = "Oben mitte";
            de["appearance.pos.bottomCenter"] = "Unten mitte";
            de["appearance.pos.center"] = "Mitte";
            de["appearance.pos.pointer"] = "Am Mauszeiger (Monitor unter dem Zeiger)";

            de["ring.graphite.name"] = "Graphit";
            de["ring.graphite.note"] = "Neutrales Anthrazit, kühles Weiß";
            de["ring.slate.name"] = "Schiefer";
            de["ring.slate.note"] = "Kühler Blaugrau, ohne Farbe im Akzent";
            de["ring.ink.name"] = "Tinte";
            de["ring.ink.note"] = "Fast reines Schwarz, hoher Kontrast";
            de["ring.warm.name"] = "Warmgrau";
            de["ring.warm.note"] = "Gedämpftes Umbra statt Blaugrau";

            de["behavior.title"] = "Verhalten";
            de["behavior.enabled"] = "DialMenu aktiv";
            de["behavior.hideAfter"] = "Menü ausblenden nach";
            de["behavior.longPress"] = "Langdruck ab";
            de["behavior.autostart"] = "Mit Windows starten";
            de["behavior.language"] = "Sprache";
            de["behavior.subtitle"] =
                "Wann das Menü erscheint, wie lange es bleibt und ob DialMenu mit Windows startet.";
            de["behavior.general"] = "Allgemein";
            de["behavior.enabledNote"] =
                "Deaktiviert bleibt das Programm im Infobereich, reagiert aber nicht auf den Regler.";
            de["behavior.hideTitle"] = "Ausblenden";
            de["behavior.hideNote"] = "Gilt, solange der Regler nicht gedreht wird.";
            de["behavior.longPressTitle"] = "Langdruck";
            de["behavior.longPressNote"] =
                "Ab dieser Dauer bricht der Druck die Auswahl ab, statt sie zu bestätigen.";
            de["behavior.startTitle"] = "Start";
            de["behavior.commandLabel"] = "Eingetragener Befehl";
            de["behavior.commandMissing"] = "(Programmpfad nicht ermittelbar)";
            de["behavior.startNote"] =
                "Der Eintrag liegt unter HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run.";
            de["behavior.languageNote"] = "Die Oberfläche wechselt sofort.";

            de["autostart.noPath"] = "Programmpfad konnte nicht ermittelt werden.";
            de["autostart.noKey"] = "Der Autostart-Schlüssel ist nicht zugänglich.";
            de["autostart.notWritten"] = "Der Eintrag wurde nicht übernommen.";
            de["autostart.mismatch"] = "Der gespeicherte Eintrag weicht ab: {0}";
            de["autostart.fileMissing"] = "Eingetragen, aber die Programmdatei fehlt.";
            de["autostart.active"] = "Aktiv – Eintrag vorhanden";
            de["autostart.removeFailed"] = "Der Eintrag konnte nicht entfernt werden.";
            de["autostart.inactive"] = "Inaktiv – kein Eintrag";
            de["autostart.error"] = "Fehler: {0}";
            de["autostart.differs"] = "Eingetragen, aber abweichend: {0}";
            de["autostart.pathMissing"] = "Programmpfad nicht ermittelbar.";
            de["autostart.exe"] = "Programmdatei: {0}";
            de["autostart.exeMissing"] = "Programmdatei fehlt: {0}";

            de["about.title"] = "Über DialMenu";
            de["about.tagline"] = "Der Drehregler als radiales Menü.";
            de["about.config"] = "Konfiguration";
            de["about.log"] = "Protokoll";
            de["about.openFolder"] = "Ordner öffnen";
            de["about.openLog"] = "Protokoll öffnen";
            de["about.copyPath"] = "Pfad kopieren";
            de["about.files"] = "Dateien";
            de["about.project"] = "Projekt";
            de["about.projectLink"] = "Projektseite";
            de["about.open"] = "Öffnen";
            de["about.linkPending"] = "Der Link wird mit der Veröffentlichung ergänzt.";
            de["about.projectNote"] =
                "Die Tastatur selbst wird nicht verändert – DialMenu liest nur die Rohdaten des Reglers.";

            de["macro.title"] = "Schritte bearbeiten";
            de["macro.macro"] = "Makro";
            de["macro.subtitle"] =
                "Die Schritte laufen von oben nach unten. Die Pause folgt jeweils nach dem Schritt.";
            de["macro.done"] = "Fertig";
            de["macro.reload"] = "Neu laden";
            de["macro.reloadTip"] =
                "Verwirft Änderungen und stellt die Schritte beim Öffnen wieder her.";
            de["macro.steps"] = "Schritte";
            de["macro.add"] = "Hinzufügen";
            de["macro.up"] = "Hoch";
            de["macro.down"] = "Runter";
            de["macro.step"] = "Schritt";
            de["macro.kind"] = "Art";
            de["macro.value"] = "Wert";
            de["macro.delayAfter"] = "Pause danach (ms)";
            de["macro.remove"] = "Schritt entfernen";
            de["macro.type.hotkey"] = "Tastenkombination";
            de["macro.type.text"] = "Text";
            de["macro.type.delay"] = "Pause";
            de["macro.type.program"] = "Programm";
            de["macro.wait"] = "Warten";
            de["macro.stepFallback"] = "Schritt";
            de["macro.noCombination"] = "keine Kombination";
            de["macro.noText"] = "kein Text";
            de["macro.interrupts"] = "unterbricht";
            de["macro.noProgram"] = "kein Programm";
            de["macro.empty"] = "Noch keine Schritte. Mit „Hinzufügen“ beginnt die Liste.";
            de["macro.caption.hotkey"] = "Tastenkombination";
            de["macro.tip.hotkey"] = "Zum Beispiel Strg+Alt+M, Alt+F4 oder F11.";
            de["macro.caption.text"] = "Text";
            de["macro.tip.text"] = "Wird als Tastatureingabe an das aktive Fenster gesendet.";
            de["macro.caption.delay"] = "Wert (nicht verwendet)";
            de["macro.tip.delay"] = "Eine Pause braucht nur die Dauer unten.";
            de["macro.caption.program"] = "Programm oder Dokument";
            de["macro.tip.program"] = "Vollständiger Pfad oder Name, z. B. notepad.exe.";
            de["macro.send"] = "sendet „{0}“";
            de["macro.typesText"] = "tippt den Text „{0}“";
            de["macro.waits"] = "wartet";
            de["macro.starts"] = "startet „{0}“";
            de["macro.runs"] = "führt „{0}“ aus";
            de["macro.summaryWithPause"] = "Schritt {0} {1}, danach {2} ms Pause.";
            de["macro.summaryNoPause"] = "Schritt {0} {1}, ohne Pause.";

            de["presets.title"] = "Befehl hinzufügen";
            de["presets.subtitle"] =
                "Ein fertiger Befehl, ein installiertes Programm oder ein eigener Eintrag.";
            de["presets.searchTip"] = "Nach Name, Gruppe oder Ziel suchen";
            de["presets.showInstalled"] = "Alle installierten Programme anzeigen";
            de["presets.custom"] = "Eigener Befehl…";
            de["presets.customTip"] = "Leeren Befehl anlegen und selbst ausfüllen.";
            de["presets.searching"] = "Programme werden gesucht …";
            de["presets.noneInstalled"] = "Keine installierten Programme gefunden";
            de["presets.foundOne"] = "1 Programm gefunden";
            de["presets.foundMany"] = "{0} Programme gefunden";
            de["presets.emptyInstalled"] = "Keine Programme gefunden.";
            de["presets.stillSearching"] = "Die Programme werden noch gesucht …";
            de["presets.emptySearch"] = "Kein Befehl und kein Programm gefunden.";
            de["presets.countProgramOne"] = "1 Programm";
            de["presets.countProgramMany"] = "{0} Programme";
            de["presets.countEntryOne"] = "1 Eintrag";
            de["presets.countEntryMany"] = "{0} Einträge";
            de["presets.installedHeader"] = "INSTALLIERTE PROGRAMME";
            de["presets.installed"] = "Installiert";
            de["presets.customCommand"] = "Eigener Befehl";
            de["presets.modePresets"] = "Fertige Befehle";
            de["presets.modeInstalled"] = "Installierte Programme";
            de["presets.backToPresets"] = "Zurück zu den fertigen Befehlen";
            de["presets.detailHotkey"] = "Tastenkombination {0}";
            de["presets.detailProgram"] = "Programm {0}";
            de["presets.detailOpen"] = "Öffnen: {0}";
            de["presets.detailMedia"] = "Mediensteuerung";
            de["presets.detailVolume"] = "Lautstärke";
            de["presets.detailSystem"] = "Systemfunktion";
            de["presets.detailMacro"] = "Mehrere Schritte nacheinander";
            de["presets.group.fenster"] = "Fenster";
            de["presets.group.system"] = "System";
            de["presets.group.programme"] = "Programme";
            de["presets.group.medien"] = "Medien";
            de["presets.group.bearbeiten"] = "Bearbeiten";
            de["presets.group.discord"] = "Discord";
            de["presets.group.makros"] = "Makros";

            Dictionary<string, string> en = new Dictionary<string, string>();
            en["app.settings"] = "Settings";
            en["app.subtitle"] = "Your keyboard's volume dial becomes a menu.";
            en["app.windowTitle"] = "DialMenu – Settings";
            en["app.preview"] = "Preview overlay";
            en["app.closeNote"] = "Closing this window leaves it in the tray.";
            en["app.trayPaused"] = "DialMenu – paused";
            en["app.traySettings"] = "Settings";
            en["app.trayAbout"] = "About DialMenu";
            en["app.trayFolder"] = "Open config folder";
            en["app.trayQuit"] = "Quit";
            en["app.settingsError"] = "The settings could not be opened.";

            en["nav.commands"] = "Commands";
            en["nav.profiles"] = "Profiles";
            en["nav.devices"] = "Devices";
            en["nav.appearance"] = "Appearance";
            en["nav.behavior"] = "Behaviour";
            en["nav.about"] = "About";

            en["common.save"] = "Save";
            en["common.cancel"] = "Cancel";
            en["common.add"] = "Add";
            en["common.choose"] = "Choose file…";
            en["common.folder"] = "Choose folder…";
            en["common.search"] = "Search";
            en["common.close"] = "Close";
            en["common.none"] = "None";
            en["common.apply"] = "Apply";
            en["common.rename"] = "Rename";
            en["common.remove"] = "Remove";
            en["common.delete"] = "Delete";
            en["common.duplicate"] = "Duplicate";
            en["common.activate"] = "Activate";
            en["common.open"] = "Open";
            en["common.reset"] = "Reset";

            en["error.pageTitle"] = "This page could not be built";
            en["error.pageHead"] = "Error while building “{0}”";
            en["error.hint"] = "The error is in the log. You can open it from “About”.";

            en["icon.title"] = "Choose icon";
            en["icon.subtitle"] = "Without a choice, DialMenu picks the icon from type and target.";
            en["icon.auto"] = "Automatic";
            en["icon.autoTip"] = "Let DialMenu choose the icon from type and target again.";
            en["icon.empty"] = "No icons available.";
            en["icon.automatic"] = "(automatic)";

            en["commands.title"] = "Commands";
            en["commands.subtitle"] = "The order on the left follows the direction you turn the dial.";
            en["commands.inMenu"] = "In the menu";
            en["commands.profile"] = "Profile";
            en["commands.edit"] = "Edit command";
            en["commands.name"] = "Label in the menu";
            en["commands.kind"] = "Type";
            en["commands.target"] = "Target";
            en["commands.args"] = "Arguments (optional)";
            en["commands.hint"] = "Short note in the menu (optional)";
            en["commands.icon"] = "Icon";
            en["commands.iconAuto"] = "Chosen from type and target";
            en["commands.iconPick"] = "Choose icon…";
            en["commands.iconReset"] = "Reset";
            en["commands.add"] = "Add command";
            en["commands.duplicate"] = "Duplicate";
            en["commands.remove"] = "Remove";
            en["commands.up"] = "Move up";
            en["commands.down"] = "Move down";
            en["commands.empty"] = "Pick a command on the left to edit it.";
            en["commands.emptyTitle"] = "No command selected";
            en["commands.emptyHint"] =
                "Pick a command on the left, or create a new one with “Add command”.";
            en["commands.steps"] = "Edit steps…";
            en["commands.order"] = "Order";
            en["commands.countEmpty"] = "empty";
            en["commands.countOne"] = "1 command";
            en["commands.countMany"] = "{0} commands";
            en["commands.unnamed"] = "Unnamed";
            en["commands.command"] = "Command";
            en["commands.iconChosen"] = "Chosen: {0}";
            en["commands.iconNote"] = "Without a choice, DialMenu picks the icon itself.";
            en["commands.macroSection"] = "Sequence";
            en["commands.macroNone"] = "No steps yet.";
            en["commands.stepsOne"] = "1 step";
            en["commands.stepsMany"] = "{0} steps";
            en["commands.stepHotkey"] = "Key combination";
            en["commands.stepText"] = "Text";
            en["commands.stepDelay"] = "Pause";
            en["commands.stepProgram"] = "Program";
            en["commands.checkCaption"] = "Please check";
            en["commands.deleteTitle"] = "Remove command";
            en["commands.deleteQuestion"] = "Remove “{0}” from the menu?";
            en["commands.copySuffix"] = " (copy)";
            en["commands.newCommand"] = "New command";
            en["commands.defaultLabel"] = "New action";
            en["commands.browseFileTitle"] = "Choose a program or file";
            en["commands.browseFilter"] =
                "Programs (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All files (*.*)|*.*";
            en["commands.browseFolder"] = "Choose a folder";
            en["commands.preview"] = "This is what the ring looks like";
            en["commands.previewEmpty"] = "Still empty";
            en["commands.previewMore"] = "+{0} more";

            en["commands.target.program"] = "Full path or short name, for example notepad.exe.";
            en["commands.target.url"] =
                "Address, folder or file – for example https://youtube.com, C:\\Pictures or ms-settings:";
            en["commands.target.shell"] = "Runs without a visible window.";
            en["commands.target.hotkey"] = "For example Ctrl+Alt+M, Win+D or F11.";
            en["commands.target.media"] = "playpause, next, prev or stop";
            en["commands.target.volume"] = "up, down or mute – the arguments set the number of steps.";
            en["commands.target.system"] =
                "lock, sleep, showdesktop, taskmgr, shutdown, restart or logoff";
            en["commands.target.activate"] =
                "Part of the window title or the program name, for example spotify";
            en["commands.target.discord"] =
                "Target: toggle (Discord's own key, the indicator stays accurate), mute or unmute " +
                "(system-wide, without an indicator in Discord), plus deafen or toggledeafen. " +
                "The arguments optionally hold the key combination from Discord; " +
                "without one, Ctrl+Shift+M is used.";
            en["commands.target.macro"] = "A macro needs no target – its sequence steps are what count.";

            en["kind.program"] = "Start a program";
            en["kind.url"] = "Open a website or folder";
            en["kind.shell"] = "Run a command";
            en["kind.hotkey"] = "Send a key combination";
            en["kind.media"] = "Media controls";
            en["kind.volume"] = "Volume";
            en["kind.system"] = "System action";
            en["kind.activate"] = "Bring a window to the front";
            en["kind.discord"] = "Discord";
            en["kind.macro"] = "Macro";

            en["describe.emptyAction"] = "Empty action";
            en["describe.program"] = "Program: {0}";
            en["describe.programNone"] = "Program without a target";
            en["describe.url"] = "URL: {0}";
            en["describe.urlNone"] = "URL without a target";
            en["describe.shell"] = "Command: {0}";
            en["describe.shellNone"] = "Command without content";
            en["describe.hotkey"] = "Key combination: {0}";
            en["describe.hotkeyNone"] = "Key combination without a key";
            en["describe.media"] = "Media: {0}";
            en["describe.volume"] = "Volume: {0}";
            en["describe.volumeMute"] = "Volume: mute";
            en["describe.volumeSteps"] = "Volume: {0} ({1} {2})";
            en["describe.louder"] = "louder";
            en["describe.quieter"] = "quieter";
            en["describe.stepOne"] = "step";
            en["describe.stepMany"] = "steps";
            en["describe.system"] = "System: {0}";
            en["describe.activate"] = "Activate window: {0}";
            en["describe.activateNone"] = "Window without a title";
            en["describe.discord"] = "Discord: {0}";
            en["describe.discordToggle"] = "Discord: toggle mute ({0})";
            en["describe.discordMute"] = "Discord: microphone muted (system-wide)";
            en["describe.discordUnmute"] = "Discord: microphone live (system-wide)";
            en["describe.discordDeafen"] = "Discord: toggle deafen ({0})";
            en["describe.macro"] = "Macro ({0} {1})";
            en["describe.noKind"] = "Action without a type";
            en["describe.unknownKind"] = "Unknown: {0}";
            en["describe.unknown"] = "unknown";

            en["media.next"] = "Next track";
            en["media.prev"] = "Previous track";
            en["media.stop"] = "Stop";

            en["system.lock"] = "Lock";
            en["system.sleep"] = "Sleep";
            en["system.hibernate"] = "Hibernate";
            en["system.showdesktop"] = "Show desktop";
            en["system.taskmgr"] = "Task Manager";
            en["system.shutdown"] = "Shut down";
            en["system.restart"] = "Restart";
            en["system.logoff"] = "Sign out";
            en["system.screenoff"] = "Screen off";
            en["system.alttab"] = "Switch window";
            en["system.taskview"] = "Task view";
            en["system.virtualdesktop"] = "Next desktop";
            en["system.snip"] = "Screenshot region";
            en["system.emojis"] = "Emoji panel";
            en["system.clipboard"] = "Clipboard";
            en["system.magnifier"] = "Magnifier";
            en["system.explorer"] = "File Explorer";
            en["system.startmenu"] = "Start menu";

            en["validate.empty"] = "The action is empty.";
            en["validate.program"] = "Program: please enter a program name or path.";
            en["validate.url"] = "URL: please enter a target (e.g. https://example.com).";
            en["validate.shell"] = "Command: please enter a shell command.";
            en["validate.hotkey"] =
                "Key combination: please enter a combination (e.g. Ctrl+Alt+M).";
            en["validate.hotkeyInvalid"] =
                "Key combination “{0}” could not be read. Example: Ctrl+Alt+M or F11.";
            en["validate.mediaMissing"] =
                "Media: please choose an action (playpause, next, prev, stop).";
            en["validate.mediaUnknown"] =
                "Media: unknown action “{0}”. Allowed: playpause, next, prev, stop.";
            en["validate.volumeMissing"] = "Volume: please enter up, down or mute.";
            en["validate.volumeUnknown"] =
                "Volume: unknown direction “{0}”. Allowed: up, down, mute.";
            en["validate.volumeNaN"] = "Volume: the step count “{0}” is not a number.";
            en["validate.volumeRange"] = "Volume: the step count must be between 1 and 50.";
            en["validate.systemMissing"] =
                "System: please enter a command (e.g. lock, sleep, taskmgr).";
            en["validate.systemUnknown"] =
                "System: unknown command “{0}”. Allowed: lock, sleep, hibernate, showdesktop, " +
                "taskmgr, shutdown, restart, logoff, screenoff, alttab, taskview, virtualdesktop, " +
                "snip, emojis, clipboard, magnifier, explorer, startmenu.";
            en["validate.activate"] = "Window: please enter a window title or process name.";
            en["validate.discordUnknown"] =
                "Discord: unknown action “{0}”. Allowed: toggle, mute, unmute, deafen, " +
                "undeafen, toggledeafen.";
            en["validate.discordHotkey"] =
                "Discord: the key combination “{0}” could not be read. Example: Ctrl+Shift+M.";
            en["validate.macroEmpty"] = "Macro: the macro contains no steps.";
            en["validate.stepEmpty"] = "Macro: step {0} is empty.";
            en["validate.stepHotkeyMissing"] =
                "Macro: step {0} (key combination) has no key.";
            en["validate.stepHotkeyInvalid"] =
                "Macro: step {0} (key combination) is invalid: “{1}”.";
            en["validate.stepTextEmpty"] = "Macro: step {0} (text) is empty.";
            en["validate.stepProgramMissing"] = "Macro: step {0} (program) has no target.";
            en["validate.stepDelayInvalid"] =
                "Macro: step {0} (wait) is not a valid number of milliseconds.";
            en["validate.stepUnknownType"] =
                "Macro: step {0} has an unknown type “{1}”. " +
                "Allowed: hotkey, text, delay, program, wait.";
            en["validate.stepNegativeDelay"] =
                "Macro: step {0} has a negative delay.";
            en["validate.kindMissing"] = "Action: the type of action is missing.";
            en["validate.kindUnknown"] = "Action: unknown type “{0}”.";

            en["profiles.title"] = "Profiles";
            en["profiles.subtitle"] =
                "Each profile has its own ring. The active profile decides what the dial shows.";
            en["profiles.new"] = "New profile";
            en["profiles.namePrefix"] = "Profile {0}";
            en["profiles.active"] = "Active";
            en["profiles.renameTitle"] = "Rename profile";
            en["profiles.deleteTitle"] = "Delete profile";
            en["profiles.deleteQuestion"] = "Delete profile “{0}” with all its commands?";
            en["profiles.name"] = "Name";
            en["profiles.defaultName"] = "Main profile";

            en["devices.title"] = "Devices";
            en["devices.subtitle"] =
                "DialMenu reads the dial's raw HID data and matches it to the right device.";
            en["devices.searching"] = "Searching …";
            en["devices.attached"] = "Connected";
            en["devices.configured"] = "Set up";
            en["devices.addStandard"] = "Add the standard device (ISY IGK-6500)";
            en["devices.configuredNote"] = "Only set-up and enabled devices trigger the menu.";
            en["devices.learnTitle"] = "Detect your keyboard automatically";
            en["devices.learnNote"] = "DialMenu can find the dial itself, so you do not have to enter any IDs. You only turn the dial and press it once.";
            en["devices.learnStart"] = "Start detection";
            en["devices.learnStop"] = "Cancel detection";
            en["devices.learnWaiting"] = "Turn the dial three steps right, three steps left, then press it once. Do not press any other key while doing so.";
            en["devices.learnDone"] = "Detected and set up:";
            en["devices.learnDoneHint"] = "Done. You can use the dial now.";
            en["devices.learnFailed"] = "The pattern was not recognised. Please start again.";
            en["devices.learnNoId"] = "This device reports no identifier and cannot be recognised reliably:";
            en["devices.foundNone"] = "No matching input device found.";
            en["devices.foundOne"] = "1 device found";
            en["devices.foundMany"] = "{0} devices found";
            en["devices.searchFailed"] = "Search failed: {0}";
            en["devices.hint"] = "Connect the dial and reopen this page to search again.";
            en["devices.rescan"] = "Search again";
            en["devices.headerName"] = "Device";
            en["devices.headerVendor"] = "Vendor";
            en["devices.headerProduct"] = "Product";
            en["devices.unnamed"] = "Unnamed device";
            en["devices.configuredState"] = "Set up";
            en["devices.notSelectable"] = "Unavailable";
            en["devices.notSelectableTip"] =
                "This device does not report its own identifier. It cannot be recognized reliably.";
            en["devices.noMatch"] = "no pattern";
            en["devices.attachedState"] = "  ·  connected";
            en["devices.detachedState"] = "  ·  not connected";
            en["devices.enabled"] = "Enabled";
            en["devices.none"] = "No device set up yet.";
            en["devices.standardAlready"] = "The standard device is already set up.";

            en["appearance.title"] = "Appearance";
            en["appearance.subtitle"] = "Where and how the ring appears.";
            en["appearance.position"] = "Position";
            en["appearance.scale"] = "Size";
            en["appearance.mode"] = "Brightness";
            en["appearance.modeAuto"] = "Match Windows";
            en["appearance.modeDark"] = "Dark";
            en["appearance.modeLight"] = "Light";
            en["appearance.ring"] = "Ring";
            en["appearance.labels"] = "Show labels inside the ring";
            en["appearance.positionNote"] =
                "“At the mouse pointer” follows the screen the pointer is currently on.";
            en["appearance.labelsNote"] =
                "Without labels only the icon stays visible – calmer, but less clear.";
            en["appearance.brightnessNote"] =
                "“Automatic” follows the Windows setting and changes with it.";
            en["appearance.ringNote"] =
                "Each tile shows the theme at the brightness selected above.";

            en["appearance.pos.bottomRight"] = "Bottom right";
            en["appearance.pos.bottomLeft"] = "Bottom left";
            en["appearance.pos.topRight"] = "Top right";
            en["appearance.pos.topLeft"] = "Top left";
            en["appearance.pos.rightTop"] = "Right top";
            en["appearance.pos.rightCenter"] = "Right centre";
            en["appearance.pos.rightBottom"] = "Right bottom";
            en["appearance.pos.leftTop"] = "Left top";
            en["appearance.pos.leftCenter"] = "Left centre";
            en["appearance.pos.leftBottom"] = "Left bottom";
            en["appearance.pos.topCenter"] = "Top centre";
            en["appearance.pos.bottomCenter"] = "Bottom centre";
            en["appearance.pos.center"] = "Centre";
            en["appearance.pos.pointer"] = "At the mouse pointer (screen under the pointer)";

            en["ring.graphite.name"] = "Graphite";
            en["ring.graphite.note"] = "Neutral anthracite, cool white";
            en["ring.slate.name"] = "Slate";
            en["ring.slate.note"] = "Cool blue-grey, no colour in the accent";
            en["ring.ink.name"] = "Ink";
            en["ring.ink.note"] = "Nearly pure black, high contrast";
            en["ring.warm.name"] = "Warm grey";
            en["ring.warm.note"] = "Muted umber instead of blue-grey";

            en["behavior.title"] = "Behaviour";
            en["behavior.enabled"] = "DialMenu active";
            en["behavior.hideAfter"] = "Hide menu after";
            en["behavior.longPress"] = "Long press from";
            en["behavior.autostart"] = "Start with Windows";
            en["behavior.language"] = "Language";
            en["behavior.subtitle"] =
                "When the menu appears, how long it stays and whether DialMenu starts with Windows.";
            en["behavior.general"] = "General";
            en["behavior.enabledNote"] =
                "When disabled, the program stays in the tray but does not react to the dial.";
            en["behavior.hideTitle"] = "Hiding";
            en["behavior.hideNote"] = "Applies while the dial is not being turned.";
            en["behavior.longPressTitle"] = "Long press";
            en["behavior.longPressNote"] =
                "Beyond this duration a press cancels the selection instead of confirming it.";
            en["behavior.startTitle"] = "Startup";
            en["behavior.commandLabel"] = "Registered command";
            en["behavior.commandMissing"] = "(program path unknown)";
            en["behavior.startNote"] =
                "The entry lives under HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run.";
            en["behavior.languageNote"] = "The interface switches immediately.";

            en["autostart.noPath"] = "Could not determine the program path.";
            en["autostart.noKey"] = "The autostart key is not accessible.";
            en["autostart.notWritten"] = "The entry was not applied.";
            en["autostart.mismatch"] = "The stored entry differs: {0}";
            en["autostart.fileMissing"] = "Added, but the program file is missing.";
            en["autostart.active"] = "Active – entry present";
            en["autostart.removeFailed"] = "The entry could not be removed.";
            en["autostart.inactive"] = "Inactive – no entry";
            en["autostart.error"] = "Error: {0}";
            en["autostart.differs"] = "Added, but different: {0}";
            en["autostart.pathMissing"] = "Program path unknown.";
            en["autostart.exe"] = "Program file: {0}";
            en["autostart.exeMissing"] = "Program file missing: {0}";

            en["about.title"] = "About DialMenu";
            en["about.tagline"] = "The volume dial as a radial menu.";
            en["about.config"] = "Configuration";
            en["about.log"] = "Log";
            en["about.openFolder"] = "Open folder";
            en["about.openLog"] = "Open log";
            en["about.copyPath"] = "Copy path";
            en["about.files"] = "Files";
            en["about.project"] = "Project";
            en["about.projectLink"] = "Project page";
            en["about.open"] = "Open";
            en["about.linkPending"] = "The link will be added with the release.";
            en["about.projectNote"] =
                "The keyboard itself is not modified – DialMenu only reads the dial's raw data.";

            en["macro.title"] = "Edit steps";
            en["macro.macro"] = "Macro";
            en["macro.subtitle"] =
                "The steps run from top to bottom. The pause follows each step.";
            en["macro.done"] = "Done";
            en["macro.reload"] = "Reload";
            en["macro.reloadTip"] = "Discards changes and restores the steps from when the window opened.";
            en["macro.steps"] = "Steps";
            en["macro.add"] = "Add";
            en["macro.up"] = "Up";
            en["macro.down"] = "Down";
            en["macro.step"] = "Step";
            en["macro.kind"] = "Type";
            en["macro.value"] = "Value";
            en["macro.delayAfter"] = "Pause after (ms)";
            en["macro.remove"] = "Remove step";
            en["macro.type.hotkey"] = "Key combination";
            en["macro.type.text"] = "Text";
            en["macro.type.delay"] = "Pause";
            en["macro.type.program"] = "Program";
            en["macro.wait"] = "Wait";
            en["macro.stepFallback"] = "Step";
            en["macro.noCombination"] = "no combination";
            en["macro.noText"] = "no text";
            en["macro.interrupts"] = "interrupts";
            en["macro.noProgram"] = "no program";
            en["macro.empty"] = "No steps yet. Use “Add” to start the list.";
            en["macro.caption.hotkey"] = "Key combination";
            en["macro.tip.hotkey"] = "For example Ctrl+Alt+M, Alt+F4 or F11.";
            en["macro.caption.text"] = "Text";
            en["macro.tip.text"] = "Sent as keyboard input to the active window.";
            en["macro.caption.delay"] = "Value (not used)";
            en["macro.tip.delay"] = "A pause only needs the duration below.";
            en["macro.caption.program"] = "Program or document";
            en["macro.tip.program"] = "Full path or name, e.g. notepad.exe.";
            en["macro.send"] = "sends “{0}”";
            en["macro.typesText"] = "types the text “{0}”";
            en["macro.waits"] = "waits";
            en["macro.starts"] = "starts “{0}”";
            en["macro.runs"] = "runs “{0}”";
            en["macro.summaryWithPause"] = "Step {0} {1}, then a {2} ms pause.";
            en["macro.summaryNoPause"] = "Step {0} {1}, without a pause.";

            en["presets.title"] = "Add command";
            en["presets.subtitle"] =
                "A ready-made command, an installed program, or your own entry.";
            en["presets.searchTip"] = "Search by name, group or target";
            en["presets.showInstalled"] = "Show all installed programs";
            en["presets.custom"] = "Custom command…";
            en["presets.customTip"] = "Create an empty command and fill it in yourself.";
            en["presets.searching"] = "Looking for programs …";
            en["presets.noneInstalled"] = "No installed programs found";
            en["presets.foundOne"] = "1 program found";
            en["presets.foundMany"] = "{0} programs found";
            en["presets.emptyInstalled"] = "No programs found.";
            en["presets.stillSearching"] = "Programs are still being searched …";
            en["presets.emptySearch"] = "No command and no program found.";
            en["presets.countProgramOne"] = "1 program";
            en["presets.countProgramMany"] = "{0} programs";
            en["presets.countEntryOne"] = "1 entry";
            en["presets.countEntryMany"] = "{0} entries";
            en["presets.installedHeader"] = "INSTALLED PROGRAMS";
            en["presets.installed"] = "Installed";
            en["presets.customCommand"] = "Custom command";
            en["presets.modePresets"] = "Ready-made commands";
            en["presets.modeInstalled"] = "Installed programs";
            en["presets.backToPresets"] = "Back to the ready-made commands";
            en["presets.detailHotkey"] = "Key combination {0}";
            en["presets.detailProgram"] = "Program {0}";
            en["presets.detailOpen"] = "Open: {0}";
            en["presets.detailMedia"] = "Media controls";
            en["presets.detailVolume"] = "Volume";
            en["presets.detailSystem"] = "System action";
            en["presets.detailMacro"] = "Several steps in a row";
            en["presets.group.fenster"] = "Windows";
            en["presets.group.system"] = "System";
            en["presets.group.programme"] = "Programs";
            en["presets.group.medien"] = "Media";
            en["presets.group.bearbeiten"] = "Editing";
            en["presets.group.discord"] = "Discord";
            en["presets.group.makros"] = "Macros";

            // Kuratierte Befehle: Schluessel ist der deutsche Text aus Presets.cs.
            en["presets.label.Fenster wechseln"] = "Switch window";
            en["presets.label.Alle Fenster zeigen"] = "Show all windows";
            en["presets.label.Desktop anzeigen"] = "Show desktop";
            en["presets.label.Nächste Arbeitsfläche"] = "Next desktop";
            en["presets.label.Fenster schließen"] = "Close window";
            en["presets.label.Vollbild umschalten"] = "Toggle fullscreen";
            en["presets.label.Fenster minimieren"] = "Minimize window";
            en["presets.label.Fenster maximieren"] = "Maximize window";
            en["presets.label.Sperren"] = "Lock";
            en["presets.label.Ruhezustand"] = "Sleep";
            en["presets.label.Bildschirm aus"] = "Screen off";
            en["presets.label.Task-Manager"] = "Task Manager";
            en["presets.label.Screenshot-Bereich"] = "Screenshot region";
            en["presets.label.Emoji-Auswahl"] = "Emoji picker";
            en["presets.label.Zwischenablage"] = "Clipboard";
            en["presets.label.Lupe"] = "Magnifier";
            en["presets.label.Explorer"] = "Explorer";
            en["presets.label.Browser"] = "Browser";
            en["presets.label.Datei-Explorer"] = "File Explorer";
            en["presets.label.Windows Terminal"] = "Windows Terminal";
            en["presets.label.Rechner"] = "Calculator";
            en["presets.label.Editor"] = "Editor";
            en["presets.label.Einstellungen"] = "Settings";
            en["presets.label.Play / Pause"] = "Play / Pause";
            en["presets.label.Nächster Titel"] = "Next track";
            en["presets.label.Vorheriger Titel"] = "Previous track";
            en["presets.label.Lauter"] = "Volume up";
            en["presets.label.Leiser"] = "Volume down";
            en["presets.label.Stumm"] = "Mute";
            en["presets.label.Kopieren"] = "Copy";
            en["presets.label.Einfügen"] = "Paste";
            en["presets.label.Ausschneiden"] = "Cut";
            en["presets.label.Rückgängig"] = "Undo";
            en["presets.label.Alles auswählen"] = "Select all";
            en["presets.label.Speichern"] = "Save";
            en["presets.label.Stummschalten umschalten"] = "Toggle mute";
            en["presets.label.Taubschalten umschalten"] = "Toggle deafen";
            en["presets.label.Mikrofon stumm (systemweit)"] = "Microphone muted (system-wide)";
            en["presets.label.Mikrofon laut (systemweit)"] = "Microphone live (system-wide)";
            en["presets.label.Leeres Makro"] = "Empty macro";

            Tables["de"] = de;
            Tables["en"] = en;
        }

        public static List<string> Available
        {
            get { return new List<string>(Tables.Keys); }
        }

        public static string Get(string key)
        {
            Dictionary<string, string> table;
            if (Tables.TryGetValue(Current, out table))
            {
                string value;
                if (table.TryGetValue(key, out value)) return value;
            }

            // Rueckfall auf Deutsch, sonst auf den Schluessel selbst.
            Dictionary<string, string> fallback;
            if (Tables.TryGetValue("de", out fallback))
            {
                string value;
                if (fallback.TryGetValue(key, out value)) return value;
            }

            return key;
        }

        // Text mit Platzhaltern ({0}, {1}) in der aktuellen Sprache.
        public static string Format(string key, params object[] args)
        {
            string text = Get(key);
            try { return string.Format(text, args); }
            catch { return text; }
        }

        // Ob es fuer diesen Schluessel in der aktuellen Sprache einen Text
        // gibt. Get() liefert sonst den Schluessel selbst zurueck, was beim
        // Zuordnen von Texten leicht als echter Text missverstanden wird.
        public static bool Has(string key)
        {
            Dictionary<string, string> table;
            return Tables.TryGetValue(Current, out table) && table.ContainsKey(key);
        }

        // Prueft, ob ein gespeicherter Wert einer der uebersetzten Texte ist -
        // etwa die Vorgabe-Bezeichnung eines neuen Befehls in jeder Sprache.
        public static bool IsTranslated(string key, string value)
        {
            foreach (Dictionary<string, string> table in Tables.Values)
            {
                string text;
                if (table.TryGetValue(key, out text) && text == value) return true;
            }
            return false;
        }

        public static string Name(string code)
        {
            if (code == "en") return "English";
            if (code == "de") return "Deutsch";
            return code;
        }

        // Erkennt die Systemsprache, damit die erste Auswahl passt.
        public static string Detect()
        {
            try
            {
                string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                if (Tables.ContainsKey(two)) return two;
            }
            catch { }
            return "de";
        }
    }
}
