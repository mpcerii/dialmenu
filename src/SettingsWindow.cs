using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Path = System.Windows.Shapes.Path;
using Pen = System.Windows.Media.Pen;

namespace DialMenu
{
    // Die Einstellungen von DialMenu.
    //
    // Ruhige, helle Oberflaeche: eine schmale Leiste links, rechts jeweils
    // eine Seite. Jede Aenderung geht sofort in die Konfiguration und wird
    // an Eingabe und Overlay weitergereicht - es gibt keinen Speichern-Knopf.
    public class SettingsWindow : Window
    {
        // --- eigene, feine Symbole fuer die Leiste --------------------

        const string GlyphList =
            "M4,7 H20 M4,12 H20 M4,17 H14";

        const string GlyphProfile =
            "M12,4.4 A3.7,3.7 0 1 1 12,11.8 A3.7,3.7 0 1 1 12,4.4 " +
            "M4.8,20.2 C4.8,16.5 8,14.2 12,14.2 C16,14.2 19.2,16.5 19.2,20.2";

        const string GlyphDial =
            "M3.6,12 A8.4,8.4 0 1 1 20.4,12 A8.4,8.4 0 1 1 3.6,12 " +
            "M12,7.8 V12 L15.2,14.1";

        const string GlyphInfo =
            "M3.6,12 A8.4,8.4 0 1 1 20.4,12 A8.4,8.4 0 1 1 3.6,12 " +
            "M12,11 V16.6 M12,7.7 V7.9";

        const string GlyphFallback =
            "M4.8,6.4 H19.2 A1.4,1.4 0 0 1 20.6,7.8 V16.2 A1.4,1.4 0 0 1 19.2,17.6 H4.8 " +
            "A1.4,1.4 0 0 1 3.4,16.2 V7.8 A1.4,1.4 0 0 1 4.8,6.4 Z";

        const string GlyphCheck =
            "M3.2,8.2 L6.3,11.3 L12.8,4.2";

        const string GlyphChevron =
            "M6,9 L12,15 L18,9";

        // --- Daten ----------------------------------------------------

        class NavEntry
        {
            public string Key;
            public string Glyph;
            public Border Row;
            public TextBlock Label;
            public ContentControl IconHost;
        }

        class DeviceRow
        {
            public string Name;
            public string Vid;
            public string Pid;
            public string Path;
            public string Match;
            public bool Friendly;
        }

        readonly Config _config;
        readonly DialMenuApp _app;
        readonly Grid _pageHost = new Grid();
        readonly Dictionary<string, NavEntry> _nav = new Dictionary<string, NavEntry>();
        readonly DispatcherTimer _debounce;

        string _section = "befehle";
        bool _loading;

        // Befehle
        Profile _profile;
        ActionItem _selected;
        ListBox _actionList;
        TextBlock _actionCount;
        StackPanel _editorHost;
        TextBox _fLabel;
        TextBox _fTarget;
        TextBox _fArgs;
        TextBox _fHint;
        SelectControl _cKind;
        TextBlock _targetHint;
        StackPanel _argsBlock;
        StackPanel _browseBlock;
        StackPanel _macroBlock;
        TextBlock _macroSummary;
        ContentControl _iconPreview;
        TextBlock _iconName;
        StackPanel _validationHost;
        bool _editorLoading;
        int _kindIndex = -1;

        // Geraete
        List<DeviceRow> _discovered;
        string _discoverError;
        bool _discovering;
        TextBlock _deviceStatus;
        StackPanel _attachedRows;
        StackPanel _configuredRows;

        // Darstellung
        Slider _scaleSlider;
        TextBlock _scaleValue;

        // Verhalten
        TextBlock _autostartState;

        // ==================================================================
        // Aufbau
        // ==================================================================

        public SettingsWindow(Config config, DialMenuApp app)
        {
            _config = config;
            _app = app;

            Title = "DialMenu – Einstellungen";
            Width = 1120;
            Height = 760;
            MinWidth = 960;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Bg;
            FontFamily = UiFont.Family;
            FontSize = 13;
            Foreground = Ui.Ink;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            _debounce = new DispatcherTimer();
            _debounce.Interval = TimeSpan.FromMilliseconds(160);
            _debounce.Tick += delegate
            {
                _debounce.Stop();
                Push();
            };

            BuildUi();
            ShowSection("befehle");
        }

        void BuildUi()
        {
            Grid root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(212) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(BuildSidebar());

            _pageHost.Background = Ui.Bg;
            Grid.SetColumn(_pageHost, 1);
            root.Children.Add(_pageHost);

            Content = root;
        }

        FrameworkElement BuildSidebar()
        {
            Border side = new Border();
            side.Background = new SolidColorBrush(Color.FromRgb(0xFB, 0xFB, 0xFC));
            side.BorderBrush = Ui.Line;
            side.BorderThickness = new Thickness(0, 0, 1, 0);

            DockPanel panel = new DockPanel();
            panel.LastChildFill = true;

            // Wortmarke: das Zeichen aus acht Ringsegmenten, wie das Menue selbst.
            StackPanel brand = new StackPanel();
            brand.Orientation = Orientation.Horizontal;
            brand.Margin = new Thickness(18, 22, 18, 22);

            // Die Marke als Bild in fester Groesse. Ein ContentControl allein
            // nimmt die natuergroesse des Zeichens (24 Einheiten) und wird
            // dadurch beschnitten.
            System.Windows.Controls.Image brandIcon = new System.Windows.Controls.Image();
            brandIcon.Source = BrandMark.Create();
            brandIcon.Width = 24;
            brandIcon.Height = 24;
            brandIcon.Stretch = Stretch.Uniform;
            brandIcon.SnapsToDevicePixels = true;
            brandIcon.VerticalAlignment = VerticalAlignment.Center;
            brand.Children.Add(brandIcon);

            StackPanel wordBlock = new StackPanel();
            wordBlock.VerticalAlignment = VerticalAlignment.Center;
            wordBlock.Margin = new Thickness(10, 0, 0, 0);

            TextBlock wordmark = new TextBlock();
            wordmark.Text = "DialMenu";
            wordmark.FontSize = 15;
            wordmark.FontWeight = FontWeights.SemiBold;
            wordmark.Foreground = Ui.Ink;
            wordmark.LineHeight = 17;
            wordBlock.Children.Add(wordmark);

            brand.Children.Add(wordBlock);

            DockPanel.SetDock(brand, Dock.Top);
            panel.Children.Add(brand);

            // Fuss
            StackPanel foot = new StackPanel();
            foot.Margin = new Thickness(0, 0, 0, 16);

            Button preview = Ui.Button("Im Overlay ansehen");
            preview.HorizontalAlignment = HorizontalAlignment.Stretch;
            preview.HorizontalContentAlignment = HorizontalAlignment.Center;
            preview.Margin = new Thickness(12, 0, 12, 10);
            preview.Click += delegate { _app.PreviewOverlay(); };
            foot.Children.Add(preview);

            TextBlock note = new TextBlock();
            note.Text = "Schließen legt das Fenster in den Infobereich.";
            note.FontSize = 10.5;
            note.Foreground = Ui.InkMuted;
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(14, 0, 14, 0);
            foot.Children.Add(note);

            DockPanel.SetDock(foot, Dock.Bottom);
            panel.Children.Add(foot);

            // Navigation
            StackPanel nav = new StackPanel();
            nav.Margin = new Thickness(10, 0, 10, 0);
            nav.VerticalAlignment = VerticalAlignment.Top;

            AddNav(nav, "befehle", "Befehle", GlyphList);
            AddNav(nav, "profile", "Profile", GlyphProfile);
            AddNav(nav, "geraete", "Geräte", GlyphDial);
            AddNav(nav, "darstellung", "Darstellung", IconLibrary.Desktop);
            AddNav(nav, "verhalten", "Verhalten", IconLibrary.Gear);
            AddNav(nav, "ueber", "Über", GlyphInfo);

            panel.Children.Add(nav);
            side.Child = panel;
            return side;
        }

        void AddNav(StackPanel host, string key, string label, string glyph)
        {
            NavEntry entry = new NavEntry();
            entry.Key = key;
            entry.Glyph = glyph;

            Border row = new Border();
            row.CornerRadius = new CornerRadius(5);
            row.Padding = new Thickness(9, 7, 9, 7);
            row.Margin = new Thickness(0, 1, 0, 1);
            row.Background = Brushes.Transparent;
            row.Cursor = Cursors.Hand;
            entry.Row = row;

            StackPanel line = new StackPanel();
            line.Orientation = Orientation.Horizontal;

            ContentControl iconHost = new ContentControl();
            iconHost.Width = 16;
            iconHost.Height = 16;
            iconHost.VerticalAlignment = VerticalAlignment.Center;
            iconHost.Content = Ui.Glyph(glyph, 16, Ui.InkMuted);
            entry.IconHost = iconHost;
            line.Children.Add(iconHost);

            TextBlock text = new TextBlock();
            text.Text = label;
            text.FontSize = 12.5;
            text.Foreground = Ui.InkSoft;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(9, 0, 0, 0);
            entry.Label = text;
            line.Children.Add(text);

            row.Child = line;
            row.MouseEnter += delegate
            {
                if (_section != key) row.Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF2, 0xF5));
            };
            row.MouseLeave += delegate
            {
                if (_section != key) row.Background = Brushes.Transparent;
            };
            row.MouseLeftButtonUp += delegate { ShowSection(key); };

