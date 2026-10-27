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
    // Radiales Overlay im Stil geschliffenen Rauchglases. Die Bildsprache ist
    // unveraendert: Segmentglas, weisse Auswahl mit Glimmen, eingelassener
    // Mittelkreis, strenge Monochromie. Neu ist die Platzierung: viele
    // Randpositionen, mehrere Monitore und ein weiches Hineingleiten.
    public class OverlayWindow : Window
    {
        // --- Farbwelt: nur Schwarz, Anthrazit, Grau, Weiss ---
        static readonly Color IconDim = Color.FromRgb(0x7C, 0x83, 0x8D);
        static readonly Color IconOn = Color.FromRgb(0xFF, 0xFF, 0xFF);
        static readonly Color TextOn = Color.FromRgb(0xF4, 0xF6, 0xF9);
        static readonly Color TextOff = Color.FromRgb(0x8E, 0x95, 0xA0);

        static readonly Brush BaseRingFill = Freeze(new SolidColorBrush(Color.FromRgb(0x04, 0x05, 0x07)));
        static readonly Brush IdleFill = Freeze(new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString("#262B33"),
            (Color)ColorConverter.ConvertFromString("#0A0C10"), 90));
        static readonly Brush IdleStroke = Freeze(new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString("#4E555F"),
            (Color)ColorConverter.ConvertFromString("#14171C"), 90));
        static readonly Brush ActiveFill = Freeze(new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString("#3E4550"),
            (Color)ColorConverter.ConvertFromString("#1B1F26"), 90));
        static readonly Brush CenterFill = Freeze(new RadialGradientBrush(
            (Color)ColorConverter.ConvertFromString("#15181D"),
            (Color)ColorConverter.ConvertFromString("#05060A")));
        static readonly Brush CenterRim = Freeze(new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString("#5A616C"),
            (Color)ColorConverter.ConvertFromString("#161A20"), 90));
        static readonly Brush CenterSheen = Freeze(new LinearGradientBrush(
            Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 90));

        // Lichtreflex: heller oben, auslaufend nach unten.
        static readonly Brush GlossSheen = Freeze(new LinearGradientBrush(
            Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF),
            Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 90));

        static Brush Freeze(Brush b) { b.Freeze(); return b; }

        // Randabstand zur Arbeitsflaeche in Geraeteeinheiten.
        const double EdgeMargin = 20;
        // Hineingleiten beim Erscheinen.
        const double SlideDistance = 12;
        const double FadeMs = 130;

        readonly DispatcherTimer _hideTimer;

        Config _config;
        Canvas _canvas;
        Grid _center;
        Path _centerIcon;
        SolidColorBrush _centerIconStroke;
        TextBlock _centerName;
        TextBlock _counter;
        TranslateTransform _slide;

        readonly List<Path> _active = new List<Path>();
        readonly List<Path> _icons = new List<Path>();
        readonly List<SolidColorBrush> _iconBrushes = new List<SolidColorBrush>();

        int _index;
        bool _visible;
        bool _hiding;

        double _cx, _cy, _rIn, _rOut, _rMid, _centerR;
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
            SizeToContent = SizeToContent.Manual;
            Visibility = Visibility.Hidden;
            Left = -10000;
            Top = -10000;

            _slide = new TranslateTransform(0, 0);
            RenderTransform = _slide;

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

        // Segmentbreite im Grundraster, vor der Skalierung.
        const double HalfBreadth = 24;
        const double BaseRadiusIn = 74;

        void Measure()
        {
            double rIn = BaseRadiusIn;
            double rOut = rIn + HalfBreadth * 2;
            double scale = _config.Scale <= 0 ? 1.0 : _config.Scale;

            // Passt der Ring nicht in die Arbeitsflaeche, wird er verkleinert,
            // statt ueber den Rand zu laufen.
            if (_fitScale > 0 && _fitScale < 1.0) scale *= _fitScale;

            _rIn = rIn * scale;
            _rOut = rOut * scale;
            _rMid = (_rIn + _rOut) / 2;
            _centerR = (_rIn - 5) * 1.0;
            _iconSize = Math.Max(14, HalfBreadth * 0.82 * scale);

            // Der Aussenring traegt einen weichen Schatten mit 44er Weichzeichnung
            // und 10 Versatz, das Auswahlsegment ein weisses Glimmen ueber 34. Der
            // Rand muss beide fassen, damit nichts am Fensterrand abgeschnitten
            // wird - deshalb eine feste Untergrenze unabhaengig von der Skalierung.
            double pad = Math.Max(24 * scale, 34);

            double size = 2 * (_rOut + pad);

            _cx = size / 2;
            _cy = size / 2;

            _canvas.Width = size;
            _canvas.Height = size;
            Width = size;
            Height = size;
        }

        // ------------------------------------------------------------------
        // Zeichnen
        // ------------------------------------------------------------------

        void Rebuild()
        {
            Measure();

            _canvas.Children.Clear();
            _active.Clear();
            _icons.Clear();
            _iconBrushes.Clear();

            List<ActionItem> items = Items;
            int n = items.Count;

            // Nach dem Bearbeiten des Profils kann die Auswahl hinter das Ende
            // gerutscht sein. Sie wird auf den letzten Eintrag zurueckgeholt.
            if (n == 0) _index = 0;
            else if (_index >= n) _index = n - 1;
            else if (_index < 0) _index = 0;

            // Weicher, metallischer Grundring: gibt dem Ganzen Halt und sorgt fuer
            // die tiefschwarzen Fugen zwischen den Segmenten.
            Path baseRing = new Path();
            baseRing.Data = Donut(_cx, _cy, _rIn - 3, _rOut + 3);
            baseRing.Fill = BaseRingFill;
            baseRing.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 44,
                ShadowDepth = 10,
                Direction = 270,
                Opacity = 0.68
            };
            _canvas.Children.Add(baseRing);

            // Feine silberne Aussenkontur als Lichtreflex.
            Path rim = new Path();
            rim.Data = Ring(_cx, _cy, _rOut + 1.6);
            rim.Stroke = new SolidColorBrush(Color.FromArgb(0x55, 0xC9, 0xD2, 0xE0));
            rim.StrokeThickness = 0.9;
            _canvas.Children.Add(rim);

            Path rimInner = new Path();
            rimInner.Data = Ring(_cx, _cy, _rIn - 1.4);
            rimInner.Stroke = new SolidColorBrush(Color.FromArgb(0x3A, 0xC9, 0xD2, 0xE0));
            rimInner.StrokeThickness = 0.8;
            _canvas.Children.Add(rimInner);

            if (n == 0)
            {
                BuildCenter(0);
                return;
            }

            double step = 360.0 / n;
            double gapDeg = Math.Min(step * 0.26, (3.0 / (_rMid * Math.PI / 180.0)));

            for (int i = 0; i < n; i++)
            {
                double a0 = -90 + i * step + gapDeg / 2;
                double a1 = -90 + (i + 1) * step - gapDeg / 2;

                Geometry geo = n == 1
                    ? Donut(_cx, _cy, _rIn, _rOut)
                    : RoundedSector(_cx, _cy, _rIn, _rOut, a0, a1, 4.2);

                // Glasflaeche
                Path idle = new Path();
                idle.Data = geo;
                idle.Fill = IdleFill;
                idle.Stroke = IdleStroke;
                idle.StrokeThickness = 1;
                _canvas.Children.Add(idle);

                // Aufliegende Lichtreflexion im oberen Bereich
                Path gloss = new Path();
                gloss.Data = geo;
                gloss.Fill = GlossSheen;
                gloss.IsHitTestVisible = false;
                _canvas.Children.Add(gloss);

                // Auswahlzustand liegt als eigene Ebene darueber und blendet ein.
                Path act = new Path();
                act.Data = geo;
                act.Fill = ActiveFill;
                act.Stroke = Brushes.White;
                act.StrokeThickness = 1.5;
                act.Opacity = 0;
                act.IsHitTestVisible = false;
                act.Effect = new DropShadowEffect
                {
                    Color = Colors.White,
                    BlurRadius = 34,
                    ShadowDepth = 0,
                    Opacity = 0.75
                };
                _canvas.Children.Add(act);
                _active.Add(act);

                AddIcon(items[i], -90 + (i + 0.5) * step);
            }

            BuildCenter(_index);
        }

        void AddIcon(ActionItem item, double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            double px = _cx + _rMid * Math.Cos(rad);
            double py = _cy + _rMid * Math.Sin(rad);

            SolidColorBrush brush = new SolidColorBrush(IconDim);
            Path icon = MakeIcon(IconLibrary.For(item), brush, 1.8);
            _iconBrushes.Add(brush);

            Viewbox vb = new Viewbox();
            vb.Width = _iconSize;
            vb.Height = _iconSize;
            vb.Child = icon;

            Canvas.SetLeft(vb, px - _iconSize / 2);
            Canvas.SetTop(vb, py - _iconSize / 2);
            _canvas.Children.Add(vb);

            _icons.Add(icon);
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
            return p;
        }

        void BuildCenter(int index)
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

            Grid host = new Grid();
            host.Width = d;
            host.Height = d;

            Ellipse body = new Ellipse();
            body.Fill = CenterFill;
            host.Children.Add(body);

            // Glasiges Oberflaechenlicht
            Ellipse sheen = new Ellipse();
            sheen.Fill = CenterSheen;
            host.Children.Add(sheen);

            Ellipse rim = new Ellipse();
            rim.Stroke = CenterRim;
            rim.StrokeThickness = 1.2;
            host.Children.Add(rim);

            Ellipse inner = new Ellipse();
            inner.Margin = new Thickness(6);
            inner.Stroke = new SolidColorBrush(Color.FromArgb(0x2E, 0x00, 0x00, 0x00));
            inner.StrokeThickness = 3;
            host.Children.Add(inner);

            StackPanel content = new StackPanel();
            content.VerticalAlignment = VerticalAlignment.Center;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.Margin = new Thickness(14, 0, 14, 0);

            _centerIconStroke = new SolidColorBrush(IconOn);
            // Ohne Auswahl zeigt die Mitte das neutrale Zahnrad, genau wie zuvor.
            _centerIcon = MakeIcon(IconLibrary.Resolve("gear"), _centerIconStroke, 1.55);

            Viewbox iconBox = new Viewbox();
            iconBox.Width = Math.Max(22, _centerR * 0.52);
            iconBox.Height = Math.Max(22, _centerR * 0.52);
            iconBox.Child = _centerIcon;
            iconBox.HorizontalAlignment = HorizontalAlignment.Center;
            iconBox.Margin = new Thickness(0, 0, 0, 2);
            content.Children.Add(iconBox);

            _centerName = new TextBlock();
            _centerName.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
            _centerName.FontSize = Math.Max(12, _centerR * 0.215);
            _centerName.FontWeight = FontWeights.SemiBold;
            _centerName.Foreground = new SolidColorBrush(TextOn);
            _centerName.TextAlignment = TextAlignment.Center;
            _centerName.TextWrapping = TextWrapping.Wrap;
            _centerName.MaxWidth = _centerR * 1.62;
            _centerName.LineHeight = _centerName.FontSize * 1.25;
            _centerName.Margin = new Thickness(0, _centerR * 0.11, 0, 0);
            content.Children.Add(_centerName);

            _counter = new TextBlock();
            _counter.FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            _counter.FontSize = Math.Max(9, _centerR * 0.135);
            _counter.Foreground = new SolidColorBrush(TextOff);
            _counter.TextAlignment = TextAlignment.Center;
            _counter.Margin = new Thickness(0, _centerR * 0.09, 0, 0);
            content.Children.Add(_counter);

            host.Children.Add(content);

            Canvas.SetLeft(host, _cx - d / 2);
            Canvas.SetTop(host, _cy - d / 2);
            _canvas.Children.Add(host);
            _center = host;

            UpdateSelection(false);
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

        // Kreisausschnitt mit abgerundeten Ecken.
        static Geometry RoundedSector(double cx, double cy, double rIn, double rOut,
                                      double a0, double a1, double cr)
        {
            double sweep = a1 - a0;
            double maxRadial = (rOut - rIn) / 2.0;
            double maxArc = (rOut * Math.Abs(sweep) * Math.PI / 180.0) / 2.0;
            cr = Math.Max(0, Math.Min(cr, Math.Min(maxRadial, maxArc) * 0.92));

            Point A = Polar(cx, cy, rOut, a0);
            Point B = Polar(cx, cy, rOut, a1);
            Point C = Polar(cx, cy, rIn, a1);
            Point D = Polar(cx, cy, rIn, a0);

            Vector t0 = Tangent(a0), t1 = Tangent(a1);
            Vector rad0 = Radial(a0), rad1 = Radial(a1);

            Point A1 = A - rad0 * cr;   // Ende der Linie von D
            Point A2 = A + t0 * cr;     // Start des Aussenbogens
            Point B1 = B - t1 * cr;     // Ende des Aussenbogens
            Point B2 = B - rad1 * cr;   // Start der Linie nach C
            Point C1 = C + rad1 * cr;   // Ende der Linie von B
            Point C2 = C - t1 * cr;     // Start des Innenbogens
            Point D1 = D + t0 * cr;     // Ende des Innenbogens
            Point D2 = D + rad0 * cr;   // Start der Linie nach A

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

        void UpdateSelection(bool animate)
        {
            List<ActionItem> items = Items;

            for (int i = 0; i < _active.Count; i++)
            {
                bool sel = i == _index;
                double target = sel ? 1.0 : 0.0;

                if (animate)
                {
                    _active[i].BeginAnimation(OpacityProperty,
                        new DoubleAnimation(target, TimeSpan.FromMilliseconds(150)));

                    if (i < _iconBrushes.Count)
                        _iconBrushes[i].BeginAnimation(SolidColorBrush.ColorProperty,
                            new ColorAnimation(sel ? IconOn : IconDim, TimeSpan.FromMilliseconds(150)));
                }
                else
                {
                    _active[i].BeginAnimation(OpacityProperty, null);
                    _active[i].Opacity = target;

                    if (i < _iconBrushes.Count)
                    {
                        _iconBrushes[i].BeginAnimation(SolidColorBrush.ColorProperty, null);
                        _iconBrushes[i].Color = sel ? IconOn : IconDim;
                    }
                }
            }

            if (_centerName == null) return;

            if (_index >= 0 && _index < items.Count)
            {
                ActionItem item = items[_index];
                _centerName.Text = item.Label;
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

            // Immer vollstaendig neu zeichnen: Groesse und Position stecken in
            // jeder Koordinate, ein blosses Umrechnen wuerde den Kreis versetzen.
            ResolvePlacement();
            Rebuild();

            double dx, dy;
            PositionWindow(out dx, out dy);

            UpdateSelection(false);
            AnimateIn(dx, dy);
        }

        // Fade und kleiner Versatz aus der naechsten Kante heraus. Liegt das
        // Overlay mittig, entfaellt der Versatz.
        void AnimateIn(double dx, double dy)
        {
            _slide.BeginAnimation(TranslateTransform.XProperty, null);
            _slide.BeginAnimation(TranslateTransform.YProperty, null);

            if (dx == 0 && dy == 0)
            {
                _slide.X = 0;
                _slide.Y = 0;
            }
            else
            {
                _slide.X = dx;
                _slide.Y = dy;

                CubicEase ease = new CubicEase();
                ease.EasingMode = EasingMode.EaseOut;
                Duration d = new Duration(TimeSpan.FromMilliseconds(FadeMs));

                DoubleAnimation sx = new DoubleAnimation(0, d);
                sx.EasingFunction = ease;
                DoubleAnimation sy = new DoubleAnimation(0, d);
                sy.EasingFunction = ease;

                _slide.BeginAnimation(TranslateTransform.XProperty, sx);
                _slide.BeginAnimation(TranslateTransform.YProperty, sy);
            }

            DoubleAnimation fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(FadeMs)));
            fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, fade);
        }

        public void HideOverlay()
        {
            _hideTimer.Stop();
            if (!_visible) return;

            _visible = false;
            _hiding = true;

            DoubleAnimation fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(FadeMs));
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
            double naturalPad = Math.Max(24 * scaleValue, 34);
            double natural = 2 * ((BaseRadiusIn + HalfBreadth * 2) * scaleValue + naturalPad);

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

        // Setzt Fensterlage und die Richtung, aus der eingeglitten wird.
        void PositionWindow()
        {
            double dx, dy;
            PositionWindow(out dx, out dy);
        }

        void PositionWindow(out double slideDx, out double slideDy)
        {
            slideDx = 0;
            slideDy = 0;

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

            // Einblendrichtung aus der tatsaechlichen Endlage: nur wenn das
            // Fenster am Rand sitzt, gleitet es von dort herein. Mittig bleibt es
            // ruhig und blendet lediglich auf.
            const double atEdge = 0.5;

            if (left <= _area.Left + EdgeMargin + atEdge) slideDx = -SlideDistance;
            else if (left >= _area.Right - w - EdgeMargin - atEdge) slideDx = SlideDistance;

            if (top <= _area.Top + EdgeMargin + atEdge) slideDy = -SlideDistance;
            else if (top >= _area.Bottom - h - EdgeMargin - atEdge) slideDy = SlideDistance;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            int style = UnsafeNativeMethods.GetWindowLong(handle, UnsafeNativeMethods.GWL_EXSTYLE);
            style |= UnsafeNativeMethods.WS_EX_NOACTIVATE | UnsafeNativeMethods.WS_EX_TOOLWINDOW;
            UnsafeNativeMethods.SetWindowLong(handle, UnsafeNativeMethods.GWL_EXSTYLE, style);
        }

        static class UnsafeNativeMethods
        {
            public const int GWL_EXSTYLE = -20;
            public const int WS_EX_NOACTIVATE = 0x08000000;
            public const int WS_EX_TOOLWINDOW = 0x00000080;

            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
            public static extern int GetWindowLong(IntPtr hWnd, int index);
            [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
            public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        }
    }
}
