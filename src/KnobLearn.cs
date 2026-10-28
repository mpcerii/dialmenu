using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DialMenu
{
    // Erkennt den Drehknopf durch eine vorgegebene Bewegung:
    // dreimal nach rechts, dreimal nach links, einmal druecken.
    //
    // Der Assistent hoert dafuer an ALLEN Eingabegeraeten mit, nicht nur an
    // den eingerichteten. Aus der Reihenfolge der Reports ergibt sich, welches
    // Geraet der Knopf ist und welche Codes zu rechts, links und Druck gehoeren -
    // ohne dass jemand Vendor- und Product-ID von Hand eintragen muss.
    public class KnobLearn
    {
        public const int RightSteps = 3;
        public const int LeftSteps = 3;
        public const int PressSteps = 1;

        // Ein Band mit nur einem Schritt ist zu unsicher: das kann auch ein
        // einzelner Fehlreport sein. Drei ist die Vorgabe, nach oben offen
        // bis acht, damit ein unruhiger Dreh nicht scheitert.
        const int MinSteps = 2;
        const int MaxSteps = 12;

        // Ein einzelner beobachteter Schritt.
        class Step
        {
            public string Device;
            public string Code;      // Reportschlüssel, z. B. "E90200"
            public DateTime When;
        }

        readonly object _lock = new object();
        readonly List<Step> _steps = new List<Step>();
        readonly HashSet<string> _seenDevices = new HashSet<string>();

        public bool Active { get; private set; }

        // Ergebnis nach erfolgreicher Erkennung, sonst null.
        public KnobResult Result { get; private set; }

        // Wird nach jedem Schritt aufgerufen, damit die Oberflaeche den
        // Fortschritt zeigen kann. Argument: menschenlesbarer Zustand.
        public event Action<string> Progress;

        // Wird gerufen, sobald ein vollstaendiges Muster erkannt wurde.
        public event Action<KnobResult> Completed;

        public void Start()
        {
            lock (_lock)
            {
                _steps.Clear();
                _seenDevices.Clear();
                Result = null;
                Active = true;
            }
            Report("");
        }

        public void Stop()
        {
            Active = false;
        }

        // Rohdaten eines beliebigen Geraets. Wird vom InputEngine fuer alle
        // HID-Reports aufgerufen, auch fuer nicht eingerichtete Geraete.
        public void OnReport(string devicePath, byte[] data)
        {
            if (!Active) return;
            if (data == null || data.Length == 0) return;

            // Loslassen interessiert hier nicht: nur Reports mit Inhalt sind
            // Schritte des Knopfs.
            if (!HasSignal(data)) return;

            string code = Key(data);

            lock (_lock)
            {
                if (!Active) return;

                _seenDevices.Add(devicePath);

                Step step = new Step();
                step.Device = devicePath;
                step.Code = code;
                step.When = DateTime.UtcNow;
                _steps.Add(step);

                // Nur die letzten Schritte betrachten; ein Fehlgriff am Anfang
                // soll die Erkennung nicht dauerhaft blockieren.
                if (_steps.Count > 60) _steps.RemoveRange(0, _steps.Count - 60);
            }

            TryComplete();
        }

        static bool HasSignal(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
                if (data[i] != 0x00) return true;
            return false;
        }

        // Ein Report wird ueber seinen Inhalt unterschieden, nicht ueber die
        // Laenge: manche Geraete schicken immer dieselbe Laenge mit wechselndem
        // Inhalt, andere wechseln die Laenge.
        static string Key(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++)
                sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        // Sucht am Ende der Aufzeichnung das Muster rechts^n, links^m, Druck^1.
        // Es wird bewusst von hinten gesucht: der Nutzer darf vorher daneben
        // gedreht haben, ohne dass die Erkennung scheitert.
        void TryComplete()
        {
            KnobResult found = null;

            lock (_lock)
            {
                if (!Active) return;

                List<Step> steps = _steps;

                // Ein Lauf ist eine Folge gleicher Codes.
                List<string> runs = new List<string>();
                List<int> counts = new List<int>();

                for (int i = 0; i < steps.Count; i++)
                {
                    string code = steps[i].Code;
                    if (runs.Count > 0 && runs[runs.Count - 1] == code)
                        counts[counts.Count - 1]++;
                    else
                    {
                        runs.Add(code);
                        counts.Add(1);
                    }
                }

                // Erwartet wird genau: rechts, links, Druck - in dieser
                // Reihenfolge. Die Anzahl darf grosszuegig sein (der Nutzer
                // dreht selten exakt drei Rasten), aber jedes Band muss
                // mindestens zwei Schritte haben: ein einzelner Report kann
                // auch ein Vorbeizucken sein.
                if (runs.Count < 3) return;
                if (counts[counts.Count - 1] != PressSteps) return;

                // Die beiden Laeufe davor sind rechts und links.
                int pressRun = runs.Count - 1;
                int leftRun = pressRun - 1;
                int rightRun = pressRun - 2;

                if (counts[rightRun] < MinSteps || counts[rightRun] > MaxSteps) return;
                if (counts[leftRun] < MinSteps || counts[leftRun] > MaxSteps) return;

                string rightCode = runs[rightRun];
                string leftCode = runs[leftRun];
                string pressCode = runs[pressRun];

                // Zwei gleiche Codes koennen nicht rechts und links sein.
                if (rightCode == leftCode) return;
                if (rightCode == pressCode || leftCode == pressCode) return;

                // Das Geraet ist das, das diese sieben Schritte geliefert hat.
                int start = 0;
                for (int i = 0; i < rightRun; i++) start += counts[i];
                if (start < 0 || start >= steps.Count) return;

                string device = steps[start].Device;

                int right = 0, left = 0, press = 0;
                for (int i = start; i < steps.Count; i++)
                {
                    if (steps[i].Device != device) return; // fremde Eingabe dazwischen
                    if (i < start + counts[rightRun]) right++;
                    else if (i < start + counts[rightRun] + counts[leftRun]) left++;
                    else press++;
                }

                if (right != counts[rightRun]) return;
                if (left != counts[leftRun]) return;

                found = new KnobResult();
                found.DevicePath = device;
                found.Match = MatchFromPath(device);
                found.RightCode = rightCode;
                found.LeftCode = leftCode;
                found.PressCode = pressCode;
                found.RightCount = right;
                found.LeftCount = left;
                found.PressCount = press;
                found.FriendlyName = Describe(device);
                found.Confidence = 1.0;

                Active = false;
                Result = found;
            }

            if (found != null)
            {
                Report(buildState(found));
                if (Completed != null) Completed(found);
                return;
            }

            Report(StateText());
        }

        void Report(string state)
        {
            if (!Active && State != null && state == "") return;
            State = state;
            if (Progress != null) Progress(state);
        }

        public string State { get; private set; }

        // Fortschritt als kurzer Text, z. B. "rechts 2/3 · links 0/3 · Druck offen".
        public string StateText()
        {
            lock (_lock)
            {
                int right = 0, left = 0, press = 0;

                if (_steps.Count > 0)
                {
                    List<string> runs = new List<string>();
                    List<int> counts = new List<int>();
                    for (int i = 0; i < _steps.Count; i++)
                    {
                        string code = _steps[i].Code;
                        if (runs.Count > 0 && runs[runs.Count - 1] == code)
                            counts[counts.Count - 1]++;
                        else { runs.Add(code); counts.Add(1); }
                    }

                    if (counts.Count == 1) right = Math.Min(counts[0], RightSteps);
                    else if (counts.Count == 2) { right = Math.Min(counts[0], RightSteps); left = Math.Min(counts[1], LeftSteps); }
                    else if (counts.Count >= 3)
                    {
                        right = Math.Min(counts[counts.Count - 3], RightSteps);
                        left = Math.Min(counts[counts.Count - 2], LeftSteps);
                        press = counts[counts.Count - 1] >= 1 ? 1 : 0;
                    }
                }

                return "rechts " + right + "/" + RightSteps
                    + " · links " + left + "/" + LeftSteps
                    + " · " + (press > 0 ? "Druck erkannt" : "Druck offen");
            }
        }

        static string buildState(KnobResult r)
        {
            return "erkannt: rechts " + r.RightCode + ", links " + r.LeftCode
                + ", Druck " + r.PressCode + " (" + r.RightCount + "/" + r.LeftCount + "/" + r.PressCount + ")";
        }

        // Aus dem Geraetepfad die Kennung ziehen, die der Konfiguration genuegt.
        // Beispiel: ...\HID#VID_258A&PID_017E&MI_01&Col02#... -> VID_258A&PID_017E
        public static string MatchFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            int vid = path.IndexOf("VID_", StringComparison.OrdinalIgnoreCase);
            int pid = path.IndexOf("PID_", StringComparison.OrdinalIgnoreCase);
            if (vid < 0 || pid < 0) return "";

            if (vid + 8 > path.Length) return "";
            if (pid + 8 > path.Length) return "";

            return path.Substring(vid, 8) + "&" + path.Substring(pid, 8);
        }

        // Lesbarer Geraetename aus dem Pfad.
        public static string Describe(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Unbekanntes Gerät";

            string match = MatchFromPath(path);
            if (match.Length > 0) return "HID " + match;

            int hash = path.LastIndexOf('#');
            if (hash > 0 && hash < path.Length - 1) return path.Substring(hash + 1);
            return path;
        }
    }

    // Ergebnis einer Erkennung.
    public class KnobResult
    {
        public string DevicePath = "";
        public string FriendlyName = "";
        public string Match = "";
        public string RightCode = "";
        public string LeftCode = "";
        public string PressCode = "";
        public int RightCount;
        public int LeftCount;
        public int PressCount;
        public double Confidence;

        public bool IsUsable
        {
            get { return Match.Length > 0 && RightCode.Length > 0 && LeftCode.Length > 0; }
        }
    }
}