            _nav[key] = entry;
            host.Children.Add(row);
        }

        // ==================================================================
        // Seitenwechsel
        // ==================================================================

        // Die Seitenleiste und die tray-eigene "Über"-Verknuepfung benutzen
        // deutsche Beschriftungen; hier auf die internen Schluessel abbilden.
        static string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "befehle";
            switch (key.Trim().ToLowerInvariant())
            {
                case "befehle":
                case "commands": return "befehle";
                case "profile": return "profile";
                case "geräte":
                case "geraete":
                case "devices": return "geraete";
                case "darstellung":
                case "appearance": return "darstellung";
                case "verhalten":
                case "behavior": return "verhalten";
                case "über":
                case "ueber":
                case "about": return "ueber";
            }
            return key;
        }

        public void ShowSection(string key)
        {
            key = NormalizeKey(key);
            if (string.IsNullOrEmpty(key) || !_nav.ContainsKey(key))
            {
                if (_nav.ContainsKey("befehle")) key = "befehle";
                else return;
            }

            _section = key;

            foreach (NavEntry entry in _nav.Values)
            {
                bool active = entry.Key == key;
                entry.Row.Background = active ? Ui.AccentSoft : Brushes.Transparent;
                entry.Label.Foreground = active ? Ui.Accent : Ui.InkSoft;
                entry.Label.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                entry.IconHost.Content = Ui.Glyph(entry.Glyph, 16, active ? Ui.Accent : Ui.InkMuted);
            }

            FrameworkElement page = null;
            try
            {
                switch (key)
                {
                    case "profile": page = BuildProfilesPage(); break;
                    case "geraete": page = BuildDevicesPage(); break;
                    case "darstellung": page = BuildAppearancePage(); break;
                    case "verhalten": page = BuildBehaviorPage(); break;
                    case "ueber": page = BuildAboutPage(); break;
                    default: page = BuildCommandsPage(); break;
                }
            }
            catch (Exception ex)
            {
                // Ohne diesen Schutz bleibt der Bereich leer und weiss - der
                // Anwender sieht nicht, dass etwas schiefging.
                Log.Write("Seite \"" + key + "\" konnte nicht aufgebaut werden: " + ex);
                page = BuildErrorPage(key, ex);
            }

            _pageHost.Children.Clear();
            if (page != null) _pageHost.Children.Add(page);
        }

        FrameworkElement BuildErrorPage(string key, Exception ex)
        {
            StackPanel body = PageBody();
            body.Children.Add(PageHeader("Diese Seite ließ sich nicht aufbauen", null, null));

            Border card = CardBox();
            StackPanel inner = new StackPanel();
            inner.Margin = new Thickness(18, 16, 18, 18);

            TextBlock head = Ui.Text("Fehler beim Aufbau von „" + key + "“", 13, Ui.Ink);
            head.FontWeight = FontWeights.SemiBold;
            inner.Children.Add(head);

            TextBlock message = Ui.Text(ex.Message, 12, Ui.InkSoft);
            message.TextWrapping = TextWrapping.Wrap;
            message.Margin = new Thickness(0, 6, 0, 0);
            inner.Children.Add(message);

            TextBlock hint = Ui.Text(
                "Der Fehler steht im Protokoll. Über „Über“ lässt es sich öffnen.",
                11.5, Ui.InkMuted);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 10, 0, 0);
            inner.Children.Add(hint);

            card.Child = inner;
            body.Children.Add(card);
            return body;
        }

        // ==================================================================
        // Gemeinsame Bausteine
        // ==================================================================

        FrameworkElement PageHeader(string title, string subtitle, FrameworkElement right)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titles = new StackPanel();
            TextBlock heading = Ui.Heading(title, 19);
            titles.Children.Add(heading);

            if (!string.IsNullOrEmpty(subtitle))
            {
                TextBlock sub = Ui.Text(subtitle, 12.5, Ui.InkMuted);
                sub.Margin = new Thickness(0, 3, 0, 0);
                sub.TextWrapping = TextWrapping.Wrap;
                titles.Children.Add(sub);
            }
            grid.Children.Add(titles);

            if (right != null)
            {
                right.VerticalAlignment = VerticalAlignment.Center;
                right.Margin = new Thickness(16, 0, 0, 0);
                Grid.SetColumn(right, 1);
                grid.Children.Add(right);
            }

            return grid;
        }

        Border CardBox()
        {
            Border b = Ui.CardBorder();
            b.Padding = new Thickness(18, 16, 18, 18);
            b.Margin = new Thickness(0, 0, 0, 12);
            return b;
        }

        FrameworkElement Card(string title, string subtitle, FrameworkElement body)
        {
            Border box = CardBox();

            StackPanel stack = new StackPanel();
            if (!string.IsNullOrEmpty(title))
            {
                TextBlock heading = Ui.Heading(title, 13);
                stack.Children.Add(heading);
            }
            if (!string.IsNullOrEmpty(subtitle))
            {
                TextBlock sub = Ui.Text(subtitle, 11.5, Ui.InkMuted);
                sub.Margin = new Thickness(0, 2, 0, 0);
                sub.TextWrapping = TextWrapping.Wrap;
                stack.Children.Add(sub);
            }
            if (body != null)
            {
                body.Margin = new Thickness(0, 14, 0, 0);
                stack.Children.Add(body);
            }

            box.Child = stack;
            return box;
        }

        FrameworkElement ScrollPage(FrameworkElement content)
        {
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Padding = new Thickness(0);
            sv.Content = content;
            return sv;
        }

        StackPanel PageBody()
        {
            StackPanel body = new StackPanel();
            body.Margin = new Thickness(30, 26, 30, 30);
            body.MaxWidth = 860;
            body.HorizontalAlignment = HorizontalAlignment.Left;
            return body;
        }

        static TextBlock SectionLabel(string text)
        {
            TextBlock t = Ui.Text(text, 11.5, Ui.InkSoft);
            t.Margin = new Thickness(0, 0, 0, 6);
            return t;
        }

        static CheckBox Check(string text, bool value, RoutedEventHandler changed)
        {
            CheckBox box = new CheckBox();
            box.Content = text;
            box.IsChecked = value;
            box.FontSize = 12.5;
            box.FontFamily = UiFont.Family;
            box.Foreground = Ui.Ink;
            box.Cursor = Cursors.Hand;
            box.Template = CheckTemplate();
            box.FocusVisualStyle = null;
            if (changed != null) box.Click += changed;
            return box;
        }

        static ControlTemplate CheckTemplate()
        {
            ControlTemplate template = new ControlTemplate(typeof(CheckBox));

            FrameworkElementFactory line = new FrameworkElementFactory(typeof(StackPanel));
            line.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            FrameworkElementFactory box = new FrameworkElementFactory(typeof(Border));
            box.Name = "Box";
            box.SetValue(FrameworkElement.WidthProperty, 16.0);
            box.SetValue(FrameworkElement.HeightProperty, 16.0);
            box.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            box.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            box.SetValue(Border.BorderBrushProperty, Ui.FieldLine);
            box.SetValue(Border.BackgroundProperty, Brushes.White);
            box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            FrameworkElementFactory check = new FrameworkElementFactory(typeof(Path));
            check.Name = "Check";
            check.SetValue(Path.DataProperty, Geometry.Parse(GlyphCheck));
            check.SetValue(Shape.StrokeProperty, Brushes.White);
            check.SetValue(Shape.StrokeThicknessProperty, 1.9);
            check.SetValue(Shape.StrokeStartLineCapProperty, PenLineCap.Round);
            check.SetValue(Shape.StrokeEndLineCapProperty, PenLineCap.Round);
            check.SetValue(Shape.StrokeLineJoinProperty, PenLineJoin.Round);
            check.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            check.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 1, 0, 0));
            box.AppendChild(check);

            line.AppendChild(box);

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 0, 0));
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            line.AppendChild(content);

            template.VisualTree = line;

            Trigger on = new Trigger();
            on.Property = ToggleButton.IsCheckedProperty;
            on.Value = true;
            on.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Accent, "Box"));
            on.Setters.Add(new Setter(Border.BorderBrushProperty, Ui.Accent, "Box"));
            on.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "Check"));
            template.Triggers.Add(on);

            return template;
        }

        static Slider Slider(double min, double max, double value, double step)
        {
            Slider s = new Slider();
            s.Minimum = min;
            s.Maximum = max;
            s.Value = value;
            s.SmallChange = step;
            s.LargeChange = step * 5;
            s.TickFrequency = step;
            s.IsSnapToTickEnabled = true;
            s.IsMoveToPointEnabled = true;
            s.Height = 22;
            s.Template = SliderTemplate();
            s.FocusVisualStyle = null;
            return s;
        }

        static ControlTemplate SliderTemplate()
        {
            // Track-Kinder lassen sich nicht ueber FrameworkElementFactory
            // verdrahten (Track erbt nicht von IAddChild und hat keine
            // Dependency Properties fuer Thumb/RepeatButton). Deshalb ein
            // XAML-Template, das WPF selbst korrekt verbindet.
            string xaml =
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Slider'>" +
                "  <Grid Height='18' VerticalAlignment='Center'>" +
                "    <Border Height='4' CornerRadius='2' VerticalAlignment='Center'" +
                "            Background='#E9EBEF'/>" +
                "    <Track x:Name='PART_Track'>" +
                "      <Track.DecreaseRepeatButton>" +
                "        <RepeatButton Focusable='False' IsTabStop='False' Command='Slider.DecreaseLarge'>" +
                "          <RepeatButton.Template>" +
                "            <ControlTemplate TargetType='RepeatButton'>" +
                "              <Border Height='4' CornerRadius='2' VerticalAlignment='Center'" +
                "                      Background='#1C1C20'/>" +
                "            </ControlTemplate>" +
                "          </RepeatButton.Template>" +
                "        </RepeatButton>" +
                "      </Track.DecreaseRepeatButton>" +
                "      <Track.Thumb>" +
                "        <Thumb Width='15' Height='15'>" +
                "          <Thumb.Template>" +
                "            <ControlTemplate TargetType='Thumb'>" +
                "              <Ellipse Fill='White' Stroke='#C5CAD2' StrokeThickness='1.2'/>" +
                "            </ControlTemplate>" +
                "          </Thumb.Template>" +
                "        </Thumb>" +
                "      </Track.Thumb>" +
                "      <Track.IncreaseRepeatButton>" +
                "        <RepeatButton Focusable='False' IsTabStop='False' Command='Slider.IncreaseLarge'>" +
                "          <RepeatButton.Template>" +
                "            <ControlTemplate TargetType='RepeatButton'>" +
                "              <Border Background='Transparent'/>" +
                "            </ControlTemplate>" +
                "          </RepeatButton.Template>" +
                "        </RepeatButton>" +
                "      </Track.IncreaseRepeatButton>" +
                "    </Track>" +
                "  </Grid>" +
                "</ControlTemplate>";

            return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        // ------------------------------------------------------------------
        // Symbole
        // ------------------------------------------------------------------

        static string IconData(ActionItem item)
        {
            if (item == null) return GlyphFallback;
            try
            {
                if (!string.IsNullOrEmpty(item.Icon))
                {
                    string custom = IconLibrary.Resolve(item.Icon);
                    if (!string.IsNullOrEmpty(custom)) return custom;
                }
                string auto = IconLibrary.For(item);
                if (!string.IsNullOrEmpty(auto)) return auto;
            }
            catch { }
            return GlyphFallback;
        }

        static FrameworkElement GlyphFor(ActionItem item, double size, Brush brush)
        {
            return Ui.Glyph(IconData(item), size, brush);
        }

        // ==================================================================
        // Seite: Befehle
        // ==================================================================

        static readonly string[] Kinds =
        {
            "program", "url", "shell", "hotkey", "media",
            "volume", "system", "activate", "discord", "macro"
        };

        static readonly string[] KindLabels =
        {
            "Programm starten",
            "Webseite oder Ordner öffnen",
            "Befehl ausführen",
            "Tastenkombination senden",
            "Mediensteuerung",
            "Lautstärke",
            "Systemfunktion",
            "Fenster in den Vordergrund",
            "Discord",
            "Makro"
        };

        FrameworkElement BuildCommandsPage()
        {
            _profile = _config.CurrentProfile;
            if (_profile == null)
            {
                _profile = new Profile();
                _profile.Name = "Hauptprofil";
                _config.Profiles.Add(_profile);
                _config.ActiveProfileId = _profile.Id;
            }

            Grid page = new Grid();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // --- Kopf mit Profilauswahl ---
            StackPanel head = new StackPanel();
            head.Margin = new Thickness(30, 26, 30, 18);
            head.MaxWidth = 1000;
            head.HorizontalAlignment = HorizontalAlignment.Stretch;

            Grid headGrid = new Grid();
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titles = new StackPanel();
            titles.Children.Add(Ui.Heading("Befehle", 19));
            TextBlock sub = Ui.Text(
                "Die Reihenfolge links entspricht der Drehrichtung am Regler.", 12.5, Ui.InkMuted);
            sub.Margin = new Thickness(0, 3, 0, 0);
            titles.Children.Add(sub);
            headGrid.Children.Add(titles);

            StackPanel profileBox = new StackPanel();
            profileBox.VerticalAlignment = VerticalAlignment.Center;
            profileBox.Margin = new Thickness(16, 0, 0, 0);
            profileBox.MinWidth = 190;

            TextBlock profileCaption = Ui.Text("Profil", 11, Ui.InkMuted);
            profileCaption.Margin = new Thickness(0, 0, 0, 4);
            profileBox.Children.Add(profileCaption);

            SelectControl profileSelect = new SelectControl();
            List<string> names = new List<string>();
            int active = 0;
            for (int i = 0; i < _config.Profiles.Count; i++)
            {
                names.Add(_config.Profiles[i].Name);
                if (_config.Profiles[i].Id == _config.ActiveProfileId) active = i;
            }
            profileSelect.SetItems(names);
            profileSelect.SelectedIndex = active;
            profileSelect.Changed += delegate
            {
                int index = profileSelect.SelectedIndex;
                if (index < 0 || index >= _config.Profiles.Count) return;
                _config.ActiveProfileId = _config.Profiles[index].Id;
                _profile = _config.Profiles[index];
                _selected = null;
                RefreshActionList();
                if (_profile.Actions.Count > 0) SelectAction(0);
                else ShowEditor(null);
                Push();
            };
            profileBox.Children.Add(profileSelect);

            Grid.SetColumn(profileBox, 1);
            headGrid.Children.Add(profileBox);

            head.Children.Add(headGrid);
            Grid.SetRow(head, 0);
            page.Children.Add(head);

            // --- Inhalt: Liste und Editor ---
            Grid body = new Grid();
            body.Margin = new Thickness(30, 0, 30, 26);
            body.MaxWidth = 1000;
            body.HorizontalAlignment = HorizontalAlignment.Stretch;
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            page.Children.Add(body);

            body.Children.Add(BuildActionListPane());

            // Editor rechts
            Grid editorFrame = new Grid();
            _editorHost = new StackPanel();

            ScrollViewer editorScroll = new ScrollViewer();
            editorScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            editorScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            editorScroll.Content = _editorHost;
            editorFrame.Children.Add(editorScroll);

            Grid.SetColumn(editorFrame, 2);
            body.Children.Add(editorFrame);

            RefreshActionList();
            if (_profile.Actions.Count > 0) SelectAction(0);
            else ShowEditor(null);

            return page;
        }

        FrameworkElement BuildActionListPane()
        {
            DockPanel pane = new DockPanel();
            pane.LastChildFill = true;

            // Kopfzeile der Liste
            Grid listHead = new Grid();
            listHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            listHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            listHead.Margin = new Thickness(0, 0, 0, 8);

            TextBlock caption = Ui.Text("Im Menü", 11.5, Ui.InkSoft);
            caption.VerticalAlignment = VerticalAlignment.Center;
            listHead.Children.Add(caption);

            _actionCount = Ui.Text("", 11, Ui.InkMuted);
            _actionCount.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_actionCount, 1);
            listHead.Children.Add(_actionCount);

            DockPanel.SetDock(listHead, Dock.Top);
            pane.Children.Add(listHead);

            // Werkzeuge unten
            StackPanel order = new StackPanel();
            order.Orientation = Orientation.Horizontal;
            order.Margin = new Thickness(0, 8, 0, 0);
            DockPanel.SetDock(order, Dock.Bottom);

            Button add = Ui.PrimaryButton("Befehl hinzufügen");
            add.Click += delegate { AddCommand(); };
            order.Children.Add(add);

            Button duplicate = Ui.SmallButton("Duplizieren");
            duplicate.Margin = new Thickness(6, 0, 0, 0);
            duplicate.Click += delegate { DuplicateCommand(); };
            order.Children.Add(duplicate);

            Button remove = Ui.SmallButton("Entfernen");
            remove.Margin = new Thickness(6, 0, 0, 0);
            remove.Click += delegate { DeleteCommand(); };
            order.Children.Add(remove);

            pane.Children.Add(order);

            // Reihenfolge direkt unter der Liste
            StackPanel sortRow = new StackPanel();
            sortRow.Orientation = Orientation.Horizontal;
            sortRow.Margin = new Thickness(0, 0, 0, 0);
            DockPanel.SetDock(sortRow, Dock.Bottom);

            Button up = Ui.IconButton("↑", "Nach oben");
            up.Click += delegate { MoveCommand(-1); };
            sortRow.Children.Add(up);

            TextBlock orderCaption = Ui.Text("Reihenfolge", 11, Ui.InkMuted);
            orderCaption.VerticalAlignment = VerticalAlignment.Center;
            orderCaption.Margin = new Thickness(8, 0, 8, 0);
            sortRow.Children.Add(orderCaption);

            Button down = Ui.IconButton("↓", "Nach unten");
            down.Click += delegate { MoveCommand(1); };
            sortRow.Children.Add(down);

            pane.Children.Add(sortRow);

            // Die Liste
            _actionList = new ListBox();
            _actionList.BorderBrush = Ui.Line;
            _actionList.BorderThickness = new Thickness(1);
            _actionList.Background = Ui.Card;
            _actionList.Padding = new Thickness(4);
            _actionList.ItemContainerStyle = Ui.ListRowStyle();
            // Ohne diese beiden Angaben schiebt eine lange Zeile die Liste
            // waagerecht auf und die Pruefhinweise werden abgeschnitten.
            ScrollViewer.SetHorizontalScrollBarVisibility(_actionList, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(_actionList, ScrollBarVisibility.Auto);
            _actionList.SelectionChanged += delegate
            {
                if (_loading) return;
                int index = _actionList.SelectedIndex;
                if (index < 0 || _profile == null || index >= _profile.Actions.Count) return;
                _selected = _profile.Actions[index];
                ShowEditor(_selected);
            };
            pane.Children.Add(_actionList);

            return pane;
        }

        void RefreshActionList()
        {
            if (_actionList == null) return;

            int keep = _actionList.SelectedIndex;
            _loading = true;
            try
            {
                _actionList.Items.Clear();

                if (_profile == null) return;

                for (int i = 0; i < _profile.Actions.Count; i++)
                {
                    ActionItem item = _profile.Actions[i];
                    ListBoxItem row = new ListBoxItem();
                    row.Content = BuildActionRow(item, i);
                    row.Tag = item;
                    _actionList.Items.Add(row);
                }

                if (keep >= 0 && keep < _actionList.Items.Count) _actionList.SelectedIndex = keep;

                if (_actionCount != null)
                {
                    int count = _profile.Actions.Count;
                    _actionCount.Text = count == 0 ? "leer"
                        : count == 1 ? "1 Befehl" : count + " Befehle";
                }
            }
            finally { _loading = false; }
        }

        FrameworkElement BuildActionRow(ActionItem item, int index)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            ContentControl icon = new ContentControl();
            icon.Content = GlyphFor(item, 17, Ui.InkSoft);
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            grid.Children.Add(icon);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(2, 0, 0, 0);

            TextBlock label = Ui.Text(string.IsNullOrEmpty(item.Label) ? "Ohne Namen" : item.Label, 12.5, Ui.Ink);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(label);

            TextBlock detail = Ui.Text(DescribeSafely(item), 10.5, Ui.InkMuted);
            detail.TextTrimming = TextTrimming.CharacterEllipsis;
            detail.Margin = new Thickness(0, 1, 0, 0);
            text.Children.Add(detail);

            string warning = FirstWarning(item);
            TextBlock warn = Ui.Text(warning ?? "", 10.5, Ui.Warn);
            warn.TextTrimming = TextTrimming.CharacterEllipsis;
            warn.Margin = new Thickness(0, 2, 0, 0);
            warn.Visibility = warning == null ? Visibility.Collapsed : Visibility.Visible;
            text.Children.Add(warn);

            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            return grid;
        }

        static string DescribeSafely(ActionItem item)
        {
            try
            {
                string text = Commands.Describe(item);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            catch { }
            return KindLabel(item.Kind);
        }

        static string FirstWarning(ActionItem item)
        {
            try
            {
                List<string> errors = Commands.Validate(item);
                if (errors != null && errors.Count > 0) return errors[0];
            }
            catch { }
            return null;
        }

        static string KindLabel(string kind)
        {
            for (int i = 0; i < Kinds.Length; i++)
                if (Kinds[i] == kind) return KindLabels[i];
            return string.IsNullOrEmpty(kind) ? "Befehl" : kind;
        }

        static int KindIndex(string kind)
        {
            for (int i = 0; i < Kinds.Length; i++)
                if (Kinds[i] == kind) return i;
            return 0;
        }

        void SelectAction(int index)
        {
            if (_actionList == null) return;
            if (index < 0 || index >= _actionList.Items.Count) return;
            _actionList.SelectedIndex = index;
        }

        // ------------------------------------------------------------------
        // Editor
        // ------------------------------------------------------------------

        void ShowEditor(ActionItem item)
        {
            _selected = item;
            _editorLoading = true;
            try
            {
                _editorHost.Children.Clear();

                if (item == null)
                {
                    _editorHost.Children.Add(EmptyState());
                    return;
                }

                Border card = CardBox();

                StackPanel stack = new StackPanel();

                // Kopf des Editors: Symbol, Name, Art
                Grid top = new Grid();
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                StackPanel nameBlock = new StackPanel();
                nameBlock.Children.Add(Ui.Heading("Befehl bearbeiten", 13));

                TextBlock kindText = Ui.Text(KindLabel(item.Kind), 11.5, Ui.InkMuted);
                kindText.Margin = new Thickness(0, 2, 0, 0);
                nameBlock.Children.Add(kindText);
                top.Children.Add(nameBlock);

                stack.Children.Add(top);
                card.Child = stack;

                // Felder
                _fLabel = Ui.TextField();
                _fLabel.Text = item.Label ?? "";
                _fLabel.TextChanged += delegate
                {
                    if (_editorLoading) return;
                    item.Label = _fLabel.Text;
                    AfterEdit(item, true);
                };
                StackPanel labelBlock = FieldBlock("Bezeichnung im Menü", _fLabel, 16);
                stack.Children.Add(labelBlock);

                _cKind = new SelectControl();
                List<string> kindOptions = new List<string>();
                for (int i = 0; i < KindLabels.Length; i++) kindOptions.Add(KindLabels[i]);
                _cKind.SetItems(kindOptions);
                _kindIndex = KindIndex(item.Kind);
                _cKind.SelectedIndex = _kindIndex;
                _cKind.Changed += delegate
                {
                    if (_editorLoading) return;
                    int index = _cKind.SelectedIndex;
                    if (index < 0 || index >= Kinds.Length) return;
                    item.Kind = Kinds[index];
                    _kindIndex = index;
                    UpdateKindBlocks(item);
                    AfterEdit(item, true);
                };
                stack.Children.Add(FieldBlock("Art", _cKind, 14));

                _fTarget = Ui.TextField();
                _fTarget.Text = item.Target ?? "";
                _fTarget.TextChanged += delegate
                {
                    if (_editorLoading) return;
                    item.Target = _fTarget.Text;
                    AfterEdit(item, false);
                };
                stack.Children.Add(FieldBlock("Ziel", _fTarget, 14));

                _targetHint = Ui.Text("", 11, Ui.InkMuted);
                _targetHint.TextWrapping = TextWrapping.Wrap;
                _targetHint.Margin = new Thickness(0, 5, 0, 0);
                stack.Children.Add(_targetHint);

                _browseBlock = new StackPanel();
                _browseBlock.Orientation = Orientation.Horizontal;
                _browseBlock.Margin = new Thickness(0, 8, 0, 0);

                Button browseFile = Ui.SmallButton("Datei wählen…");
                browseFile.Click += delegate { BrowseFile(item); };
                _browseBlock.Children.Add(browseFile);

                Button browseFolder = Ui.SmallButton("Ordner wählen…");
                browseFolder.Margin = new Thickness(6, 0, 0, 0);
                browseFolder.Click += delegate { BrowseFolder(item); };
                _browseBlock.Children.Add(browseFolder);

                stack.Children.Add(_browseBlock);

                _fArgs = Ui.TextField();
                _fArgs.Text = item.Args ?? "";
                _fArgs.TextChanged += delegate
                {
                    if (_editorLoading) return;
                    item.Args = _fArgs.Text;
                    AfterEdit(item, false);
                };
                _argsBlock = FieldBlock("Argumente (optional)", _fArgs, 14);
                stack.Children.Add(_argsBlock);

                _fHint = Ui.TextField();
                _fHint.Text = item.Hint ?? "";
                _fHint.TextChanged += delegate
                {
                    if (_editorLoading) return;
                    item.Hint = _fHint.Text;
                    AfterEdit(item, true);
                };
                stack.Children.Add(FieldBlock("Kurzhinweis im Menü (optional)", _fHint, 14));

                // Symbol
                stack.Children.Add(BuildIconBlock(item));

                // Makro
                _macroBlock = new StackPanel();
                _macroBlock.Margin = new Thickness(0, 16, 0, 0);

                TextBlock macroCaption = SectionLabel("Ablauf");
                _macroBlock.Children.Add(macroCaption);

                Grid macroRow = new Grid();
                macroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                macroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                _macroSummary = Ui.Text("", 12, Ui.InkSoft);
                _macroSummary.TextWrapping = TextWrapping.Wrap;
                _macroSummary.VerticalAlignment = VerticalAlignment.Center;
                macroRow.Children.Add(_macroSummary);

                Button editSteps = Ui.Button("Schritte bearbeiten…");
                editSteps.VerticalAlignment = VerticalAlignment.Center;
                editSteps.Margin = new Thickness(10, 0, 0, 0);
                editSteps.Click += delegate
                {
                    MacroEditor editor = new MacroEditor(item.Steps, item.Label, delegate
                    {
                        UpdateMacroSummary(item);
                        AfterEdit(item, false);
                    });
                    editor.Owner = this;
                    editor.ShowDialog();
                    UpdateMacroSummary(item);
                    AfterEdit(item, false);
                };
                Grid.SetColumn(editSteps, 1);
                macroRow.Children.Add(editSteps);

                _macroBlock.Children.Add(macroRow);
                stack.Children.Add(_macroBlock);

                // Prüfung
                _validationHost = new StackPanel();
                _validationHost.Margin = new Thickness(0, 16, 0, 0);
                stack.Children.Add(_validationHost);

                _editorHost.Children.Add(card);

                UpdateKindBlocks(item);
                UpdateMacroSummary(item);
                UpdateValidation(item);
            }
            finally { _editorLoading = false; }
        }

        FrameworkElement EmptyState()
        {
            Border box = CardBox();
            StackPanel stack = new StackPanel();
            stack.Margin = new Thickness(0, 26, 0, 26);

            ContentControl icon = new ContentControl();
            icon.Content = Ui.Glyph(GlyphList, 24, new SolidColorBrush(Color.FromRgb(0xC3, 0xC8, 0xD0)));
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            stack.Children.Add(icon);

            TextBlock title = Ui.Heading("Kein Befehl ausgewählt", 14);
            title.Margin = new Thickness(0, 12, 0, 0);
            stack.Children.Add(title);

            TextBlock text = Ui.Text(
                "Wähle links einen Befehl aus oder lege mit „Befehl hinzufügen“ einen neuen an.",
                12.5, Ui.InkMuted);
            text.Margin = new Thickness(0, 4, 0, 0);
            text.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(text);

            box.Child = stack;
            return box;
        }

        static StackPanel FieldBlock(string label, FrameworkElement control, double top)
        {
            StackPanel block = new StackPanel();
            block.Margin = new Thickness(0, top, 0, 0);

            TextBlock caption = Ui.Text(label, 11, Ui.InkMuted);
            caption.Margin = new Thickness(0, 0, 0, 4);
            block.Children.Add(caption);
            block.Children.Add(control);
            return block;
        }

        FrameworkElement BuildIconBlock(ActionItem item)
        {
            StackPanel block = new StackPanel();
            block.Margin = new Thickness(0, 16, 0, 0);
            block.Children.Add(SectionLabel("Symbol"));

            // Obere Zeile: Vorschau und Beschriftung.
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border well = new Border();
            well.Width = 38;
            well.Height = 38;
            well.CornerRadius = new CornerRadius(6);
            well.Background = Ui.Subtle;
            well.BorderBrush = Ui.Line;
            well.BorderThickness = new Thickness(1);

            _iconPreview = new ContentControl();
            _iconPreview.Content = GlyphFor(item, 20, Ui.Ink);
            _iconPreview.HorizontalAlignment = HorizontalAlignment.Center;
            _iconPreview.VerticalAlignment = VerticalAlignment.Center;
            well.Child = _iconPreview;
            row.Children.Add(well);

            StackPanel iconText = new StackPanel();
            iconText.VerticalAlignment = VerticalAlignment.Center;
            iconText.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(iconText, 1);
            row.Children.Add(iconText);

            _iconName = Ui.Text(
                string.IsNullOrEmpty(item.Icon) ? "Automatisch nach Art und Ziel"
                    : "Gewählt: " + item.Icon,
                12, Ui.InkSoft);
            _iconName.TextTrimming = TextTrimming.CharacterEllipsis;
            iconText.Children.Add(_iconName);

            TextBlock iconNote = Ui.Text("Ohne Auswahl bestimmt DialMenu das Symbol selbst.",
                11, Ui.InkMuted);
            iconNote.TextWrapping = TextWrapping.Wrap;
            iconText.Children.Add(iconNote);

            block.Children.Add(row);

            // Untere Zeile: Schaltflächen umbrechen, statt die Karte zu sprengen.
            WrapPanel iconButtons = new WrapPanel();
            iconButtons.Margin = new Thickness(0, 10, 0, 0);

            Button pick = Ui.SmallButton("Symbol wählen…");
            pick.Margin = new Thickness(0, 0, 6, 6);
            pick.Click += delegate { PickIcon(item); };
            iconButtons.Children.Add(pick);

            Button resetIcon = Ui.SmallButton("Zurücksetzen");
            resetIcon.Margin = new Thickness(0, 0, 0, 6);
            resetIcon.Click += delegate
            {
                item.Icon = "";
                if (_iconPreview != null) _iconPreview.Content = GlyphFor(item, 20, Ui.Ink);
                if (_iconName != null) _iconName.Text = "Automatisch nach Art und Ziel";
                AfterEdit(item, true);
            };
            iconButtons.Children.Add(resetIcon);

            block.Children.Add(iconButtons);
            return block;
        }

        void PickIcon(ActionItem item)
        {
            IconPicker picker = new IconPicker(item.Icon);
            picker.Owner = this;
            if (picker.ShowDialog() != true) return;

            item.Icon = picker.SelectedName ?? "";
            if (_iconPreview != null) _iconPreview.Content = GlyphFor(item, 20, Ui.Ink);
            if (_iconName != null)
                _iconName.Text = string.IsNullOrEmpty(item.Icon)
                    ? "Automatisch nach Art und Ziel"
                    : "Gewählt: " + item.Icon;
            AfterEdit(item, true);
        }

        static string TargetHintFor(string kind)
        {
            switch (kind)
            {
                case "program":
                    return "Vollständiger Pfad oder Kurzname, zum Beispiel notepad.exe.";
                case "url":
                    return "Adresse, Ordner oder Datei – zum Beispiel https://youtube.com, C:\\Bilder oder ms-settings:";
                case "shell":
                    return "Wird ohne sichtbares Fenster ausgeführt.";
                case "hotkey":
                    return "Zum Beispiel Strg+Alt+M, Win+D oder F11.";
                case "media":
                    return "playpause, next, prev oder stop";
                case "volume":
                    return "up, down oder mute – die Argumente geben die Schritte an.";
                case "system":
                    return "lock, sleep, showdesktop, taskmgr, shutdown, restart oder logoff";
                case "activate":
                    return "Teil des Fenstertitels oder Programmname, zum Beispiel spotify";
                case "discord":
                    return "Ziel: toggle (Discords eigene Taste, Anzeige stimmt), mute oder unmute " +
                        "(systemweit, ohne Anzeige in Discord) sowie deafen oder toggledeafen. " +
                        "In den Argumenten steht optional die Tastenkombination aus Discord; " +
                        "ohne Angabe gilt Strg+Shift+M.";
                case "macro":
                    return "Ein Makro braucht kein Ziel – die Ablaufschritte zählen.";
            }
            return "";
        }

        void UpdateKindBlocks(ActionItem item)
        {
            if (_targetHint != null) _targetHint.Text = TargetHintFor(item.Kind);

            bool isMacro = item.Kind == "macro";
            if (_macroBlock != null)
                _macroBlock.Visibility = isMacro ? Visibility.Visible : Visibility.Collapsed;

            bool showArgs = item.Kind == "program" || item.Kind == "shell"
                || item.Kind == "volume" || item.Kind == "discord";
            if (_argsBlock != null)
                _argsBlock.Visibility = showArgs ? Visibility.Visible : Visibility.Collapsed;

            if (_browseBlock != null)
            {
                bool show = item.Kind == "program" || item.Kind == "url" || item.Kind == "shell";
                _browseBlock.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_fTarget != null)
            {
                _fTarget.IsEnabled = !isMacro;
                _fTarget.Opacity = isMacro ? 0.55 : 1.0;
            }
        }

        void UpdateMacroSummary(ActionItem item)
        {
            if (_macroSummary == null) return;

            int count = item.Steps == null ? 0 : item.Steps.Count;
            if (count == 0)
            {
                _macroSummary.Text = "Noch keine Schritte hinterlegt.";
                return;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < count && i < 4; i++)
            {
                MacroStep step = item.Steps[i];
                switch (step.Type)
                {
                    case "hotkey": parts.Add(string.IsNullOrEmpty(step.Value) ? "Tastenkombination" : step.Value); break;
                    case "text": parts.Add(string.IsNullOrEmpty(step.Value) ? "Text" : "„" + Shorten(step.Value, 14) + "“"); break;
                    case "delay": parts.Add("Pause"); break;
                    case "program": parts.Add(string.IsNullOrEmpty(step.Value) ? "Programm" : Shorten(step.Value, 18)); break;
                    default: parts.Add(step.Type); break;
                }
            }

            string summary = string.Join(" · ", parts.ToArray());
            if (count > 4) summary += " · …";
            _macroSummary.Text = count + " Schritte: " + summary;
        }

        static string Shorten(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Length <= max) return value;
            return value.Substring(0, max - 1) + "…";
        }

        void UpdateValidation(ActionItem item)
        {
            if (_validationHost == null) return;
            _validationHost.Children.Clear();

            List<string> errors = null;
            try { errors = Commands.Validate(item); }
            catch { }

            if (errors == null || errors.Count == 0) return;

            Border box = new Border();
            box.Background = Ui.WarnSoft;
            box.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEC, 0xDF, 0xC2));
            box.BorderThickness = new Thickness(1);
            box.CornerRadius = new CornerRadius(5);
            box.Padding = new Thickness(12, 10, 12, 10);

            StackPanel stack = new StackPanel();

            TextBlock caption = Ui.Text("Bitte prüfen", 11.5, Ui.Warn);
            caption.FontWeight = FontWeights.SemiBold;
            stack.Children.Add(caption);

            for (int i = 0; i < errors.Count; i++)
            {
                if (string.IsNullOrEmpty(errors[i])) continue;
                TextBlock line = Ui.Text("· " + errors[i], 12, Ui.Warn);
                line.TextWrapping = TextWrapping.Wrap;
                line.Margin = new Thickness(0, i == 0 ? 4 : 2, 0, 0);
                stack.Children.Add(line);
            }

            box.Child = stack;
            _validationHost.Children.Add(box);
        }

        // Nach jeder Feldänderung: Liste auffrischen und verzoegert speichern.
        void AfterEdit(ActionItem item, bool rebuildList)
        {
            if (_editorLoading || item == null) return;

            if (rebuildList) RefreshActionList();
            else
            {
                int index = _profile != null ? _profile.Actions.IndexOf(item) : -1;
                if (index >= 0 && _actionList != null && index < _actionList.Items.Count)
                {
                    ListBoxItem row = _actionList.Items[index] as ListBoxItem;
                    if (row != null) row.Content = BuildActionRow(item, index);
                }
            }

            UpdateValidation(item);
            MarkDirty();
        }

        void MarkDirty()
        {
            _debounce.Stop();
            _debounce.Start();
        }

        void Push()
        {
            if (_loading) return;
            _app.ConfigChanged();
        }

        // ------------------------------------------------------------------
        // Befehle verwalten
        // ------------------------------------------------------------------

        void AddCommand()
        {
            EnsureProfile();

            PresetWindow picker = new PresetWindow(delegate(ActionItem created)
            {
                if (created == null) return;

                _profile.Actions.Add(created);
                RefreshActionList();
                SelectAction(_profile.Actions.Count - 1);
                Push();
            });
            picker.Owner = this;
            picker.ShowDialog();
        }

        void DuplicateCommand()
        {
            if (_profile == null || _selected == null) return;

            int index = _profile.Actions.IndexOf(_selected);
            if (index < 0) return;

            ActionItem copy = _selected.Clone();
            copy.Label = _selected.Label + " (Kopie)";
            _profile.Actions.Insert(index + 1, copy);

            RefreshActionList();
            SelectAction(index + 1);
            Push();
        }

        void DeleteCommand()
        {
            if (_profile == null || _selected == null) return;

            int index = _profile.Actions.IndexOf(_selected);
            if (index < 0) return;

            string label = _selected.Label;
            MessageBoxResult answer = MessageBox.Show(
                "„" + label + "“ aus dem Menü entfernen?",
                "Befehl entfernen", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (answer != MessageBoxResult.OK) return;

            _profile.Actions.RemoveAt(index);
            _selected = null;
            RefreshActionList();

            if (_profile.Actions.Count > 0)
                SelectAction(Math.Min(index, _profile.Actions.Count - 1));
            else
                ShowEditor(null);

            Push();
        }

        void MoveCommand(int delta)
        {
            if (_profile == null || _selected == null) return;

            int index = _profile.Actions.IndexOf(_selected);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= _profile.Actions.Count) return;

            ActionItem item = _profile.Actions[index];
            _profile.Actions.RemoveAt(index);
            _profile.Actions.Insert(target, item);

            RefreshActionList();
            SelectAction(target);
            Push();
        }

        void EnsureProfile()
        {
            if (_profile != null) return;
            if (_config.Profiles.Count == 0)
            {
                _profile = new Profile();
                _profile.Name = "Hauptprofil";
                _config.Profiles.Add(_profile);
            }
            else
            {
                _profile = _config.CurrentProfile;
            }
            if (_profile != null) _config.ActiveProfileId = _profile.Id;
        }

        void BrowseFile(ActionItem item)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "Programm oder Datei wählen";
            dialog.Filter = "Programme (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|Alle Dateien (*.*)|*.*";

            if (dialog.ShowDialog(this) != true) return;

            item.Target = dialog.FileName;
            if (_fTarget != null) _fTarget.Text = dialog.FileName;

            if (string.IsNullOrWhiteSpace(item.Label) || item.Label == "Neue Aktion"
                || item.Label == "Neuer Befehl")
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                item.Label = name;
                if (_fLabel != null) _fLabel.Text = name;
            }

            if (item.Kind != "program" && item.Kind != "shell")
            {
                item.Kind = "program";
                if (_cKind != null) _cKind.SelectedIndex = KindIndex("program");
                UpdateKindBlocks(item);
            }

            AfterEdit(item, true);
            MarkDirty();
        }

        void BrowseFolder(ActionItem item)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Ordner wählen";

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            item.Target = dialog.SelectedPath;
            if (_fTarget != null) _fTarget.Text = dialog.SelectedPath;
            AfterEdit(item, true);
            MarkDirty();
        }

        // ==================================================================
        // Seite: Profile
        // ==================================================================

        FrameworkElement BuildProfilesPage()
        {
            StackPanel body = PageBody();
            body.Children.Add(PageHeader("Profile",
                "Jedes Profil hat einen eigenen Ring. Das aktive Profil bestimmt, was der Regler zeigt.", null));

            StackPanel list = new StackPanel();
            list.Margin = new Thickness(0, 18, 0, 0);

            for (int i = 0; i < _config.Profiles.Count; i++)
            {
                Profile profile = _config.Profiles[i];
                list.Children.Add(BuildProfileRow(profile, i == _config.Profiles.Count - 1));
            }

            body.Children.Add(Card(null, null, list));

            Button create = Ui.Button("Neues Profil");
            create.HorizontalAlignment = HorizontalAlignment.Left;
            create.Margin = new Thickness(0, 14, 0, 0);
            create.Click += delegate
            {
                Profile profile = new Profile();
                profile.Name = NextProfileName();
                _config.Profiles.Add(profile);
                Push();
                ShowSection("profile");
            };
            body.Children.Add(create);

            return ScrollPage(body);
        }

        string NextProfileName()
        {
            int number = _config.Profiles.Count + 1;
            string name = "Profil " + number;
            bool taken = true;
            while (taken)
            {
                taken = false;
                foreach (Profile p in _config.Profiles)
                    if (string.Equals(p.Name, name, StringComparison.Ordinal)) { taken = true; break; }
                if (taken) { number++; name = "Profil " + number; }
            }
            return name;
        }

        FrameworkElement BuildProfileRow(Profile profile, bool last)
        {
            bool active = profile.Id == _config.ActiveProfileId;

            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Margin = new Thickness(0, 4, 0, last ? 0 : 10);

            StackPanel info = new StackPanel();
            info.VerticalAlignment = VerticalAlignment.Center;

            StackPanel titleLine = new StackPanel();
            titleLine.Orientation = Orientation.Horizontal;

            TextBlock name = Ui.Text(profile.Name, 13, Ui.Ink);
            name.FontWeight = FontWeights.SemiBold;
            name.VerticalAlignment = VerticalAlignment.Center;
            titleLine.Children.Add(name);

            if (active)
            {
                Border badge = new Border();
                badge.Background = Ui.AccentSoft;
                badge.CornerRadius = new CornerRadius(3);
                badge.Padding = new Thickness(6, 1, 6, 2);
                badge.Margin = new Thickness(8, 1, 0, 0);
                badge.VerticalAlignment = VerticalAlignment.Center;

                TextBlock badgeText = Ui.Text("Aktiv", 10, Ui.Accent);
                badgeText.FontWeight = FontWeights.SemiBold;
                badge.Child = badgeText;
                titleLine.Children.Add(badge);
            }

            info.Children.Add(titleLine);

            int count = profile.Actions == null ? 0 : profile.Actions.Count;
            TextBlock detail = Ui.Text(
                count == 1 ? "1 Befehl" : count + " Befehle", 11.5, Ui.InkMuted);
            detail.Margin = new Thickness(0, 2, 0, 0);
            info.Children.Add(detail);

            row.Children.Add(info);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;

            if (!active)
            {
                Button activate = Ui.SmallButton("Aktivieren");
                activate.Margin = new Thickness(0, 0, 6, 0);
                activate.Click += delegate
                {
                    _config.ActiveProfileId = profile.Id;
                    _profile = profile;
                    _selected = null;
                    Push();
                    ShowSection("profile");
                };
                actions.Children.Add(activate);
            }

            Button rename = Ui.SmallButton("Umbenennen");
            rename.Margin = new Thickness(0, 0, 6, 0);
            rename.Click += delegate
            {
                string entered = PromptForName(profile.Name);
                if (entered == null) return;
                profile.Name = entered;
                Push();
                ShowSection("profile");
            };
            actions.Children.Add(rename);

            Button duplicate = Ui.SmallButton("Duplizieren");
            duplicate.Margin = new Thickness(0, 0, 6, 0);
            duplicate.Click += delegate
            {
                Profile copy = new Profile();
                copy.Name = profile.Name + " (Kopie)";
                copy.Actions = new List<ActionItem>();
                foreach (ActionItem item in profile.Actions) copy.Actions.Add(item.Clone());
                _config.Profiles.Insert(_config.Profiles.IndexOf(profile) + 1, copy);
                Push();
                ShowSection("profile");
            };
            actions.Children.Add(duplicate);

            Button delete = Ui.SmallButton("Löschen");
            delete.IsEnabled = _config.Profiles.Count > 1;
            delete.Click += delegate
            {
                if (_config.Profiles.Count <= 1) return;

                MessageBoxResult answer = MessageBox.Show(
                    "Profil „" + profile.Name + "“ mit allen Befehlen löschen?",
                    "Profil löschen", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (answer != MessageBoxResult.OK) return;

                _config.Profiles.Remove(profile);
                if (_config.ActiveProfileId == profile.Id && _config.Profiles.Count > 0)
                    _config.ActiveProfileId = _config.Profiles[0].Id;

                if (_profile == profile) _profile = _config.CurrentProfile;
                Push();
                ShowSection("profile");
            };
            actions.Children.Add(delete);

            Grid.SetColumn(actions, 1);
            row.Children.Add(actions);

            Border wrapper = new Border();
            wrapper.Padding = new Thickness(0, 6, 0, 6);
            wrapper.Child = row;

            FrameworkElement content = wrapper;
            if (!last)
            {
                StackPanel stack = new StackPanel();
                stack.Children.Add(wrapper);
                stack.Children.Add(Ui.Divider());
                content = stack;
            }
            return content;
        }

        string PromptForName(string current)
        {
            Window dialog = new Window();
            dialog.Title = "Profil umbenennen";
            dialog.Width = 380;
            dialog.SizeToContent = SizeToContent.Height;
            dialog.ResizeMode = ResizeMode.NoResize;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Owner = this;
            dialog.Background = Ui.Bg;
            dialog.FontFamily = UiFont.Family;
            dialog.FontSize = 13;
            dialog.ShowInTaskbar = false;

            string result = null;

            StackPanel body = new StackPanel();
            body.Margin = new Thickness(20, 18, 20, 18);

            TextBox field = Ui.TextField();
            field.Text = current ?? "";
            field.SelectAll();
            body.Children.Add(FieldBlock("Name", field, 0));

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 18, 0, 0);

            Button cancel = Ui.Button("Abbrechen");
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += delegate { dialog.Close(); };
            buttons.Children.Add(cancel);

            Button save = Ui.PrimaryButton("Übernehmen");
            save.IsDefault = true;
            save.Click += delegate
            {
                string trimmed = (field.Text ?? "").Trim();
                if (trimmed.Length > 0) result = trimmed;
                dialog.Close();
            };
            buttons.Children.Add(save);

            body.Children.Add(buttons);
            dialog.Content = body;

            dialog.Loaded += delegate { field.Focus(); };
            dialog.ShowDialog();

            return result;
        }

        // ==================================================================
        // Seite: Geraete
        // ==================================================================

        FrameworkElement BuildDevicesPage()
        {
            StackPanel body = PageBody();
            body.Children.Add(PageHeader("Geräte",
                "DialMenu liest die Rohdaten des Drehreglers über HID und ordnet sie dem passenden Gerät zu.", null));

            // Angeschlossene Geräte
            StackPanel attached = new StackPanel();

            _deviceStatus = Ui.Text("Suche läuft …", 11.5, Ui.InkMuted);
            _deviceStatus.Margin = new Thickness(0, 0, 0, 8);
            attached.Children.Add(_deviceStatus);

            _attachedRows = new StackPanel();
            attached.Children.Add(_attachedRows);

            body.Children.Add(Card("Angeschlossen", null, attached));

            // Eingerichtete Geräte
            StackPanel configured = new StackPanel();

            _configuredRows = new StackPanel();
            configured.Children.Add(_configuredRows);

            Button addPreset = Ui.Button("Standard-Gerät hinzufügen (ISY IGK-6500)");
            addPreset.HorizontalAlignment = HorizontalAlignment.Left;
            addPreset.Margin = new Thickness(0, 12, 0, 0);
            addPreset.Click += delegate { AddStandardDevice(); };
            configured.Children.Add(addPreset);

            body.Children.Add(Card("Eingerichtet",
                "Nur eingerichtete und aktivierte Geräte lösen das Menü aus.", configured));

            RebuildConfiguredDevices();
            StartDiscovery();

            return ScrollPage(body);
        }

        void StartDiscovery()
        {
            if (_discovering) return;
            _discovering = true;
            _discoverError = null;
            _discovered = null;

            if (_deviceStatus != null) _deviceStatus.Text = "Suche läuft …";

            Thread worker = new Thread(delegate()
            {
                List<DeviceRow> found = new List<DeviceRow>();
                string error = null;
                try
                {
                    List<DeviceInfo> devices = InputEngine.Discover();
                    if (devices != null)
                    {
                        foreach (DeviceInfo device in devices)
                        {
                            DeviceRow row = new DeviceRow();
                            row.Name = device.Name;

                            // VendorId/ProductId sind Rohzahlen; Anzeige und
                            // Muster brauchen die uebliche Hex-Schreibweise.
                            row.Vid = "VID_" + device.VendorId.ToString("X4", CultureInfo.InvariantCulture);
                            row.Pid = "PID_" + device.ProductId.ToString("X4", CultureInfo.InvariantCulture);
                            row.Path = device.Path;
                            row.Match = string.IsNullOrEmpty(device.Match)
                                ? row.Vid + "&" + row.Pid
                                : device.Match;
                            row.Friendly = device.HasConsumerControls;
                            found.Add(row);
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    Log.Write("Gerätesuche fehlgeschlagen: " + ex.Message);
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _discovering = false;
                    _discovered = found;
                    _discoverError = error;
                    RenderDiscoveredDevices();
                }));
            });
            worker.IsBackground = true;
            worker.Name = "DialMenu.Discover";
            worker.Start();
        }

        static string MatchFor(string vid, string pid)
        {
            if (string.IsNullOrEmpty(vid) || string.IsNullOrEmpty(pid)) return "";
            return vid + "&" + pid;
        }

        void RenderDiscoveredDevices()
        {
            if (_attachedRows == null) return;
            _attachedRows.Children.Clear();

            if (_deviceStatus != null)
            {
                if (_discoverError != null)
                {
                    _deviceStatus.Text = "Suche nicht möglich: " + _discoverError;
                    _deviceStatus.Foreground = Ui.Warn;
                }
                else if (_discovered == null || _discovered.Count == 0)
                {
                    _deviceStatus.Text = "Kein passendes Eingabegerät gefunden.";
                    _deviceStatus.Foreground = Ui.InkMuted;
                }
                else
                {
                    _deviceStatus.Text = _discovered.Count == 1
                        ? "1 Gerät gefunden"
                        : _discovered.Count + " Geräte gefunden";
                    _deviceStatus.Foreground = Ui.InkMuted;
                }
            }

            if (_discovered == null || _discovered.Count == 0)
            {
                TextBlock hint = Ui.Text(
                    "Regler anschließen und die Seite erneut öffnen, um erneut zu suchen.",
                    12, Ui.InkMuted);
                hint.TextWrapping = TextWrapping.Wrap;
                hint.Margin = new Thickness(0, 6, 0, 0);
                _attachedRows.Children.Add(hint);
                return;
            }

            _attachedRows.Children.Add(DeviceHeaderRow());

            for (int i = 0; i < _discovered.Count; i++)
            {
                DeviceRow device = _discovered[i];
                _attachedRows.Children.Add(DeviceDataRow(device, i == _discovered.Count - 1));
            }

            Button rescan = Ui.SmallButton("Erneut suchen");
            rescan.HorizontalAlignment = HorizontalAlignment.Left;
            rescan.Margin = new Thickness(0, 12, 0, 0);
            rescan.Click += delegate { StartDiscovery(); };
            _attachedRows.Children.Add(rescan);
        }

        static FrameworkElement DeviceHeaderRow()
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            grid.Margin = new Thickness(0, 0, 0, 6);

            AddCell(grid, 0, "Gerät", true);
            AddCell(grid, 1, "Hersteller", true);
            AddCell(grid, 2, "Produkt", true);
            AddCell(grid, 3, "", true);
            return grid;
        }

        static void AddCell(Grid grid, int column, string text, bool header)
        {
            TextBlock block = Ui.Text(text, 10.5, Ui.InkMuted);
            block.VerticalAlignment = VerticalAlignment.Center;
            if (header) block.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(block, column);
            grid.Children.Add(block);
        }

        FrameworkElement DeviceDataRow(DeviceRow device, bool last)
        {
            Border border = new Border();
            border.BorderBrush = Ui.Line;
            border.BorderThickness = new Thickness(0, 1, 0, 0);
            border.Padding = new Thickness(0, 8, 0, 8);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });

            string name = string.IsNullOrEmpty(device.Name) ? "Unbenanntes Gerät" : device.Name;
            TextBlock nameCell = Ui.Text(name, 12.5, Ui.Ink);
            nameCell.TextTrimming = TextTrimming.CharacterEllipsis;
            nameCell.VerticalAlignment = VerticalAlignment.Center;
            nameCell.ToolTip = device.Path;
            grid.Children.Add(nameCell);

            AddCell(grid, 1, Strip(device.Vid), false);
            AddCell(grid, 2, Strip(device.Pid), false);

            bool configured = FindBinding(device) != null;

            // Geraete ohne Vendor-/Product-ID lassen sich nicht zuverlaessig
            // wiedererkennen. Sie wuerden jedes Geraet treffen und fremde
            // Lautstaerketasten abfangen.
            bool addressable = !string.IsNullOrEmpty(device.Match)
                && !device.Match.Contains("VID_0000")
                && !device.Match.Contains("PID_0000");

            Button action = Ui.SmallButton(configured ? "Eingerichtet"
                : (addressable ? "Hinzufügen" : "Nicht wählbar"));
            action.IsEnabled = !configured && addressable;
            if (!addressable)
                action.ToolTip = "Dieses Gerät meldet keine eigene Kennung. "
                    + "Es lässt sich nicht zuverlässig wiedererkennen.";
            action.HorizontalAlignment = HorizontalAlignment.Right;
            action.Click += delegate
            {
                if (!addressable) return;

                DeviceBinding binding = new DeviceBinding();
                binding.Name = name;
                binding.Match = device.Match;
                binding.Enabled = true;
                _config.Devices.Add(binding);
                Push();
                RebuildConfiguredDevices();
                RenderDiscoveredDevices();
            };

            Grid.SetColumn(action, 3);
            grid.Children.Add(action);

            border.Child = grid;
            return border;
        }

        static string Strip(string value)
        {
            if (string.IsNullOrEmpty(value)) return "–";
            int underscore = value.IndexOf('_');
            return underscore >= 0 ? value.Substring(underscore + 1) : value;
        }

        DeviceBinding FindBinding(DeviceRow device)
        {
            if (device == null || string.IsNullOrEmpty(device.Match)) return null;
            foreach (DeviceBinding binding in _config.Devices)
            {
                if (binding == null) continue;
                if (string.Equals(binding.Match, device.Match, StringComparison.OrdinalIgnoreCase))
                    return binding;
            }
            return null;
        }

        void RebuildConfiguredDevices()
        {
            if (_configuredRows == null) return;
            _configuredRows.Children.Clear();

            if (_config.Devices.Count == 0)
            {
                TextBlock empty = Ui.Text("Noch kein Gerät eingerichtet.", 12, Ui.InkMuted);
                empty.Margin = new Thickness(0, 2, 0, 2);
                _configuredRows.Children.Add(empty);
                return;
            }

            for (int i = 0; i < _config.Devices.Count; i++)
            {
                DeviceBinding binding = _config.Devices[i];
                _configuredRows.Children.Add(BuildConfiguredRow(binding, i == _config.Devices.Count - 1));
            }
        }

        FrameworkElement BuildConfiguredRow(DeviceBinding binding, bool last)
        {
            Border border = new Border();
            border.BorderBrush = Ui.Line;
            border.BorderThickness = new Thickness(0, last ? 0 : 1, 0, last ? 0 : 1);
            border.Padding = new Thickness(0, 8, 0, 8);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel info = new StackPanel();
            info.VerticalAlignment = VerticalAlignment.Center;

            TextBlock name = Ui.Text(string.IsNullOrEmpty(binding.Name) ? "Unbenanntes Gerät" : binding.Name,
                12.5, Ui.Ink);
            info.Children.Add(name);

            bool attached = IsAttached(binding);

            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;
            meta.Margin = new Thickness(0, 2, 0, 0);

            TextBlock match = Ui.Text(
                string.IsNullOrEmpty(binding.Match) ? "kein Muster" : binding.Match, 11, Ui.InkMuted);
            meta.Children.Add(match);

            TextBlock state = Ui.Text(attached ? "  ·  angeschlossen" : "  ·  nicht angeschlossen",
                11, attached ? Ui.InkMuted : Ui.Warn);
            meta.Children.Add(state);

            info.Children.Add(meta);
            grid.Children.Add(info);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;

            CheckBox enabled = Check("Aktiv", binding.Enabled, null);
            enabled.Margin = new Thickness(0, 0, 12, 0);
            enabled.Click += delegate
            {
                binding.Enabled = enabled.IsChecked == true;
                Push();
            };
            actions.Children.Add(enabled);

            Button rename = Ui.SmallButton("Umbenennen");
            rename.Margin = new Thickness(0, 0, 6, 0);
            rename.Click += delegate
            {
                string entered = PromptForName(binding.Name);
                if (entered == null) return;
                binding.Name = entered;
                Push();
                RebuildConfiguredDevices();
                RenderDiscoveredDevices();
            };
            actions.Children.Add(rename);

            Button remove = Ui.SmallButton("Entfernen");
            remove.Click += delegate
            {
                _config.Devices.Remove(binding);
                Push();
                RebuildConfiguredDevices();
                RenderDiscoveredDevices();
            };
            actions.Children.Add(remove);

            Grid.SetColumn(actions, 1);
            grid.Children.Add(actions);

            border.Child = grid;
            return border;
        }

        bool IsAttached(DeviceBinding binding)
        {
            if (_discovered == null || string.IsNullOrEmpty(binding.Match)) return false;
            foreach (DeviceRow device in _discovered)
                if (string.Equals(device.Match, binding.Match, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        void AddStandardDevice()
        {
            foreach (DeviceBinding binding in _config.Devices)
            {
                if (binding.Match == "VID_258A&PID_017E")
                {
                    MessageBox.Show("Das Standard-Gerät ist bereits eingerichtet.",
                        "DialMenu", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            DeviceBinding standard = new DeviceBinding();
            standard.Name = "ISY IGK-6500";
            standard.Match = "VID_258A&PID_017E";
            standard.Enabled = true;
            _config.Devices.Add(standard);
            Push();
            RebuildConfiguredDevices();
            RenderDiscoveredDevices();
        }

        // ==================================================================
        // Seite: Darstellung
        // ==================================================================

        static readonly string[] Positions =
        {
            "unten rechts", "unten links", "oben rechts", "oben links",
            "rechts oben", "rechts mitte", "rechts unten",
            "links oben", "links mitte", "links unten",
            "oben mitte", "unten mitte", "mitte", "zeiger"
        };

        static readonly string[] PositionLabels =
        {
            "Unten rechts", "Unten links", "Oben rechts", "Oben links",
            "Rechts oben", "Rechts mitte", "Rechts unten",
            "Links oben", "Links mitte", "Links unten",
            "Oben mitte", "Unten mitte", "Mitte",
            "Am Mauszeiger (Monitor unter dem Zeiger)"
        };

        FrameworkElement BuildAppearancePage()
        {
            StackPanel body = PageBody();
            body.Children.Add(PageHeader("Darstellung",
                "Wo und wie groß der Ring erscheint.", null));

            // Position
            StackPanel positionBlock = new StackPanel();

            SelectControl position = new SelectControl();
            List<string> labels = new List<string>();
            for (int i = 0; i < PositionLabels.Length; i++) labels.Add(PositionLabels[i]);
            position.SetItems(labels);

            int index = IndexOfPosition(_config.Position);
            position.SelectedIndex = index;
            position.Changed += delegate
            {
                int selected = position.SelectedIndex;
                if (selected < 0 || selected >= Positions.Length) return;
                _config.Position = Positions[selected];
                MarkDirty();
            };

            positionBlock.Children.Add(FieldBlock("Position", position, 0));

            TextBlock positionNote = Ui.Text(
                "„Am Mauszeiger“ folgt dem Bildschirm, auf dem sich der Zeiger gerade befindet.",
                11, Ui.InkMuted);
            positionNote.TextWrapping = TextWrapping.Wrap;
            positionNote.Margin = new Thickness(0, 5, 0, 0);
            positionBlock.Children.Add(positionNote);

            body.Children.Add(Card("Position", null, positionBlock));

            // Groesse
            StackPanel scaleBlock = new StackPanel();

            Grid scaleRow = new Grid();
            scaleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            scaleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _scaleSlider = Slider(70, 180, _config.Scale * 100.0, 5);
            _scaleSlider.VerticalAlignment = VerticalAlignment.Center;
            _scaleSlider.ValueChanged += delegate
            {
                if (_scaleValue != null)
                    _scaleValue.Text = Math.Round(_scaleSlider.Value) + " %";
                if (_loading) return;
                _config.Scale = Math.Round(_scaleSlider.Value / 100.0, 3);
                MarkDirty();
            };
            scaleRow.Children.Add(_scaleSlider);

            _scaleValue = Ui.Text(Math.Round(_config.Scale * 100) + " %", 12.5, Ui.Ink);
            _scaleValue.MinWidth = 44;
            _scaleValue.TextAlignment = TextAlignment.Right;
            _scaleValue.VerticalAlignment = VerticalAlignment.Center;
            _scaleValue.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(_scaleValue, 1);
            scaleRow.Children.Add(_scaleValue);

            scaleBlock.Children.Add(scaleRow);

            StackPanel scaleFoot = new StackPanel();
            scaleFoot.Orientation = Orientation.Horizontal;
            scaleFoot.Margin = new Thickness(0, 10, 0, 0);

            Button preview = Ui.SmallButton("Im Overlay ansehen");
            preview.Click += delegate { _app.PreviewOverlay(); };
            scaleFoot.Children.Add(preview);

            TextBlock scaleNote = Ui.Text("70 – 180 %", 11, Ui.InkMuted);
            scaleNote.VerticalAlignment = VerticalAlignment.Center;
            scaleNote.Margin = new Thickness(10, 0, 0, 0);
            scaleFoot.Children.Add(scaleNote);

            scaleBlock.Children.Add(scaleFoot);
            body.Children.Add(Card("Größe", null, scaleBlock));

            // Beschriftungen
            StackPanel labelsBlock = new StackPanel();

            CheckBox showLabels = Check("Beschriftungen im Ring anzeigen", _config.ShowLabels, null);
            showLabels.Click += delegate
            {
                _config.ShowLabels = showLabels.IsChecked == true;
                MarkDirty();
            };
            labelsBlock.Children.Add(showLabels);

            TextBlock labelsNote = Ui.Text(
                "Ohne Beschriftungen bleibt nur das Symbol sichtbar – ruhiger, aber weniger eindeutig.",
                11, Ui.InkMuted);
            labelsNote.TextWrapping = TextWrapping.Wrap;
            labelsNote.Margin = new Thickness(24, 5, 0, 0);
            labelsBlock.Children.Add(labelsNote);

            body.Children.Add(Card("Beschriftungen", null, labelsBlock));

            return ScrollPage(body);
        }

        static int IndexOfPosition(string position)
        {
            if (!string.IsNullOrEmpty(position))
                for (int i = 0; i < Positions.Length; i++)
                    if (string.Equals(Positions[i], position, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        // ==================================================================
        // Seite: Verhalten
        // ==================================================================

        FrameworkElement BuildBehaviorPage()
        {
            StackPanel body = PageBody();
            body.Children.Add(PageHeader("Verhalten",
                "Wann das Menü erscheint, wie lange es bleibt und ob DialMenu mit Windows startet.", null));

            // Allgemein
            StackPanel general = new StackPanel();

            CheckBox enabled = Check("DialMenu aktiv", _config.Enabled, null);
            enabled.Click += delegate
            {
                _config.Enabled = enabled.IsChecked == true;
                MarkDirty();
            };
            general.Children.Add(enabled);

            TextBlock enabledNote = Ui.Text(
                "Deaktiviert bleibt das Programm im Infobereich, reagiert aber nicht auf den Regler.",
                11, Ui.InkMuted);
            enabledNote.TextWrapping = TextWrapping.Wrap;
            enabledNote.Margin = new Thickness(24, 5, 0, 0);
            general.Children.Add(enabledNote);

            body.Children.Add(Card("Allgemein", null, general));

            // Ausblenden
            StackPanel hide = new StackPanel();

            Grid hideRow = new Grid();
            hideRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hideRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Slider hideDelay = Slider(400, 6000, _config.HideDelayMs, 100);
            hideDelay.VerticalAlignment = VerticalAlignment.Center;
            hideRow.Children.Add(hideDelay);

            TextBlock hideValue = Ui.Text(_config.HideDelayMs + " ms", 12.5, Ui.Ink);
            hideValue.MinWidth = 62;
            hideValue.TextAlignment = TextAlignment.Right;
            hideValue.VerticalAlignment = VerticalAlignment.Center;
            hideValue.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(hideValue, 1);
            hideRow.Children.Add(hideValue);

            hideDelay.ValueChanged += delegate
            {
                int value = (int)Math.Round(hideDelay.Value);
                hideValue.Text = value + " ms";
                if (_loading) return;
                _config.HideDelayMs = value;
                MarkDirty();
            };

            hide.Children.Add(FieldBlock("Menü ausblenden nach", hideRow, 0));
            body.Children.Add(Card("Ausblenden",
                "Gilt, solange der Regler nicht gedreht wird.", hide));

            // Langdruck
            StackPanel longPress = new StackPanel();

            Grid longRow = new Grid();
            longRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            longRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Slider longDelay = Slider(250, 3000, _config.LongPressMs, 50);
            longDelay.VerticalAlignment = VerticalAlignment.Center;
            longRow.Children.Add(longDelay);

            TextBlock longValue = Ui.Text(_config.LongPressMs + " ms", 12.5, Ui.Ink);
            longValue.MinWidth = 62;
            longValue.TextAlignment = TextAlignment.Right;
            longValue.VerticalAlignment = VerticalAlignment.Center;
            longValue.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(longValue, 1);
            longRow.Children.Add(longValue);

            longDelay.ValueChanged += delegate
            {
                int value = (int)Math.Round(longDelay.Value);
                longValue.Text = value + " ms";
                if (_loading) return;
                _config.LongPressMs = value;
                MarkDirty();
            };

            longPress.Children.Add(FieldBlock("Langdruck ab", longRow, 0));
            body.Children.Add(Card("Langdruck",
                "Ab dieser Dauer bricht der Druck die Auswahl ab, statt sie zu bestätigen.", longPress));

            // Autostart
            StackPanel startup = new StackPanel();

            CheckBox autostart = Check("Mit Windows starten", _config.StartWithWindows, null);
            autostart.Click += delegate
            {
                _config.StartWithWindows = autostart.IsChecked == true;
                Push();

                string detail;
                Autostart.Apply(_config.StartWithWindows, out detail);

                // Den tatsaechlichen Zustand anzeigen, nicht den gewuenschten.
                RefreshAutostartState();

                if (_autostartDetail != null)
                {
                    _autostartDetail.Text = detail;
                    _autostartDetail.Foreground =
                        detail.StartsWith("Aktiv", StringComparison.Ordinal)
                            || detail.StartsWith("Inaktiv", StringComparison.Ordinal)
                            ? Ui.InkSoft : Ui.Warn;
                }
            };
            startup.Children.Add(autostart);

            _autostartState = Ui.Text("", 11.5, Ui.InkSoft);
            _autostartState.TextWrapping = TextWrapping.Wrap;
            _autostartState.Margin = new Thickness(24, 6, 0, 0);
            startup.Children.Add(_autostartState);

            _autostartDetail = Ui.Text("", 11, Ui.InkMuted);
            _autostartDetail.TextWrapping = TextWrapping.Wrap;
            _autostartDetail.Margin = new Thickness(24, 3, 0, 0);
            startup.Children.Add(_autostartDetail);

            _autostartPath = Ui.Text("", 11, Ui.InkMuted);
            _autostartPath.TextWrapping = TextWrapping.Wrap;
            _autostartPath.Margin = new Thickness(24, 3, 0, 0);
            startup.Children.Add(_autostartPath);

            string command = Autostart.CommandLine();
            TextBlock commandLabel = Ui.Text("Eingetragener Befehl", 11, Ui.InkMuted);
            commandLabel.Margin = new Thickness(24, 12, 0, 3);
            startup.Children.Add(commandLabel);

            TextBox commandField = Ui.TextField();
            commandField.Text = string.IsNullOrEmpty(command) ? "(Programmpfad nicht ermittelbar)" : command;
            commandField.IsReadOnly = true;
            commandField.Margin = new Thickness(24, 0, 0, 0);
            commandField.Background = Ui.Subtle;
            commandField.TextWrapping = TextWrapping.Wrap;
            startup.Children.Add(commandField);

            body.Children.Add(Card("Start",
                "Der Eintrag liegt unter HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run.",
                startup));

            RefreshAutostartState();

            return ScrollPage(body);
        }

        TextBlock _autostartDetail;
        TextBlock _autostartPath;

        void RefreshAutostartState()
        {
            if (_autostartState != null)
            {
                bool registered;
                string state = Autostart.State(out registered);
                _autostartState.Text = state;
                _autostartState.Foreground = registered ? Ui.InkSoft : Ui.InkMuted;
            }

            if (_autostartPath != null)
            {
                string exe = Autostart.ExePath();
                bool exists = !string.IsNullOrEmpty(exe) && File.Exists(exe);
                _autostartPath.Text = string.IsNullOrEmpty(exe)
                    ? "Programmpfad nicht ermittelbar."
                    : exists ? "Programmdatei: " + exe
                    : "Programmdatei fehlt: " + exe;
                _autostartPath.Foreground = exists ? Ui.InkMuted : Ui.Warn;
            }
        }

        // ==================================================================
        // Seite: Ueber
        // ==================================================================

        FrameworkElement BuildAboutPage()
        {
            StackPanel body = PageBody();
            body.Children.Add(PageHeader("Über DialMenu", null, null));

            // Marke
            StackPanel brandBlock = new StackPanel();
            brandBlock.Orientation = Orientation.Horizontal;

            Border mark = new Border();
            mark.Width = 48;
            mark.Height = 48;
            mark.CornerRadius = new CornerRadius(10);
            mark.Background = Tokens.Primary;
            mark.VerticalAlignment = VerticalAlignment.Top;

            // Dieselbe Marke wie links oben, nur hell auf dunklem Feld.
            System.Windows.Controls.Image markIcon = new System.Windows.Controls.Image();
            markIcon.Source = BrandMark.CreateLight();
            markIcon.Width = 26;
            markIcon.Height = 26;
            markIcon.Stretch = Stretch.Uniform;
            markIcon.HorizontalAlignment = HorizontalAlignment.Center;
            markIcon.VerticalAlignment = VerticalAlignment.Center;
            mark.Child = markIcon;
            brandBlock.Children.Add(mark);

            StackPanel brandText = new StackPanel();
            brandText.Margin = new Thickness(14, 2, 0, 0);
            brandText.VerticalAlignment = VerticalAlignment.Center;

            TextBlock product = Ui.Heading("DialMenu", 15);
            brandText.Children.Add(product);

            TextBlock tagline = Ui.Text("Der Drehregler als radiales Menü.", 12, Ui.InkMuted);
            tagline.Margin = new Thickness(0, 2, 0, 0);
            brandText.Children.Add(tagline);

            string version = VersionText();
            TextBlock versionText = Ui.Text(version, 11.5, Ui.InkSoft);
            versionText.Margin = new Thickness(0, 6, 0, 0);
            brandText.Children.Add(versionText);

            brandBlock.Children.Add(brandText);

            Border brandCard = CardBox();
            brandCard.Child = brandBlock;
            body.Children.Add(brandCard);

            // Speicherort
            StackPanel files = new StackPanel();

            TextBlock configLabel = Ui.Text("Konfiguration", 11, Ui.InkMuted);
            files.Children.Add(configLabel);

            TextBox configPath = Ui.TextField();
            configPath.Text = Config.FilePath;
            configPath.IsReadOnly = true;
            configPath.Background = Ui.Subtle;
            configPath.Margin = new Thickness(0, 4, 0, 0);
            files.Children.Add(configPath);

            TextBlock logLabel = Ui.Text("Protokoll", 11, Ui.InkMuted);
            logLabel.Margin = new Thickness(0, 12, 0, 0);
            files.Children.Add(logLabel);

            TextBox logPath = Ui.TextField();
            logPath.Text = Log.FilePath;
            logPath.IsReadOnly = true;
            logPath.Background = Ui.Subtle;
            logPath.Margin = new Thickness(0, 4, 0, 0);
            files.Children.Add(logPath);

            StackPanel fileButtons = new StackPanel();
            fileButtons.Orientation = Orientation.Horizontal;
            fileButtons.Margin = new Thickness(0, 12, 0, 0);

            Button openFolder = Ui.SmallButton("Ordner öffnen");
            openFolder.Click += delegate { OpenPath(Config.Folder); };
            fileButtons.Children.Add(openFolder);

            Button openLog = Ui.SmallButton("Protokoll öffnen");
            openLog.Margin = new Thickness(6, 0, 0, 0);
            openLog.Click += delegate { OpenPath(Log.FilePath); };
            fileButtons.Children.Add(openLog);

            Button copyConfig = Ui.SmallButton("Pfad kopieren");
            copyConfig.Margin = new Thickness(6, 0, 0, 0);
            copyConfig.Click += delegate
            {
                try { Clipboard.SetText(Config.FilePath); }
                catch (Exception ex) { Log.Write("Zwischenablage: " + ex.Message); }
            };
            fileButtons.Children.Add(copyConfig);

            files.Children.Add(fileButtons);
            body.Children.Add(Card("Dateien", null, files));

            // Projekt
            StackPanel project = new StackPanel();

            Grid linkRow = new Grid();
            linkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel linkText = new StackPanel();
            TextBlock linkTitle = Ui.Text("Projektseite", 12.5, Ui.Ink);
            linkText.Children.Add(linkTitle);
            TextBlock linkUrl = Ui.Text("github.com/dialmenu", 11.5, Ui.InkMuted);
            linkUrl.Margin = new Thickness(0, 2, 0, 0);
            linkText.Children.Add(linkUrl);
            linkRow.Children.Add(linkText);

            Button openProject = Ui.SmallButton("Öffnen");
            openProject.IsEnabled = false;
            openProject.ToolTip = "Der Link wird mit der Veröffentlichung ergänzt.";
            Grid.SetColumn(openProject, 1);
            linkRow.Children.Add(openProject);

            project.Children.Add(linkRow);

            TextBlock note = Ui.Text(
                "Die Tastatur selbst wird nicht verändert – DialMenu liest nur die Rohdaten des Reglers.",
                11, Ui.InkMuted);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 14, 0, 0);
            project.Children.Add(note);

            body.Children.Add(Card("Projekt", null, project));

            return ScrollPage(body);
        }

        static string VersionText()
        {
            try
            {
                Assembly asm = Assembly.GetEntryAssembly();
                if (asm != null)
                {
                    Version version = asm.GetName().Version;
                    if (version != null) return "Version " + version.ToString(3);
                }
            }
            catch { }
            return "Version 1.0.0";
        }

        void OpenPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    string folder = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                        path = folder;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Write("Pfad konnte nicht geöffnet werden: " + ex.Message);
            }
        }

        // ==================================================================
        // Fensterverhalten
        // ==================================================================

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Schliessen legt das Fenster nur in den Infobereich.
            if (!_app.IsExiting)
            {
                e.Cancel = true;
                Push();
                Hide();
                return;
            }
            base.OnClosing(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }
    }

    // ==================================================================
    // Symbolauswahl
    // ==================================================================

    public class IconPicker : Window
    {
        readonly string _initial;
        readonly Dictionary<string, Border> _tiles = new Dictionary<string, Border>();
        string _selected;

        ContentControl _preview;
        TextBlock _name;

        public IconPicker(string current)
        {
            _initial = current ?? "";
            _selected = _initial;

            Title = "Symbol wählen";
            Width = 620;
            Height = 560;
            MinWidth = 480;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Ui.Bg;
            FontFamily = UiFont.Family;
            FontSize = 13;
            UseLayoutRounding = true;

            BuildUi();
        }

        public string SelectedName { get { return _selected; } }

        void BuildUi()
        {
            DockPanel root = new DockPanel();
            root.LastChildFill = true;

            // Kopf
            Border header = new Border();
            header.Background = Ui.Card;
            header.BorderBrush = Ui.Line;
            header.BorderThickness = new Thickness(0, 0, 0, 1);
            header.Padding = new Thickness(22, 16, 22, 14);

            StackPanel head = new StackPanel();
            head.Children.Add(Ui.Heading("Symbol", 15));

            TextBlock sub = Ui.Text("Ohne Auswahl bestimmt DialMenu das Symbol nach Art und Ziel.",
                12, Ui.InkMuted);
            sub.Margin = new Thickness(0, 2, 0, 0);
            head.Children.Add(sub);

            header.Child = head;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // Fuss
            Border footer = new Border();
            footer.Background = Ui.Card;
            footer.BorderBrush = Ui.Line;
            footer.BorderThickness = new Thickness(0, 1, 0, 0);
            footer.Padding = new Thickness(22, 12, 22, 12);

            Grid foot = new Grid();
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel chosen = new StackPanel();
            chosen.Orientation = Orientation.Horizontal;
            chosen.VerticalAlignment = VerticalAlignment.Center;

            _preview = new ContentControl();
            _preview.Width = 26;
            _preview.Height = 26;
            _preview.Content = Ui.Glyph(PreviewData(), 22, Ui.Ink);
            chosen.Children.Add(_preview);

            _name = Ui.Text(string.IsNullOrEmpty(_selected) ? "(automatisch)" : _selected, 12, Ui.InkSoft);
            _name.Margin = new Thickness(10, 0, 0, 0);
            _name.VerticalAlignment = VerticalAlignment.Center;
            chosen.Children.Add(_name);

            foot.Children.Add(chosen);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;

            Button clear = Ui.Button("Automatisch");
            clear.Margin = new Thickness(0, 0, 8, 0);
            clear.ToolTip = "Symbol wieder nach Art und Ziel bestimmen lassen.";
            clear.Click += delegate
            {
                _selected = "";
                DialogResult = true;
            };
            buttons.Children.Add(clear);

            Button cancel = Ui.Button("Abbrechen");
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += delegate { DialogResult = false; };
            buttons.Children.Add(cancel);

            Button apply = Ui.PrimaryButton("Übernehmen");
            apply.IsDefault = true;
            apply.Click += delegate { DialogResult = true; };
            buttons.Children.Add(apply);

            Grid.SetColumn(buttons, 1);
            foot.Children.Add(buttons);

            footer.Child = foot;
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            // Raster
            WrapPanel grid = new WrapPanel();
            grid.Margin = new Thickness(18, 14, 18, 14);

            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Content = grid;
            root.Children.Add(scroll);

            List<string> names = new List<string>();
            try
            {
                List<string> all = IconLibrary.AllNames();
                if (all != null) names.AddRange(all);
            }
            catch (Exception ex)
            {
                Log.Write("Symbolbibliothek nicht lesbar: " + ex.Message);
            }

            if (names.Count == 0)
            {
                TextBlock empty = Ui.Text("Keine Symbole verfügbar.", 12, Ui.InkMuted);
                empty.Margin = new Thickness(4, 4, 0, 0);
                grid.Children.Add(empty);
            }

            for (int i = 0; i < names.Count; i++)
                grid.Children.Add(Tile(names[i]));

            Content = root;
        }

        string PreviewData()
        {
            if (string.IsNullOrEmpty(_selected)) return IconLibrary.Gear;
            try
            {
                string data = IconLibrary.Resolve(_selected);
                if (!string.IsNullOrEmpty(data)) return data;
            }
            catch { }
            return IconLibrary.Gear;
        }

        FrameworkElement Tile(string name)
        {
            Border tile = new Border();
            tile.Width = 66;
            tile.Height = 66;
            tile.Margin = new Thickness(4);
            tile.CornerRadius = new CornerRadius(6);
            tile.Background = Ui.Card;
            tile.BorderBrush = name == _selected ? Ui.Accent : Ui.Line;
            tile.BorderThickness = new Thickness(name == _selected ? 1.5 : 1);
            tile.Cursor = Cursors.Hand;
            tile.ToolTip = name;

            StackPanel content = new StackPanel();
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.VerticalAlignment = VerticalAlignment.Center;

            string data = IconLibrary.Gear;
            try
            {
                string resolved = IconLibrary.Resolve(name);
                if (!string.IsNullOrEmpty(resolved)) data = resolved;
            }
            catch { }

            ContentControl icon = new ContentControl();
            icon.Content = Ui.Glyph(data, 21, name == _selected ? Ui.Accent : Ui.InkSoft);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(icon);

            TextBlock label = Ui.Text(name, 9.5, Ui.InkMuted);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 5, 0, 0);
            label.MaxWidth = 58;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            content.Children.Add(label);

            tile.Child = content;

            _tiles[name] = tile;

            tile.MouseEnter += delegate
            {
                if (name != _selected) tile.Background = Ui.Subtle;
            };
            tile.MouseLeave += delegate
            {
                if (name != _selected) tile.Background = Ui.Card;
            };
            tile.MouseLeftButtonUp += delegate
            {
                _selected = name;
                if (_preview != null) _preview.Content = Ui.Glyph(PreviewData(), 22, Ui.Ink);
                if (_name != null) _name.Text = string.IsNullOrEmpty(name) ? "(automatisch)" : name;
                RefreshTiles();
            };
            tile.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount != 2) return;
                _selected = name;
                DialogResult = true;
            };

            return tile;
        }

        void RefreshTiles()
        {
            foreach (KeyValuePair<string, Border> pair in _tiles)
            {
                bool selected = pair.Key == _selected;
                Border tile = pair.Value;
                tile.BorderBrush = selected ? Ui.Accent : Ui.Line;
                tile.BorderThickness = new Thickness(selected ? 1.5 : 1);
                tile.Background = Ui.Card;

                StackPanel content = tile.Child as StackPanel;
                if (content == null || content.Children.Count == 0) continue;

                ContentControl icon = content.Children[0] as ContentControl;
                if (icon == null) continue;

                string data = IconLibrary.Gear;
                try
                {
                    string resolved = IconLibrary.Resolve(pair.Key);
                    if (!string.IsNullOrEmpty(resolved)) data = resolved;
                }
                catch { }
                icon.Content = Ui.Glyph(data, 21, selected ? Ui.Accent : Ui.InkSoft);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; return; }
            base.OnKeyDown(e);
        }
    }
}
