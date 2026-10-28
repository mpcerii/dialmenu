using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace DialMenu
{
    // Ein Ringthema: alles, was das radiale Menue an Farbe und Material
    // braucht, an einer Stelle. Zwei Helligkeiten, damit dasselbe Thema
    // bei Tag und bei Nacht funktioniert.
    public class RingTheme
    {
        public string Id = "graphite";
        public string Name = "Graphit";
        public string Note = "";

        // Segmente
        public Color IdleTop = Color.FromRgb(0x26, 0x2B, 0x33);
        public Color IdleBottom = Color.FromRgb(0x0A, 0x0C, 0x10);
        public Color IdleStrokeTop = Color.FromRgb(0x4E, 0x55, 0x5F);
        public Color IdleStrokeBottom = Color.FromRgb(0x14, 0x17, 0x1C);

        public Color ActiveTop = Color.FromRgb(0x3E, 0x45, 0x50);
        public Color ActiveBottom = Color.FromRgb(0x1B, 0x1F, 0x26);
        public Color ActiveStroke = Color.FromRgb(0xFF, 0xFF, 0xFF);

        // Grundring hinter den Fugen
        public Color Ground = Color.FromRgb(0x04, 0x05, 0x07);

        // Mittelkreis
        public Color CenterOuter = Color.FromRgb(0x15, 0x18, 0x1D);
        public Color CenterInner = Color.FromRgb(0x05, 0x06, 0x0A);
        public Color CenterRimTop = Color.FromRgb(0x5A, 0x61, 0x6C);
        public Color CenterRimBottom = Color.FromRgb(0x16, 0x1A, 0x20);

        // Schrift und Symbole
        public Color IconIdle = Color.FromRgb(0x7C, 0x83, 0x8D);
        public Color IconActive = Color.FromRgb(0xFF, 0xFF, 0xFF);
        public Color Text = Color.FromRgb(0xF4, 0xF6, 0xF9);
        public Color TextMuted = Color.FromRgb(0x8E, 0x95, 0xA0);

        // Lichtkanten
        public Color RimOuter = Color.FromArgb(0x55, 0xC9, 0xD2, 0xE0);
        public Color RimInner = Color.FromArgb(0x3A, 0xC9, 0xD2, 0xE0);
        public Color Gloss = Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);
        public Color GlowColor = Color.FromRgb(0xFF, 0xFF, 0xFF);
        public double GlowOpacity = 0.75;

        public bool IsLight;
    }

    // Die verfuegbaren Ringthemen, jeweils in hell und dunkel.
    public static class RingThemes
    {
        public static List<RingTheme> All = Build();

        public static RingTheme Get(string id, bool light)
        {
            RingTheme baseTheme = Find(id);
            if (light) return Light(baseTheme);
            return baseTheme;
        }

        public static RingTheme Find(string id)
        {
            foreach (RingTheme t in All)
                if (string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) return t;
            return All[0];
        }

        public static List<string> Ids()
        {
            List<string> ids = new List<string>();
            foreach (RingTheme t in All) ids.Add(t.Id);
            return ids;
        }

        // ------------------------------------------------ dunkle Fassungen

        static List<RingTheme> Build()
        {
            List<RingTheme> list = new List<RingTheme>();

            RingTheme graphite = new RingTheme();
            graphite.Id = "graphite";
            graphite.Name = "Graphit";
            graphite.Note = "Neutrales Anthrazit, kühles Weiß";
            list.Add(graphite);

            RingTheme slate = new RingTheme();
            slate.Id = "slate";
            slate.Name = "Schiefer";
            slate.Note = "Kühler Blaugrau, ohne Farbe im Akzent";
            slate.IdleTop = Color.FromRgb(0x2A, 0x2E, 0x36);
            slate.IdleBottom = Color.FromRgb(0x0C, 0x0E, 0x12);
            slate.IdleStrokeTop = Color.FromRgb(0x54, 0x5B, 0x68);
            slate.ActiveTop = Color.FromRgb(0x42, 0x49, 0x55);
            slate.CenterOuter = Color.FromRgb(0x17, 0x1A, 0x21);
            slate.CenterRimTop = Color.FromRgb(0x62, 0x6A, 0x78);
            list.Add(slate);

            RingTheme ink = new RingTheme();
            ink.Id = "ink";
            ink.Name = "Tinte";
            ink.Note = "Fast reines Schwarz, hoher Kontrast";
            ink.IdleTop = Color.FromRgb(0x1A, 0x1A, 0x1C);
            ink.IdleBottom = Color.FromRgb(0x06, 0x06, 0x07);
            ink.IdleStrokeTop = Color.FromRgb(0x3C, 0x3C, 0x40);
            ink.ActiveTop = Color.FromRgb(0x2E, 0x2E, 0x32);
            ink.Ground = Color.FromRgb(0x00, 0x00, 0x00);
            ink.CenterOuter = Color.FromRgb(0x10, 0x10, 0x12);
            ink.CenterInner = Color.FromRgb(0x00, 0x00, 0x00);
            ink.CenterRimTop = Color.FromRgb(0x4A, 0x4A, 0x4E);
            ink.IconIdle = Color.FromRgb(0x76, 0x76, 0x7C);
            list.Add(ink);

            RingTheme warm = new RingTheme();
            warm.Id = "warm";
            warm.Name = "Warmgrau";
            warm.Note = "Gedämpftes Umbra statt Blaugrau";
            warm.IdleTop = Color.FromRgb(0x2C, 0x28, 0x24);
            warm.IdleBottom = Color.FromRgb(0x0D, 0x0C, 0x0A);
            warm.IdleStrokeTop = Color.FromRgb(0x59, 0x52, 0x4A);
            warm.IdleStrokeBottom = Color.FromRgb(0x17, 0x15, 0x12);
            warm.ActiveTop = Color.FromRgb(0x45, 0x3E, 0x36);
            warm.ActiveBottom = Color.FromRgb(0x1E, 0x1B, 0x17);
            warm.Ground = Color.FromRgb(0x06, 0x05, 0x04);
            warm.CenterOuter = Color.FromRgb(0x19, 0x16, 0x13);
            warm.CenterInner = Color.FromRgb(0x07, 0x06, 0x05);
            warm.CenterRimTop = Color.FromRgb(0x66, 0x5E, 0x54);
            warm.IconIdle = Color.FromRgb(0x88, 0x81, 0x78);
            warm.TextMuted = Color.FromRgb(0x98, 0x91, 0x88);
            warm.RimOuter = Color.FromArgb(0x50, 0xD8, 0xD0, 0xC6);
            warm.RimInner = Color.FromArgb(0x36, 0xD8, 0xD0, 0xC6);
            warm.GlowColor = Color.FromRgb(0xFF, 0xF6, 0xEA);
            list.Add(warm);

            return list;
        }

        // ------------------------------------------------ helle Fassungen

        // Helles Glas: dieselbe Form, umgekehrtes Licht. Statt heller
        // Reflexe auf dunklem Grund liegen hier weiche Schatten unter
        // hellem Material.
        static RingTheme Light(RingTheme dark)
        {
            RingTheme t = new RingTheme();
            t.Id = dark.Id;
            t.Name = dark.Name;
            t.Note = dark.Note;
            t.IsLight = true;

            bool warm = dark.Id == "warm";
            bool ink = dark.Id == "ink";

            t.Ground = warm ? Color.FromRgb(0xF3, 0xF0, 0xEC) : Color.FromRgb(0xF2, 0xF2, 0xF4);

            t.IdleTop = warm ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0xFF, 0xFF, 0xFF);
            t.IdleBottom = warm ? Color.FromRgb(0xEF, 0xEC, 0xE7) : Color.FromRgb(0xED, 0xED, 0xF0);
            t.IdleStrokeTop = warm ? Color.FromRgb(0xD6, 0xD0, 0xC8) : Color.FromRgb(0xD8, 0xD8, 0xDD);
            t.IdleStrokeBottom = warm ? Color.FromRgb(0xE8, 0xE4, 0xDE) : Color.FromRgb(0xE8, 0xE8, 0xEC);

            t.ActiveTop = warm ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0xFF, 0xFF, 0xFF);
            t.ActiveBottom = warm ? Color.FromRgb(0xF6, 0xF3, 0xEE) : Color.FromRgb(0xF7, 0xF7, 0xF9);
            t.ActiveStroke = ink ? Color.FromRgb(0x1A, 0x1A, 0x1C)
                : (warm ? Color.FromRgb(0x2A, 0x25, 0x20) : Color.FromRgb(0x1C, 0x1C, 0x20));

            t.CenterOuter = warm ? Color.FromRgb(0xF7, 0xF5, 0xF1) : Color.FromRgb(0xF7, 0xF7, 0xF9);
            t.CenterInner = warm ? Color.FromRgb(0xEA, 0xE6, 0xE0) : Color.FromRgb(0xE9, 0xE9, 0xED);
            t.CenterRimTop = warm ? Color.FromRgb(0xCF, 0xC8, 0xBE) : Color.FromRgb(0xD0, 0xD0, 0xD6);
            t.CenterRimBottom = warm ? Color.FromRgb(0xEE, 0xEA, 0xE4) : Color.FromRgb(0xEE, 0xEE, 0xF1);

            t.IconIdle = warm ? Color.FromRgb(0x8A, 0x82, 0x78) : Color.FromRgb(0x8C, 0x8C, 0x94);
            t.IconActive = warm ? Color.FromRgb(0x1F, 0x1B, 0x17) : Color.FromRgb(0x16, 0x16, 0x1A);
            t.Text = warm ? Color.FromRgb(0x1F, 0x1B, 0x17) : Color.FromRgb(0x16, 0x16, 0x1A);
            t.TextMuted = warm ? Color.FromRgb(0x8A, 0x82, 0x78) : Color.FromRgb(0x8C, 0x8C, 0x94);

            t.RimOuter = warm ? Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF);
            t.RimInner = warm ? Color.FromArgb(0x30, 0xBF, 0xB8, 0xAE) : Color.FromArgb(0x38, 0xC0, 0xC0, 0xC8);
            t.Gloss = warm ? Color.FromArgb(0x3B, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x42, 0xFF, 0xFF, 0xFF);

            // Der Schein ist im Hellen ein Schatten, kein Leuchten.
            t.GlowColor = warm ? Color.FromRgb(0x8A, 0x82, 0x78) : Color.FromRgb(0x90, 0x90, 0x98);
            t.GlowOpacity = 0.30;

            return t;
        }
    }
}
