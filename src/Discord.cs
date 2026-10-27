using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DialMenu
{
    // Discord-Anbindung.
    //
    // Discord bietet keine dokumentierte, stabile Schnittstelle, um sich von
    // aussen stumm zu schalten. Der verlaessliche Weg ist deshalb der Umweg
    // ueber Discords eigene Tastenbelegung: DialMenu schickt die Kombination,
    // die in Discord unter "Taste zum Stummschalten" hinterlegt ist. Discord
    // verarbeitet sie wie einen echten Tastendruck, der Zustand stimmt also
    // inklusive Anzeige.
    //
    // Der systemweite Weg schaltet das Mikrofon auf Windows-Ebene stumm. Das
    // wirkt sofort und ohne Einrichtung, aber Discord zeigt den Zustand nicht
    // an - deshalb bleibt es der Ersatzweg.
    public static class Discord
    {
        // Discords Voreinstellung fuer "Stummschalten". Aenderbar, weil viele
        // Nutzer sie auf eine eigene Kombination legen.
        public const string DefaultMuteHotkey = "Ctrl+Shift+M";
        public const string DefaultDeafenHotkey = "Ctrl+Shift+D";

        // Schaltet das Windows-Mikrofon stumm. Funktioniert in jedem Programm,
        // das das Standard-Aufnahmegeraet benutzt.
        public static bool SetSystemMicMute(bool muted)
        {
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                IMMDevice device;
                Guid dataFlowCapture = IID_MMDeviceEnumerator_DataFlow_Capture;
                int hr = enumerator.GetDefaultAudioEndpoint(0 /* eCapture */, 1 /* eCommunications */, out device);

                if (hr != 0 || device == null)
                {
                    // Kein Kommunikationsgeraet - normales Aufnahmegeraet versuchen.
                    hr = enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                }
                if (hr != 0 || device == null)
                {
                    Log.Write("Discord: kein Aufnahmegeraet gefunden.");
                    return false;
                }

                Guid iidEndpoints = typeof(IAudioEndpointVolume).GUID;
                object raw;
                hr = device.Activate(ref iidEndpoints, 23 /* CLSCTX_ALL */, IntPtr.Zero, out raw);
                if (hr != 0 || raw == null)
                {
                    Log.Write("Discord: Lautstaerke-Schnittstelle nicht verfuegbar.");
                    return false;
                }

                IAudioEndpointVolume volume = (IAudioEndpointVolume)raw;
                Guid ctx = Guid.Empty;
                hr = volume.SetMute(muted, ref ctx);
                Marshal.ReleaseComObject(volume);
                Marshal.ReleaseComObject(device);
                Marshal.ReleaseComObject(enumerator);

                Log.Write("Discord: Systemmikrofon " + (muted ? "stumm" : "laut") + " (hr=" + hr + ")");
                return hr == 0;
            }
            catch (Exception ex)
            {
                Log.Write("Discord: Mikrofon stummschalten fehlgeschlagen: " + ex.Message);
                return false;
            }
        }

        public static bool ToggleSystemMicMute()
        {
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                IMMDevice device;
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out device);
                if (hr != 0 || device == null) hr = enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                if (hr != 0 || device == null) return false;

                Guid iidEndpoints = typeof(IAudioEndpointVolume).GUID;
                object raw;
                hr = device.Activate(ref iidEndpoints, 23, IntPtr.Zero, out raw);
                if (hr != 0 || raw == null) return false;

                IAudioEndpointVolume volume = (IAudioEndpointVolume)raw;
                bool current;
                Guid ctx = Guid.Empty;
                volume.GetMute(out current);

                bool target = !current;
                volume.SetMute(target, ref ctx);

                Marshal.ReleaseComObject(volume);
                Marshal.ReleaseComObject(device);
                Marshal.ReleaseComObject(enumerator);

                Log.Write("Discord: Systemmikrofon umgeschaltet auf " + (target ? "stumm" : "laut"));
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Discord: Umschalten fehlgeschlagen: " + ex.Message);
                return false;
            }
        }

        // Ist Discord gerade offen? Dann lohnt der Tastenweg, sonst nicht.
        public static bool IsRunning()
        {
            try
            {
                Process[] list = Process.GetProcessesByName("Discord");
                if (list.Length > 0) { foreach (Process p in list) p.Dispose(); return true; }

                // Store-Version heisst anders.
                list = Process.GetProcessesByName("DiscordPTB");
                if (list.Length > 0) { foreach (Process p in list) p.Dispose(); return true; }

                list = Process.GetProcessesByName("DiscordCanary");
                if (list.Length > 0) { foreach (Process p in list) p.Dispose(); return true; }
            }
            catch { }
            return false;
        }

        // Bringt das Discord-Fenster nach vorn und schickt die Kombination.
        // Ohne Fokus ignoriert Discord globale Tasten naemlich.
        public static void SendMuteHotkey(string hotkey)
        {
            string combo = string.IsNullOrEmpty(hotkey) ? DefaultMuteHotkey : hotkey;

            bool focused = Commands.FocusWindowByProcess("discord");
            if (focused)
            {
                // Kurz warten, damit der Fokuswechsel greift - sonst landet der
                // Tastendruck noch im vorherigen Fenster.
                System.Threading.Thread.Sleep(90);
            }

            Commands.SendHotkey(combo);

            if (focused)
            {
                System.Threading.Thread.Sleep(60);
                Commands.RestoreFocus();
            }
        }

        public static void SendDeafenHotkey(string hotkey)
        {
            string combo = string.IsNullOrEmpty(hotkey) ? DefaultDeafenHotkey : hotkey;

            bool focused = Commands.FocusWindowByProcess("discord");
            if (focused) System.Threading.Thread.Sleep(90);

            Commands.SendHotkey(combo);

            if (focused)
            {
                System.Threading.Thread.Sleep(60);
                Commands.RestoreFocus();
            }
        }

        // ---- COM-Schnittstellen fuer das Aufnahmegeraet ----

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumerator { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            int NotImpl1();
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            int NotImpl1();
            int NotImpl2();
            int NotImpl3();
            int NotImpl4();

            int SetMute([MarshalAs(UnmanagedType.Bool)] bool allowMute, ref Guid eventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool allowMute);
        }

        static readonly Guid IID_MMDeviceEnumerator_DataFlow_Capture = Guid.Empty;
    }
}
