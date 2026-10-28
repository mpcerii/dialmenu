using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DialMenu
{
    // Ein angeschlossenes Raw-Input-HID-Geraet, wie es die Geraeteauswahl
    // in den Einstellungen anbietet.
    public class DeviceInfo
    {
        public string Name = "";
        public ushort VendorId;
        public ushort ProductId;
        public string Path = "";
        public ushort UsagePage;
        public ushort Usage;
        public bool HasConsumerControls;

        // Direkt als DeviceBinding.Match verwendbar: "VID_258A&PID_017E".
        public string Match = "";

        public override string ToString()
        {
            if (HasConsumerControls) return Name + " (Verbrauchersteuerung)";
            return Name;
        }
    }

    // Liest die rohen HID-Reports beliebig vieler konfigurierter Geraete
    // (UsagePage 0x01/0x06 Tastatur und 0x0C/0x01 Verbrauchersteuerung) und
    // schluckt die daraus entstehenden Lautstaerketasten. Tasten, die von
    // anderen Quellen stammen, laufen ungehindert durch.
    public class InputEngine : IDisposable
    {
        public const byte CodeRight = 0xE9;  // drehen nach rechts -> VK_VOLUME_UP
        public const byte CodeLeft = 0xEA;   // drehen nach links   -> VK_VOLUME_DOWN
        public const byte CodePress = 0xE2;  // druecken            -> VK_VOLUME_MUTE

        const int VK_VOLUP = 0xAF;
        const int VK_VOLDN = 0xAE;
        const int VK_MUTE = 0xAD;

        // Aktionen der Reports, absichtlich nicht die rohen Codes, damit
        // Geraete mit eigenen Codes dieselbe Logik benutzen koennen.
        const int ActNone = 0;
        const int ActRight = 1;
        const int ActLeft = 2;
        const int ActPress = 3;

        const int BlockWindowMs = 400;
        const int ReleaseWindowMs = 150;
        const int WatchdogMs = 500;
        const int WatchdogLimitMs = 5000;
        const int RecentReportLimit = 20;
        const int MaxReportBytes = 64;

        // Eigene, per SendInput erzeugte Tasten werden hier markiert und nie
        // geblockt. Sonst wuerde die App ihre eigenen Lautstaerke-Tasten fressen.
        public static readonly IntPtr InjectionTag = new IntPtr(unchecked((int)0x4B4E4F42));

        public event Action<int> Rotated;
        public event Action Pressed;
        public event Action LongPressed;
        public event Action Activity;

        Config _config;
        SinkWindow _sink;
        IntPtr _hook;
        LowLevelKeyboardProc _hookProc;
        System.Threading.Timer _buttonWatchdog;
        bool _started;
        bool _disposed;

        readonly object _stateLock = new object();
        List<KnobRuntime> _knobs = new List<KnobRuntime>();
        readonly List<string> _recentReports = new List<string>();

        // Sammelzustand fuer den Tastatur-Hook: die zuletzt gesehenen
        // Report-Aktionen aller konfigurierten Geraete. Eine Liste statt eines
        // Einzelwerts, weil sich bei mehreren Geraeten die Reports mischen.
        readonly List<RecentAction> _recentActions = new List<RecentAction>();

        int _blocked;
        int _passed;

        public int Blocked { get { return _blocked; } }
        public int Passed { get { return _passed; } }

        // Letzte rohe Reports konfigurierter Geraete, neueste zuerst.
        public List<string> RecentReports
        {
            get
            {
                lock (_stateLock)
                {
                    return new List<string>(_recentReports);
                }
            }
        }

        public InputEngine(Config config)
        {
            _config = config;
        }

        // ------------------------------------------------------------------
        // Lebenszyklus
        // ------------------------------------------------------------------

        public void Start()
        {
            if (_disposed || _started) return;
            _started = true;

            RebuildKnobs();

            _hookProc = HookCallback;

            // Hook und Raw-Input-Fenster gehoeren zusammen auf einen eigenen
            // Thread. Laeuft beides auf dem Oberflaechen-Thread, dann stauen
            // sich die Dreh-Reports, solange dort gezeichnet wird - der
            // Tastendruck kommt dann beim Hook an, bevor der zugehoerige
            // Report vermerkt ist, und die Lautstaerke aendert sich trotzdem.
            StartHookThread();

            // Auf das Fenster warten, es entsteht im Hook-Thread.
            _sinkReady.WaitOne(3000);

            LogDeviceSummary();

            _buttonWatchdog = new System.Threading.Timer(ButtonWatchdog, null,
                WatchdogMs, WatchdogMs);
        }

        // Wird von den Einstellungen aufgerufen, wenn sich die Geraeteliste
        // oder die Reports geaendert haben.
        public void UpdateConfig(Config config)
        {
            _config = config;
            if (_disposed) return;
            RebuildKnobs();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopHookThread();
            if (_buttonWatchdog != null)
            {
                try { _buttonWatchdog.Dispose(); } catch { }
                _buttonWatchdog = null;
            }

            lock (_stateLock)
            {
                foreach (KnobRuntime rt in _knobs) DisposePressTimer(rt);
                _knobs.Clear();
                _recentActions.Clear();
            }

            if (_sink != null)
            {
                _sink.Current = null;
                try { _sink.DestroyHandle(); } catch { }
                _sink = null;
            }
        }

        // ------------------------------------------------------------------
        // Hook-Thread
        // ------------------------------------------------------------------

        const uint WM_QUIT = 0x0012;
        const uint WM_APP_STOP = 0x8000;

        Thread _hookThread;
        uint _hookThreadId;
        readonly ManualResetEvent _hookReady = new ManualResetEvent(false);
        readonly ManualResetEvent _sinkReady = new ManualResetEvent(false);

        void StartHookThread()
        {
            _hookThread = new Thread(HookThreadMain);
            _hookThread.IsBackground = true;
            _hookThread.Name = "DialMenu.Hook";
            _hookThread.Start();

            // Auf die Installation warten, damit der Aufrufer weiss, ob es
            // geklappt hat.
            _hookReady.WaitOne(2000);
        }

        void StopHookThread()
        {
            if (_hookThreadId != 0)
                PostThreadMessage(_hookThreadId, WM_APP_STOP, IntPtr.Zero, IntPtr.Zero);

            if (_hookThread != null)
            {
                try { _hookThread.Join(1500); } catch { }
                _hookThread = null;
            }
        }

        void HookThreadMain()
        {
            try
            {
                _hookThreadId = GetCurrentThreadId();

                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
                Log.Write("Hook installiert: " + (_hook != IntPtr.Zero)
                    + " Fehler=" + Marshal.GetLastWin32Error()
                    + " (eigener Thread " + _hookThreadId + ")");
            }
            catch (Exception ex)
            {
                Log.Write("Hook-Thread: " + ex.Message);
            }
            finally
            {
                _hookReady.Set();
            }

            // Raw Input braucht ein Fenster, das Nachrichten empfaengt. Es
            // entsteht hier, damit die Reports auf demselben unbelasteten
            // Thread ankommen wie der Hook.
            try
            {
                _sink = new SinkWindow();
                _sink.Current = this;
                _sink.CreateSink();

                RAWINPUTDEVICE[] devices = new RAWINPUTDEVICE[2];

                devices[0].usUsagePage = 0x01; // Tastatur
                devices[0].usUsage = 0x06;
                devices[0].dwFlags = RIDEV_INPUTSINK;
                devices[0].hwndTarget = _sink.Handle;

                devices[1].usUsagePage = 0x0C; // Verbrauchersteuerung
                devices[1].usUsage = 0x01;
                devices[1].dwFlags = RIDEV_INPUTSINK;
                devices[1].hwndTarget = _sink.Handle;

                bool ok = RegisterRawInputDevices(devices, (uint)devices.Length,
                    (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
                Log.Write("Raw Input registriert: " + ok
                    + " Fehler=" + Marshal.GetLastWin32Error()
                    + " (Hook-Thread)");
            }
            catch (Exception ex)
            {
                Log.Write("Sink-Fenster fehlgeschlagen: " + ex.Message);
                _sink = null;
            }
            finally
            {
                _sinkReady.Set();
            }

            // Nachrichtenschleife: sie haelt den Thread ansprechbar, damit
            // Windows den Hook nie wegen Zeitueberschreitung verwirft, und
            // stellt die Raw-Input-Reports zu.
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_APP_STOP) break;
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            if (_sink != null)
            {
                _sink.Current = null;
                try { _sink.DestroyHandle(); } catch { }
                _sink = null;
            }

            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
                Log.Write("Hook entfernt");
            }
        }

        // Baut aus der Konfiguration die aktiven Geraete neu auf.
        void RebuildKnobs()        {
            List<KnobRuntime> fresh = new List<KnobRuntime>();
            Config cfg = _config;

            if (cfg != null && cfg.Devices != null)
            {
                foreach (DeviceBinding binding in cfg.Devices)
                {
                    if (binding == null || !binding.Enabled) continue;
                    // Ohne Match wuerde jede Tastatur passen - bewusst ignorieren.
                    if (string.IsNullOrEmpty(binding.Match)) continue;

                    KnobRuntime rt = new KnobRuntime();
                    rt.Binding = binding;
                    rt.Map = BuildReportMap(binding.Reports);
                    fresh.Add(rt);
                }
            }

            lock (_stateLock)
            {
                foreach (KnobRuntime old in _knobs) DisposePressTimer(old);
                _knobs = fresh;
                _recentActions.Clear();
            }
        }

        void LogDeviceSummary()
        {
            try
            {
                Config cfg = _config;
                if (cfg == null || cfg.Devices == null)
                {
                    Log.Write("Keine Geraetekonfiguration - Eingaben werden nicht abgefangen.");
                    return;
                }

                int configured = 0;
                int enabled = 0;
                foreach (DeviceBinding b in cfg.Devices)
                {
                    if (b == null) continue;
                    configured++;
                    if (b.Enabled && !string.IsNullOrEmpty(b.Match)) enabled++;
                }

                if (enabled == 0)
                {
                    Log.Write("Kein aktives Geraet konfiguriert - Eingaben werden nicht abgefangen.");
                    return;
                }

                List<DeviceInfo> attached = Discover();
                int matched = 0;
                foreach (DeviceBinding b in cfg.Devices)
                {
                    if (b == null || !b.Enabled || string.IsNullOrEmpty(b.Match)) continue;

                    bool found = false;
                    foreach (DeviceInfo di in attached)
                    {
                        if (MatchScore(b.Match, di.Path) >= 0) { found = true; break; }
                    }
                    if (found) matched++;
                    else Log.Write("Geraet nicht angeschlossen: " + b.Name + " (" + b.Match + ")");
                }

                Log.Write(enabled + " aktive(s) Geraet(e), " + matched + " gefunden, "
                    + configured + " konfiguriert.");
            }
            catch (Exception ex)
            {
                Log.Write("Geraete-Zusammenfassung fehlgeschlagen: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Rohe HID-Reports
        // ------------------------------------------------------------------

        void OnRawInput(IntPtr lParam)
        {
            try
            {
                uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
                uint size = 0;
                GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, headerSize);
                if (size == 0) return;

                IntPtr buffer = Marshal.AllocHGlobal((int)size);
                try
                {
                    uint read = size;
                    if (GetRawInputData(lParam, RID_INPUT, buffer, ref read, headerSize) != size)
                        return;

                    uint type = (uint)Marshal.ReadInt32(buffer, 0);
                    if (type != RIM_TYPEHID) return;

                    // RAWINPUTHEADER: dwType(4), dwSize(4), hDevice
                    IntPtr device = Marshal.ReadIntPtr(buffer, 8);
                    if (device == IntPtr.Zero) return;

                    string deviceName = DeviceName(device);
                    if (string.IsNullOrEmpty(deviceName)) return;

                    KnobRuntime rt = FindKnob(deviceName);
                    if (rt == null) return; // nicht konfiguriert: komplett ignorieren

                    uint reportSize = (uint)Marshal.ReadInt32(buffer, (int)headerSize);
                    uint reportCount = (uint)Marshal.ReadInt32(buffer, (int)headerSize + 4);
                    if (reportSize == 0 || reportSize > MaxReportBytes) return;
                    if (reportCount == 0 || reportCount > MaxReportBytes) return;

                    int length = (int)(reportSize * reportCount);
                    if (length <= 0 || length > MaxReportBytes) return;

                    int hidHeader = Marshal.SizeOf(typeof(RAWHID));
                    byte[] data = new byte[length];
                    Marshal.Copy(new IntPtr(buffer.ToInt64() + headerSize + hidHeader),
                        data, 0, length);

                    RememberReport(data);

                    Config cfg = _config;
                    if (cfg != null && !cfg.Enabled) return;

                    byte code;
                    if (!TryExtractCode(rt, data, out code)) return;
                    HandleReport(rt, code);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch (Exception ex)
            {
                Log.Write("Raw-Input-Fehler: " + ex.Message);
            }
        }

        // Sucht das spezifischste konfigurierte Geraet zu einem Geraetenamen.
        // Mehrere Eintraege koennen auf dieselbe HID-Sammlung passen; der
        // Eintrag mit den meisten Treffern gewinnt.
        KnobRuntime FindKnob(string name)
        {
            KnobRuntime best = null;
            int bestScore = -1;

            lock (_stateLock)
            {
                foreach (KnobRuntime rt in _knobs)
                {
                    int score = MatchScore(rt.Binding.Match, name);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = rt;
                    }
                }
            }

            return best;
        }

        static int MatchScore(string match, string name)
        {
            if (string.IsNullOrEmpty(match) || string.IsNullOrEmpty(name)) return -1;

            // Ein Muster ohne echte Kennung wuerde jedes Geraet treffen - dann
            // landen fremde Lautstaerketasten im Hook und die Lautstaerke
            // aendert sich trotz laufendem DialMenu.
            if (match.Contains("VID_0000") || match.Contains("PID_0000")) return -1;

            string[] parts = match.Split('&');
            int used = 0;

            foreach (string raw in parts)
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                if (name.IndexOf(part, StringComparison.OrdinalIgnoreCase) < 0) return -1;
                used++;
            }

            if (used == 0) return -1;
            return used * 1000 + match.Length;
        }

        // Report-ID 0x02 mit Nutzbyte ist der bewaehrte IGK-Aufbau. Geraete mit
        // anderem Aufbau werden ueber ihre deklarierten Codes erkannt.
        static bool TryExtractCode(KnobRuntime rt, byte[] data, out byte code)
        {
            code = 0;
            if (data == null || data.Length == 0) return false;

            // Bewaehrtes Layout: Report-ID 0x02, Code im zweiten Byte.
            if (data[0] == 0x02)
            {
                if (data.Length < 2) return false;
                code = data[1];
                return code == 0x00 || rt.Map.ContainsKey(code);
            }

            // Layout ohne Report-ID: erstes Byte traegt den Code.
            if (data[0] == 0x00) return true;
            if (rt.Map.ContainsKey(data[0]))
            {
                code = data[0];
                return true;
            }

            // Seltene Layouts: Code steht weiter hinten. Nur die ersten Bytes
            // pruefen, damit Tastatur-Reports nicht faelschlich ausloesen.
            int limit = Math.Min(data.Length, 8);
            for (int i = 1; i < limit; i++)
            {
                if (rt.Map.ContainsKey(data[i]))
                {
                    code = data[i];
                    return true;
                }
            }

            return false;
        }

        void RememberReport(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            string hex = sb.ToString();

            lock (_stateLock)
            {
                _recentReports.Insert(0, hex);
                while (_recentReports.Count > RecentReportLimit)
                    _recentReports.RemoveAt(_recentReports.Count - 1);
            }
        }

        void HandleReport(KnobRuntime rt, byte code)
        {
            DateTime now = DateTime.UtcNow;
            Config cfg = _config;
            int longMs = cfg != null ? cfg.LongPressMs : 700;
            if (longMs < 50) longMs = 50;

            bool doActivity = false;
            bool doRotate = false;
            bool doPress = false;
            bool armLongPress = false;
            int direction = 0;

            lock (_stateLock)
            {
                rt.LastSeen = now;

                if (code == 0)
                {
                    // Loslassen: Sammelzustand zuruecksetzen, damit auch das
                    // zugehoerige VK-UP noch als "unser" erkannt wird.
                    rt.LastAction = ActNone;
                    rt.LastActivity = now;
                    RememberAction(ActNone, now);

                    if (rt.ButtonHeld)
                    {
                        bool wasLong = rt.LongFired;
                        rt.ButtonHeld = false;
                        rt.LongFired = false;
                        DisposePressTimer(rt);
                        if (!wasLong)
                        {
                            doActivity = true;
                            doPress = true;
                        }
                    }
                }
                else
                {
                    int action;
                    if (!rt.Map.TryGetValue(code, out action)) return;

                    rt.LastAction = action;
                    rt.LastActivity = now;
                    RememberAction(action, now);

                    if (action == ActRight || action == ActLeft)
                    {
                        direction = action == ActRight ? 1 : -1;
                        doActivity = true;
                        doRotate = true;
                    }
                    else if (action == ActPress)
                    {
                        rt.ButtonHeld = true;
                        rt.LongFired = false;
                        rt.DownAt = now;
                        doActivity = true;
                        armLongPress = true;
                    }
                }
            }

            if (doActivity)
                RaiseOnUi(delegate
                {
                    if (!IsEnabledNow()) return;
                    if (Activity != null) Activity();
                });

            if (doRotate)
            {
                int dir = direction;
                RaiseOnUi(delegate
                {
                    if (!IsEnabledNow()) return;
                    if (Rotated != null) Rotated(dir);
                });
            }

            if (doPress)
                RaiseOnUi(delegate
                {
                    if (!IsEnabledNow()) return;
                    if (Pressed != null) Pressed();
                });

            if (armLongPress) ScheduleLongPress(rt, longMs);
        }

        void ScheduleLongPress(KnobRuntime rt, int longMs)
        {
            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer(delegate
            {
                bool fire = false;
                try { timer.Dispose(); } catch { }

                lock (_stateLock)
                {
                    if (object.ReferenceEquals(rt.PressTimer, timer)) rt.PressTimer = null;

                    if (!_disposed && _knobs.Contains(rt) && rt.ButtonHeld && !rt.LongFired
                        && (DateTime.UtcNow - rt.DownAt).TotalMilliseconds >= longMs - 40)
                    {
                        rt.LongFired = true;
                        fire = true;
                    }
                }

                if (fire)
                    RaiseOnUi(delegate
                    {
                        if (!IsEnabledNow()) return;
                        if (LongPressed != null) LongPressed();
                    });
            }, null, Math.Max(50, longMs), System.Threading.Timeout.Infinite);

            lock (_stateLock)
            {
                DisposePressTimer(rt);
                rt.PressTimer = timer;
            }
        }

        void DisposePressTimer(KnobRuntime rt)
        {
            if (rt == null || rt.PressTimer == null) return;
            try { rt.PressTimer.Dispose(); } catch { }
            rt.PressTimer = null;
        }

        void ButtonWatchdog(object state)
        {
            DateTime now = DateTime.UtcNow;
            bool stale = false;

            lock (_stateLock)
            {
                if (_disposed) return;

                foreach (KnobRuntime rt in _knobs)
                {
                    if (rt.ButtonHeld && !rt.LongFired
                        && (now - rt.DownAt).TotalMilliseconds > WatchdogLimitMs)
                    {
                        rt.ButtonHeld = false;
                        DisposePressTimer(rt);
                        stale = true;
                    }
                }
            }

            if (stale) Log.Write("Knopf-Watchdog: Loslass-Report ausgeblieben");
        }

        // ------------------------------------------------------------------
        // Tastatur-Hook: nur die eigenen Lautstaerketasten abfangen
        // ------------------------------------------------------------------

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode != 0) return CallNextHookEx(_hook, nCode, wParam, lParam);

            Config cfg = _config;
            if (cfg == null || !cfg.Enabled)
                return CallNextHookEx(_hook, nCode, wParam, lParam);

            try
            {
                KBDLLHOOKSTRUCT info = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(
                    lParam, typeof(KBDLLHOOKSTRUCT));
                int vk = (int)info.vkCode;

                if (vk != VK_VOLUP && vk != VK_VOLDN && vk != VK_MUTE)
                    return CallNextHookEx(_hook, nCode, wParam, lParam);

                // Selbst erzeugte Tasten (Lautstaerke-Aktionen der App) nie fressen.
                if (info.dwExtraInfo == InjectionTag)
                    return CallNextHookEx(_hook, nCode, wParam, lParam);

                int expected;
                if (vk == VK_VOLUP) expected = ActRight;
                else if (vk == VK_VOLDN) expected = ActLeft;
                else expected = ActPress;

                bool ours = false;
                DateTime now = DateTime.UtcNow;

                lock (_stateLock)
                {
                    ours = HasMatchingAction(expected, now);
                }

                if (ours)
                {
                    Interlocked.Increment(ref _blocked);
                    return new IntPtr(1);
                }

                Interlocked.Increment(ref _passed);
            }
            catch (Exception ex)
            {
                Log.Write("Hook-Fehler: " + ex.Message);
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        bool IsEnabledNow()
        {
            Config cfg = _config;
            return cfg == null ? true : cfg.Enabled;
        }

        // Merkt sich eine Report-Aktion, neueste zuerst. Aufzurufen unter
        // _stateLock.
        void RememberAction(int action, DateTime when)
        {
            _recentActions.Insert(0, new RecentAction(action, when));
            DateTime limit = when.AddMilliseconds(-BlockWindowMs * 2);
            while (_recentActions.Count > 0 && _recentActions[_recentActions.Count - 1].When < limit)
                _recentActions.RemoveAt(_recentActions.Count - 1);
            while (_recentActions.Count > 32)
                _recentActions.RemoveAt(_recentActions.Count - 1);
        }

        // Entscheidet, ob eine Lautstaerketaste von einem unserer Knoepfe
        // stammt. Nicht verbrauchend, damit auch das zugehoerige VK-UP im
        // selben Zeitfenster geblockt wird. Aufzurufen unter _stateLock.
        bool HasMatchingAction(int expected, DateTime now)
        {
            for (int i = 0; i < _recentActions.Count; i++)
            {
                RecentAction ra = _recentActions[i];
                double age = (now - ra.When).TotalMilliseconds;
                if (age < 0) continue;

                if (ra.Action == expected && age <= BlockWindowMs) return true;
                if (ra.Action == ActNone && age <= ReleaseWindowMs) return true;
            }

            return false;
        }

        void RaiseOnUi(Action action)
        {
            try
            {
                if (System.Windows.Application.Current != null)
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
                else
                    action();
            }
            catch (Exception ex)
            {
                Log.Write("UI-Dispatch fehlgeschlagen: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // Report-Codes je Geraet
        // ------------------------------------------------------------------

        static Dictionary<byte, int> BuildReportMap(List<string> reports)
        {
            List<byte> codes = new List<byte>();

            if (reports != null)
            {
                foreach (string entry in reports)
                {
                    byte value;
                    if (!TryParseHexCode(entry, out value)) continue;
                    if (value == 0x00) continue; // 0x00 ist immer Loslassen
                    if (!codes.Contains(value)) codes.Add(value);
                }
            }

            // Ohne eigene Angaben die bekannten IGK-Codes verwenden.
            if (codes.Count == 0)
            {
                codes.Add(CodeRight);
                codes.Add(CodeLeft);
                codes.Add(CodePress);
            }

            Dictionary<byte, int> map = new Dictionary<byte, int>();
            for (int i = 0; i < codes.Count && i < 8; i++)
            {
                byte code = codes[i];
                int action;

                // Bekannte Codes behalten ihre Bedeutung, auch wenn sie in
                // anderer Reihenfolge eingetragen wurden. Unbekannte Codes
                // gelten in Listenreihenfolge als rechts / links / Druck.
                if (code == CodeRight) action = ActRight;
                else if (code == CodeLeft) action = ActLeft;
                else if (code == CodePress) action = ActPress;
                else if (i == 0) action = ActRight;
                else if (i == 1) action = ActLeft;
                else if (i == 2) action = ActPress;
                else continue;

                if (!map.ContainsKey(code)) map[code] = action;
            }

            return map;
        }

        static bool TryParseHexCode(string text, out byte value)
        {
            value = 0;
            if (text == null) return false;

            string s = text.Trim();
            if (s.Length == 0) return false;
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (s.Length == 0 || s.Length > 2) return false;

            int parsed;
            if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
                return false;
            if (parsed < 0 || parsed > 0xFF) return false;

            value = (byte)parsed;
            return true;
        }

        // ------------------------------------------------------------------
        // Geraeteerkennung fuer die Einstellungen
        // ------------------------------------------------------------------

        public static List<DeviceInfo> Discover()
        {
            List<DeviceInfo> result = new List<DeviceInfo>();

            try
            {
                uint structSize = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));
                uint count = 0;

                uint res = GetRawInputDeviceList(null, ref count, structSize);
                if (res == 0xFFFFFFFF || count == 0) return result;

                RAWINPUTDEVICELIST[] list = new RAWINPUTDEVICELIST[count];
                uint got = GetRawInputDeviceList(list, ref count, structSize);
                if (got == 0xFFFFFFFF) return result;

                for (uint i = 0; i < got && i < count; i++)
                {
                    // Windows meldet dieselbe Tastatur je nach Collection als HID,
                    // KEYBOARD oder MOUSE. Alle drei sind relevant: der Drehknopf
                    // sitzt auf der Consumer-Collection, die als HID erscheint,
                    // waehrend die Tasten als KEYBOARD gefuehrt werden.
                    if (list[i].dwType != RIM_TYPEHID
                        && list[i].dwType != RIM_TYPEKEYBOARD
                        && list[i].dwType != RIM_TYPEMOUSE) continue;
                    if (list[i].hDevice == IntPtr.Zero) continue;

                    string path = DeviceName(list[i].hDevice);
                    if (string.IsNullOrEmpty(path)) continue;

                    ushort vid;
                    ushort pid;
                    ushort page;
                    ushort usage;
                    bool haveInfo = TryReadDeviceInfo(list[i].hDevice,
                        out vid, out pid, out page, out usage);

                    DeviceInfo info = new DeviceInfo();
                    info.Path = path;

                    if (haveInfo)
                    {
                        info.VendorId = vid;
                        info.ProductId = pid;
                        info.UsagePage = page;
                        info.Usage = usage;
                        info.HasConsumerControls = (page == 0x0C && usage == 0x01);
                    }
                    else
                    {
                        ParseVidPid(path, out vid, out pid);
                        info.VendorId = vid;
                        info.ProductId = pid;
                    }

                    if (info.VendorId != 0 || info.ProductId != 0)
                        info.Match = "VID_" + info.VendorId.ToString("X4", CultureInfo.InvariantCulture)
                            + "&PID_" + info.ProductId.ToString("X4", CultureInfo.InvariantCulture);

                    string friendly = FriendlyName(path);
                    if (string.IsNullOrEmpty(friendly))
                        friendly = info.Match.Length > 0 ? "HID " + info.Match : path;
                    info.Name = friendly;

                    result.Add(info);
                }

                result.Sort(CompareDevices);
            }
            catch (Exception ex)
            {
                Log.Write("Geraete-Erkennung fehlgeschlagen: " + ex.Message);
            }

            return result;
        }

        static int CompareDevices(DeviceInfo a, DeviceInfo b)
        {
            if (a.HasConsumerControls != b.HasConsumerControls)
                return a.HasConsumerControls ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        static bool TryReadDeviceInfo(IntPtr device,
            out ushort vendorId, out ushort productId, out ushort usagePage, out ushort usage)
        {
            vendorId = 0;
            productId = 0;
            usagePage = 0;
            usage = 0;

            uint size = (uint)Marshal.SizeOf(typeof(RID_DEVICE_INFO));
            IntPtr buffer = Marshal.AllocHGlobal((int)size);

            try
            {
                RID_DEVICE_INFO seed = new RID_DEVICE_INFO();
                seed.cbSize = size;
                seed.dwType = RIM_TYPEHID;
                Marshal.StructureToPtr(seed, buffer, false);

                uint res = GetRawInputDeviceInfo(device, RIDI_DEVICEINFO, buffer, ref size);
                if (res == 0xFFFFFFFF || res == 0) return false;

                RID_DEVICE_INFO info = (RID_DEVICE_INFO)Marshal.PtrToStructure(
                    buffer, typeof(RID_DEVICE_INFO));
                if (info.dwType != RIM_TYPEHID) return false;

                vendorId = (ushort)info.data.hid.dwVendorId;
                productId = (ushort)info.data.hid.dwProductId;
                usagePage = info.data.hid.usUsagePage;
                usage = info.data.hid.usUsage;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        static void ParseVidPid(string path, out ushort vendorId, out ushort productId)
        {
            vendorId = ParseHex4(path, "VID_");
            productId = ParseHex4(path, "PID_");
        }

        static ushort ParseHex4(string text, string marker)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            int index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0 || index + marker.Length + 4 > text.Length) return 0;

            int value;
            if (!int.TryParse(text.Substring(index + marker.Length, 4),
                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return 0;

            return (ushort)value;
        }

        // Registry-Name des Geraets, z.B. "HID-konforme Systemsteuerung".
        static string FriendlyName(string path)
        {
            try
            {
                string[] parts = path.Split('#');
                if (parts.Length < 3) return null;

                string hardwareId = parts[1];
                string instanceId = parts[2];
                if (hardwareId.Length == 0 || instanceId.Length == 0) return null;

                string key = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Enum\\HID\\"
                    + hardwareId + "\\" + instanceId;

                string desc = Microsoft.Win32.Registry.GetValue(key, "DeviceDesc", null) as string;
                if (string.IsNullOrEmpty(desc)) return null;

                int semi = desc.LastIndexOf(';');
                if (semi >= 0 && semi + 1 < desc.Length) desc = desc.Substring(semi + 1);

                desc = desc.Trim();
                return desc.Length > 0 ? desc : null;
            }
            catch
            {
                return null;
            }
        }

        static string DeviceName(IntPtr device)
        {
            if (device == IntPtr.Zero) return null;

            uint size = 0;
            GetRawInputDeviceInfo(device, RIDI_DEVICENAME, IntPtr.Zero, ref size);
            if (size == 0) return null;

            StringBuilder sb = new StringBuilder((int)size + 2);
            uint res = GetRawInputDeviceInfoString(device, RIDI_DEVICENAME, sb, ref size);
            if (res == 0xFFFFFFFF) return null;

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // Fenster fuer die Rohdaten
        // ------------------------------------------------------------------

        class SinkWindow : NativeWindow
        {
            public InputEngine Current;

            // Absichtlich eigener Name ohne CreateParams, damit nichts mit
            // NativeWindow-Mitgliedern kollidiert.
            public void CreateSink()
            {
                CreateParams cp = new CreateParams();
                cp.Caption = "DialMenuSink";
                cp.X = -2000;
                cp.Y = -2000;
                cp.Width = 1;
                cp.Height = 1;
                cp.Style = 0x00000000;
                cp.ExStyle = 0x00000080; // WS_EX_TOOLWINDOW
                base.CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_INPUT)
                {
                    try
                    {
                        InputEngine engine = Current;
                        if (engine != null) engine.OnRawInput(m.LParam);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Sink-Fehler: " + ex.Message);
                    }
                    return;
                }
                base.WndProc(ref m);
            }
        }

        // ------------------------------------------------------------------
        // Laufzeitzustand je Geraet
        // ------------------------------------------------------------------

        class KnobRuntime
        {
            public DeviceBinding Binding;
            public Dictionary<byte, int> Map;
            public int LastAction = ActNone;
            public DateTime LastActivity = DateTime.MinValue;
            public DateTime LastSeen = DateTime.MinValue;
            public bool ButtonHeld;
            public bool LongFired;
            public DateTime DownAt;
            public System.Threading.Timer PressTimer;
        }

        // Ein kuerzlich gesehener Report, fuer die Zuordnung im Tasten-Hook.
        struct RecentAction
        {
            public readonly int Action;
            public readonly DateTime When;

            public RecentAction(int action, DateTime when)
            {
                Action = action;
                When = when;
            }
        }

        // ------------------------------------------------------------------
        // Interop
        // ------------------------------------------------------------------

        delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWHID
        {
            public uint dwSizeHid;
            public uint dwCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RAWINPUTDEVICELIST
        {
            public IntPtr hDevice;
            public uint dwType;
        }

        // Die Union ist so gross wie ihr groesstes Mitglied - das ist die
        // Tastatur-Struktur mit 24 Byte, nicht die HID-Variante mit 16.
        // Ein zu kleiner cbSize laesst GetRawInputDeviceInfo fehlschlagen.
        [StructLayout(LayoutKind.Sequential)]
        struct RID_DEVICE_INFO_KEYBOARD
        {
            public uint dwType;
            public uint dwSubType;
            public uint dwKeyboardMode;
            public uint dwNumberOfFunctionKeys;
            public uint dwNumberOfIndicators;
            public uint dwNumberOfKeysTotal;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RID_DEVICE_INFO_HID
        {
            public uint dwVendorId;
            public uint dwProductId;
            public uint dwVersionNumber;
            public ushort usUsagePage;
            public ushort usUsage;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct RID_DEVICE_INFO_UNION
        {
            [FieldOffset(0)] public RID_DEVICE_INFO_KEYBOARD keyboard;
            [FieldOffset(0)] public RID_DEVICE_INFO_HID hid;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RID_DEVICE_INFO
        {
            public uint cbSize;
            public uint dwType;
            public RID_DEVICE_INFO_UNION data;
        }

        const int WH_KEYBOARD_LL = 13;
        const uint WM_INPUT = 0x00FF;
        const uint RIDEV_INPUTSINK = 0x00000100;
        const uint RID_INPUT = 0x10000003;
        const uint RIDI_DEVICENAME = 0x20000007;
        const uint RIDI_DEVICEINFO = 0x2000000B;
        const uint RIM_TYPEHID = 2;
        const uint RIM_TYPEMOUSE = 0;
        const uint RIM_TYPEKEYBOARD = 1;

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
            IntPtr hMod, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string name);

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [DllImport("user32.dll")]
        static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data,
            ref uint size, uint headerSize);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data,
            ref uint size);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode,
            EntryPoint = "GetRawInputDeviceInfoW")]
        static extern uint GetRawInputDeviceInfoString(IntPtr device, uint command,
            StringBuilder data, ref uint size);

        // [Out] ist zwingend: ohne dieses Attribut kopiert der Marshaler das
        // gefuellte Array nicht zurueck und alle Eintraege bleiben 0.
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[] pRawInputDeviceList,
            ref uint puiNumDevices, uint cbSize);
    }
}
