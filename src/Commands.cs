using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DialMenu
{
    // ------------------------------------------------------------------
    // Aktions-Engine
    //
    // Fuehrt alle ActionItem-Arten aus. Tastatureingaben werden mit
    // SendInput injiziert und tragen dwExtraInfo = 0x4B4E4F42 ("KNOB"),
    // damit der eigene Tastatur-Hook sie ignoriert.
    // ------------------------------------------------------------------

    public static class Commands
    {
        // ---- SendInput ----

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_UNICODE = 0x0004;

        // Muss mit dem Tag im Tastatur-Hook uebereinstimmen.
        static readonly IntPtr InjectedTag = (IntPtr)0x4B4E4F42;

        // ---- Virtuelle Tasten ----

        const ushort VK_TAB = 0x09;
        const ushort VK_RETURN = 0x0D;
        const ushort VK_LWIN = 0x5B;

        const ushort VK_MEDIA_NEXT_TRACK = 0xB0;
        const ushort VK_MEDIA_PREV_TRACK = 0xB1;
        const ushort VK_MEDIA_STOP = 0xB2;
        const ushort VK_MEDIA_PLAY_PAUSE = 0xB3;

        const ushort VK_VOLUME_MUTE = 0xAD;
        const ushort VK_VOLUME_DOWN = 0xAE;
        const ushort VK_VOLUME_UP = 0xAF;

        // ---- Fenster ----

        const int SW_RESTORE = 9;
        const uint WM_SYSCOMMAND = 0x0112;
        const int SC_MONITORPOWER = 0xF170;
        static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        const uint SMTO_ABORTIFHUNG = 0x0002;

        // ------------------------------------------------------------------
        // Oeffentliche API
        // ------------------------------------------------------------------

        // Fuehrt eine Aktion aus. Fehler werden protokolliert, nie geworfen.
        public static void Execute(ActionItem a)
        {
            if (a == null) return;
            string kind = Norm(a.Kind);

            try
            {
                switch (kind)
                {
                    case "program": RunProgram(a.Target, a.Args); break;
                    case "url": RunUrl(a.Target); break;
                    case "shell": RunShell(a.Target, a.Args); break;
                    case "hotkey": SendHotkey(a.Target); break;
                    case "media": RunMedia(a.Target); break;
                    case "volume": RunVolume(a.Target, a.Args); break;
                    case "system": RunSystem(a.Target); break;
                    case "activate": ActivateWindow(a.Target); break;
                    case "macro": RunMacro(a); break;
                    case "discord": RunDiscord(a.Target, a.Args); break;
                    default:
                        Log.Write("Unbekannte Aktionsart: " + a.Kind);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Write("Aktion \"" + a.Label + "\" (" + a.Kind + ") fehlgeschlagen: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Aktionen
        // ------------------------------------------------------------------

        static void RunProgram(string target, string args)
        {
            string t = Clean(target);
            if (t.Length == 0) { Log.Write("Programm ohne Ziel."); return; }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = t;
            psi.Arguments = args == null ? "" : args;
            psi.UseShellExecute = true;
            psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            Process.Start(psi);
            Log.Write("Programm gestartet: " + t + (string.IsNullOrEmpty(args) ? "" : " " + args));
        }

        static void RunUrl(string target)
        {
            string t = Clean(target);
            if (t.Length == 0) { Log.Write("URL ohne Ziel."); return; }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = t;
            psi.UseShellExecute = true;

            Process.Start(psi);
            Log.Write("Ziel geoeffnet: " + t);
        }

        static void RunShell(string target, string args)
        {
            string t = Clean(target);
            if (args != null && args.Trim().Length > 0)
                t = t.Length > 0 ? t + " " + args : args;

            if (t.Length == 0) { Log.Write("Shell ohne Befehl."); return; }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "cmd.exe";
            psi.Arguments = "/c " + t;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            Process.Start(psi);
            Log.Write("Shell: " + t);
        }

        // ---- Tastenkombination ----

        public static void SendHotkey(string combo)
        {            List<ushort> mods;
            ushort vk;
            if (!TryParseHotkey(combo, out mods, out vk))
            {
                Log.Write("Tastenkombination ungueltig: " + (combo == null ? "" : combo));
                return;
            }
            SendCombo(mods, vk);
        }

        // Modifikatoren halten, Taste druecken, alles wieder loslassen.
        static void SendCombo(List<ushort> mods, ushort vk)
        {
            List<INPUT> down = new List<INPUT>();
            for (int i = 0; i < mods.Count; i++) down.Add(KeyInput(mods[i], false));
            down.Add(KeyInput(vk, false));
            SendInputArray(down);

            Thread.Sleep(15);

            List<INPUT> up = new List<INPUT>();
            up.Add(KeyInput(vk, true));
            for (int i = mods.Count - 1; i >= 0; i--) up.Add(KeyInput(mods[i], true));
            SendInputArray(up);
        }

        static void TapKey(ushort vk)
        {
            List<INPUT> list = new List<INPUT>();
            list.Add(KeyInput(vk, false));
            list.Add(KeyInput(vk, true));
            SendInputArray(list);
        }

        // ---- Medien ----

        static void RunMedia(string action)
        {
            string a = CanonicalMedia(action);
            if (a.Length == 0)
            {
                Log.Write("Unbekannte Medienaktion: " + (action == null ? "" : action));
                return;
            }

            if (a == "playpause") TapKey(VK_MEDIA_PLAY_PAUSE);
            else if (a == "next") TapKey(VK_MEDIA_NEXT_TRACK);
            else if (a == "prev") TapKey(VK_MEDIA_PREV_TRACK);
            else TapKey(VK_MEDIA_STOP);
        }

        // ---- Lautstaerke ----

        static void RunVolume(string dir, string args)
        {
            string d = CanonicalVolume(dir);
            if (d.Length == 0)
            {
                Log.Write("Unbekannte Lautstaerke-Aktion: " + (dir == null ? "" : dir));
                return;
            }

            ushort vk;
            bool mute = false;

            if (d == "mute") { vk = VK_VOLUME_MUTE; mute = true; }
            else if (d == "up") vk = VK_VOLUME_UP;
            else vk = VK_VOLUME_DOWN;

            int steps = 1;
            int parsed;
            if (!string.IsNullOrEmpty(args) && int.TryParse(args.Trim(), out parsed)) steps = parsed;
            if (steps < 1) steps = 1;
            if (steps > 50) steps = 50;
            if (mute) steps = 1;

            // Alle Schritte in einem Rutsch senden: kein Thread.Sleep im
            // UI-Thread, und Windows verarbeitet die Wiederholungen sauber.
            List<INPUT> list = new List<INPUT>();
            for (int i = 0; i < steps; i++)
            {
                list.Add(KeyInput(vk, false));
                list.Add(KeyInput(vk, true));
            }
            SendInputArray(list);
        }

        // ---- System ----

        static void RunSystem(string verb)
        {
            string v = CanonicalSystem(verb);
            if (v.Length == 0)
            {
                Log.Write("Unbekannter Systembefehl: " + (verb == null ? "" : verb));
                return;
            }

            switch (v)
            {
                case "lock": LockWorkStation(); break;
                case "sleep": SetSuspendState(false, false, false); break;
                case "hibernate": SetSuspendState(true, false, false); break;
                case "showdesktop": SendHotkey("Win+D"); break;
                case "taskmgr": RunTool("taskmgr.exe", ""); break;
                case "shutdown": RunTool("shutdown.exe", "/s /t 0"); break;
                case "restart": RunTool("shutdown.exe", "/r /t 0"); break;
                case "logoff": RunTool("shutdown.exe", "/l"); break;
                case "screenoff": ScreenOff(); break;
                case "alttab": SendHotkey("Alt+Tab"); break;
                case "taskview": SendHotkey("Win+Tab"); break;
                case "virtualdesktop": SendHotkey("Win+Ctrl+Right"); break;
                case "snip": SendHotkey("Win+Shift+S"); break;
                case "emojis": SendHotkey("Win+."); break;
                case "clipboard": SendHotkey("Win+V"); break;
                case "magnifier": RunTool("magnify.exe", ""); break;
                case "explorer": RunTool("explorer.exe", ""); break;
                case "startmenu": TapKey(VK_LWIN); break;
            }
        }

        // ------------------------------------------------------------------
        // Discord
        // ------------------------------------------------------------------

        // Target: mute | unmute | toggle | deafen | undeafen | toggledeafen
        // Args:   optionale Tastenkombination, die in Discord hinterlegt ist.
        //         Ohne Angabe gilt Discords Voreinstellung (Ctrl+Shift+M).
        //
        // Wichtig: Discords Taste ist ein Umschalter. "mute" und "unmute"
        // lassen sich damit nicht gezielt ausloesen, ohne den Zustand zu
        // kennen. Deshalb gilt:
        //   toggle  -> Discords eigene Taste (Zustand stimmt inkl. Anzeige)
        //   mute    -> Mikrofon auf Windows-Ebene stumm
        //   unmute  -> Mikrofon auf Windows-Ebene laut
        static void RunDiscord(string target, string args)
        {
            string action = Norm(target);

            switch (action)
            {
                case "toggle":
                case "mute2":
                    if (!Discord.IsRunning())
                    {
                        Log.Write("Discord laeuft nicht - nutze das Systemmikrofon.");
                        Discord.ToggleSystemMicMute();
                        return;
                    }
                    Discord.SendMuteHotkey(Clean(args));
                    break;

                case "mute":
                    if (!Discord.IsRunning())
                        Log.Write("Discord laeuft nicht - schalte das Systemmikrofon stumm.");
                    Discord.SetSystemMicMute(true);
                    break;

                case "unmute":
                    if (!Discord.IsRunning())
                        Log.Write("Discord laeuft nicht - schalte das Systemmikrofon laut.");
                    Discord.SetSystemMicMute(false);
                    break;

                case "deafen":
                case "undeafen":
                case "toggledeafen":
                    if (!Discord.IsRunning())
                    {
                        Log.Write("Discord laeuft nicht - Taubschalten entfaellt.");
                        return;
                    }
                    Discord.SendDeafenHotkey(Clean(args));
                    break;

                default:
                    Log.Write("Discord: unbekannte Aktion \"" + target + "\".");
                    break;
            }
        }

        static void RunTool(string file, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = file;
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
        }

        static void ScreenOff()
        {
            IntPtr result;
            SendMessageTimeout(HWND_BROADCAST, WM_SYSCOMMAND,
                (IntPtr)SC_MONITORPOWER, (IntPtr)2, SMTO_ABORTIFHUNG, 1000, out result);
        }

        // ---- Fenster aktivieren ----

        public static void ActivateWindow(string query)
        {
            string q = Clean(query);
            if (q.Length == 0) { Log.Write("Fenster ohne Titel."); return; }

            IntPtr found = IntPtr.Zero;

            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd)) return true;

                string title = WindowTitle(hWnd);
                if (!string.IsNullOrEmpty(title) &&
                    title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = hWnd;
                    return false;
                }

                string proc = WindowProcessName(hWnd);
                if (!string.IsNullOrEmpty(proc) &&
                    proc.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = hWnd;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero)
            {
                Log.Write("Kein Fenster gefunden: " + q);
                return;
            }

            ForceForeground(found);
            Log.Write("Fenster aktiviert: " + WindowTitle(found));
        }

        static void ForceForeground(IntPtr hWnd)
        {
            if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);            if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);

            IntPtr fg = GetForegroundWindow();
            if (fg == hWnd) return;

            uint fgProc;
            uint fgThread = GetWindowThreadProcessId(fg, out fgProc);
            uint myThread = GetCurrentThreadId();

            if (fgThread != 0 && fgThread != myThread)
            {
                AttachThreadInput(myThread, fgThread, true);
                SetForegroundWindow(hWnd);
                BringWindowToTop(hWnd);
                AttachThreadInput(myThread, fgThread, false);
            }
            else
            {
                SetForegroundWindow(hWnd);
                BringWindowToTop(hWnd);
            }
        }

        static string WindowTitle(IntPtr hWnd)
        {
            int len = GetWindowTextLength(hWnd);
            if (len <= 0) return "";
            StringBuilder sb = new StringBuilder(len + 2);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // Fenster fuer einen kurzen Tastendruck nach vorn holen und danach
        // den vorherigen Fokus wiederherstellen. Discord ignoriert globale
        // Tasten, solange es nicht im Vordergrund ist.
        // ------------------------------------------------------------------

        static IntPtr _previousForeground = IntPtr.Zero;

        public static bool FocusWindowByProcess(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return false;

            IntPtr found = IntPtr.Zero;
            string wanted = processName.ToLowerInvariant();

            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (GetWindowTextLength(hWnd) == 0) return true;

                string proc = WindowProcessName(hWnd);
                if (!string.IsNullOrEmpty(proc) && proc.ToLowerInvariant() == wanted)
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            if (found == IntPtr.Zero) return false;
            if (GetForegroundWindow() == found) { _previousForeground = found; return true; }

            _previousForeground = GetForegroundWindow();
            ForceForeground(found);
            return true;
        }

        public static void RestoreFocus()
        {
            if (_previousForeground == IntPtr.Zero) return;
            if (_previousForeground == GetForegroundWindow()) return;
            ForceForeground(_previousForeground);
            _previousForeground = IntPtr.Zero;
        }

        static string WindowProcessName(IntPtr hWnd)
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0) return "";
                using (Process p = Process.GetProcessById((int)pid))
                    return p.ProcessName;
            }
            catch { return ""; }
        }

        // ---- Makro ----

        // Kopiert die Schrittliste, damit spaetere Aenderungen in der
        // Oberflaeche das laufende Makro nicht mehr beeinflussen.
        static List<MacroStep> SnapshotSteps(List<MacroStep> steps)
        {
            List<MacroStep> copy = new List<MacroStep>();
            if (steps == null) return copy;

            foreach (MacroStep s in steps)
            {
                if (s == null) continue;   // defekte Eintraege still ueberspringen

                MacroStep c = new MacroStep();
                c.Type = s.Type;
                c.Value = s.Value;
                c.DelayMs = s.DelayMs;
                copy.Add(c);
            }
            return copy;
        }

        static void RunMacro(ActionItem a)
        {
            // Nicht a.Clone(): das bricht bei defekten (null) Schritten ab.
            List<MacroStep> steps = SnapshotSteps(a.Steps);

            Thread t = new Thread(delegate()
            {
                ExecuteMacro(steps);
            });
            t.IsBackground = true;
            t.Name = "DialMenu-Makro";
            t.Start();
        }

        static void ExecuteMacro(List<MacroStep> steps)
        {
            if (steps == null) return;

            foreach (MacroStep s in steps)
            {
                if (s == null) continue;

                string type = Norm(s.Type);
                int pause = s.DelayMs;
                if (pause < 0) pause = 0;
                if (pause > 60000) pause = 60000;

                try
                {
                    switch (type)
                    {
                        case "hotkey":
                            SendHotkey(s.Value);
                            break;

                        case "text":
                            TypeText(s.Value);
                            break;

                        case "program":
                            RunProgram(s.Value, "");
                            break;

                        case "delay":
                        case "wait":
                            if (pause == 0)
                            {
                                int n;
                                if (!string.IsNullOrEmpty(s.Value) && int.TryParse(s.Value.Trim(), out n))
                                {
                                    if (n > 60000) n = 60000;
                                    pause = n;
                                }
                            }
                            break;

                        default:
                            Log.Write("Unbekannter Makroschritt: " + s.Type);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("Makroschritt \"" + s.Type + "\" fehlgeschlagen: " + ex.Message);
                }

                if (pause > 0) Thread.Sleep(pause);
            }
        }

        static void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n') { TapKey(VK_RETURN); continue; }
                if (c == '\t') { TapKey(VK_TAB); continue; }
                SendUnicodeChar(c);
            }
        }

        static void SendUnicodeChar(char c)
        {
            INPUT[] arr = new INPUT[] { UnicodeInput(c, false), UnicodeInput(c, true) };
            SendInput(2, arr, Marshal.SizeOf(typeof(INPUT)));
        }

        // ------------------------------------------------------------------
        // Hilfen: Werte normalisieren
        // ------------------------------------------------------------------

        static string Clean(string s)
        {
            return s == null ? "" : s.Trim();
        }

        // Kleinschreibung ohne Leerzeichen/Bindestriche, Umlaute gefaltet.
        static string Slug(string s)
        {
            if (s == null) return "";

            StringBuilder sb = new StringBuilder();
            foreach (char raw in s.Trim().ToLowerInvariant())
            {
                char c = raw;
                if (c == 'ä') c = 'a';
                else if (c == 'ö') c = 'o';
                else if (c == 'ü') c = 'u';
                else if (c == 'ß') { sb.Append("ss"); continue; }

                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        static string Norm(string s)
        {
            return s == null ? "" : s.Trim().ToLowerInvariant();
        }

        // Macht aus englischen und deutschen Schreibweisen einen Kernbegriff.
        static string CanonicalMedia(string s)
        {
            string v = Slug(s);
            switch (v)
            {
                case "playpause": case "play": case "pause": case "toggle":
                case "wiedergabe": case "wiedergabepause": case "pauseplay":
                    return "playpause";
                case "next": case "weiter": case "naechster": case "naechstes":
                case "vorspulen": case "nexttrack":
                    return "next";
                case "prev": case "previous": case "back": case "zurueck":
                case "vorheriger": case "prevtrack":
                    return "prev";
                case "stop": case "stopp": case "anhalten":
                    return "stop";
            }
            return "";
        }

        static string CanonicalVolume(string s)
        {
            string v = Slug(s);
            switch (v)
            {
                case "mute": case "stumm": case "stummschalten": case "tonaus": case "tonausschalten":
                    return "mute";
                case "up": case "lauter": case "hoch": case "louder": case "plus": case "laut":
                case "lautstarkelauter":
                    return "up";
                case "down": case "leiser": case "runter": case "leise": case "minus": case "leiserstellen":
                    return "down";
            }
            return "";
        }

        static string CanonicalSystem(string s)
        {
            string v = Slug(s);
            switch (v)
            {
                case "lock": case "sperren": case "lockworkstation": case "computersperren":
                    return "lock";
                case "sleep": case "standby": case "schlafen": case "energiesparen": case "ruhemodus":
                    return "sleep";
                case "hibernate": case "ruhezustand": case "tiefschlaf":
                    return "hibernate";
                case "showdesktop": case "desktop": case "desktopzeigen": case "desktopanzeigen":
                    return "showdesktop";
                case "taskmgr": case "taskmanager": case "aufgabenmanager":
                    return "taskmgr";
                case "shutdown": case "herunterfahren": case "ausschalten": case "aus":
                    return "shutdown";
                case "restart": case "reboot": case "neustart": case "neustarten":
                    return "restart";
                case "logoff": case "abmelden": case "logout": case "benutzerwechseln":
                    return "logoff";
                case "screenoff": case "monitoraus": case "monitoroff": case "bildschirmaus":
                case "monitorausschalten":
                    return "screenoff";
                case "alttab": case "fensterwechseln": case "wechseln": case "fensterwechsel":
                    return "alttab";
                case "taskview": case "aufgabenansicht": case "fensteruebersicht":
                    return "taskview";
                case "virtualdesktop": case "desktoprechts": case "naechsterdesktop":
                case "virtuellerdesktop":
                    return "virtualdesktop";
                case "snip": case "screenshot": case "ausschnitt": case "ausschnittaufnehmen":
                    return "snip";
                case "emojis": case "emoji": case "emojitafel": case "emojiauswahl":
                    return "emojis";
                case "clipboard": case "zwischenablage": case "zwischenablageverlauf":
                    return "clipboard";
                case "magnifier": case "lupe": case "bildschirmlupe":
                    return "magnifier";
                case "explorer": case "dateiexplorer": case "dateimanager":
                    return "explorer";
                case "startmenu": case "start": case "startmenue": case "windowsstart":
                    return "startmenu";
            }
            return "";
        }

        // ------------------------------------------------------------------
        // Tastenkombination parsen
        // ------------------------------------------------------------------

        // Liefert die Modifikatoren in Drueck-Reihenfolge: Win, Ctrl, Alt, Shift.
        static bool TryParseHotkey(string combo, out List<ushort> mods, out ushort vk)
        {
            mods = new List<ushort>();
            vk = 0;

            if (combo == null) return false;
            string s = combo.Trim();
            if (s.Length == 0) return false;

            bool ctrl = false, shift = false, alt = false, win = false;
            bool haveKey = false;

            // "Ctrl++" bedeutet Strg + Plus-Taste; Split entfernt das leere
            // Segment, daher hier vorab in "Ctrl+plus" uebersetzen.
            if (s.EndsWith("++", StringComparison.Ordinal))
                s = s.Substring(0, s.Length - 2) + "+plus";

            string[] parts = s.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in parts)
            {
                // "Ctrl-Shift-Esc" erlauben, ohne die Minus-Taste zu verschlucken.
                string[] pieces = SplitDashes(raw);
                foreach (string piece in pieces)
                {
                    string p = piece.Trim().ToLowerInvariant();
                    if (p.Length == 0) continue;

                    if (p == "ctrl" || p == "strg" || p == "control" || p == "steuerung")
                    { ctrl = true; continue; }
                    if (p == "shift" || p == "umschalt" || p == "umschalten")
                    { shift = true; continue; }
                    if (p == "alt" || p == "option")
                    { alt = true; continue; }
                    if (p == "win" || p == "windows" || p == "super" || p == "meta" ||
                        p == "cmd" || p == "befehl")
                    { win = true; continue; }

                    ushort found;
                    if (!TryParseKey(p, out found)) return false;
                    if (haveKey) return false;   // nur eine echte Taste erlaubt
                    vk = found;
                    haveKey = true;
                }
            }

            if (!haveKey) return false;

            if (win) mods.Add(0x5B);    // VK_LWIN
            if (ctrl) mods.Add(0x11);   // VK_CONTROL
            if (alt) mods.Add(0x12);    // VK_MENU
            if (shift) mods.Add(0x10);  // VK_SHIFT

            return true;
        }

        // Zerlegt "Ctrl-Shift-Esc" in Einzelteile; ein einzelnes "-" oder
        // ein Teil wie "Strg" bleibt dabei unangetastet.
        static string[] SplitDashes(string s)
        {
            if (s.IndexOf('-') < 0) return new string[] { s };

            List<string> outp = new List<string>();
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '-') continue;
                // Ein Bindestrich am Rand oder zwischen zwei Bindestrichen
                // gehoert zur Taste selbst (z. B. "-" oder "Ctrl--").
                if (i == 0 || i == s.Length - 1 || s[i - 1] == '-')
                    continue;
                outp.Add(s.Substring(start, i - start));
                start = i + 1;
            }
            outp.Add(s.Substring(start));
            return outp.ToArray();
        }

        static bool TryParseKey(string p, out ushort vk)
        {
            vk = 0;
            if (p.Length == 0) return false;

            if (p.Length == 1)
            {
                char c = p[0];
                if (c >= 'a' && c <= 'z') { vk = (ushort)(0x41 + (c - 'a')); return true; }
                if (c >= '0' && c <= '9') { vk = (ushort)(0x30 + (c - '0')); return true; }
                if (c == '.') { vk = 0xBE; return true; }    // OEM period
                if (c == ',') { vk = 0xBC; return true; }
                if (c == '-') { vk = 0xBD; return true; }
                if (c == '=') { vk = 0xBB; return true; }
                if (c == ';') { vk = 0xBA; return true; }
                if (c == '/') { vk = 0xBF; return true; }
                if (c == '\\') { vk = 0xDC; return true; }
                if (c == '[') { vk = 0xDB; return true; }
                if (c == ']') { vk = 0xDD; return true; }
                if (c == '\'') { vk = 0xDE; return true; }
                if (c == '`') { vk = 0xC0; return true; }
                return false;
            }

            // F1 .. F24
            if (p[0] == 'f')
            {
                int n;
                if (int.TryParse(p.Substring(1), out n) && n >= 1 && n <= 24)
                {
                    vk = (ushort)(0x70 + (n - 1));
                    return true;
                }
                return false;
            }

            switch (p)
            {
                case "esc": case "escape":
                    vk = 0x1B; return true;
                case "tab":
                    vk = 0x09; return true;
                case "enter": case "return": case "eingabe":
                    vk = 0x0D; return true;
                case "space": case "leertaste": case "leerzeichen":
                    vk = 0x20; return true;
                case "backspace": case "rueck": case "ruecktaste":
                    vk = 0x08; return true;
                case "delete": case "del": case "entf": case "entfernen":
                    vk = 0x2E; return true;
                case "insert": case "ins": case "einfg": case "einfuegen":
                    vk = 0x2D; return true;
                case "home": case "pos1":
                    vk = 0x24; return true;
                case "end": case "ende":
                    vk = 0x23; return true;
                case "pageup": case "pgup": case "bildauf":
                    vk = 0x21; return true;
                case "pagedown": case "pgdn": case "bildab":
                    vk = 0x22; return true;
                case "up": case "oben": case "pfeiloben": case "arrowup":
                    vk = 0x26; return true;
                case "down": case "unten": case "pfeilunten": case "arrowdown":
                    vk = 0x28; return true;
                case "left": case "links": case "pfeillinks": case "arrowleft":
                    vk = 0x25; return true;
                case "right": case "rechts": case "pfeilrechts": case "arrowright":
                    vk = 0x27; return true;
                case "print": case "printscreen": case "druck": case "drucken":
                    vk = 0x2C; return true;
                case "pause": case "pausieren":
                    vk = 0x13; return true;
                case "capslock": case "feststelltaste":
                    vk = 0x14; return true;
                case "numlock": case "num":
                    vk = 0x90; return true;
                case "scrolllock": case "rollen":
                    vk = 0x91; return true;
                case "plus": case "add": case "numplus":
                    vk = 0x6B; return true;          // NumPad +
                case "minus": case "sub": case "numminus":
                    vk = 0x6D; return true;          // NumPad -
                case "comma": case "semikolon": case "semicolon":
                    vk = 0xBA; return true;
                case "period": case "dot": case "punkt":
                    vk = 0xBE; return true;
                case "slash": case "schraegstrich":
                    vk = 0xBF; return true;
                case "backslash": case "umgekehrtschraegstrich":
                    vk = 0xDC; return true;
                case "grave": case "backtick": case "akzent":
                    vk = 0xC0; return true;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // Pruefung fuer die Oberflaeche (deutsche Meldungen)
        // ------------------------------------------------------------------

        public static List<string> Validate(ActionItem a)
        {
            List<string> errors = new List<string>();
            if (a == null)
            {
                errors.Add("Aktion ist leer.");
                return errors;
            }

            string kind = Norm(a.Kind);
            string target = Clean(a.Target);

            switch (kind)
            {
                case "program":
                    if (target.Length == 0)
                        errors.Add("Programm: Bitte einen Programmnamen oder Pfad angeben.");
                    break;

                case "url":
                    if (target.Length == 0)
                        errors.Add("URL: Bitte ein Ziel angeben (z. B. https://example.com).");
                    break;

                case "shell":
                    if (target.Length == 0)
                        errors.Add("Befehl: Bitte einen Shell-Befehl angeben.");
                    break;

                case "hotkey":
                    if (target.Length == 0)
                    {
                        errors.Add("Tastenkombination: Bitte eine Kombination angeben (z. B. Ctrl+Alt+M).");
                    }
                    else
                    {
                        List<ushort> mods;
                        ushort vk;
                        if (!TryParseHotkey(target, out mods, out vk))
                            errors.Add("Tastenkombination \"" + target +
                                "\" konnte nicht gelesen werden. Beispiel: Ctrl+Alt+M oder F11.");
                    }
                    break;

                case "media":
                    if (target.Length == 0)
                        errors.Add("Medien: Bitte eine Aktion waehlen (playpause, next, prev, stop).");
                    else if (CanonicalMedia(target).Length == 0)
                        errors.Add("Medien: Unbekannte Aktion \"" + target +
                            "\". Erlaubt: playpause, next, prev, stop.");
                    break;

                case "volume":
                    if (target.Length == 0)
                        errors.Add("Lautstaerke: Bitte up, down oder mute angeben.");
                    else if (CanonicalVolume(target).Length == 0)
                        errors.Add("Lautstaerke: Unbekannte Richtung \"" + target +
                            "\". Erlaubt: up, down, mute.");
                    else
                    {
                        string extra = Clean(a.Args);
                        if (extra.Length > 0)
                        {
                            int n;
                            if (!int.TryParse(extra, out n))
                                errors.Add("Lautstaerke: Die Schrittanzahl \"" + extra + "\" ist keine Zahl.");
                            else if (n < 1 || n > 50)
                                errors.Add("Lautstaerke: Die Schrittanzahl muss zwischen 1 und 50 liegen.");
                        }
                    }
                    break;

                case "system":
                    if (target.Length == 0)
                        errors.Add("System: Bitte einen Befehl angeben (z. B. lock, sleep, taskmgr).");
                    else if (CanonicalSystem(target).Length == 0)
                        errors.Add("System: Unbekannter Befehl \"" + target +
                            "\". Erlaubt: lock, sleep, hibernate, showdesktop, taskmgr, shutdown, " +
                            "restart, logoff, screenoff, alttab, taskview, virtualdesktop, snip, " +
                            "emojis, clipboard, magnifier, explorer, startmenu.");
                    break;

                case "activate":
                    if (target.Length == 0)
                        errors.Add("Fenster: Bitte einen Fenstertitel oder Prozessnamen angeben.");
                    break;

                case "discord":
                {
                    string[] allowed = { "toggle", "mute", "unmute",
                                         "deafen", "undeafen", "toggledeafen" };
                    bool known = false;
                    foreach (string s in allowed)
                        if (s == target) { known = true; break; }

                    if (!known)
                        errors.Add("Discord: Unbekannte Aktion \"" + target +
                            "\". Erlaubt: toggle, mute, unmute, deafen, undeafen, toggledeafen.");

                    string extra = Clean(a.Args);
                    if (extra.Length > 0)
                    {
                        List<ushort> mods;
                        ushort vk;
                        if (!TryParseHotkey(extra, out mods, out vk))
                            errors.Add("Discord: Die Tastenkombination \"" + extra +
                                "\" konnte nicht gelesen werden. Beispiel: Ctrl+Shift+M.");
                    }
                    break;
                }

                case "macro":
                    if (a.Steps == null || a.Steps.Count == 0)
                    {
                        errors.Add("Makro: Das Makro enthaelt keine Schritte.");
                    }
                    else
                    {
                        for (int i = 0; i < a.Steps.Count; i++)
                        {
                            MacroStep s = a.Steps[i];
                            int nr = i + 1;
                            if (s == null)
                            {
                                errors.Add("Makro: Schritt " + nr + " ist leer.");
                                continue;
                            }

                            string type = Norm(s.Type);
                            string val = Clean(s.Value);

                            if (type == "hotkey")
                            {
                                if (val.Length == 0)
                                {
                                    errors.Add("Makro: Schritt " + nr + " (Tastenkombination) hat keine Taste.");
                                }
                                else
                                {
                                    List<ushort> m;
                                    ushort k;
                                    if (!TryParseHotkey(val, out m, out k))
                                        errors.Add("Makro: Schritt " + nr +
                                            " (Tastenkombination) ist ungueltig: \"" + val + "\".");
                                }
                            }
                            else if (type == "text")
                            {
                                if (val.Length == 0)
                                    errors.Add("Makro: Schritt " + nr + " (Text) ist leer.");
                            }
                            else if (type == "program")
                            {
                                if (val.Length == 0)
                                    errors.Add("Makro: Schritt " + nr + " (Programm) hat kein Ziel.");
                            }
                            else if (type == "delay" || type == "wait")
                            {
                                if (val.Length > 0)
                                {
                                    int n;
                                    if (!int.TryParse(val, out n) || n < 0)
                                        errors.Add("Makro: Schritt " + nr +
                                            " (Wartezeit) ist keine gueltige Millisekundenzahl.");
                                }
                            }
                            else
                            {
                                errors.Add("Makro: Schritt " + nr + " hat einen unbekannten Typ \"" +
                                    s.Type + "\". Erlaubt: hotkey, text, delay, program, wait.");
                            }

                            if (s.DelayMs < 0)
                                errors.Add("Makro: Schritt " + nr + " hat eine negative Verzoegerung.");
                        }
                    }
                    break;

                case "":
                    errors.Add("Aktion: Die Art der Aktion fehlt.");
                    break;

                default:
                    errors.Add("Aktion: Unbekannte Art \"" + a.Kind + "\".");
                    break;
            }

            return errors;
        }

        // ------------------------------------------------------------------
        // Kurzbeschreibung fuer die Liste (deutsch)
        // ------------------------------------------------------------------

        public static string Describe(ActionItem a)
        {
            if (a == null) return "Leere Aktion";

            string kind = Norm(a.Kind);
            string target = Clean(a.Target);

            switch (kind)
            {
                case "program":
                    return target.Length > 0 ? "Programm: " + target : "Programm ohne Ziel";

                case "url":
                    return target.Length > 0 ? "URL: " + target : "URL ohne Ziel";

                case "shell":
                    return target.Length > 0 ? "Befehl: " + target : "Befehl ohne Inhalt";

                case "hotkey":
                    return target.Length > 0 ? "Tastenkombination: " + target : "Tastenkombination ohne Taste";

                case "media":
                {
                    string m = CanonicalMedia(target);
                    if (m.Length == 0) return "Medien: " + (target.Length > 0 ? target : "unbekannt");
                    return "Medien: " + MediaLabel(m);
                }

                case "volume":
                {
                    string v = CanonicalVolume(target);
                    if (v.Length == 0) return "Lautstaerke: " + (target.Length > 0 ? target : "unbekannt");
                    int steps = 1;
                    int n;
                    if (!string.IsNullOrEmpty(a.Args) && int.TryParse(a.Args.Trim(), out n) && n > 0) steps = n;
                    if (v == "mute") return "Lautstaerke: Stumm schalten";
                    return "Lautstaerke: " + (v == "up" ? "lauter" : "leiser") +
                        " (" + steps + (steps == 1 ? " Schritt)" : " Schritte)");
                }

                case "system":
                {
                    string s = CanonicalSystem(target);
                    if (s.Length == 0) return "System: " + (target.Length > 0 ? target : "unbekannt");
                    return "System: " + SystemLabel(s);
                }

                case "activate":
                    return target.Length > 0 ? "Fenster aktivieren: " + target : "Fenster ohne Titel";

                case "discord":
                {
                    string extra = Clean(a.Args);
                    string note = extra.Length > 0 ? extra : Discord.DefaultMuteHotkey;

                    if (target == "toggle")
                        return "Discord: Stummschalten umschalten (" + note + ")";
                    if (target == "mute") return "Discord: Mikrofon stumm (systemweit)";
                    if (target == "unmute") return "Discord: Mikrofon laut (systemweit)";
                    if (target == "deafen" || target == "undeafen" || target == "toggledeafen")
                        return "Discord: Taubschalten umschalten (" + note + ")";
                    return "Discord: " + (target.Length > 0 ? target : "unbekannt");
                }

                case "macro":
                {
                    int count = a.Steps == null ? 0 : a.Steps.Count;
                    return "Makro (" + count + (count == 1 ? " Schritt)" : " Schritte)");
                }

                case "":
                    return "Aktion ohne Art";

                default:
                    return "Unbekannt: " + a.Kind;
            }
        }

        static string MediaLabel(string m)
        {
            if (m == "playpause") return "Play/Pause";
            if (m == "next") return "Naechster Titel";
            if (m == "prev") return "Vorheriger Titel";
            return "Stop";
        }

        static string SystemLabel(string s)
        {
            switch (s)
            {
                case "lock": return "Sperren";
                case "sleep": return "Standby";
                case "hibernate": return "Ruhezustand";
                case "showdesktop": return "Desktop zeigen";
                case "taskmgr": return "Task-Manager";
                case "shutdown": return "Herunterfahren";
                case "restart": return "Neustart";
                case "logoff": return "Abmelden";
                case "screenoff": return "Monitor aus";
                case "alttab": return "Fenster wechseln";
                case "taskview": return "Aufgabenansicht";
                case "virtualdesktop": return "Naechster Desktop";
                case "snip": return "Screenshot-Ausschnitt";
                case "emojis": return "Emoji-Tafel";
                case "clipboard": return "Zwischenablage";
                case "magnifier": return "Bildschirmlupe";
                case "explorer": return "Datei-Explorer";
                case "startmenu": return "Startmenue";
            }
            return s;
        }

        // ------------------------------------------------------------------
        // SendInput-Interop
        // ------------------------------------------------------------------

        static void SendInputArray(List<INPUT> list)
        {
            if (list == null || list.Count == 0) return;
            INPUT[] arr = list.ToArray();
            SendInput((uint)arr.Length, arr, Marshal.SizeOf(typeof(INPUT)));
        }

        static INPUT KeyInput(ushort vk, bool up)
        {
            INPUT inp = new INPUT();
            inp.type = INPUT_KEYBOARD;
            inp.u.ki.wVk = vk;
            inp.u.ki.wScan = 0;

            uint flags = 0;
            if (IsExtendedKey(vk)) flags |= KEYEVENTF_EXTENDEDKEY;
            if (up) flags |= KEYEVENTF_KEYUP;
            inp.u.ki.dwFlags = flags;

            inp.u.ki.time = 0;
            inp.u.ki.dwExtraInfo = InjectedTag;
            return inp;
        }

        // Tasten, die Windows als "erweitert" erwartet (Media, Navigation, Win).
        static bool IsExtendedKey(ushort vk)
        {
            switch (vk)
            {
                case 0xAD: case 0xAE: case 0xAF:            // Volume mute/down/up
                case 0xB0: case 0xB1: case 0xB2: case 0xB3: // Media keys
                case 0x2C:                                   // Print
                case 0x2D: case 0x2E:                        // Insert, Delete
                case 0x21: case 0x22: case 0x23: case 0x24:  // PageUp/Down, End, Home
                case 0x25: case 0x26: case 0x27: case 0x28:  // Arrows
                case 0x5B: case 0x5C:                        // LWin, RWin
                case 0x90: case 0x91:                        // NumLock, ScrollLock
                case 0xA3: case 0xA5:                        // Right Ctrl, Right Alt
                case 0x6F:                                   // Divide (numpad)
                    return true;
            }
            return false;
        }

        static INPUT UnicodeInput(char c, bool up)
        {
            INPUT inp = new INPUT();
            inp.type = INPUT_KEYBOARD;
            inp.u.ki.wVk = 0;
            inp.u.ki.wScan = (ushort)c;
            inp.u.ki.dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0);
            inp.u.ki.time = 0;
            inp.u.ki.dwExtraInfo = InjectedTag;
            return inp;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        // Union mit LayoutKind.Explicit; alle Varianten beginnen bei 0.
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        // Sequenziell, damit die Union unter x64 korrekt bei Offset 8 liegt
        // (Windows erwartet dort die 8-Byte-Ausrichtung des Zeigers).
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        // ------------------------------------------------------------------
        // Win32
        // ------------------------------------------------------------------

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern bool LockWorkStation();

        // BOOLEAN ist 1 Byte, nicht 4 wie BOOL.
        [DllImport("powrprof.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        static extern bool SetSuspendState(
            [MarshalAs(UnmanagedType.U1)] bool hibernate,
            [MarshalAs(UnmanagedType.U1)] bool forceCritical,
            [MarshalAs(UnmanagedType.U1)] bool disableWakeEvent);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam,
            IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    }
}
