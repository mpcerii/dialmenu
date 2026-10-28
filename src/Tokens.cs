using System.Windows.Media;

namespace DialMenu
{
    // Alle Farben und Schriftgroessen an einer Stelle.
    //
    // Die Oberflaeche ist bewusst farblos: kein Blau, kein Akzentton.
    // Auswahl und Betonung entstehen allein durch Helligkeit, Flaeche und
    // Schriftgewicht - so bleibt das Fenster ruhig und konkurriert nicht
    // mit dem Inhalt.
    internal static class Tokens
    {
        // --- Flaechen ---
        public static readonly Brush Canvas = Freeze(0xF6, 0xF6, 0xF7);  // Fenstergrund
        public static readonly Brush Surface = Freeze(0xFF, 0xFF, 0xFF); // Karten, Felder
        public static readonly Brush Recess = Freeze(0xF0, 0xF0, 0xF2);  // eingelassene Flaechen
        public static readonly Brush Sunken = Freeze(0xE9, 0xE9, 0xEC);  // Vorschau, Symbolwell
        public static readonly Brush Rail = Freeze(0xFA, 0xFA, 0xFB);    // Seitenleiste

        // --- Linien ---
        public static readonly Brush Line = Freeze(0xE2, 0xE2, 0xE5);       // Trennlinien
        public static readonly Brush LineStrong = Freeze(0xD0, 0xD0, 0xD5); // Feldrahmen

        // --- Schrift ---
        public static readonly Brush Ink = Freeze(0x18, 0x18, 0x1B);      // Ueberschriften
        public static readonly Brush InkSoft = Freeze(0x55, 0x55, 0x5C);  // Fliesstext
        public static readonly Brush InkMuted = Freeze(0x8A, 0x8A, 0x92); // Hinweise
        public static readonly Brush InkFaint = Freeze(0xAB, 0xAB, 0xB3); // Platzhalter

        // --- Betonung ohne Farbe ---
        public static readonly Brush Selected = Freeze(0xEB, 0xEB, 0xEE); // aktive Zeile
        public static readonly Brush Hover = Freeze(0xF2, 0xF2, 0xF4);
        public static readonly Brush Primary = Freeze(0x1C, 0x1C, 0x20);  // Hauptknopf
        public static readonly Brush PrimaryText = Freeze(0xFF, 0xFF, 0xFF);
        public static readonly Brush PrimaryHover = Freeze(0x2E, 0x2E, 0x34);

        // --- Warnung: der einzige Farbton, und nur wo er traegt ---
        public static readonly Brush Warn = Freeze(0x8A, 0x5A, 0x12);
        public static readonly Brush WarnSoft = Freeze(0xFB, 0xF6, 0xEB);
        public static readonly Brush WarnLine = Freeze(0xE8, 0xDD, 0xC6);

        // --- Masse ---
        public const double SidebarWidth = 208;
        public const double ContentMax = 1000;
        public const double Gap = 20;

        public static double Step1 = 12;   // Hinweise
        public static double Step2 = 12.5; // Fliesstext
        public static double Step3 = 13;   // Felder, Listenzeilen
        public static double Heading = 18;
        public static double Section = 13.5;

        static Brush Freeze(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
