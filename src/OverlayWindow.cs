using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DialMenu
{
    // Radiales Overlay als ruhiges Instrument: Glasflaechen auf einem
    // geschliffenen Grundring, eine einzige Lichtkante, eingelassene Mitte.
    //
    // Saemtliche Farbe kommt aus RingThemes - hell und dunkel sind zwei
    // Fassungen desselben Materials. Bewusst weggelassen sind Glanzfolien,
    // Leuchtsaeume und doppelte Zierlinien; die Auswahl liest sich ueber
    // eine duenne Kontur und eine Spur hellere Flaeche.
    public class OverlayWindow : Window
    {
        // --- Grundraster, vor der Skalierung ---------------------------
        const double BaseRadiusIn = 74;
        const double BaseBreadth = 48;
        const double BaseCorner = 4.0;
        const double BaseGap = 3.4;

        // Randabstand zur Arbeitsflaeche in Geraeteeinheiten.
        const double EdgeMargin = 20;

        // Bewegung: nur Deckkraft, ruhig und kurz.
        const double FadeMs = 130;
        const double SelectionMs = 130;

        // Eine Strichstaerke fuer Symbole, eine fuer Segmentkanten.
        const double IconStroke = 1.7;
        const double SegmentStroke = 1.0;
        static readonly FontFamily DisplayFont =
            new FontFamily("Segoe UI Variable Display, Segoe UI");
        static readonly FontFamily TextFont =
            new FontFamily("Segoe UI Variable Text, Segoe UI");

        // --- Zustand ---------------------------------------------------
        readonly DispatcherTimer _hideTimer;

        Config _config;
        Canvas _canvas;
        Grid _center;
        Path _centerIcon;
        SolidColorBrush _centerIconBrush;
        TextBlock _centerName;
        TextBlock _counter;

        readonly List<Path> _active = new List<Path>();
        readonly List<SolidColorBrush> _iconBrushes = new List<SolidColorBrush>();
        readonly List<SolidColorBrush> _labelBrushes = new List<SolidColorBrush>();

        // --- Material, bei jedem Aufbau aus dem Thema erneuert ---------
        RingTheme _theme;
        Brush _fillIdle;
        Brush _strokeIdle;
        Brush _fillActive;
        Brush _strokeActive;
        Brush _ground;
        Brush _rim;
        Brush _centerFill;
        Brush _centerRim;
        Color _iconIdle;
        Color _labelIdle;
        Color _iconActive;
        Brush _text;
        Brush _textMuted;
        Color _shadowColor;
        double _shadowOpacity;

        int _index;
        bool _visible;
        bool _hiding;
        bool _light;

        double _cx, _cy, _rIn, _rOut, _rMid, _centerR;
        double _scale = 1.0;
        double _iconSize = 19;

        public event Action<ActionItem> Executed;

        public OverlayWindow(Config config)
        {
            if (config == null) config = new Config();
            _config = config;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;
            SizeToContent = SizeToContent.Manual;
            Visibility = Visibility.Hidden;
            Left = -10000;
            Top = -10000;

            // Kanten glatt zeichnen, Text in der Anzeige-Aufloesung setzen.
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);

            BuildShell();

            _hideTimer = new DispatcherTimer();
            _hideTimer.Tick += delegate { HideOverlay(); };
        }

        public void ApplyConfig(Config config)
        {
            if (config == null) return;
            _config = config;

            // Groesse und Anzahl stecken in jeder gezeichneten Koordinate. Ein
            // blosses Umrechnen des Fensters wuerde den Kreis versetzen und
            // anschneiden. Deshalb wird die Geometrie hier vollstaendig verworfen
            // und beim naechsten Anzeigen neu aufgebaut.
            if (_visible)
            {
                ResolvePlacement();
                Rebuild();
                PositionWindow();
                UpdateSelection(false);
            }
        }

        // ------------------------------------------------------------------
        // Grundgeruest
        // ------------------------------------------------------------------

        void BuildShell()
        {
            _canvas = new Canvas();
            Content = _canvas;
        }

        List<ActionItem> Items
        {
            get
            {
                List<ActionItem> list = _config.CurrentActions;
                return list != null ? list : new List<ActionItem>();
            }
        }

        // ------------------------------------------------------------------
        // Masse
        // ------------------------------------------------------------------

        void Measure()
        {
            double scale = _config.Scale <= 0 ? 1.0 : _config.Scale;

            // Passt der Ring nicht in die Arbeitsflaeche, wird er verkleinert,
            // statt ueber den Rand zu laufen.
            if (_fitScale > 0 && _fitScale < 1.0) scale *= _fitScale;
            _scale = scale;

            _rIn = BaseRadiusIn * scale;
            _rOut = _rIn + BaseBreadth * scale;
            _rMid = (_rIn + _rOut) / 2;

            // Die Mitte liegt mit gleichmaessigem Rand innerhalb des Ringes.
            double inset = Math.Max(6.0, 9.0 * scale);
            _centerR = _rIn - inset;

            _iconSize = Math.Max(14.0, BaseBreadth * 0.50 * scale);

            // Der weiche Schatten unter dem Grundring braucht Platz, damit
            // nichts am Fensterrand abgeschnitten wird.
            double pad = ShadowPadding(scale);

            // Gerade Kantenzahl: der Mittelpunkt faellt dann auf ein ganzes
            // Pixel und die aeussere Lichtkante kann exakt auf Pixelgrenzen
            // gelegt werden.
            double size = Math.Round((_rOut + pad) / 2.0) * 4.0;

            _cx = size / 2;
            _cy = size / 2;

            _canvas.Width = size;
            _canvas.Height = size;
            Width = size;
            Height = size;
        }

        static double ShadowPadding(double scale)
        {
            return Math.Max(22.0, 26.0 * scale);
        }

        // ------------------------------------------------------------------
        // Material
        // ------------------------------------------------------------------

        static bool ResolveLight(string appearance)
        {
            string a = appearance == null ? "" : appearance.Trim().ToLowerInvariant();
            if (a == "light" || a == "hell") return true;
            if (a == "dark" || a == "dunkel") return false;

            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", false))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme", 0);
                    if (value is int) return (int)value != 0;
                }
            }
            catch { }

            return false;
        }

        // Baut alle Pinsel neu. Wird bei jedem Aufbau aufgerufen, damit ein
        // Wechsel von Thema oder Helligkeit sofort greift.
        void BuildMaterial()
        {
            _light = ResolveLight(_config.Appearance);
            _theme = RingThemes.Get(_config.RingTheme, _light);

            _fillIdle = Vertical(_theme.IdleTop, _theme.IdleBottom);
            _strokeIdle = Vertical(_theme.IdleStrokeTop, _theme.IdleStrokeBottom);
            _fillActive = Vertical(_theme.ActiveTop, _theme.ActiveBottom);
            _strokeActive = Solid(_theme.ActiveStroke);
            _ground = Solid(_theme.Ground);
            _rim = Solid(_theme.RimOuter);

            RadialGradientBrush center = new RadialGradientBrush();
            center.GradientStops.Add(new GradientStop(_theme.CenterInner, 0.0));
            center.GradientStops.Add(new GradientStop(_theme.CenterOuter, 1.0));
            center.Freeze();
            _centerFill = center;

            _centerRim = Vertical(_theme.CenterRimTop, _theme.CenterRimBottom);

            // Beschriftungen liegen eine Stufe ueber der gedaempften Schrift:
            // bei 9 Pixeln braucht die Kontur mehr Deckkraft als ein Hinweis.
            _labelIdle = Mix(_theme.TextMuted, _theme.Text, 0.45);

            _iconIdle = _theme.IconIdle;
            _iconActive = _theme.IconActive;
            _text = Solid(_theme.Text);
            _textMuted = Solid(_theme.TextMuted);

            // Im Dunkeln ein weicher Schatten aus dem Grundton, im Hellen
            // der im Thema hinterlegte weiche Schatten. In beiden Faellen
            // kommt die Farbe aus dem Thema, nicht aus einer Konstanten.
            _shadowColor = _light ? _theme.GlowColor : _theme.Ground;
            _shadowOpacity = _light ? _theme.GlowOpacity : 0.62;
        }

        // Lineare Mischung zweier Themenfarben; a endet bei t=0, b bei t=1.
        static Color Mix(Color a, Color b, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * t),
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        static Brush Solid(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        static Brush Vertical(Color top, Color bottom)
        {
            LinearGradientBrush b = new LinearGradientBrush(top, bottom, 90);
            b.Freeze();
            return b;
        }

        // ------------------------------------------------------------------
        // Zeichnen
        // ------------------------------------------------------------------

        void Rebuild()
        {
            Measure();
            BuildMaterial();

            _canvas.Children.Clear();
            _active.Clear();
            _iconBrushes.Clear();
            _labelBrushes.Clear();
            _center = null;
            _centerName = null;
            _counter = null;
            _centerIcon = null;

            List<ActionItem> items = Items;
            int n = items.Count;

            // Nach dem Bearbeiten des Profils kann die Auswahl hinter das Ende
            // gerutscht sein. Sie wird auf den letzten Eintrag zurueckgeholt.
            if (n == 0) _index = 0;
            else if (_index >= n) _index = n - 1;
            else if (_index < 0) _index = 0;

            BuildGround();

            if (n == 0)
            {
                BuildCenter();
                UpdateSelection(false);
                return;
            }

            double step = 360.0 / n;
            double gapPx = Math.Max(2.0, BaseGap * _scale);
            double gapDeg = Math.Min(step * 0.30,
                Math.Asin(Math.Min(0.9, gapPx / (2.0 * _rOut))) * 2.0 * 180.0 / Math.PI);

            for (int i = 0; i < n; i++)
            {
                double a0 = -90 + i * step + gapDeg / 2;
                double a1 = -90 + (i + 1) * step - gapDeg / 2;

                Geometry geo = n == 1
                    ? Donut(_cx, _cy, _rIn, _rOut)
                    : RoundedSector(_cx, _cy, _rIn, _rOut, a0, a1, BaseCorner * _scale);

                Path idle = new Path();
                idle.Data = geo;
                idle.Fill = _fillIdle;
                idle.Stroke = _strokeIdle;
                idle.StrokeThickness = SegmentStroke;
                idle.StrokeLineJoin = PenLineJoin.Round;
                idle.IsHitTestVisible = false;
                _canvas.Children.Add(idle);

                // Auswahl liegt als eigene Ebene darueber und blendet ein.
                Path act = new Path();
                act.Data = geo;
                act.Fill = _fillActive;
                act.Stroke = _strokeActive;
                act.StrokeThickness = SegmentStroke;
                act.StrokeLineJoin = PenLineJoin.Round;
                act.Opacity = 0;
                act.IsHitTestVisible = false;
                _canvas.Children.Add(act);
                _active.Add(act);

                AddSegmentContent(items[i], -90 + (i + 0.5) * step, step);
            }

            BuildCenter();
            UpdateSelection(false);
        }

        void BuildGround()
        {
            double edge = Math.Max(1.5, 2.0 * _scale);

            // Der Grund reicht bis unter die Mitte. Damit ist der schmale
            // Kanal zwischen Mittelkreis und Segmenten gefuellt und nicht
            // durchsichtig.
            double inner = Math.Max(0, _centerR - 2 * _scale);

            Path ground = new Path();
            ground.Data = Donut(_cx, _cy, inner, _rOut + edge);
            ground.Fill = _ground;
            ground.IsHitTestVisible = false;
            ground.Effect = new DropShadowEffect
            {
                Color = _shadowColor,
                BlurRadius = Math.Max(18.0, 30.0 * _scale),
                ShadowDepth = Math.Max(3.0, 6.0 * _scale),
                Direction = 270,
                Opacity = _shadowOpacity
            };
            _canvas.Children.Add(ground);

            // Eine einzige, saubere Lichtkante aussen. Auf ganze Pixel
            // gelegt, damit sie auch bei 100 Prozent scharf bleibt.
            // Radius auf n + 0.5: bei 1 Pixel Strichstaerke liegen dann beide
            // Kanten des Kreises genau auf Pixelgrenzen.
            double rimRadius = Math.Floor(_rOut + edge) + 0.5;

            Path rim = new Path();
            rim.Data = Ring(_cx, _cy, rimRadius);
            rim.Stroke = _rim;
            rim.StrokeThickness = 1.0;
            rim.IsHitTestVisible = false;
            _canvas.Children.Add(rim);
        }
        // Symbol und ggf. Beschriftung eines Segments. Mit Beschriftung liegt
        // beides in einem Container, der exakt auf die Segmentflaeche
        // beschnitten ist - ein Label kann also nie ueber seinen Ausschnitt
        // laufen. Ohne Beschriftung bleibt es beim blanken Symbol.
        void AddSegmentContent(ActionItem item, double angleDeg, double stepDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double breadth = _rOut - _rIn;
            bool labels = _config.ShowLabels;

            double iconSize = labels ? breadth * 0.36 : _iconSize;
            iconSize = Math.Max(12.0, iconSize);

            double iconR = labels ? _rMid - breadth * 0.20 : _rMid;
            double px = _cx + iconR * Math.Cos(rad);
            double py = _cy + iconR * Math.Sin(rad);

            SolidColorBrush brush = new SolidColorBrush(_iconIdle);
            Path icon = MakeIcon(IconLibrary.For(item), brush, IconStroke);
            _iconBrushes.Add(brush);

            // Feste 24er-Bezugsflaeche um das Symbol: dadurch skalieren ein
            // rundes und ein breites Zeichen gleich, statt sich der Pfadbreite
            // anzupassen. Das haelt das optische Gewicht konstant.
            Viewbox iconBox = new Viewbox();
            iconBox.Width = iconSize;
            iconBox.Height = iconSize;
            iconBox.Stretch = Stretch.Uniform;
            iconBox.Child = WrapOnGrid(icon, 24, 24);
            iconBox.IsHitTestVisible = false;

            if (!labels)
            {
                Canvas.SetLeft(iconBox, px - iconSize / 2);
                Canvas.SetTop(iconBox, py - iconSize / 2);
                _canvas.Children.Add(iconBox);
                return;
            }

            // Mit Beschriftung: Symbol ueber Text, beides mittig auf der
            // Kreisbahn.
            //
            // Bei mehr als sechs Segmenten ist der Bogen an der Innenkante
            // schmaler als ein deutsches Wort: "Einstellungen" braucht rund
            // 78 px, das Segment bietet bei acht Eintraegen aber nur etwa
            // 58. Der Text wuerde also zwangslaeufig abgeschnitten. Deshalb
            // zeigt nur ein Ring mit wenigen Segmenten Beschriftungen -
            // dort, wo sie wirklich hineinpassen.
            int segments = Math.Max(1, (int)Math.Round(360.0 / stepDeg));
            if (segments > 6)
            {
                // Zu eng: nur Symbol, aber etwas groesser als ohne Labelwunsch.
                double wide = Math.Max(_iconSize, breadth * 0.46);
                iconBox.Width = wide;
                iconBox.Height = wide;
                Canvas.SetLeft(iconBox, px - wide / 2);
                Canvas.SetTop(iconBox, py - wide / 2);
                _canvas.Children.Add(iconBox);
                return;
            }

            // Bis sechs Segmente ist der Bogen breit genug. Die nutzbare
            // Breite ist an der Innenkante am groessten.
            double arcInner = 2 * Math.PI * _rIn / segments;
            double groupWidth = Math.Min(arcInner * 0.92, breadth * 1.10);

            double labelSize = Math.Max(9.5, breadth * 0.20);
            double smallIcon = Math.Min(breadth * 0.36, groupWidth * 0.44);
            iconBox.Width = smallIcon;
            iconBox.Height = smallIcon;
            iconBox.HorizontalAlignment = HorizontalAlignment.Center;

            double lx = _cx + _rMid * Math.Cos(rad);
            double ly = _cy + _rMid * Math.Sin(rad);

            Grid host = new Grid();
            host.Width = _canvas.Width;
            host.Height = _canvas.Height;
            host.IsHitTestVisible = false;

            // Bei nur einem Eintrag umschliesst das Segment den ganzen Ring;
            // dann ist der Beschnitt schlicht der Ring selbst.
            if (stepDeg >= 359.0)
            {
                host.Clip = Donut(_cx, _cy, _rIn + 1, _rOut - 1);
            }
            else
            {
                double halfStep = stepDeg / 2;
                host.Clip = RoundedSector(_cx, _cy, _rIn + 1, _rOut - 1,
                    angleDeg - halfStep, angleDeg + halfStep, BaseCorner * _scale);
            }

            StackPanel stack = new StackPanel();
            stack.HorizontalAlignment = HorizontalAlignment.Center;
            stack.VerticalAlignment = VerticalAlignment.Center;
            stack.Width = groupWidth;
            stack.RenderTransform = new TranslateTransform(lx - _cx, ly - _cy);
            stack.Children.Add(iconBox);

            TextBlock label = new TextBlock();
            label.FontFamily = TextFont;
            label.FontSize = labelSize;
            label.FontWeight = FontWeights.Medium;
            label.TextAlignment = TextAlignment.Center;
            label.TextWrapping = TextWrapping.NoWrap;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.Foreground = new SolidColorBrush(_labelIdle);
            label.Margin = new Thickness(0, Math.Max(1.0, labelSize * 0.22), 0, 0);
            label.Text = item == null || item.Label == null ? "" : item.Label;
            label.IsHitTestVisible = false;
            _labelBrushes.Add((SolidColorBrush)label.Foreground);
            stack.Children.Add(label);

            host.Children.Add(stack);
            _canvas.Children.Add(host);
        }

        // Legt ein Zeichen auf ein festes Raster, damit der Viewbox immer
        // dieselbe Bezugsflaeche sieht.
        static Canvas WrapOnGrid(UIElement child, double w, double h)
        {
            Canvas c = new Canvas();
            c.Width = w;
            c.Height = h;
            c.Children.Add(child);
            return c;
        }

        static Path MakeIcon(string data, Brush stroke, double thickness)
        {
            Path p = new Path();
            p.Data = Geometry.Parse(data);
            p.Stroke = stroke;
            p.StrokeThickness = thickness;
            p.StrokeLineJoin = PenLineJoin.Round;
            p.StrokeStartLineCap = PenLineCap.Round;
            p.StrokeEndLineCap = PenLineCap.Round;
            p.Fill = Brushes.Transparent;
            p.IsHitTestVisible = false;
            return p;
        }

        // ------------------------------------------------------------------
        // Mitte
        // ------------------------------------------------------------------

        void BuildCenter()
        {
            if (_center != null)
            {
                _canvas.Children.Remove(_center);
                _center = null;
                _centerName = null;
                _counter = null;
                _centerIcon = null;
            }

            double d = _centerR * 2;
            double rim = 1.0;

            Grid host = new Grid();
            host.Width = d;
            host.Height = d;
            host.IsHitTestVisible = false;

            // Eingelassene Flaeche: ein Radialverlauf, eine Kante. Kein
            // zusaetzlicher Schein, keine zweite Zierlinie.
            Ellipse body = new Ellipse();
            body.Fill = _centerFill;
            host.Children.Add(body);

            Ellipse edge = new Ellipse();
            edge.Stroke = _centerRim;
            edge.StrokeThickness = rim;
            edge.Margin = new Thickness(rim / 2);
            host.Children.Add(edge);

            StackPanel content = new StackPanel();
            content.VerticalAlignment = VerticalAlignment.Center;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.Margin = new Thickness(_centerR * 0.22, 0, _centerR * 0.22, 0);

            _centerIconBrush = new SolidColorBrush(_theme.IconActive);
            _centerIcon = MakeIcon(IconLibrary.Resolve("gear"), _centerIconBrush, IconStroke);

            double iconWidth = Math.Max(20.0, _centerR * 0.86);
            Viewbox iconBox = new Viewbox();
            iconBox.Width = Math.Min(iconWidth, _centerR * 0.52);
            iconBox.Height = iconBox.Width;
            iconBox.Stretch = Stretch.Uniform;
            iconBox.Child = WrapOnGrid(_centerIcon, 24, 24);
            iconBox.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(iconBox);

            _centerName = new TextBlock();
            _centerName.FontFamily = DisplayFont;
            _centerName.FontSize = Math.Max(11.0, _centerR * 0.205);
            _centerName.FontWeight = FontWeights.SemiBold;
            _centerName.Foreground = _text;
            _centerName.TextAlignment = TextAlignment.Center;
            _centerName.TextWrapping = TextWrapping.Wrap;
            _centerName.TextTrimming = TextTrimming.CharacterEllipsis;
            _centerName.MaxWidth = _centerR * 1.62;
            _centerName.LineHeight = _centerName.FontSize * 1.28;
            _centerName.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            _centerName.MaxHeight = _centerName.LineHeight * 2.02;
            _centerName.Margin = new Thickness(0, _centerR * 0.09, 0, 0);
            content.Children.Add(_centerName);

            _counter = new TextBlock();
            _counter.FontFamily = TextFont;
            _counter.FontSize = Math.Max(8.5, _centerR * 0.125);
            _counter.Foreground = _textMuted;
            _counter.TextAlignment = TextAlignment.Center;
            _counter.Margin = new Thickness(0, _centerR * 0.07, 0, 0);
            content.Children.Add(_counter);

            host.Children.Add(content);

            Canvas.SetLeft(host, _cx - d / 2);
            Canvas.SetTop(host, _cy - d / 2);
            _canvas.Children.Add(host);
            _center = host;
        }

        // ------------------------------------------------------------------
        // Geometrie
        // ------------------------------------------------------------------

        static Point Polar(double cx, double cy, double r, double deg)
        {
            double a = deg * Math.PI / 180.0;
            return new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }

        static Geometry Ring(double cx, double cy, double r)
        {
            return new EllipseGeometry(new Point(cx, cy), r, r);
        }

        static Vector Radial(double deg)
        {
            double a = deg * Math.PI / 180.0;
            return new Vector(Math.Cos(a), Math.Sin(a));
        }

        static Vector Tangent(double deg)
        {
            double a = deg * Math.PI / 180.0;
            return new Vector(-Math.Sin(a), Math.Cos(a));
        }

        static Geometry Donut(double cx, double cy, double rIn, double rOut)
        {
            GeometryGroup g = new GeometryGroup();
            g.FillRule = FillRule.EvenOdd;
            g.Children.Add(new EllipseGeometry(new Point(cx, cy), rOut, rOut));
            g.Children.Add(new EllipseGeometry(new Point(cx, cy), rIn, rIn));
            return g;
        }

        // Kreisausschnitt mit abgerundeten Ecken, gleicher Radius an allen vier.
        static Geometry RoundedSector(double cx, double cy, double rIn, double rOut,
                                      double a0, double a1, double cr)
        {
            double sweep = a1 - a0;
            double maxRadial = (rOut - rIn) / 2.0;
            double maxArc = (rIn * Math.Abs(sweep) * Math.PI / 180.0) / 2.0;
            cr = Math.Max(0, Math.Min(cr, Math.Min(maxRadial, maxArc) * 0.92));

            Point A = Polar(cx, cy, rOut, a0);
            Point B = Polar(cx, cy, rOut, a1);
            Point C = Polar(cx, cy, rIn, a1);
            Point D = Polar(cx, cy, rIn, a0);

            Vector t0 = Tangent(a0), t1 = Tangent(a1);
            Vector rad0 = Radial(a0), rad1 = Radial(a1);

            Point A1 = A - rad0 * cr;
            Point A2 = A + t0 * cr;
            Point B1 = B - t1 * cr;
            Point B2 = B - rad1 * cr;
            Point C1 = C + rad1 * cr;
            Point C2 = C - t1 * cr;
            Point D1 = D + t0 * cr;
            Point D2 = D + rad0 * cr;

            bool large = Math.Abs(sweep) > 180;

            PathFigure f = new PathFigure();
            f.StartPoint = A2;
            f.IsClosed = true;
            f.Segments.Add(new ArcSegment(B1, new Size(rOut, rOut), 0, large, SweepDirection.Clockwise, true));
            f.Segments.Add(new QuadraticBezierSegment(B, B2, true));
            f.Segments.Add(new LineSegment(C1, true));
            f.Segments.Add(new QuadraticBezierSegment(C, C2, true));
            f.Segments.Add(new ArcSegment(D1, new Size(rIn, rIn), 0, large, SweepDirection.Counterclockwise, true));
            f.Segments.Add(new QuadraticBezierSegment(D, D2, true));
            f.Segments.Add(new LineSegment(A1, true));
            f.Segments.Add(new QuadraticBezierSegment(A, A2, true));

            PathGeometry g = new PathGeometry();
            g.Figures.Add(f);
            g.Freeze();
            return g;
        }
        // ------------------------------------------------------------------
        // Auswahl
        // ------------------------------------------------------------------

        // Kurzes Ueberblenden, kein Federn, keine Groessenaenderung.
        void UpdateSelection(bool animate)
        {
            List<ActionItem> items = Items;
            Color labelOn = _theme == null ? Colors.White : _theme.Text;
            Color labelOff = _theme == null ? Colors.Gray : _labelIdle;

            for (int i = 0; i < _active.Count; i++)
            {
                bool sel = i == _index;
                double target = sel ? 1.0 : 0.0;
                Duration d = new Duration(TimeSpan.FromMilliseconds(SelectionMs));

                if (animate)
                {
                    DoubleAnimation fade = new DoubleAnimation(target, d);
                    fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                    _active[i].BeginAnimation(OpacityProperty, fade);

                    if (i < _iconBrushes.Count)
                    {
                        ColorAnimation c = new ColorAnimation(sel ? _iconActive : _iconIdle, d);
                        _iconBrushes[i].BeginAnimation(SolidColorBrush.ColorProperty, c);
                    }
                    if (i < _labelBrushes.Count)
                    {
                        ColorAnimation c = new ColorAnimation(sel ? labelOn : labelOff, d);
                        _labelBrushes[i].BeginAnimation(SolidColorBrush.ColorProperty, c);
                    }
                }
                else
                {
                    _active[i].BeginAnimation(OpacityProperty, null);
                    _active[i].Opacity = target;

                    if (i < _iconBrushes.Count)
                    {
                        _iconBrushes[i].BeginAnimation(SolidColorBrush.ColorProperty, null);
                        _iconBrushes[i].Color = sel ? _iconActive : _iconIdle;
                    }
                    if (i < _labelBrushes.Count)
                    {
                        _labelBrushes[i].BeginAnimation(SolidColorBrush.ColorProperty, null);
                        _labelBrushes[i].Color = sel ? labelOn : labelOff;
                    }
                }
            }

            if (_centerName == null) return;

            if (_index >= 0 && _index < items.Count)
            {
                ActionItem item = items[_index];
                _centerName.Text = item.Label == null ? "" : item.Label;
                if (_centerIcon != null) _centerIcon.Data = Geometry.Parse(IconLibrary.For(item));
                _counter.Text = string.Format(CultureInfo.InvariantCulture,
                    "{0:00} / {1:00}", _index + 1, items.Count);
            }
            else if (items.Count == 0)
            {
                _centerName.Text = "Keine Befehle";
                _counter.Text = "";
            }
            else
            {
                _centerName.Text = "Drehen";
                _counter.Text = "01 / " + items.Count.ToString("00", CultureInfo.InvariantCulture);
            }
        }

        // ------------------------------------------------------------------
        // Einblenden
        // ------------------------------------------------------------------

        public void ShowOverlay()
        {
            if (_config.Enabled == false) return;
            if (_visible && !_hiding) return;

            // Erst sichtbar schalten (noch unsichtbar und ausserhalb), damit
            // Fenster-Handle und DPI feststehen, bevor Groesse und Lage berechnet
            // werden. Sonst waere die DPI beim ersten Anzeigen unzuverlaessig.
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            _visible = true;
            _hiding = false;
            Visibility = Visibility.Visible;

            // Immer vollstaendig neu zeichnen: Groesse, Farben und Position
            // stecken in jeder Koordinate, ein blosses Umrechnen wuerde den
            // Kreis versetzen.
            ResolvePlacement();
            Rebuild();
            PositionWindow();

            DoubleAnimation fade = new DoubleAnimation(0, 1,
                new Duration(TimeSpan.FromMilliseconds(FadeMs)));
            fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, fade);
        }

        public void HideOverlay()
        {
            _hideTimer.Stop();
            if (!_visible) return;

            _visible = false;
            _hiding = true;

            DoubleAnimation fade = new DoubleAnimation(Opacity, 0,
                TimeSpan.FromMilliseconds(FadeMs));
            fade.Completed += delegate
            {
                if (_visible) return;
                _hiding = false;
                Visibility = Visibility.Hidden;
                BeginAnimation(OpacityProperty, null);
                Opacity = 0;

                // Fenster aus dem Sichtbereich nehmen, damit auch der Schatten
                // nichts mehr ueber der Taskleiste zeichnet.
                Left = -10000;
                Top = -10000;
            };
            BeginAnimation(OpacityProperty, fade);
        }

        public void Nudge()
        {
            ShowOverlay();
            ScheduleHide();
        }

        public bool MenuVisible { get { return _visible; } }

        public void OpenMenu()
        {
            ShowOverlay();
            ScheduleHide();
        }

        void ScheduleHide()
        {
            _hideTimer.Stop();
            _hideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(200, _config.HideDelayMs));
            _hideTimer.Start();
        }

        // ------------------------------------------------------------------
        // Bedienung
        // ------------------------------------------------------------------

        public void Move(int direction)
        {
            List<ActionItem> items = Items;
            if (items.Count == 0) return;

            if (!_visible)
            {
                // Der erste Rastschritt weckt nur das Menue, er waehlt nicht.
                ShowOverlay();
                ScheduleHide();
                return;
            }

            _index += direction;
            if (_index < 0) _index = items.Count - 1;
            if (_index >= items.Count) _index = 0;

            UpdateSelection(true);
            ScheduleHide();
        }

        public void Commit()
        {
            List<ActionItem> items = Items;
            if (items.Count == 0) return;

            if (!_visible)
            {
                ShowOverlay();
                ScheduleHide();
                return;
            }

            if (_index < 0 || _index >= items.Count) return;
            FireSelected();
        }

        public void DismissSelection()
        {
            HideOverlay();
        }

        // Fuehrt sofort aus, ohne jede Verzoegerung.
        void FireSelected()
        {
            List<ActionItem> items = Items;
            if (_index < 0 || _index >= items.Count) { HideOverlay(); return; }

            ActionItem item = items[_index];
            HideOverlay();
            Log.Write("Ausgefuehrt: " + (item.Label == null ? "" : item.Label));

            if (Executed != null) Executed(item);
            Commands.Execute(item);
        }
        // ------------------------------------------------------------------
        // Platzierung
        // ------------------------------------------------------------------
        //
        // Positionszeichenketten (Kleinschreibung, Leerzeichen frei):
        //
        //   Ecken          "unten rechts"  "unten links"  "oben rechts"  "oben links"
        //   Seitenbaender  "rechts oben"   "rechts mitte" "rechts unten"
        //                  "links oben"    "links mitte"  "links unten"
        //   Kantenmitte    "oben mitte"    "unten mitte"
        //   Bildschirm     "mitte"
        //   Zeiger         "zeiger"  = Monitor unter dem Mauszeiger, unten rechts
        //
        // Ein Monitor kann mit "|" angehaengt werden:
        //   "unten rechts|0"     Monitor 0 aus Screen.AllScreens (Index)
        //   "oben links|1"       Monitor 1
        //   "unten links|zeiger" Monitor unter dem Mauszeiger
        // Ohne Angabe wird der Primaerbildschirm benutzt.

        Rect _area;          // Arbeitsflaeche des Zielmonitors in Geraeteeinheiten
        double _fitScale = 1.0;

        // Loest Position und Monitor auf, bevor gezeichnet wird: die Groesse
        // haengt davon ab, wie viel Platz der Monitor bietet.
        void ResolvePlacement()
        {
            System.Windows.Forms.Screen screen = PickScreen();
            if (screen == null)
            {
                _area = SystemParameters.WorkArea;
                _fitScale = 1.0;
                return;
            }

            System.Drawing.Rectangle px = screen.WorkingArea;

            double sx, sy;
            GetScale(out sx, out sy);

            _area = new Rect(px.Left / sx, px.Top / sy, px.Width / sx, px.Height / sy);

            // Kreis samt Schatten muss vollstaendig in die Arbeitsflaeche passen.
            double usable = Math.Min(_area.Width, _area.Height) - 2 * EdgeMargin;
            double scaleValue = _config.Scale <= 0 ? 1.0 : _config.Scale;
            double natural = 2 * ((BaseRadiusIn + BaseBreadth) * scaleValue + ShadowPadding(scaleValue));

            if (usable > 40 && natural > usable)
                _fitScale = usable / natural;
            else
                _fitScale = 1.0;
        }

        System.Windows.Forms.Screen PickScreen()
        {
            string name;
            int monitor;
            ParsePosition(out name, out monitor);

            System.Windows.Forms.Screen[] all = System.Windows.Forms.Screen.AllScreens;

            if (monitor == MonitorCursor)
                return CursorScreen();

            if (monitor >= 0 && all != null && all.Length > 0)
            {
                if (monitor >= all.Length) monitor = all.Length - 1;
                return all[monitor];
            }

            System.Windows.Forms.Screen primary = System.Windows.Forms.Screen.PrimaryScreen;
            return primary != null ? primary : (all != null && all.Length > 0 ? all[0] : null);
        }

        const int MonitorCursor = -2;
        const int MonitorNone = -1;

        System.Windows.Forms.Screen CursorScreen()
        {
            try
            {
                System.Drawing.Point p = System.Windows.Forms.Cursor.Position;
                System.Windows.Forms.Screen s = System.Windows.Forms.Screen.FromPoint(p);
                if (s != null) return s;
            }
            catch { }

            System.Windows.Forms.Screen primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null) return primary;
            System.Windows.Forms.Screen[] all = System.Windows.Forms.Screen.AllScreens;
            return all != null && all.Length > 0 ? all[0] : null;
        }

        // Zerlegt "unten rechts|1" in Grundposition und Monitorwahl.
        void ParsePosition(out string name, out int monitor)
        {
            string raw = _config.Position == null ? "" : _config.Position.Trim();
            monitor = MonitorNone;
            name = raw;

            int bar = raw.LastIndexOf('|');
            if (bar >= 0)
            {
                name = raw.Substring(0, bar).Trim();
                string token = Normalize(raw.Substring(bar + 1));

                int idx;
                if (token == "zeiger" || token == "cursor" || token == "maus" || token == "mouse")
                {
                    monitor = MonitorCursor;
                }
                else if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out idx))
                {
                    if (idx < 0) idx = 0;
                    monitor = idx;
                }
            }

            string bare = Normalize(name);

            if (bare == "zeiger" || bare == "cursor" || bare == "maus" || bare == "mouse")
            {
                // "zeiger" allein waehlt den Monitor unter dem Mauszeiger und
                // benutzt die gewohnte Ecke.
                if (monitor == MonitorNone) monitor = MonitorCursor;
                name = "unten rechts";
            }
            else if (bare.Length == 0)
            {
                name = "unten rechts";
            }
        }

        static string Normalize(string s)
        {
            if (s == null) return "";
            s = s.Trim().ToLowerInvariant();
            s = s.Replace("_", " ").Replace("-", " ").Replace(".", " ");
            while (s.IndexOf("  ", StringComparison.Ordinal) >= 0)
                s = s.Replace("  ", " ");
            return s.Trim();
        }

        // Anteilige Verankerung im Arbeitsbereich: 0 = links/oben, 1 = rechts/unten.
        static void Anchor(string name, out double fx, out double fy)
        {
            fx = 1.0;
            fy = 1.0;

            string s = Normalize(name);
            if (s.Length == 0) return;

            bool left = false, right = false, top = false, bottom = false;
            bool middle = false;

            string[] words = s.Split(' ');
            foreach (string w in words)
            {
                switch (w)
                {
                    case "links": case "left": left = true; break;
                    case "rechts": case "right": right = true; break;
                    case "oben": case "top": top = true; break;
                    case "unten": case "bottom": bottom = true; break;
                    case "mitte": case "mittig": case "zentriert":
                    case "center": case "centre": case "middle":
                        middle = true; break;
                }
            }

            if (left) fx = 0.0;
            else if (right) fx = 1.0;
            else if (middle) fx = 0.5;

            if (top) fy = 0.0;
            else if (bottom) fy = 1.0;
            else if (middle) fy = 0.5;

            // "mitte" allein zentriert in beide Richtungen.
            if (middle && !left && !right && !top && !bottom)
            {
                fx = 0.5;
                fy = 0.5;
            }
        }

        // DPI-Skala: massgeblich ist die DPI des Fensters. Bei einer nicht
        // per-Monitor-DPI-faehigen WPF-Anwendung skaliert Windows das Fenster
        // mit der System-DPI, deshalb waere die Monitor-DPI hier falsch.
        void GetScale(out double sx, out double sy)
        {
            try
            {
                DpiScale d = VisualTreeHelper.GetDpi(this);
                if (d.DpiScaleX > 0 && d.DpiScaleY > 0)
                {
                    sx = d.DpiScaleX;
                    sy = d.DpiScaleY;
                    return;
                }
            }
            catch { }

            double dpiX, dpiY;
            if (TryMonitorDpi(out dpiX, out dpiY))
            {
                sx = dpiX / 96.0;
                sy = dpiY / 96.0;
                if (sx > 0 && sy > 0) return;
            }

            sx = 1.0;
            sy = 1.0;
        }

        bool TryMonitorDpi(out double dpiX, out double dpiY)
        {
            dpiX = 0;
            dpiY = 0;
            try
            {
                System.Drawing.Point p = System.Windows.Forms.Cursor.Position;
                NativePoint pt;
                pt.X = p.X;
                pt.Y = p.Y;

                // Fuer die Groesse ist der Zielmonitor massgeblich, nicht der Zeiger.
                System.Windows.Forms.Screen target = PickScreen();
                if (target != null)
                {
                    System.Drawing.Rectangle b = target.Bounds;
                    pt.X = b.Left + b.Width / 2;
                    pt.Y = b.Top + b.Height / 2;
                }

                IntPtr hmon = MonitorFromPoint(pt, 1 /* MONITOR_DEFAULTTOPRIMARY */);
                if (hmon == IntPtr.Zero) return false;

                uint dx, dy;
                if (GetDpiForMonitor(hmon, 0 /* MDT_EFFECTIVE_DPI */, out dx, out dy) != 0) return false;

                dpiX = dx;
                dpiY = dy;
                return dpiX > 0 && dpiY > 0;
            }
            catch { return false; }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct NativePoint
        {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr MonitorFromPoint(NativePoint pt, uint flags);

        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr hmon, int dpiType, out uint dpiX, out uint dpiY);

        // Setzt die Fensterlage. Der Kreis steht still; eingeblendet wird
        // ausschliesslich ueber die Deckkraft.
        void PositionWindow()
        {
            if (_area.IsEmpty) ResolvePlacement();
            if (_area.IsEmpty) return;

            string name;
            int monitor;
            ParsePosition(out name, out monitor);

            double w = Width;
            double h = Height;

            // Passt das Fenster nicht in den Randabstand, bleibt es zentriert.
            double spanX = _area.Width - w - 2 * EdgeMargin;
            double spanY = _area.Height - h - 2 * EdgeMargin;

            double fx, fy;
            Anchor(name, out fx, out fy);

            double left = spanX >= 0
                ? _area.Left + EdgeMargin + fx * spanX
                : _area.Left + (_area.Width - w) / 2;
            double top = spanY >= 0
                ? _area.Top + EdgeMargin + fy * spanY
                : _area.Top + (_area.Height - h) / 2;

            // Nachrechnen und klemmen: der Kreis darf unter keinen Umstaenden
            // ueber den Rand der Arbeitsflaeche hinauslaufen.
            double minLeft = _area.Left;
            double maxLeft = Math.Max(_area.Left, _area.Right - w);
            double minTop = _area.Top;
            double maxTop = Math.Max(_area.Top, _area.Bottom - h);

            if (left < minLeft) left = minLeft;
            if (left > maxLeft) left = maxLeft;
            if (top < minTop) top = minTop;
            if (top > maxTop) top = maxTop;

            // Bei sehr kleinem Monitor das Fenster mittig halten statt klemmen.
            if (w > _area.Width) left = _area.Left + (_area.Width - w) / 2;
            if (h > _area.Height) top = _area.Top + (_area.Height - h) / 2;

            Left = left;
            Top = top;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            int style = UnsafeNativeMethods.GetWindowLong(handle, UnsafeNativeMethods.GWL_EXSTYLE);
            style |= UnsafeNativeMethods.WS_EX_NOACTIVATE
                   | UnsafeNativeMethods.WS_EX_TOOLWINDOW
                   | UnsafeNativeMethods.WS_EX_TRANSPARENT;
            UnsafeNativeMethods.SetWindowLong(handle, UnsafeNativeMethods.GWL_EXSTYLE, style);
        }

        static class UnsafeNativeMethods
        {
            public const int GWL_EXSTYLE = -20;
            public const int WS_EX_NOACTIVATE = 0x08000000;
            public const int WS_EX_TOOLWINDOW = 0x00000080;
            public const int WS_EX_TRANSPARENT = 0x00000020;

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
            public static extern int GetWindowLong(IntPtr hWnd, int index);
            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
            public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        }
    }
}
