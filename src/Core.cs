using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace DialMenu
{
    // ------------------------------------------------------------------
    // Datenmodell
    // ------------------------------------------------------------------

    // Eine Aktion im Ring. Alles, was der Drehknopf ausloesen kann.
    [DataContract]
    public class ActionItem
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Label = "Neue Aktion";
        [DataMember] public string Kind = "program";
        [DataMember] public string Target = "";
        [DataMember] public string Args = "";
        [DataMember] public string Hint = "";
        [DataMember] public string Icon = "";
        [DataMember] public string Group = "";

        // Nur fuer Makros: Schrittliste.
        [DataMember] public List<MacroStep> Steps = new List<MacroStep>();

        public ActionItem Clone()
        {
            ActionItem c = new ActionItem();
            c.Id = Guid.NewGuid().ToString("N");
            c.Label = Label; c.Kind = Kind; c.Target = Target;
            c.Args = Args; c.Hint = Hint; c.Icon = Icon; c.Group = Group;
            c.Steps = new List<MacroStep>();
            foreach (MacroStep s in Steps) c.Steps.Add(s.Clone());
            return c;
        }
    }

    // Ein Schritt innerhalb eines Makros.
    [DataContract]
    public class MacroStep
    {
        [DataMember] public string Type = "hotkey";   // hotkey | text | delay | program | wait
        [DataMember] public string Value = "";
        [DataMember] public int DelayMs = 120;

        public MacroStep Clone()
        {
            MacroStep c = new MacroStep();
            c.Type = Type; c.Value = Value; c.DelayMs = DelayMs;
            return c;
        }
    }

    // Eine Gruppe von Aktionen mit eigenem Ring.
    [DataContract]
    public class Profile
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Name = "Hauptprofil";
        [DataMember] public List<ActionItem> Actions = new List<ActionItem>();
    }

    // Ein unterstuetztes Eingabegeraet.
    [DataContract]
    public class DeviceBinding
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Name = "ISY IGK-6500";
        [DataMember] public string Match = "VID_258A&PID_017E";
        [DataMember] public bool Enabled = true;

        // Rohe HID-Reports dieses Geraets, falls bekannt.
        [DataMember] public List<string> Reports = new List<string>();
    }

    [DataContract]
    public class Config
    {
        [DataMember] public int Version = 2;
        [DataMember] public string Position = "unten rechts";
        [DataMember] public int HideDelayMs = 1800;
        [DataMember] public int LongPressMs = 700;
        [DataMember] public bool Enabled = true;
        [DataMember] public bool StartWithWindows = false;
        [DataMember] public double Scale = 1.0;
        [DataMember] public bool ShowLabels = false;

        // Sprache der Oberflaeche: "de" oder "en".
        [DataMember] public string Language = "";

        // "auto" folgt Windows, sonst "dark" oder "light".
        [DataMember] public string Appearance = "auto";

        // Ringthema, siehe RingThemes.
        [DataMember] public string RingTheme = "graphite";

        [DataMember] public List<DeviceBinding> Devices = new List<DeviceBinding>();
        [DataMember] public List<Profile> Profiles = new List<Profile>();
        [DataMember] public string ActiveProfileId = "";

        // Alte flache Liste bleibt lesbar, damit bestehende Konfigurationen nicht brechen.
        [DataMember] public List<ActionItem> Actions = new List<ActionItem>();

        public static string AppName { get { return "DialMenu"; } }

        public static string Folder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
            }
        }

        public static string FilePath { get { return Path.Combine(Folder, "config.json"); } }

        // Aktives Profil, notfalls das erste.
        public Profile CurrentProfile
        {
            get
            {
                if (Profiles == null || Profiles.Count == 0) return null;
                if (!string.IsNullOrEmpty(ActiveProfileId))
                {
                    foreach (Profile p in Profiles)
                        if (p.Id == ActiveProfileId) return p;
                }
                return Profiles[0];
            }
        }

        public List<ActionItem> CurrentActions
        {
            get
            {
                Profile p = CurrentProfile;
                return p != null ? p.Actions : new List<ActionItem>();
            }
        }

        public static Config Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    // Der JSON-Serier lehnt eine Datei mit Byte-Order-Mark ab.
                    // Manche Editoren schreiben ein BOM, deshalb hier entfernen -
                    // sonst ginge die ganze Konfiguration verloren.
                    byte[] raw = File.ReadAllBytes(FilePath);
                    int offset = 0;
                    if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
                        offset = 3;

                    using (MemoryStream ms = new MemoryStream(raw, offset, raw.Length - offset))
                    {
                        DataContractJsonSerializer s = new DataContractJsonSerializer(typeof(Config));
                        Config c = (Config)s.ReadObject(ms);
                        if (c != null)
                        {
                            c.Migrate();
                            c.Normalize();
                            return c;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string bad = FilePath + ".defekt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    File.Copy(FilePath, bad, true);
                }
                catch { }
                Log.Write("Konfiguration konnte nicht gelesen werden: " + ex.Message);
            }

            Config fresh = CreateDefault();
            try { fresh.Save(); } catch { }
            return fresh;
        }

        // Alte Strukturen (flache Actions, ohne Profile/Geraete) uebernehmen.
        public void Migrate()
        {
            if (Profiles == null) Profiles = new List<Profile>();
            if (Devices == null) Devices = new List<DeviceBinding>();
            if (Actions == null) Actions = new List<ActionItem>();

            if (Profiles.Count == 0)
            {
                Profile p = new Profile();
                p.Name = "Hauptprofil";
                p.Actions = Actions.Count > 0 ? Actions : CreateDefault().Actions;
                Profiles.Add(p);
                ActiveProfileId = p.Id;
            }

            foreach (Profile p in Profiles)
            {
                if (p.Actions == null) p.Actions = new List<ActionItem>();
                if (string.IsNullOrEmpty(p.Id)) p.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrEmpty(p.Name)) p.Name = "Profil";
                foreach (ActionItem a in p.Actions)
                    if (string.IsNullOrEmpty(a.Id)) a.Id = Guid.NewGuid().ToString("N");
            }

            if (Devices.Count == 0)
            {
                DeviceBinding d = new DeviceBinding();
                d.Name = "ISY IGK-6500";
                d.Match = "VID_258A&PID_017E";
                Devices.Add(d);
            }

            if (string.IsNullOrEmpty(ActiveProfileId) && Profiles.Count > 0)
                ActiveProfileId = Profiles[0].Id;

            Version = 2;
        }

        public void Normalize()
        {
            if (Devices == null) Devices = new List<DeviceBinding>();
            if (Profiles == null) Profiles = new List<Profile>();
            if (Actions == null) Actions = new List<ActionItem>();

            if (HideDelayMs < 400) HideDelayMs = 400;
            if (HideDelayMs > 20000) HideDelayMs = 20000;
            if (LongPressMs < 250) LongPressMs = 250;
            if (LongPressMs > 3000) LongPressMs = 3000;
            if (Scale < 0.6) Scale = 0.6;
            if (Scale > 2.2) Scale = 2.2;
            if (string.IsNullOrEmpty(Position)) Position = "unten rechts";
            if (string.IsNullOrEmpty(Language)) Language = Strings.Detect();
            if (string.IsNullOrEmpty(Appearance)) Appearance = "auto";
            if (string.IsNullOrEmpty(RingTheme)) RingTheme = "graphite";
            if (string.IsNullOrEmpty(ActiveProfileId) && Profiles.Count > 0)
                ActiveProfileId = Profiles[0].Id;

            Strings.Current = Language;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                using (FileStream fs = File.Create(FilePath))
                {
                    DataContractJsonSerializer s = new DataContractJsonSerializer(typeof(Config));
                    s.WriteObject(fs, this);
                }
            }
            catch (Exception ex)
            {
                Log.Write("Speichern fehlgeschlagen: " + ex.Message);
            }
        }

        public static Config CreateDefault()
        {
            Config c = new Config();

            DeviceBinding dev = new DeviceBinding();
            dev.Name = "ISY IGK-6500";
            dev.Match = "VID_258A&PID_017E";
            c.Devices.Add(dev);

            Profile main = new Profile();
            main.Name = "Hauptprofil";
            main.Actions.Add(Item("Browser", "url", "https://www.google.com", ""));
            main.Actions.Add(Item("Explorer", "program", "explorer.exe", ""));
            main.Actions.Add(Item("Terminal", "program", "wt.exe", ""));
            main.Actions.Add(Item("Rechner", "program", "calc.exe", ""));
            main.Actions.Add(Item("Einstellungen", "url", "ms-settings:", ""));
            main.Actions.Add(Item("Task-Manager", "system", "taskmgr", ""));
            main.Actions.Add(Item("Play / Pause", "media", "playpause", ""));
            main.Actions.Add(Item("Lauter", "volume", "up", "4"));
            main.Actions.Add(Item("Leiser", "volume", "down", "4"));
            main.Actions.Add(Item("Desktop zeigen", "system", "showdesktop", ""));
            main.Actions.Add(Item("Sperren", "system", "lock", ""));
            c.Profiles.Add(main);
            c.ActiveProfileId = main.Id;

            c.Actions = main.Actions;
            return c;
        }

        static ActionItem Item(string label, string kind, string target, string args)
        {
            ActionItem a = new ActionItem();
            a.Label = label; a.Kind = kind; a.Target = target; a.Args = args;
            return a;
        }
    }

    // ------------------------------------------------------------------
    // Protokoll
    // ------------------------------------------------------------------

    public static class Log
    {
        static readonly object Lock = new object();

        public static string FilePath { get { return Path.Combine(Config.Folder, "dialmenu.log"); } }

        public static void Write(string msg)
        {
            lock (Lock)
            {
                try
                {
                    if (!Directory.Exists(Config.Folder)) Directory.CreateDirectory(Config.Folder);
                    FileInfo fi = new FileInfo(FilePath);
                    if (fi.Exists && fi.Length > 512 * 1024) File.Delete(FilePath);
                    File.AppendAllText(FilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + msg + Environment.NewLine,
                        Encoding.UTF8);
                }
                catch { }
            }
        }
    }
}
