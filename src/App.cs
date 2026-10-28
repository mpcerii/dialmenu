using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace DialMenu
{
    // Anwendungseinstieg, Einzelinstanz, Infobereich und Verdrahtung
    // zwischen Eingabe, Overlay und Konfiguration.
    public class DialMenuApp : System.Windows.Application
    {
        const string MutexName = "DialMenu.SingleInstance";
        const string ShowEventName = "DialMenu.ShowSettings";

        internal static EventWaitHandle ShowSignal;

        Config _config;
        InputEngine _input;
        OverlayWindow _overlay;
        NotifyIcon _tray;
        SettingsWindow _settings;
        bool _exiting;
        bool _lastStartWithWindows;
        string _trayLanguage;

        [STAThread]
        public static void Main(string[] args)
        {
            bool createdMutex;
            bool createdEvent;
            Mutex mutex = new Mutex(true, MutexName, out createdMutex);

            // Das Signal wird in beiden Faellen angelegt: Die bereits laufende
            // Instanz wartet darauf, die zweite weckt sie nur.
            EventWaitHandle signal = new EventWaitHandle(
                false, EventResetMode.AutoReset, ShowEventName, out createdEvent);

            if (!createdMutex)
            {
                try
                {
                    signal.Set();
                }
                catch (Exception ex)
                {
                    Log.Write("Zweite Instanz konnte nicht signalisieren: " + ex.Message);
                }
                return;
            }

            ShowSignal = signal;

            DialMenuApp app = new DialMenuApp();
            app.Initialize();
            app.Run();

            try { mutex.ReleaseMutex(); }
            catch { }
            try { signal.Close(); }
            catch { }
        }

        void Initialize()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DispatcherUnhandledException += OnDispatcherUnhandled;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnTaskUnobserved;
        }

        // ------------------------------------------------------------------
        // Lebenszyklus
        // ------------------------------------------------------------------

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _config = Config.Load();
            Log.Write("Start. " + _config.Profiles.Count + " Profile, "
                + _config.CurrentActions.Count + " Befehle im aktiven Profil.");

            try
            {
                _overlay = new OverlayWindow(_config);
                _overlay.Executed += OnExecuted;
            }
            catch (Exception ex)
            {
                Log.Write("Overlay konnte nicht erzeugt werden: " + ex.Message);
            }

            try
            {
                _input = new InputEngine(_config);
                _input.Rotated += OnRotated;
                _input.Pressed += OnPressed;
                _input.LongPressed += OnLongPressed;
                _input.Start();
            }
            catch (Exception ex)
            {
                Log.Write("Eingabe konnte nicht gestartet werden: " + ex.Message);
            }

            BuildTray();

            // Autostart bei jedem Start mit der Konfiguration abgleichen -
            // so repariert sich ein von Hand geloeschter Eintrag selbst.
            _lastStartWithWindows = _config.StartWithWindows;
            string autostartDetail;
            Autostart.Apply(_config.StartWithWindows, out autostartDetail);
            Log.Write("Autostart: " + autostartDetail);

            StartSignalListener();

            if (!HasArgument("--tray"))
                Dispatcher.BeginInvoke(new Action(OpenSettings));
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _exiting = true;
            DisposeTray();
            if (_input != null) { try { _input.Dispose(); } catch { } _input = null; }
            if (_overlay != null) { try { _overlay.Close(); } catch { } _overlay = null; }
            Log.Write("Beendet");
            base.OnExit(e);
        }

        static bool HasArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void StartSignalListener()
        {
            Thread thread = new Thread(delegate()
            {
                while (true)
                {
                    bool signalled;
                    try { signalled = ShowSignal.WaitOne(); }
                    catch { return; }
                    if (!signalled) continue;

                    try
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (!_exiting) OpenSettings();
                        }));
                    }
                    catch
                    {
                        return;
                    }
                }
            });
            thread.IsBackground = true;
            thread.Name = "DialMenu.Signal";
            thread.Start();
        }

        // ------------------------------------------------------------------
        // Eingabe
        // ------------------------------------------------------------------

        void OnRotated(int direction)
        {
            RunOnUi(delegate
            {
                if (_overlay != null) _overlay.Move(direction);
            });
        }

        void OnPressed()
        {
            RunOnUi(delegate
            {
                if (_overlay != null) _overlay.Commit();
            });
        }

        void OnLongPressed()
        {
            RunOnUi(delegate
            {
                if (_overlay != null) _overlay.DismissSelection();
            });
        }

        void OnExecuted(ActionItem item)
        {
            if (item == null) return;
            Log.Write("Ausgeführt: " + item.Label + " (" + item.Kind + ")");
        }

        void RunOnUi(Action action)
        {
            if (_exiting) return;
            try
            {
                if (Dispatcher.CheckAccess()) action();
                else Dispatcher.BeginInvoke(action);
            }
            catch (Exception ex)
            {
                Log.Write("UI-Aufruf fehlgeschlagen: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Oeffentliche Schnittstelle fuer die Einstellungen
        // ------------------------------------------------------------------

        public Config CurrentConfig { get { return _config; } }

        // Die Eingabe-Engine, damit die Einstellungen den Lernassistenten
        // anschliessen koennen.
        public InputEngine Engine { get { return _input; } }

        public bool IsExiting { get { return _exiting; } }

        // Speichert sofort und schiebt die Aenderung in Eingabe und Overlay.
        public void ConfigChanged()
        {
            try
            {
                _config.Save();

                if (_input != null) _input.UpdateConfig(_config);
                if (_overlay != null) _overlay.ApplyConfig(_config);

                if (_config.StartWithWindows != _lastStartWithWindows)
                {
                    _lastStartWithWindows = _config.StartWithWindows;
                    string detail;
                    Autostart.Apply(_config.StartWithWindows, out detail);
                    Log.Write("Autostart: " + detail);
                }

                // Ein Sprachwechsel aendert Text und Menue des Infobereichs.
                if (!string.Equals(_trayLanguage, _config.Language, StringComparison.Ordinal))
                {
                    _trayLanguage = _config.Language;
                    RefreshTray();
                }
                else if (_tray != null)
                {
                    _tray.Text = _config.Enabled ? Config.AppName : Strings.Get("app.trayPaused");
                }
            }
            catch (Exception ex)
            {
                Log.Write("Konfiguration konnte nicht übernommen werden: " + ex.Message);
            }
        }

        public void PreviewOverlay()
        {
            if (_overlay == null) return;
            _overlay.ShowOverlay();
            _overlay.Nudge();
        }

        public void OpenSettings()
        {
            OpenSettings(null);
        }

        public void OpenSettings(string section)
        {
            try
            {
                if (_settings == null)
                {
                    _settings = new SettingsWindow(_config, this);
                    _settings.Closed += delegate { _settings = null; };
                    _settings.Show();
                }
                else
                {
                    if (_settings.WindowState == WindowState.Minimized)
                        _settings.WindowState = WindowState.Normal;
                    _settings.Show();
                    _settings.Activate();
                }

                if (!string.IsNullOrEmpty(section)) _settings.ShowSection(section);

                _settings.Topmost = true;
                _settings.Topmost = false;
            }
            catch (Exception ex)
            {
                Log.Write("Einstellungen konnten nicht geöffnet werden: " + ex.Message);
                MessageBox.Show(Strings.Get("app.settingsError") + "\r\n\r\n" + ex.Message,
                    Config.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ExitApplication()
        {
            _exiting = true;
            DisposeTray();
            Shutdown();
        }

        // ------------------------------------------------------------------
        // Infobereich
        // ------------------------------------------------------------------

        void BuildTray()
        {
            if (_tray == null)
            {
                _tray = new NotifyIcon();
                _tray.Icon = TrayIconFactory.Create();
                _tray.Visible = true;
                _tray.DoubleClick += delegate { OpenSettings(); };
            }

            RefreshTray();
        }

        // Text und Menue des Infobereichs in der aktuellen Sprache. Wird auch
        // nach einem Sprachwechsel gerufen, damit das Kontextmenue mitgeht.
        void RefreshTray()
        {
            if (_tray == null) return;

            _tray.Text = _config.Enabled ? Config.AppName : Strings.Get("app.trayPaused");
            if (string.IsNullOrEmpty(_trayLanguage)) _trayLanguage = _config.Language;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;

            ToolStripMenuItem settings = new ToolStripMenuItem(Strings.Get("app.traySettings"));
            settings.Font = new Font(settings.Font, System.Drawing.FontStyle.Bold);
            settings.Click += delegate { OpenSettings(); };
            menu.Items.Add(settings);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem about = new ToolStripMenuItem(Strings.Get("app.trayAbout"));
            about.Click += delegate { OpenSettings("about"); };
            menu.Items.Add(about);

            ToolStripMenuItem folder = new ToolStripMenuItem(Strings.Get("app.trayFolder"));
            folder.Click += delegate { OpenConfigFolder(); };
            menu.Items.Add(folder);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem quit = new ToolStripMenuItem(Strings.Get("app.trayQuit"));
            quit.Click += delegate { ExitApplication(); };
            menu.Items.Add(quit);

            // Das alte Menue wird von NotifyIcon nicht entsorgt.
            ContextMenuStrip old = _tray.ContextMenuStrip;
            _tray.ContextMenuStrip = menu;
            if (old != null) old.Dispose();
        }

        void DisposeTray()
        {
            if (_tray == null) return;
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        void OpenConfigFolder()
        {
            try
            {
                if (!Directory.Exists(Config.Folder)) Directory.CreateDirectory(Config.Folder);
                Process.Start(new ProcessStartInfo(Config.Folder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Write("Ordner konnte nicht geöffnet werden: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Fehler
        // ------------------------------------------------------------------

        void OnDispatcherUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Write("UI-Fehler: " + e.Exception);
            e.Handled = true;
        }

        void OnDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Write("UNBEHANDELT: " + e.ExceptionObject);
        }

        void OnTaskUnobserved(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            Log.Write("Hintergrundfehler: " + e.Exception);
            e.SetObserved();
        }
    }

    // ------------------------------------------------------------------
    // Autostart ueber HKCU\...\Run, mit Kontrolllesen.
    // ------------------------------------------------------------------

    public static class Autostart
    {
        public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ValueName = "DialMenu";

        public static string ExePath()
        {
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetEntryAssembly();
                if (asm != null && !string.IsNullOrEmpty(asm.Location)) return asm.Location;
            }
            catch { }

            try
            {
                Process p = Process.GetCurrentProcess();
                if (p != null && p.MainModule != null) return p.MainModule.FileName;
            }
            catch { }

            return "";
        }

        public static string CommandLine()
        {
            string exe = ExePath();
            if (string.IsNullOrEmpty(exe)) return "";
            return "\"" + exe + "\" --tray";
        }

        public static string ReadEntry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return null;
                    return key.GetValue(ValueName, null) as string;
                }
            }
            catch { return null; }
        }

        // Schreibt oder entfernt den Eintrag und liest ihn anschliessend
        // zur Kontrolle zurueck. detail beschreibt das tatsaechliche Ergebnis.
        public static bool Apply(bool enabled, out string detail)
        {
            detail = "";
            string exe = ExePath();
            if (string.IsNullOrEmpty(exe))
            {
                detail = Strings.Get("autostart.noPath");
                return false;
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                    {
                        detail = Strings.Get("autostart.noKey");
                        return false;
                    }

                    if (enabled)
                    {
                        string wanted = CommandLine();
                        key.SetValue(ValueName, wanted, RegistryValueKind.String);

                        string written = key.GetValue(ValueName, null) as string;
                        if (written == null)
                        {
                            detail = Strings.Get("autostart.notWritten");
                            return false;
                        }
                        if (!string.Equals(written, wanted, StringComparison.Ordinal))
                        {
                            detail = Strings.Format("autostart.mismatch", written);
                            return false;
                        }
                        if (!File.Exists(exe))
                        {
                            detail = Strings.Get("autostart.fileMissing");
                            return false;
                        }
                        detail = Strings.Get("autostart.active");
                        return true;
                    }

                    key.DeleteValue(ValueName, false);
                    if (key.GetValue(ValueName, null) != null)
                    {
                        detail = Strings.Get("autostart.removeFailed");
                        return false;
                    }
                    detail = Strings.Get("autostart.inactive");
                    return true;
                }
            }
            catch (Exception ex)
            {
                detail = Strings.Format("autostart.error", ex.Message);
                Log.Write("Autostart: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        // Aktueller Zustand fuer die Anzeige, ohne etwas zu veraendern.
        public static string State(out bool registered)
        {
            string value = ReadEntry();
            registered = !string.IsNullOrEmpty(value);
            if (!registered) return Strings.Get("autostart.inactive");

            string wanted = CommandLine();
            if (string.Equals(value, wanted, StringComparison.OrdinalIgnoreCase))
                return Strings.Get("autostart.active");

            return Strings.Format("autostart.differs", value);
        }
    }

    // ------------------------------------------------------------------
    // Tray-Symbol, zur Laufzeit gezeichnet.
    // ------------------------------------------------------------------

    internal static class TrayIconFactory
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr handle);

        // Traegt dasselbe Zeichen wie die Wortmarke im Fenster: acht
        // Ringsegmente mit Punkt, hell auf dunklem Grund, damit es im
        // Infobereich auch bei heller Taskleiste lesbar bleibt.
        public static Icon Create()
        {
            const int Size = 32;

            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(Size, Size))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(System.Drawing.Color.Transparent);

                    float cx = Size / 2f;
                    float cy = Size / 2f;
                    float radius = 10.6f;
                    float thickness = 3.1f;
                    float dot = 3.4f;
                    int segments = 8;
                    float gapDegrees = 17f;

                    using (System.Drawing.Pen pen = new System.Drawing.Pen(
                        System.Drawing.Color.FromArgb(0xF2, 0xF2, 0xF4), thickness))
                    {
                        pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                        pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                        // Boegen einzeln zeichnen, damit die Luecken entstehen.
                        System.Drawing.RectangleF box = new System.Drawing.RectangleF(
                            cx - radius, cy - radius, radius * 2f, radius * 2f);

                        for (int i = 0; i < segments; i++)
                        {
                            float start = (i * (360f / segments)) + (gapDegrees / 2f) - 90f;
                            float sweep = (360f / segments) - gapDegrees;
                            g.DrawArc(pen, box, start, sweep);
                        }
                    }

                    using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(
                        System.Drawing.Color.FromArgb(0xF2, 0xF2, 0xF4)))
                    {
                        g.FillEllipse(brush, cx - dot / 2f, cy - dot / 2f, dot, dot);
                    }
                }

                IntPtr handle = bmp.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                        return (Icon)temporary.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }
    }
}
