using System;
using System.Collections.Generic;

namespace DialMenu
{
    // Kuratierte Befehle fuer den schnellen Einstieg.
    public static class Presets
    {
        public class Entry
        {
            public string Group;
            public string Label;
            public string Kind;
            public string Target;
            public string Args;

            public Entry(string group, string label, string kind, string target, string args)
            {
                Group = group; Label = label; Kind = kind; Target = target; Args = args;
            }

            public ActionItem ToAction()
            {
                ActionItem a = new ActionItem();
                a.Label = Label;
                a.Kind = Kind;
                a.Target = Target;
                a.Args = Args;
                return a;
            }
        }

        public static List<Entry> All()
        {
            List<Entry> list = new List<Entry>();

            // --- Fenster ---
            list.Add(new Entry("Fenster", "Fenster wechseln", "system", "alttab", ""));
            list.Add(new Entry("Fenster", "Alle Fenster zeigen", "system", "taskview", ""));
            list.Add(new Entry("Fenster", "Desktop anzeigen", "system", "showdesktop", ""));
            list.Add(new Entry("Fenster", "Nächste Arbeitsfläche", "system", "virtualdesktop", ""));
            list.Add(new Entry("Fenster", "Fenster schließen", "hotkey", "Alt+F4", ""));
            list.Add(new Entry("Fenster", "Vollbild umschalten", "hotkey", "F11", ""));
            list.Add(new Entry("Fenster", "Fenster minimieren", "hotkey", "Win+Down", ""));
            list.Add(new Entry("Fenster", "Fenster maximieren", "hotkey", "Win+Up", ""));

            // --- System ---
            list.Add(new Entry("System", "Sperren", "system", "lock", ""));
            list.Add(new Entry("System", "Ruhezustand", "system", "sleep", ""));
            list.Add(new Entry("System", "Bildschirm aus", "system", "screenoff", ""));
            list.Add(new Entry("System", "Task-Manager", "system", "taskmgr", ""));
            list.Add(new Entry("System", "Screenshot-Bereich", "system", "snip", ""));
            list.Add(new Entry("System", "Emoji-Auswahl", "system", "emojis", ""));
            list.Add(new Entry("System", "Zwischenablage", "system", "clipboard", ""));
            list.Add(new Entry("System", "Lupe", "system", "magnifier", ""));
            list.Add(new Entry("System", "Explorer", "system", "explorer", ""));

            // --- Programme ---
            list.Add(new Entry("Programme", "Browser", "url", "https://www.google.com", ""));
            list.Add(new Entry("Programme", "Datei-Explorer", "program", "explorer.exe", ""));
            list.Add(new Entry("Programme", "Windows Terminal", "program", "wt.exe", ""));
            list.Add(new Entry("Programme", "Rechner", "program", "calc.exe", ""));
            list.Add(new Entry("Programme", "Editor", "program", "notepad.exe", ""));
            list.Add(new Entry("Programme", "Einstellungen", "url", "ms-settings:", ""));

            // --- Medien ---
            list.Add(new Entry("Medien", "Play / Pause", "media", "playpause", ""));
            list.Add(new Entry("Medien", "Nächster Titel", "media", "next", ""));
            list.Add(new Entry("Medien", "Vorheriger Titel", "media", "prev", ""));
            list.Add(new Entry("Medien", "Lauter", "volume", "up", "4"));
            list.Add(new Entry("Medien", "Leiser", "volume", "down", "4"));
            list.Add(new Entry("Medien", "Stumm", "volume", "mute", ""));

            // --- Bearbeiten ---
            list.Add(new Entry("Bearbeiten", "Kopieren", "hotkey", "Ctrl+C", ""));
            list.Add(new Entry("Bearbeiten", "Einfügen", "hotkey", "Ctrl+V", ""));
            list.Add(new Entry("Bearbeiten", "Ausschneiden", "hotkey", "Ctrl+X", ""));
            list.Add(new Entry("Bearbeiten", "Rückgängig", "hotkey", "Ctrl+Z", ""));
            list.Add(new Entry("Bearbeiten", "Alles auswählen", "hotkey", "Ctrl+A", ""));
            list.Add(new Entry("Bearbeiten", "Speichern", "hotkey", "Ctrl+S", ""));

            // --- Discord ---
            list.Add(new Entry("Discord", "Stummschalten umschalten", "discord", "toggle", ""));
            list.Add(new Entry("Discord", "Taubschalten umschalten", "discord", "toggledeafen", ""));
            list.Add(new Entry("Discord", "Mikrofon stumm (systemweit)", "discord", "mute", ""));
            list.Add(new Entry("Discord", "Mikrofon laut (systemweit)", "discord", "unmute", ""));

            // --- Makros ---
            list.Add(new Entry("Makros", "Leeres Makro", "macro", "", ""));

            return list;
        }

        // Beispielmakro: Text einfuegen und Enter druecken.
        public static ActionItem SampleMacro()
        {
            ActionItem a = new ActionItem();
            a.Label = "Beispiel-Makro";
            a.Kind = "macro";

            MacroStep s1 = new MacroStep();
            s1.Type = "delay"; s1.DelayMs = 200;
            a.Steps.Add(s1);

            MacroStep s2 = new MacroStep();
            s2.Type = "text"; s2.Value = "Hallo Welt"; s2.DelayMs = 100;
            a.Steps.Add(s2);

            MacroStep s3 = new MacroStep();
            s3.Type = "hotkey"; s3.Value = "Enter"; s3.DelayMs = 0;
            a.Steps.Add(s3);

            return a;
        }
    }
}
