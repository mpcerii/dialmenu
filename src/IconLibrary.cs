using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DialMenu
{
    // ------------------------------------------------------------------
    // Icon-Bibliothek
    //
    // Alle Symbole sind reine SVG-Pfaddaten auf einem 24x24-Raster,
    // nur Kontur, keine Fuellung. Rundungen und Strichstaerke kommen
    // vom Zeichner (Stroke ~1.75-1.8, Round Caps/Joins).
    // ------------------------------------------------------------------

    public static class IconLibrary
    {
        // --- Grundsymbole (aus der ersten Fassung uebernommen) ---------

        public const string Globe =
            "M3.4,12 A8.6,8.6 0 1 1 20.6,12 A8.6,8.6 0 1 1 3.4,12 " +
            "M3.4,12 H20.6 " +
            "M12,3.4 C15.2,6.6 15.2,17.4 12,20.6 C8.8,17.4 8.8,6.6 12,3.4";

        public const string Folder =
            "M3.2,18.6 V6.6 A1.4,1.4 0 0 1 4.6,5.2 H9.1 L11,7.4 H19.4 " +
            "A1.4,1.4 0 0 1 20.8,8.8 V18.6 A1.4,1.4 0 0 1 19.4,20 H4.6 " +
            "A1.4,1.4 0 0 1 3.2,18.6 Z";

        public const string Terminal =
            "M5,5.4 H19 A1.6,1.6 0 0 1 20.6,7 V17 A1.6,1.6 0 0 1 19,18.6 H5 " +
            "A1.6,1.6 0 0 1 3.4,17 V7 A1.6,1.6 0 0 1 5,5.4 Z " +
            "M6.4,10.1 L9.2,12 L6.4,13.9 M12.4,14.2 H17.4";

        public const string Calculator =
            "M6.2,3.2 H17.8 A1.6,1.6 0 0 1 19.4,4.8 V19.2 A1.6,1.6 0 0 1 17.8,20.8 H6.2 " +
            "A1.6,1.6 0 0 1 4.6,19.2 V4.8 A1.6,1.6 0 0 1 6.2,3.2 Z " +
            "M7.4,6.8 H16.6 V10.2 H7.4 Z " +
            "M8.2,13.5 H9.2 M11.5,13.5 H12.5 M14.8,13.5 H15.8 " +
            "M8.2,16.9 H9.2 M11.5,16.9 H12.5 M14.8,16.9 H15.8";

        public const string Gear =
            "M5.9,12 A6.1,6.1 0 1 1 18.1,12 A6.1,6.1 0 1 1 5.9,12 " +
            "M12,1.9 V4.3 M12,19.7 V22.1 M1.9,12 H4.3 M19.7,12 H22.1 " +
            "M4.9,4.9 L6.6,6.6 M17.4,17.4 L19.1,19.1 " +
            "M19.1,4.9 L17.4,6.6 M6.6,17.4 L4.9,19.1";

        public const string Bars =
            "M3.6,20.4 V3.6 M3.6,20.4 H20.4 " +
            "M7.6,20.4 V13.4 M12,20.4 V8.2 M16.4,20.4 V15.6";

        public const string PlayPause =
            "M8.2,6.1 V17.9 L17.4,12 Z M20.2,6.1 V17.9";

        public const string VolumeUp =
            "M3.9,9.2 H7.3 L11.7,5.4 V18.6 L7.3,14.8 H3.9 Z " +
            "M14.7,9.4 A3.7,3.7 0 0 1 14.7,14.6 " +
            "M17.5,6.8 A7.6,7.6 0 0 1 17.5,17.2";

        public const string VolumeDown =
            "M3.9,9.2 H7.3 L11.7,5.4 V18.6 L7.3,14.8 H3.9 Z " +
            "M14.7,9.4 A3.7,3.7 0 0 1 14.7,14.6";

        public const string VolumeMute =
            "M3.9,9.2 H7.3 L11.7,5.4 V18.6 L7.3,14.8 H3.9 Z " +
            "M15.2,10 L19.4,14.2 M19.4,10 L15.2,14.2";

        public const string Desktop =
            "M4,4.9 H20 A1.6,1.6 0 0 1 21.6,6.5 V15 A1.6,1.6 0 0 1 20,16.6 H4 " +
            "A1.6,1.6 0 0 1 2.4,15 V6.5 A1.6,1.6 0 0 1 4,4.9 Z " +
            "M9,20.4 H15 M12,16.6 V20.4";

        public const string Lock =
            "M8.2,10.4 V7.6 A3.8,3.8 0 0 1 15.8,7.6 V10.4 " +
            "M6.4,10.4 H17.6 A1.4,1.4 0 0 1 19,11.8 V19 A1.4,1.4 0 0 1 17.6,20.4 H6.4 " +
            "A1.4,1.4 0 0 1 5,19 V11.8 A1.4,1.4 0 0 1 6.4,10.4 Z";

        public const string Power =
            "M12,3.4 V11.4 M6.9,6.6 A7.4,7.4 0 1 0 17.1,6.6";

        public const string Keyboard =
            "M4,7.2 H20 A1.4,1.4 0 0 1 21.4,8.6 V15.4 A1.4,1.4 0 0 1 20,16.8 H4 " +
            "A1.4,1.4 0 0 1 2.6,15.4 V8.6 A1.4,1.4 0 0 1 4,7.2 Z " +
            "M6.2,10.6 H6.9 M9.4,10.6 H10.1 M12.6,10.6 H13.3 M15.8,10.6 H16.5 " +
            "M8.4,13.6 H15.6";

        public const string Window =
            "M4,5.4 H20 A1.4,1.4 0 0 1 21.4,6.8 V16 A1.4,1.4 0 0 1 20,17.4 H4 " +
            "A1.4,1.4 0 0 1 2.6,16 V6.8 A1.4,1.4 0 0 1 4,5.4 Z " +
            "M2.6,9.2 H21.4";

        // --- Kommunikation ---------------------------------------------

        public const string Mail =
            "M5,5.6 H19 A1.6,1.6 0 0 1 20.6,7.2 V16.8 A1.6,1.6 0 0 1 19,18.4 H5 " +
            "A1.6,1.6 0 0 1 3.4,16.8 V7.2 A1.6,1.6 0 0 1 5,5.6 Z " +
            "M3.8,7.2 L12,13 L20.2,7.2";

        public const string Chat =
            "M5,5.2 H19 A1.6,1.6 0 0 1 20.6,6.8 V14 A1.6,1.6 0 0 1 19,15.6 " +
            "H11.4 L7.2,18.8 V15.6 H5 A1.6,1.6 0 0 1 3.4,14 V6.8 " +
            "A1.6,1.6 0 0 1 5,5.2 Z " +
            "M7.6,9.2 H16.4 M7.6,12 H13.4";

        public const string Phone =
            "M7.2,3.8 L9.6,3.8 A1.2,1.2 0 0 1 10.8,4.8 L11.4,8 " +
            "A1.2,1.2 0 0 1 10.8,9.2 L9.2,9.8 C10.2,12.4 11.6,13.8 14.2,14.8 " +
            "L14.8,13.2 A1.2,1.2 0 0 1 16,12.6 L19.2,13.2 A1.2,1.2 0 0 1 20.2,14.4 " +
            "L20.2,16.8 A2.4,2.4 0 0 1 17.7,19.2 C10.3,18.6 5.4,13.7 4.8,6.3 " +
            "A2.4,2.4 0 0 1 7.2,3.8 Z";

        // --- Medien ----------------------------------------------------

        public const string Music =
            "M8.8,17.2 A2.2,2.2 0 1 1 4.4,17.2 A2.2,2.2 0 1 1 8.8,17.2 " +
            "M8.8,17.2 V5.9 L18.8,3.7 V15.2 " +
            "M18.8,15.2 A2.2,2.2 0 1 1 14.4,15.2 A2.2,2.2 0 1 1 18.8,15.2";

        public const string Video =
            "M4.6,6.4 H19.4 A1.6,1.6 0 0 1 21,8 V16 A1.6,1.6 0 0 1 19.4,17.6 H4.6 " +
            "A1.6,1.6 0 0 1 3,16 V8 A1.6,1.6 0 0 1 4.6,6.4 Z " +
            "M10.2,9.4 L15.4,12 L10.2,14.6 Z";

        public const string Camera =
            "M3.4,8.6 H7.8 L9.2,6.2 H14.8 L16.2,8.6 H20.6 " +
            "A1.4,1.4 0 0 1 22,10 V18.2 A1.4,1.4 0 0 1 20.6,19.6 H3.4 " +
            "A1.4,1.4 0 0 1 2,18.2 V10 A1.4,1.4 0 0 1 3.4,8.6 Z " +
            "M9,14 A3,3 0 1 1 15,14 A3,3 0 1 1 9,14";

        public const string Image =
            "M4.6,4.6 H19.4 A1.4,1.4 0 0 1 20.8,6 V18 A1.4,1.4 0 0 1 19.4,19.4 H4.6 " +
            "A1.4,1.4 0 0 1 3.2,18 V6 A1.4,1.4 0 0 1 4.6,4.6 Z " +
            "M10.6,8.6 A1.6,1.6 0 1 1 7.4,8.6 A1.6,1.6 0 1 1 10.6,8.6 " +
            "M3.6,16.8 L9,11.4 L12.6,15 L15.4,12.2 L20.4,17.2";

        public const string Headphones =
            "M4.2,15 V12 A7.8,7.8 0 0 1 19.8,12 V15 " +
            "M4.2,14.4 H6 A1.4,1.4 0 0 1 7.4,15.8 V18.6 A1.4,1.4 0 0 1 6,20 H4.2 Z " +
            "M19.8,14.4 H18 A1.4,1.4 0 0 0 16.6,15.8 V18.6 A1.4,1.4 0 0 0 18,20 H19.8 Z";

        public const string Microphone =
            "M12,3.6 A2.8,2.8 0 0 1 14.8,6.4 V12 A2.8,2.8 0 0 1 9.2,12 V6.4 " +
            "A2.8,2.8 0 0 1 12,3.6 Z " +
            "M5.8,11.4 V12 A6.2,6.2 0 0 0 18.2,12 V11.4 " +
            "M12,18.2 V20.8 M8.6,20.8 H15.4";

        public const string Gamepad =
            "M7.6,8.2 H16.4 A4.4,4.4 0 0 1 20.6,12.6 V15 " +
            "A3,3 0 0 1 15.6,16.6 L14.4,15.4 H9.6 L8.4,16.6 " +
            "A3,3 0 0 1 3.4,15 V12.6 A4.4,4.4 0 0 1 7.6,8.2 Z " +
            "M7.4,11 V13.4 M6.2,12.2 H8.6 M15.4,11.6 H15.9 M17.2,13 H17.7";

        public const string Spotify =
            "M3.4,12 A8.6,8.6 0 1 1 20.6,12 A8.6,8.6 0 1 1 3.4,12 " +
            "M7.4,9.2 C10.6,8.2 14.2,8.6 17,10.2 " +
            "M7.8,12.6 C10.4,11.8 13.4,12.1 15.8,13.5 " +
            "M8.4,15.8 C10.4,15.2 12.8,15.4 14.6,16.5";

        // --- Entwicklung ----------------------------------------------

        public const string Code =
            "M8.6,7.4 L3.2,12 L8.6,16.6 M15.4,7.4 L20.8,12 L15.4,16.6 " +
            "M13.4,5.2 L10.6,18.8";

        public const string Terminal2 =
            "M4,5.4 H20 A1.4,1.4 0 0 1 21.4,6.8 V17.2 A1.4,1.4 0 0 1 20,18.6 H4 " +
            "A1.4,1.4 0 0 1 2.6,17.2 V6.8 A1.4,1.4 0 0 1 4,5.4 Z " +
            "M6.6,9.6 L9.6,12.2 L6.6,14.8 M11.6,14.8 H17";

        public const string Database =
            "M4.2,6.6 A7.8,2.8 0 1 1 19.8,6.6 A7.8,2.8 0 1 1 4.2,6.6 " +
            "M4.2,6.6 V17.4 A7.8,2.8 0 0 0 19.8,17.4 V6.6 " +
            "M4.2,12 A7.8,2.8 0 0 0 19.8,12";

        public const string Git =
            "M3.8,5.6 A2.2,2.2 0 1 1 8.2,5.6 A2.2,2.2 0 1 1 3.8,5.6 " +
            "M3.8,18.4 A2.2,2.2 0 1 1 8.2,18.4 A2.2,2.2 0 1 1 3.8,18.4 " +
            "M15.8,9.4 A2.2,2.2 0 1 1 20.2,9.4 A2.2,2.2 0 1 1 15.8,9.4 " +
            "M6,7.8 V16.2 M6,13.4 H10.8 A4,4 0 0 0 14.8,9.4 H15.8";

        public const string Docker =
            "M6.2,13.4 V10.6 H9 V13.4 M9.6,13.4 V10.6 H12.4 V13.4 " +
            "M13,13.4 V10.6 H15.8 V13.4 M9.6,10 V7.2 H12.4 V10 " +
            "M3.4,14.2 H20.6 V15.4 A4.4,4.4 0 0 1 16.2,19.8 H7.8 " +
            "A4.4,4.4 0 0 1 3.4,15.4 Z";

        public const string Bug =
            "M9,7 A3,3 0 0 1 15,7 " +
            "M8.2,8.4 H15.8 A4.2,4.2 0 0 1 20,12.6 V14 A8,8 0 0 1 4,14 V12.6 " +
            "A4.2,4.2 0 0 1 8.2,8.4 Z " +
            "M4,12.6 H20 M7.4,16.2 L4.8,18.2 M16.6,16.2 L19.2,18.2 " +
            "M8.2,8.6 L6.2,5.8 M15.8,8.6 L17.8,5.8 M12,8.4 V20.4";

        public const string Steam =
            "M3.4,12 A8.6,8.6 0 1 1 20.6,12 A8.6,8.6 0 1 1 3.4,12 " +
            "M5.6,17.6 L12.2,13.4 " +
            "M11.4,11 A3.2,3.2 0 1 1 17.8,11 A3.2,3.2 0 1 1 11.4,11";

        // --- Werkzeuge und Verwaltung ---------------------------------

        public const string Settings2 =
            "M4,7 H20 M4,12 H20 M4,17 H20 " +
            "M8.4,4.8 V9.2 M15.6,9.8 V14.2 M10.6,14.8 V19.2";

        public const string Wrench =
            "M14.7,6.3 A1,1 0 0 0 14.7,7.7 L16.3,9.3 A1,1 0 0 0 17.7,9.3 L21.5,5.5 " +
            "A6,6 0 0 1 13.5,13.5 L6.6,20.4 A2.12,2.12 0 0 1 3.6,17.4 L10.5,10.5 " +
            "A6,6 0 0 1 18.5,2.5 Z";

        public const string Shield =
            "M12,3.4 L19.4,6.2 V11.8 C19.4,16.4 16.2,19.6 12,20.8 " +
            "C7.8,19.6 4.6,16.4 4.6,11.8 V6.2 Z " +
            "M8.8,12.1 L11.2,14.5 L15.4,10.3";

        public const string Key =
            "M4.6,9.4 A3.4,3.4 0 1 1 11.4,9.4 A3.4,3.4 0 1 1 4.6,9.4 " +
            "M10.4,11.8 L19.6,21 M16,17.4 L18.2,15.2 M13.6,15 L15.8,12.8";

        public const string Lock2 =
            "M7.2,10.6 V7.8 A4.8,4.8 0 0 1 16.8,7.8 V10.6 " +
            "M5.4,10.6 H18.6 A1.6,1.6 0 0 1 20.2,12.2 V18.8 A1.6,1.6 0 0 1 18.6,20.4 " +
            "H5.4 A1.6,1.6 0 0 1 3.8,18.8 V12.2 A1.6,1.6 0 0 1 5.4,10.6 Z " +
            "M12,14.2 V16.8";

        public const string Unlock =
            "M7.2,10.6 V7.8 A4.8,4.8 0 0 1 16.8,7.8 " +
            "M5.4,10.6 H18.6 A1.6,1.6 0 0 1 20.2,12.2 V18.8 A1.6,1.6 0 0 1 18.6,20.4 " +
            "H5.4 A1.6,1.6 0 0 1 3.8,18.8 V12.2 A1.6,1.6 0 0 1 5.4,10.6 Z";

        public const string Trash =
            "M4.4,6.6 H19.6 M9.2,6.6 V4.4 A1,1 0 0 1 10.2,3.4 H13.8 " +
            "A1,1 0 0 1 14.8,4.4 V6.6 " +
            "M6.4,6.6 L7.2,19.4 A1.4,1.4 0 0 0 8.6,20.6 H15.4 " +
            "A1.4,1.4 0 0 0 16.8,19.4 L17.6,6.6 M10.2,10.4 V16.8 M13.8,10.4 V16.8";

        public const string Printer =
            "M7,8.4 V4.4 H17 V8.4 M4.4,8.4 H19.6 A1.4,1.4 0 0 1 21,9.8 V15.6 " +
            "A1.4,1.4 0 0 1 19.6,17 H18 M6,14.6 H18 V20.4 H6 Z " +
            "M4.4,8.4 A1.4,1.4 0 0 0 3,9.8 V15.6 A1.4,1.4 0 0 0 4.4,17 H6";

        public const string Save =
            "M5,3.8 H16.2 L20.2,7.8 V19 A1.4,1.4 0 0 1 18.8,20.4 H5 " +
            "A1.4,1.4 0 0 1 3.6,19 V5.2 A1.4,1.4 0 0 1 5,3.8 Z " +
            "M7.4,3.8 V9 H15.4 V3.8 M7.4,20.4 V14.4 H16.6 V20.4";

        public const string Copy =
            "M8.6,8.6 H18.8 A1.4,1.4 0 0 1 20.2,10 V19 A1.4,1.4 0 0 1 18.8,20.4 " +
            "H8.6 A1.4,1.4 0 0 1 7.2,19 V10 A1.4,1.4 0 0 1 8.6,8.6 Z " +
            "M4.8,15.4 A1.4,1.4 0 0 1 3.4,14 V5 A1.4,1.4 0 0 1 4.8,3.6 " +
            "H14.4 A1.4,1.4 0 0 1 15.8,5 V6.4";

        public const string Paste =
            "M8.4,4.4 H6.4 A1.4,1.4 0 0 0 5,5.8 V19.2 A1.4,1.4 0 0 0 6.4,20.6 " +
            "H17.6 A1.4,1.4 0 0 0 19,19.2 V5.8 A1.4,1.4 0 0 0 17.6,4.4 H15.6 " +
            "M8.4,3.2 H15.6 V6.6 H8.4 Z";

        public const string Cut =
            "M6.4,4.6 L17.6,18.6 M17.6,4.6 L6.4,18.6 " +
            "M3.6,19 A2.4,2.4 0 1 1 8.4,19 A2.4,2.4 0 1 1 3.6,19 " +
            "M15.6,19 A2.4,2.4 0 1 1 20.4,19 A2.4,2.4 0 1 1 15.6,19";

        public const string Undo =
            "M9,6 L4.6,10.4 L9,14.8 M4.6,10.4 H14.8 A4.8,4.8 0 0 1 14.8,20 H11.4";

        public const string Redo =
            "M15,6 L19.4,10.4 L15,14.8 M19.4,10.4 H9.2 A4.8,4.8 0 0 0 9.2,20 H12.6";

        public const string Filter =
            "M3.6,5.2 H20.4 L14.2,12.6 V19.4 L9.8,21 V12.6 Z";

        public const string Tag =
            "M3.6,11.2 V4.6 A1,1 0 0 1 4.6,3.6 H11.2 L20.4,12.8 L12.8,20.4 Z " +
            "M6.6,7.6 A1,1 0 1 1 8.6,7.6 A1,1 0 1 1 6.6,7.6";

        public const string Link =
            "M10,14 A3.6,3.6 0 0 0 15.1,14 L18.3,10.8 A3.6,3.6 0 0 0 13.2,5.7 " +
            "L11.4,7.5 M14,10 A3.6,3.6 0 0 0 8.9,10 L5.7,13.2 " +
            "A3.6,3.6 0 0 0 10.8,18.3 L12.6,16.5";

        // --- Personen und Inhalte -------------------------------------

        public const string User =
            "M8,8.2 A4,4 0 1 1 16,8.2 A4,4 0 1 1 8,8.2 " +
            "M4.6,20.4 C4.6,16.8 7.8,14.4 12,14.4 C16.2,14.4 19.4,16.8 19.4,20.4";

        public const string Users =
            "M5.6,8 A3.4,3.4 0 1 1 12.4,8 A3.4,3.4 0 1 1 5.6,8 " +
            "M2.8,19.6 C2.8,16.6 5.6,14.4 9,14.4 C12.4,14.4 15.2,16.6 15.2,19.6 " +
            "M16,5.2 A3.2,3.2 0 0 1 16,11.4 " +
            "M17.2,14.8 C19.4,15.6 20.8,17.4 20.8,19.6";

        public const string Search =
            "M4.4,10.2 A5.8,5.8 0 1 1 16,10.2 A5.8,5.8 0 1 1 4.4,10.2 " +
            "M14.6,14.6 L20,20";

        public const string Star =
            "M12,3.8 L14.5,9 L20.2,9.8 L16.1,13.8 L17.1,19.5 " +
            "L12,16.8 L6.9,19.5 L7.9,13.8 L3.8,9.8 L9.5,9 Z";

        public const string Bookmark =
            "M6.4,4.2 H17.6 A1.2,1.2 0 0 1 18.8,5.4 V20 L12,15.6 L5.2,20 " +
            "V5.4 A1.2,1.2 0 0 1 6.4,4.2 Z";

        public const string Heart =
            "M12,20.2 C8.4,17.8 3.8,14.4 3.8,9.9 A4.5,4.5 0 0 1 12,7 " +
            "A4.5,4.5 0 0 1 20.2,9.9 C20.2,14.4 15.6,17.8 12,20.2 Z";

        public const string Calendar =
            "M4.6,6.4 H19.4 A1.4,1.4 0 0 1 20.8,7.8 V18.6 A1.4,1.4 0 0 1 19.4,20 " +
            "H4.6 A1.4,1.4 0 0 1 3.2,18.6 V7.8 A1.4,1.4 0 0 1 4.6,6.4 Z " +
            "M3.2,10.6 H20.8 M8,3.6 V7.4 M16,3.6 V7.4";

        public const string Clock =
            "M3.4,12 A8.6,8.6 0 1 1 20.6,12 A8.6,8.6 0 1 1 3.4,12 " +
            "M12,7 V12 L15.6,14.2";

        public const string Bell =
            "M12,3.6 A1.2,1.2 0 0 1 13.2,4.8 V5.7 C16.5,6.4 18.4,9 18.4,12.6 " +
            "V16.2 L20,18.4 H4 L5.6,16.2 V12.6 C5.6,9 7.5,6.4 10.8,5.7 V4.8 " +
            "A1.2,1.2 0 0 1 12,3.6 Z " +
            "M9.8,18.4 A2.4,2.4 0 0 0 14.2,18.4";

        public const string FileText =
            "M6.4,3.6 H14 L19,8.6 V19.6 A1.4,1.4 0 0 1 17.6,21 H6.4 " +
            "A1.4,1.4 0 0 1 5,19.6 V5 A1.4,1.4 0 0 1 6.4,3.6 Z " +
            "M14,3.6 V8.6 H19 M8,12.4 H16 M8,16 H16";

        public const string FilePdf =
            "M6.4,3.6 H14 L19,8.6 V19.6 A1.4,1.4 0 0 1 17.6,21 H6.4 " +
            "A1.4,1.4 0 0 1 5,19.6 V5 A1.4,1.4 0 0 1 6.4,3.6 Z " +
            "M14,3.6 V8.6 H19 M7.8,18.4 V13.4 H9.4 A1.2,1.2 0 0 1 9.4,15.8 " +
            "H7.8 M11.8,18.4 V13.4 H12.8 A1.6,1.6 0 0 1 12.8,18.4 Z";

        // --- Orte und Geraete -----------------------------------------

        public const string Map =
            "M9,4.6 L3.4,6.6 V19.4 L9,17.4 L15,20.2 L20.6,18.2 V5.4 L15,7.4 Z " +
            "M9,4.6 V17.4 M15,7.4 V20.2";

        public const string Car =
            "M4.2,16.6 V12 L6.4,7 H17.6 L19.8,12 V16.6 " +
            "M3.4,16.6 H20.6 M6.6,12 H17.4 " +
            "M3.6,16.6 A1.6,1.6 0 1 1 6.8,16.6 A1.6,1.6 0 1 1 3.6,16.6 " +
            "M17.2,16.6 A1.6,1.6 0 1 1 20.4,16.6 A1.6,1.6 0 1 1 17.2,16.6";

        public const string Home =
            "M3.4,11 L12,3.8 L20.6,11 " +
            "M5.6,9.6 V19.4 A1.2,1.2 0 0 0 6.8,20.6 H17.2 A1.2,1.2 0 0 0 18.4,19.4 " +
            "V9.6 M9.8,20.6 V14.6 H14.2 V20.6";

        public const string Monitor =
            "M3.6,4.4 H20.4 A1.6,1.6 0 0 1 22,6 V15.4 A1.6,1.6 0 0 1 20.4,17 " +
            "H3.6 A1.6,1.6 0 0 1 2,15.4 V6 A1.6,1.6 0 0 1 3.6,4.4 Z " +
            "M9.4,20.4 H14.6 M12,17 V20.4";

        public const string FolderOpen =
            "M3.2,18.6 V6.6 A1.4,1.4 0 0 1 4.6,5.2 H9.1 L11,7.4 H18.8 " +
            "A1.4,1.4 0 0 1 20.2,8.8 V10.2 " +
            "M3.2,18.6 L5.8,10.8 A1.2,1.2 0 0 1 6.9,10 H21 " +
            "A1,1 0 0 1 22,11.1 L19.8,18.6 A1.4,1.4 0 0 1 18.5,19.6 H4.6 " +
            "A1.4,1.4 0 0 1 3.2,18.6 Z";

        public const string Network =
            "M9,3.4 H15 V7 H9 Z M2.6,17 H8.6 V20.6 H2.6 Z M15.4,17 H21.4 V20.6 H15.4 Z " +
            "M12,7 V12 M5.6,17 V12 H18.4 V17";

        public const string Wifi =
            "M2.4,8.6 A13.6,13.6 0 0 1 21.6,8.6 " +
            "M5.4,12 A9.3,9.3 0 0 1 18.6,12 " +
            "M8.4,15.4 A5,5 0 0 1 15.6,15.4 " +
            "M10.9,18.7 A1.1,1.1 0 1 1 13.1,18.7 A1.1,1.1 0 1 1 10.9,18.7";

        public const string Bluetooth =
            "M7.2,7.4 L16.8,16.6 L12,20.8 V3.2 L16.8,7.4 L7.2,16.6";

        public const string BluetoothOff =
            "M7.2,7.4 L16.8,16.6 L12,20.8 V3.2 L16.8,7.4 L7.2,16.6 " +
            "M3.4,3.4 L20.6,20.6";

        public const string Battery =
            "M3.4,8.4 H17 A1.6,1.6 0 0 1 18.6,10 V14 A1.6,1.6 0 0 1 17,15.6 H3.4 " +
            "A1.6,1.6 0 0 1 1.8,14 V10 A1.6,1.6 0 0 1 3.4,8.4 Z " +
            "M21,10.4 V13.6 M4.8,10.6 V13.4 H10.6 V10.6 Z";

        public const string Wallet =
            "M3.4,7.6 A1.6,1.6 0 0 1 5,6 H18 A1.4,1.4 0 0 1 19.4,7.4 V8.6 " +
            "M3.4,7.6 V17.4 A1.6,1.6 0 0 0 5,19 H19 A1.4,1.4 0 0 0 20.4,17.6 " +
            "V11.4 A1.4,1.4 0 0 0 19,10 H5 A1.6,1.6 0 0 1 3.4,8.4 Z " +
            "M16,14.4 H16.6";

        public const string ShoppingCart =
            "M3,4.4 H5.2 L7.4,15 H18.2 L20.6,7.4 H6.2 " +
            "M7.4,15 L6.6,17.6 H18.4 " +
            "M8.4,19.2 A1.2,1.2 0 1 1 10.8,19.2 A1.2,1.2 0 1 1 8.4,19.2 " +
            "M15.2,19.2 A1.2,1.2 0 1 1 17.6,19.2 A1.2,1.2 0 1 1 15.2,19.2";

        // --- Umgebung und Ansicht -------------------------------------

        public const string Sun =
            "M8.2,12 A3.8,3.8 0 1 1 15.8,12 A3.8,3.8 0 1 1 8.2,12 " +
            "M12,2.4 V4.4 M12,19.6 V21.6 M2.4,12 H4.4 M19.6,12 H21.6 " +
            "M5.2,5.2 L6.6,6.6 M17.4,17.4 L18.8,18.8 " +
            "M18.8,5.2 L17.4,6.6 M6.6,17.4 L5.2,18.8";

        public const string Moon =
            "M20,14.6 A8.6,8.6 0 0 1 9.4,4 A8.8,8.8 0 1 0 20,14.6 Z";

        public const string Contrast =
            "M3.4,12 A8.6,8.6 0 1 1 20.6,12 A8.6,8.6 0 1 1 3.4,12 " +
            "M12,3.4 V20.6";

        public const string ZoomIn =
            "M4.4,10.2 A5.8,5.8 0 1 1 16,10.2 A5.8,5.8 0 1 1 4.4,10.2 " +
            "M14.6,14.6 L20,20 M7.4,10.2 H13 M10.2,7.4 V13";

        public const string ZoomOut =
            "M4.4,10.2 A5.8,5.8 0 1 1 16,10.2 A5.8,5.8 0 1 1 4.4,10.2 " +
            "M14.6,14.6 L20,20 M7.4,10.2 H13";

        public const string Fullscreen =
            "M9,3.6 H3.6 V9 M15,3.6 H20.4 V9 " +
            "M20.4,15 V20.4 H15 M9,20.4 H3.6 V15";

        public const string Minimize =
            "M4.6,4.8 H19.4 A1.4,1.4 0 0 1 20.8,6.2 V16.4 A1.4,1.4 0 0 1 19.4,17.8 " +
            "H4.6 A1.4,1.4 0 0 1 3.2,16.4 V6.2 A1.4,1.4 0 0 1 4.6,4.8 Z " +
            "M8,20.4 H16";

        public const string Grid =
            "M4,4 H10 V10 H4 Z M14,4 H20 V10 H14 Z " +
            "M4,14 H10 V20 H4 Z M14,14 H20 V20 H14 Z";

        public const string List =
            "M8.4,6.4 H20 M8.4,12 H20 M8.4,17.6 H20 " +
            "M4,6.4 H4.2 M4,12 H4.2 M4,17.6 H4.2";

        public const string Eye =
            "M2.4,12 C5.4,7.4 8.7,5.8 12,5.8 C15.3,5.8 18.6,7.4 21.6,12 " +
            "C18.6,16.6 15.3,18.2 12,18.2 C8.7,18.2 5.4,16.6 2.4,12 Z " +
            "M9.4,12 A2.6,2.6 0 1 1 14.6,12 A2.6,2.6 0 1 1 9.4,12";

        public const string EyeOff =
            "M2.4,12 C5.4,7.4 8.7,5.8 12,5.8 C15.3,5.8 18.6,7.4 21.6,12 " +
            "C18.6,16.6 15.3,18.2 12,18.2 C8.7,18.2 5.4,16.6 2.4,12 Z " +
            "M9.4,12 A2.6,2.6 0 1 1 14.6,12 A2.6,2.6 0 1 1 9.4,12 " +
            "M4,4 L20,20";

        // --- Zeichen und Aktionen -------------------------------------

        public const string Plus =
            "M12,4.6 V19.4 M4.6,12 H19.4";

        public const string Minus =
            "M4.6,12 H19.4";

        public const string Check =
            "M4.8,12.6 L9.6,17.4 L19.2,6.8";

        public const string Close =
            "M6,6 L18,18 M18,6 L6,18";

        public const string ArrowUp =
            "M12,19.4 V5 M5.6,11.4 L12,5 L18.4,11.4";

        public const string ArrowDown =
            "M12,4.6 V19 M5.6,12.6 L12,19 L18.4,12.6";

        public const string ArrowLeft =
            "M19.4,12 H5 M11,5.6 L4.6,12 L11,18.4";

        public const string ArrowRight =
            "M4.6,12 H19 M13,5.6 L19.4,12 L13,18.4";

        public const string Refresh =
            "M19.6,4.6 V9 H15.2 M19.2,9 A7.8,7.8 0 1 0 19.4,15.4";

        public const string Cloud =
            "M7.4,18.6 A4.6,4.6 0 0 1 7.2,9.4 A5.6,5.6 0 0 1 18,9.8 " +
            "A4.4,4.4 0 0 1 17.6,18.6 Z";

        public const string Download =
            "M12,3.6 V15.2 M7,10.2 L12,15.2 L17,10.2 " +
            "M4.2,17.6 V19.6 A1,1 0 0 0 5.2,20.6 H18.8 A1,1 0 0 0 19.8,19.6 V17.6";

        public const string Upload =
            "M12,15.4 V3.8 M7,8.8 L12,3.8 L17,8.8 " +
            "M4.2,17.6 V19.6 A1,1 0 0 0 5.2,20.6 H18.8 A1,1 0 0 0 19.8,19.6 V17.6";

        // --- Nachschlagewerk ------------------------------------------

        static readonly Dictionary<string, string> ByName = BuildMap();
        static readonly Dictionary<string, string> ByNameFlat = BuildFlatMap();

        static Dictionary<string, string> BuildMap()
        {
            Dictionary<string, string> m =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            FieldInfo[] fields = typeof(IconLibrary).GetFields(
                BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType != typeof(string)) continue;
                m[fields[i].Name] = (string)fields[i].GetValue(null);
            }
            return m;
        }

        // Zweite Zuordnung ohne Trennzeichen, damit "arrow up" und
        // "ArrowUp" dasselbe Symbol finden.
        static Dictionary<string, string> BuildFlatMap()
        {
            Dictionary<string, string> m =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> kv in ByName)
                m[Flatten(kv.Key)] = kv.Value;
            return m;
        }

        static string Flatten(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        // Symbol zu einer Aktion waehlen.
        public static string For(ActionItem a)
        {
            if (a == null) return Gear;
            return ForText(a.Kind, a.Target, a.Label);
        }

        // Symbol aus einem expliziten Namen (Feld ActionItem.Icon).
        // Kennt der Name kein Symbol, greifen dieselben Hinweise wie For().
        public static string Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return Gear;

            string hit;
            if (ByName.TryGetValue(name.Trim(), out hit)) return hit;
            if (ByNameFlat.TryGetValue(Flatten(name), out hit)) return hit;

            return ForText(GuessKind(name), name, name);
        }

        // Symbol fuer eine Aktion, mit ihrem Icon-Feld als Vorrang.
        // Eigener Name, damit Resolve(null) eindeutig bleibt.
        public static string ResolveItem(ActionItem a)
        {
            if (a == null) return Gear;
            if (!string.IsNullOrEmpty(a.Icon))
            {
                string hit;
                if (ByName.TryGetValue(a.Icon.Trim(), out hit)) return hit;
                if (ByNameFlat.TryGetValue(Flatten(a.Icon), out hit)) return hit;
            }
            return For(a);
        }

        // Alle Namen, alphabetisch, fuer die Symbolauswahl.
        public static List<string> AllNames()
        {
            List<string> names = new List<string>(ByName.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static bool Exists(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (ByName.ContainsKey(name.Trim())) return true;
            return ByNameFlat.ContainsKey(Flatten(name));
        }

        // --- Auswahlheuristik -----------------------------------------

        static string ForText(string kind, string target, string label)
        {
            string k = (kind ?? "").ToLowerInvariant();
            string t = (target ?? "").ToLowerInvariant();
            string l = (label ?? "").ToLowerInvariant();
            string all = t + " " + l;

            if (k == "volume")
            {
                if (all.Contains("mute") || all.Contains("stumm")) return VolumeMute;
                if (all.Contains("down") || all.Contains("leiser") || all.Contains("leise")) return VolumeDown;
                return VolumeUp;
            }

            if (k == "media")
            {
                if (all.Contains("next") || all.Contains("weiter") || all.Contains("vor")) return ArrowRight;
                if (all.Contains("prev") || all.Contains("zurueck")) return ArrowLeft;
                if (all.Contains("mute") || all.Contains("stumm")) return VolumeMute;
                if (all.Contains("stop")) return Close;
                return PlayPause;
            }

            if (k == "hotkey") return Keyboard;
            if (k == "activate") return Window;
            if (k == "text") return Keyboard;
            if (k == "macro") return PlayPause;
            if (k == "shell" || k == "script") return Terminal;

            if (k == "discord")
            {
                if (t == "deafen" || t == "undeafen" || t == "toggledeafen"
                    || l.Contains("taub")) return Headphones;
                if (t == "mute" || t == "unmute" || t == "toggle"
                    || l.Contains("stumm") || l.Contains("mikro")) return Microphone;
                return Chat;
            }

            if (k == "system")
            {
                if (t.Contains("showdesktop")) return Desktop;
                if (t.Contains("lock") || l.Contains("sperren")) return Lock;
                if (t.Contains("taskmgr") || l.Contains("task-manager")) return Bars;
                if (t.Contains("shutdown") || t.Contains("restart") ||
                    t.Contains("sleep") || t.Contains("hibernate")) return Power;
                if (t.Contains("screenshot") || l.Contains("screenshot")) return Camera;
                if (t.Contains("settings") || t.Contains("ms-settings")) return Gear;
                if (t.Contains("wifi") || l.Contains("wlan")) return Wifi;
                if (t.Contains("bluetooth")) return Bluetooth;
                if (t.Contains("brightness") || l.Contains("helligkeit")) return Sun;
                return Gear;
            }

            if (k == "file") return FileText;
            if (k == "folder" || k == "directory") return Folder;

            // Webziele zuerst, damit github.com nicht als git.exe gilt.
            if (k == "url" || k == "web" || k == "browser" || k == "link")
            {
                string web = WebsiteIcon(t);
                if (web != null) return web;
                return Globe;
            }

            if (k == "explorer") return Folder;

            // Programme anhand von Ziel und Beschriftung erkennen.
            string p;
            p = ProgramIcon(t, l);
            if (p != null) return p;

            p = ProgramIcon(l, l);
            if (p != null) return p;

            p = WordIcon(l);
            if (p != null) return p;

            if (k == "program") return Window;
            return Gear;
        }

        // Gattungsbegriffe, die als Beschriftung auftauchen.
        static string WordIcon(string l)
        {
            if (l.Length == 0) return null;
            if (l.Contains("browser") || l.Contains("internet")) return Globe;
            if (l.Contains("mail") || l.Contains("e-mail") || l.Contains("postfach")) return Mail;
            if (l.Contains("musik") || l.Contains("music")) return Music;
            if (l.Contains("video") || l.Contains("film")) return Video;
            if (l.Contains("foto") || l.Contains("photo") || l.Contains("bild")) return Image;
            if (l.Contains("rechner") && !l.Contains("taschenrechner")) return Calculator;
            if (l.Contains("taschenrechner") || l.Contains("calculator")) return Calculator;
            if (l.Contains("einstellung") || l.Contains("setting")) return Gear;
            if (l.Contains("terminal") || l.Contains("konsole")) return Terminal;
            if (l.Contains("explorer") || l.Contains("dateien")) return Folder;
            if (l.Contains("papierkorb") || l.Contains("trash")) return Trash;
            if (l.Contains("suchen") || l.Contains("search")) return Search;
            if (l.Contains("kalender") || l.Contains("calendar")) return Calendar;
            if (l.Contains("uhr") || l.Contains("clock")) return Clock;
            if (l.Contains("lauter") || l.Contains("volume") || l.Contains("lautst")) return VolumeUp;
            if (l.Contains("headset") || l.Contains("kopfh")) return Headphones;
            if (l.Contains("mikro") || l.Contains("micro")) return Microphone;
            return null;
        }

        // Domains und Dienste, die als Webziel erkennbar sind.
        static string WebsiteIcon(string t)
        {
            if (t.Contains("ms-settings")) return Gear;
            if (t.Contains("mailto") || t.Contains("gmail") || t.Contains("outlook") ||
                t.Contains("mail.") || t.Contains("webmail")) return Mail;
            if (t.Contains("youtube") || t.Contains("vimeo") || t.Contains("twitch") ||
                t.Contains("netflix")) return Video;
            if (t.Contains("github") || t.Contains("gitlab") || t.Contains("stackoverflow")) return Code;
            if (t.Contains("maps.google") || t.Contains("openstreetmap") || t.Contains("maps.")) return Map;
            if (t.Contains("chatgpt") || t.Contains("claude") || t.Contains("chat.") ||
                t.Contains("discord") || t.Contains("web.whatsapp") || t.Contains("teams")) return Chat;
            if (t.Contains("spotify")) return Spotify;
            if (t.Contains("steam")) return Steam;
            if (t.Contains("drive.google") || t.Contains("dropbox") || t.Contains("onedrive")) return Cloud;
            if (t.Contains("docs.google") || t.Contains("notion")) return FileText;
            if (t.Contains("calendar")) return Calendar;
            if (t.Contains("search") || t.Contains("google.")) return Search;
            return null;
        }

        // Freitext aus dem Icon-Feld: sieht es nach einem Ziel aus,
        // gelten die Webregeln, sonst die Programmregeln.
        static string GuessKind(string text)
        {
            string s = (text ?? "").ToLowerInvariant();
            if (s.Contains("://") || s.StartsWith("www.") || s.Contains(".com") ||
                s.Contains(".org") || s.Contains(".net") || s.Contains(".io") ||
                s.Contains(".de") || s.StartsWith("ms-settings") ||
                s.StartsWith("mailto")) return "url";
            return "";
        }

        static string ProgramIcon(string t, string l)
        {
            if (t.Contains("chrome") || t.Contains("firefox") || t.Contains("msedge") ||
                t.Contains("edge.exe") || t.Contains("brave") || t.Contains("opera")) return Globe;
            if (t.Contains("discord") || t.Contains("slack") || t.Contains("teams") ||
                t.Contains("whatsapp") || t.Contains("telegram") || t.Contains("zoom")) return Chat;
            if (t.Contains("spotify")) return Spotify;
            if (t.Contains("steam")) return Steam;
            if (t.Contains("devenv") || t.Contains("vscode") || t.Contains("code.exe") ||
                t.Contains("rider") || t.Contains("idea") || t.Contains("studio64") ||
                l.Contains("visual studio") || l == "code" || l.Contains("vs code")) return Code;
            if (t.Contains("git")) return Git;
            if (t.Contains("docker")) return Docker;
            if (t.Contains("putty")) return Terminal;
            if (t.Contains("winscp") || t.Contains("filezilla")) return Network;
            if (t.Contains("wt.exe") || t.Contains("cmd") || t.Contains("powershell") ||
                t.Contains("pwsh") || t.Contains("conhost") || l.Contains("terminal") ||
                l.Contains("konsole") || l.Contains("shell")) return Terminal;
            if (t.Contains("notepad")) return FileText;
            if (t.Contains("calc")) return Calculator;
            if (t.Contains("mspaint") || t.Contains("paint")) return Image;
            if (t.Contains("taskmgr")) return Bars;
            if (t.Contains("vlc") || t.Contains("obs") || t.Contains("mpv") ||
                t.Contains("wmplayer")) return Video;
            if (t.Contains("explorer")) return Folder;
            if (t.Contains("word") || t.Contains("winword") || t.Contains("acrobat")) return FileText;
            if (t.Contains("excel")) return Grid;
            if (t.Contains("outlook") || t.Contains("thunderbird")) return Mail;
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Bildmarke: Ring aus acht Segmenten um einen gefuellten Punkt.
    // Monochrom, damit sie auf hellem Grund genauso sitzt wie auf dunklem.
    // ------------------------------------------------------------------

    public static class BrandMark
    {
        const double Grid = 24.0;
        const double Radius = 9.4;
        const double StrokeWidth = 2.15;
        const double DotRadius = 2.9;
        const int Segments = 8;
        const double GapDegrees = 16.0;

        static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)));

        static Brush Freeze(Brush b)
        {
            b.Freeze();
            return b;
        }

        static Drawing CreateDrawing(Brush ink)
        {
            DrawingGroup group = new DrawingGroup();

            Pen pen = new Pen(ink, StrokeWidth);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            pen.Freeze();

            group.Children.Add(new GeometryDrawing(null, pen, CreateRing()));

            EllipseGeometry dot = new EllipseGeometry(new Point(Grid / 2.0, Grid / 2.0), DotRadius, DotRadius);
            dot.Freeze();
            group.Children.Add(new GeometryDrawing(ink, null, dot));

            group.Freeze();
            return group;
        }

        // Acht Boegen mit gleichem Winkel und gleich grossen Luecken.
        static Geometry CreateRing()
        {
            double cx = Grid / 2.0;
            double cy = Grid / 2.0;
            double sweep = (360.0 / Segments) - GapDegrees;

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                for (int i = 0; i < Segments; i++)
                {
                    double start = (i * (360.0 / Segments)) + (GapDegrees / 2.0) - 90.0;
                    double end = start + sweep;

                    Point from = PointOnCircle(cx, cy, Radius, start);
                    Point to = PointOnCircle(cx, cy, Radius, end);

                    ctx.BeginFigure(from, false, false);
                    ctx.ArcTo(to, new Size(Radius, Radius), 0.0, false,
                             SweepDirection.Clockwise, true, false);
                }
            }
            geometry.Freeze();
            return geometry;
        }

        static Point PointOnCircle(double cx, double cy, double r, double degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
        }

        // Fertiges, eingefrorenes Bild fuer die Oberflaeche.
        public static DrawingImage Create()
        {
            DrawingImage image = new DrawingImage(CreateDrawing(Ink));
            image.Freeze();
            return image;
        }

        // Fuer dunkle Flaechen: Ring und Punkt in Weiss.
        public static DrawingImage CreateLight()
        {
            Brush light = Freeze(new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF6)));
            DrawingImage image = new DrawingImage(CreateDrawing(light));
            image.Freeze();
            return image;
        }

        // Dieselbe Marke als PNG in der gewuenschten Pixelgroesse.
        public static BitmapSource CreatePng(int size)
        {
            return CreatePng(size, Ink);
        }

        public static BitmapSource CreatePng(int size, Brush brush)
        {
            if (size <= 0) size = 256;

            Drawing drawing = CreateDrawing(brush);
            double scale = size / Grid;

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(scale, scale));
                dc.DrawDrawing(drawing);
                dc.Pop();
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(
                size, size, 96.0, 96.0, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
