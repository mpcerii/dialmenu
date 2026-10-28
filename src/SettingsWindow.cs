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

        // Ringvorschau rechts neben dem Editor
        Canvas _previewCanvas;
        Border _previewWell;
        TextBlock _previewCount;

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

        // Darstellung: Helligkeit und Ringthema
        Border[] _modeChips;
        Border[] _themeTiles;
        ContentControl[] _themeSwatchHosts;
        TextBlock[] _themeNames;

        // Verhalten
        TextBlock _autostartState;

        // ==================================================================
        // Aufbau
        // ==================================================================

        public SettingsWindow(Config config, DialMenuApp app)
        {
            _config = config;
            _app = app;

            Title = Strings.Get("app.windowTitle");
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
            LocalizeSeededDefaults();
            ShowSection("befehle");
        }

        // Die Beispielbefehle und das erste Profil legt Core.cs mit deutschen
        // Namen an. Beim Start und nach jedem Sprachwechsel werden genau diese
        // Vorgaben in die aktuelle Sprache uebernommen; selbst vergebene Namen
        // bleiben unangetastet.
        void LocalizeSeededDefaults()
        {
            foreach (Profile profile in _config.Profiles)
            {
                if (profile == null) continue;

                if (!string.IsNullOrEmpty(profile.Name)
                    && (Strings.IsTranslated("profiles.defaultName", profile.Name)
                        || string.Equals(profile.Name, "Hauptprofil", StringComparison.Ordinal)))
                {
                    profile.Name = Strings.Get("profiles.defaultName");
                }

                if (profile.Actions == null) continue;
                foreach (ActionItem item in profile.Actions)
                {
                    if (item == null || string.IsNullOrEmpty(item.Label)) continue;
                    string key = SeededLabelKey(item);
                    if (key != null) item.Label = Strings.Get(key);
                }
            }
        }

        // Die von CreateDefault() angelegten Befehle, jeweils mit Art und Ziel.
        // Nur wenn beides uebereinstimmt, gilt die Bezeichnung als Vorgabe und
        // wird uebersetzt - ein selbst benannter Befehl bleibt unangetastet.
        static readonly string[][] SeededCommands =
        {
            new string[] { "Browser", "url", "https://www.google.com" },
            new string[] { "Explorer", "program", "explorer.exe" },
            new string[] { "Terminal", "program", "wt.exe" },
            new string[] { "Rechner", "program", "calc.exe" },
            new string[] { "Einstellungen", "url", "ms-settings:" },
            new string[] { "Task-Manager", "system", "taskmgr" },
            new string[] { "Play / Pause", "media", "playpause" },
            new string[] { "Lauter", "volume", "up" },
            new string[] { "Leiser", "volume", "down" },
            new string[] { "Desktop zeigen", "system", "showdesktop" },
            new string[] { "Sperren", "system", "lock" }
        };

        static string SeededLabelKey(ActionItem item)
        {
            for (int i = 0; i < SeededCommands.Length; i++)
            {
                string[] seeded = SeededCommands[i];
                if (!string.Equals(item.Kind, seeded[1], StringComparison.Ordinal)) continue;
                if (!string.Equals(item.Target, seeded[2], StringComparison.Ordinal)) continue;

                // Der deutsche Name steht nicht in der Tabelle (er ist der
                // Schluessel selbst), deshalb beides pruefen.
                string key = "presets.label." + seeded[0];
                if (!Strings.Has(key)) continue;
                if (string.Equals(item.Label, seeded[0], StringComparison.Ordinal)
                    || Strings.IsTranslated(key, item.Label))
                {
                    return key;
                }
            }
            return null;
        }

        void BuildUi()
        {
            Title = Strings.Get("app.windowTitle");

            // Beim Sprachwechsel laeuft der Aufbau erneut; der Seitenhost
            // haengt dann noch am alten Baum und muss zuerst geloest werden.
            Panel oldParent = LogicalTreeHelper.GetParent(_pageHost) as Panel;
            if (oldParent != null) oldParent.Children.Remove(_pageHost);

            _nav.Clear();

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

            Button preview = Ui.Button(Strings.Get("app.preview"));
            preview.HorizontalAlignment = HorizontalAlignment.Stretch;
            preview.HorizontalContentAlignment = HorizontalAlignment.Center;
            preview.Margin = new Thickness(12, 0, 12, 10);
            preview.Click += delegate { _app.PreviewOverlay(); };
            foot.Children.Add(preview);

            TextBlock note = new TextBlock();
            note.Text = Strings.Get("app.closeNote");
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

            AddNav(nav, "befehle", Strings.Get("nav.commands"), GlyphList);
            AddNav(nav, "profile", Strings.Get("nav.profiles"), GlyphProfile);
            AddNav(nav, "geraete", Strings.Get("nav.devices"), GlyphDial);
            AddNav(nav, "darstellung", Strings.Get("nav.appearance"), IconLibrary.Desktop);
            AddNav(nav, "verhalten", Strings.Get("nav.behavior"), IconLibrary.Gear);
            AddNav(nav, "ueber", Strings.Get("nav.about"), GlyphInfo);

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
            body.Children.Add(PageHeader(Strings.Get("error.pageTitle"), null, null));

            Border card = CardBox();
            StackPanel inner = new StackPanel();
            inner.Margin = new Thickness(18, 16, 18, 18);

            TextBlock head = Ui.Text(Strings.Format("error.pageHead", key), 13, Ui.Ink);
            head.FontWeight = FontWeights.SemiBold;
            inner.Children.Add(head);

            TextBlock message = Ui.Text(ex.Message, 12, Ui.InkSoft);
            message.TextWrapping = TextWrapping.Wrap;
            message.Margin = new Thickness(0, 6, 0, 0);
            inner.Children.Add(message);

            TextBlock hint = Ui.Text(Strings.Get("error.hint"), 11.5, Ui.InkMuted);
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

        static readonly string[] KindKeys =
        {
            "kind.program", "kind.url", "kind.shell", "kind.hotkey", "kind.media",
            "kind.volume", "kind.system", "kind.activate", "kind.discord", "kind.macro"
        };

        FrameworkElement BuildCommandsPage()
        {
            _profile = _config.CurrentProfile;
            if (_profile == null)
            {
                _profile = new Profile();
                _profile.Name = Strings.Get("profiles.defaultName");
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
            titles.Children.Add(Ui.Heading(Strings.Get("commands.title"), 19));
            TextBlock sub = Ui.Text(Strings.Get("commands.subtitle"), 12.5, Ui.InkMuted);
            sub.Margin = new Thickness(0, 3, 0, 0);
            titles.Children.Add(sub);
            headGrid.Children.Add(titles);

            StackPanel profileBox = new StackPanel();
            profileBox.VerticalAlignment = VerticalAlignment.Center;
            profileBox.Margin = new Thickness(16, 0, 0, 0);
            profileBox.MinWidth = 190;

            TextBlock profileCaption = Ui.Text(Strings.Get("commands.profile"), 11, Ui.InkMuted);
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

            // --- Inhalt: Liste, Editor und Ringvorschau ---
            // Drei Spalten: Liste, Editor, Vorschau. Die Vorschau hat eine
            // feste Breite, der Editor bekommt den Rest - bei 1120 px
            // Fensterbreite bleiben ihm 348 px, genug fuer alle Felder.
            Grid body = new Grid();
            body.Margin = new Thickness(24, 0, 24, 26);
            body.MaxWidth = 1000;
            body.HorizontalAlignment = HorizontalAlignment.Stretch;
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PreviewColumnWidth) });
            Grid.SetRow(body, 1);
            page.Children.Add(body);

            body.Children.Add(BuildActionListPane());

            // Editor in der Mitte
            Grid editorFrame = new Grid();
            _editorHost = new StackPanel();

            ScrollViewer editorScroll = new ScrollViewer();
            editorScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            editorScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            editorScroll.Content = _editorHost;
            editorFrame.Children.Add(editorScroll);

            Grid.SetColumn(editorFrame, 2);
            body.Children.Add(editorFrame);

            // Ringvorschau rechts - zeigt denselben Ring wie das Overlay.
            FrameworkElement previewPane = BuildRingPreviewPane();
            Grid.SetColumn(previewPane, 4);
            body.Children.Add(previewPane);

            // In schmalen Fenstern weicht die Vorschau, damit der Editor
            // seine 320 px behaelt. Unter 820 px ist fuer beides kein Platz.
            body.SizeChanged += delegate
            {
                // Beim ersten Layout steht die Breite noch auf 0. Dann bleibt
                // die Vorschau stehen, statt kurz zu verschwinden.
                if (body.ActualWidth <= 0) return;
                bool show = body.ActualWidth >= PreviewMinBodyWidth;
                body.ColumnDefinitions[3].Width = new GridLength(show ? 12 : 0);
                body.ColumnDefinitions[4].Width =
                    new GridLength(show ? PreviewColumnWidth : 0);
                previewPane.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            };

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

            TextBlock caption = Ui.Text(Strings.Get("commands.inMenu"), 11.5, Ui.InkSoft);
            caption.VerticalAlignment = VerticalAlignment.Center;
            listHead.Children.Add(caption);

            _actionCount = Ui.Text("", 11, Ui.InkMuted);
            _actionCount.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_actionCount, 1);
            listHead.Children.Add(_actionCount);

            DockPanel.SetDock(listHead, Dock.Top);
            pane.Children.Add(listHead);

            // Werkzeuge unten. In der schmalen Spalte stehen sie in zwei
            // Zeilen: der Hauptknopf über die volle Breite, darunter die
            // beiden kleinen Knöpfe nebeneinander.
            StackPanel order = new StackPanel();
            order.Margin = new Thickness(0, 8, 0, 0);
            DockPanel.SetDock(order, Dock.Bottom);

            Button add = Ui.PrimaryButton(Strings.Get("commands.add"));
            add.HorizontalAlignment = HorizontalAlignment.Stretch;
            add.HorizontalContentAlignment = HorizontalAlignment.Center;
            add.Click += delegate { AddCommand(); };
            order.Children.Add(add);

            Grid smallRow = new Grid();
            smallRow.Margin = new Thickness(0, 6, 0, 0);
            smallRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            smallRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            smallRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button duplicate = Ui.SmallButton(Strings.Get("commands.duplicate"));
            duplicate.HorizontalAlignment = HorizontalAlignment.Stretch;
            duplicate.HorizontalContentAlignment = HorizontalAlignment.Center;
            duplicate.Click += delegate { DuplicateCommand(); };
            smallRow.Children.Add(duplicate);

            Button remove = Ui.SmallButton(Strings.Get("commands.remove"));
            remove.HorizontalAlignment = HorizontalAlignment.Stretch;
            remove.HorizontalContentAlignment = HorizontalAlignment.Center;
            remove.Margin = new Thickness(6, 0, 0, 0);
            remove.Click += delegate { DeleteCommand(); };
            Grid.SetColumn(remove, 2);
            smallRow.Children.Add(remove);

            order.Children.Add(smallRow);

            pane.Children.Add(order);

            // Reihenfolge direkt unter der Liste
            StackPanel sortRow = new StackPanel();
            sortRow.Orientation = Orientation.Horizontal;
            sortRow.Margin = new Thickness(0, 0, 0, 0);
            DockPanel.SetDock(sortRow, Dock.Bottom);

            Button up = Ui.IconButton("↑", Strings.Get("commands.up"));
            up.Click += delegate { MoveCommand(-1); };
            sortRow.Children.Add(up);

            TextBlock orderCaption = Ui.Text(Strings.Get("commands.order"), 11, Ui.InkMuted);
            orderCaption.VerticalAlignment = VerticalAlignment.Center;
            orderCaption.Margin = new Thickness(8, 0, 8, 0);
            sortRow.Children.Add(orderCaption);

            Button down = Ui.IconButton("↓", Strings.Get("commands.down"));
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
                    _actionCount.Text = count == 0 ? Strings.Get("commands.countEmpty")
                        : count == 1 ? Strings.Get("commands.countOne")
                        : Strings.Format("commands.countMany", count);
                }
            }
            finally
            {
                _loading = false;
                // Jede Änderung an der Liste - Anlegen, Löschen, Sortieren,
                // Umbenennen - läuft hier durch. Die Vorschau hängt deshalb
                // an genau dieser Stelle.
                RefreshRingPreview();
            }
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

            TextBlock label = Ui.Text(string.IsNullOrEmpty(item.Label)
                ? Strings.Get("commands.unnamed") : item.Label, 12.5, Ui.Ink);
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
                if (Kinds[i] == kind) return Strings.Get(KindKeys[i]);
            return string.IsNullOrEmpty(kind) ? Strings.Get("commands.command") : kind;
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
                nameBlock.Children.Add(Ui.Heading(Strings.Get("commands.edit"), 13));

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
                StackPanel labelBlock = FieldBlock(Strings.Get("commands.name"), _fLabel, 16);
                stack.Children.Add(labelBlock);

                _cKind = new SelectControl();
                List<string> kindOptions = new List<string>();
                for (int i = 0; i < KindKeys.Length; i++) kindOptions.Add(Strings.Get(KindKeys[i]));
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
                stack.Children.Add(FieldBlock(Strings.Get("commands.kind"), _cKind, 14));

                _fTarget = Ui.TextField();
                _fTarget.Text = item.Target ?? "";
                _fTarget.TextChanged += delegate
                {
                    if (_editorLoading) return;
                    item.Target = _fTarget.Text;
                    AfterEdit(item, false);
                };
                stack.Children.Add(FieldBlock(Strings.Get("commands.target"), _fTarget, 14));

                _targetHint = Ui.Text("", 11, Ui.InkMuted);
                _targetHint.TextWrapping = TextWrapping.Wrap;
                _targetHint.Margin = new Thickness(0, 5, 0, 0);
                stack.Children.Add(_targetHint);

                _browseBlock = new StackPanel();
                _browseBlock.Orientation = Orientation.Horizontal;
                _browseBlock.Margin = new Thickness(0, 8, 0, 0);

                Button browseFile = Ui.SmallButton(Strings.Get("common.choose"));
                browseFile.Click += delegate { BrowseFile(item); };
                _browseBlock.Children.Add(browseFile);

                Button browseFolder = Ui.SmallButton(Strings.Get("common.folder"));
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
                _argsBlock = FieldBlock(Strings.Get("commands.args"), _fArgs, 14);
                stack.Children.Add(_argsBlock);

                _fHint = Ui.TextField();
                _fHint.Text = item.Hint ?? "";
                _fHint.TextChanged += delegate
                {
                    if (_editorLoading) return;
                    item.Hint = _fHint.Text;
                    AfterEdit(item, true);
                };
                stack.Children.Add(FieldBlock(Strings.Get("commands.hint"), _fHint, 14));

                // Symbol
                stack.Children.Add(BuildIconBlock(item));

                // Makro
                _macroBlock = new StackPanel();
                _macroBlock.Margin = new Thickness(0, 16, 0, 0);

                TextBlock macroCaption = SectionLabel(Strings.Get("commands.macroSection"));
                _macroBlock.Children.Add(macroCaption);

                Grid macroRow = new Grid();
                macroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                macroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                _macroSummary = Ui.Text("", 12, Ui.InkSoft);
                _macroSummary.TextWrapping = TextWrapping.Wrap;
                _macroSummary.VerticalAlignment = VerticalAlignment.Center;
                macroRow.Children.Add(_macroSummary);

                Button editSteps = Ui.Button(Strings.Get("commands.steps"));
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

            TextBlock title = Ui.Heading(Strings.Get("commands.emptyTitle"), 14);
            title.Margin = new Thickness(0, 12, 0, 0);
            title.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(title);

            TextBlock text = Ui.Text(Strings.Get("commands.emptyHint"), 12.5, Ui.InkMuted);
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
            block.Children.Add(SectionLabel(Strings.Get("commands.icon")));

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
                string.IsNullOrEmpty(item.Icon) ? Strings.Get("commands.iconAuto")
                    : Strings.Format("commands.iconChosen", item.Icon),
                12, Ui.InkSoft);
            _iconName.TextTrimming = TextTrimming.CharacterEllipsis;
            iconText.Children.Add(_iconName);

            TextBlock iconNote = Ui.Text(Strings.Get("commands.iconNote"), 11, Ui.InkMuted);
            iconNote.TextWrapping = TextWrapping.Wrap;
            iconText.Children.Add(iconNote);

            block.Children.Add(row);

            // Untere Zeile: Schaltflächen umbrechen, statt die Karte zu sprengen.
            WrapPanel iconButtons = new WrapPanel();
            iconButtons.Margin = new Thickness(0, 10, 0, 0);

            Button pick = Ui.SmallButton(Strings.Get("commands.iconPick"));
            pick.Margin = new Thickness(0, 0, 6, 6);
            pick.Click += delegate { PickIcon(item); };
            iconButtons.Children.Add(pick);

            Button resetIcon = Ui.SmallButton(Strings.Get("commands.iconReset"));
            resetIcon.Margin = new Thickness(0, 0, 0, 6);
            resetIcon.Click += delegate
            {
                item.Icon = "";
                if (_iconPreview != null) _iconPreview.Content = GlyphFor(item, 20, Ui.Ink);
                if (_iconName != null) _iconName.Text = Strings.Get("commands.iconAuto");
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
                    ? Strings.Get("commands.iconAuto")
                    : Strings.Format("commands.iconChosen", item.Icon);
            AfterEdit(item, true);
        }

        static string TargetHintFor(string kind)
        {
            switch (kind)
            {
                case "program": return Strings.Get("commands.target.program");
                case "url": return Strings.Get("commands.target.url");
                case "shell": return Strings.Get("commands.target.shell");
                case "hotkey": return Strings.Get("commands.target.hotkey");
                case "media": return Strings.Get("commands.target.media");
                case "volume": return Strings.Get("commands.target.volume");
                case "system": return Strings.Get("commands.target.system");
                case "activate": return Strings.Get("commands.target.activate");
                case "discord": return Strings.Get("commands.target.discord");
                case "macro": return Strings.Get("commands.target.macro");
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
                _macroSummary.Text = Strings.Get("commands.macroNone");
                return;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < count && i < 4; i++)
            {
                MacroStep step = item.Steps[i];
                switch (step.Type)
                {
                    case "hotkey": parts.Add(string.IsNullOrEmpty(step.Value) ? Strings.Get("commands.stepHotkey") : step.Value); break;
                    case "text": parts.Add(string.IsNullOrEmpty(step.Value) ? Strings.Get("commands.stepText") : "„" + Shorten(step.Value, 14) + "“"); break;
                    case "delay": parts.Add(Strings.Get("commands.stepDelay")); break;
                    case "program": parts.Add(string.IsNullOrEmpty(step.Value) ? Strings.Get("commands.stepProgram") : Shorten(step.Value, 18)); break;
                    default: parts.Add(step.Type); break;
                }
            }

            string summary = string.Join(" · ", parts.ToArray());
            if (count > 4) summary += " · …";
            _macroSummary.Text = (count == 1
                ? Strings.Get("commands.stepsOne")
                : Strings.Format("commands.stepsMany", count)) + ": " + summary;
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

            TextBlock caption = Ui.Text(Strings.Get("commands.checkCaption"), 11.5, Ui.Warn);
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
            // Auch der Weg ohne Listenaufbau muss die Vorschau nachziehen:
            // Symbol und Art ändern sich hierüber, und die Vorschau zeigt
            // beides.
            RefreshRingPreview();
            MarkDirty();
        }

        void MarkDirty()
        {
            _debounce.Stop();
            _debounce.Start();
        }

        // Sprache wechseln: erst die Konfiguration, dann der Neuaufbau.
        // Der gesamte Baum wird neu erzeugt, weil die Texte beim Bauen
        // gelesen werden - einzelne Beschriftungen liessen sich sonst nicht
        // nachziehen. Die gewaehlte Seite bleibt erhalten.
        void SwitchLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) return;

            _config.Language = code;
            Strings.Current = code;

            _debounce.Stop();
            string section = _section;
            ActionItem selected = _selected;

            LocalizeSeededDefaults();
            BuildUi();
            ShowSection(section);

            // Die Auswahl im Befehlseditor ueber den Neuaufbau retten.
            if (section == "befehle" && selected != null && _profile != null)
            {
                int index = _profile.Actions.IndexOf(selected);
                if (index >= 0) SelectAction(index);
            }

            Push();
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
            copy.Label = _selected.Label + Strings.Get("commands.copySuffix");
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
                Strings.Format("commands.deleteQuestion", label),
                Strings.Get("commands.deleteTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
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
                _profile.Name = Strings.Get("profiles.defaultName");
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
            dialog.Title = Strings.Get("commands.browseFileTitle");
            dialog.Filter = Strings.Get("commands.browseFilter");

            if (dialog.ShowDialog(this) != true) return;

            item.Target = dialog.FileName;
            if (_fTarget != null) _fTarget.Text = dialog.FileName;

            if (string.IsNullOrWhiteSpace(item.Label) || Strings.IsTranslated("commands.defaultLabel", item.Label)
                || Strings.IsTranslated("commands.newCommand", item.Label))
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
            dialog.Description = Strings.Get("commands.browseFolder");

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
            body.Children.Add(PageHeader(Strings.Get("profiles.title"),
                Strings.Get("profiles.subtitle"), null));

            StackPanel list = new StackPanel();
            list.Margin = new Thickness(0, 18, 0, 0);

            for (int i = 0; i < _config.Profiles.Count; i++)
            {
                Profile profile = _config.Profiles[i];
                list.Children.Add(BuildProfileRow(profile, i == _config.Profiles.Count - 1));
            }

            body.Children.Add(Card(null, null, list));

            Button create = Ui.Button(Strings.Get("profiles.new"));
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
            string name = Strings.Format("profiles.namePrefix", number);
            bool taken = true;
            while (taken)
            {
                taken = false;
                foreach (Profile p in _config.Profiles)
                    if (string.Equals(p.Name, name, StringComparison.Ordinal)) { taken = true; break; }
                if (taken) { number++; name = Strings.Format("profiles.namePrefix", number); }
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

                TextBlock badgeText = Ui.Text(Strings.Get("profiles.active"), 10, Ui.Accent);
                badgeText.FontWeight = FontWeights.SemiBold;
                badge.Child = badgeText;
                titleLine.Children.Add(badge);
            }

            info.Children.Add(titleLine);

            int count = profile.Actions == null ? 0 : profile.Actions.Count;
            TextBlock detail = Ui.Text(
                count == 1 ? Strings.Get("commands.countOne")
                    : Strings.Format("commands.countMany", count), 11.5, Ui.InkMuted);
            detail.Margin = new Thickness(0, 2, 0, 0);
            info.Children.Add(detail);

            row.Children.Add(info);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;

            if (!active)
            {
                Button activate = Ui.SmallButton(Strings.Get("common.activate"));
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

            Button rename = Ui.SmallButton(Strings.Get("common.rename"));
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

            Button duplicate = Ui.SmallButton(Strings.Get("common.duplicate"));
            duplicate.Margin = new Thickness(0, 0, 6, 0);
            duplicate.Click += delegate
            {
                Profile copy = new Profile();
                copy.Name = profile.Name + Strings.Get("commands.copySuffix");
                copy.Actions = new List<ActionItem>();
                foreach (ActionItem item in profile.Actions) copy.Actions.Add(item.Clone());
                _config.Profiles.Insert(_config.Profiles.IndexOf(profile) + 1, copy);
                Push();
                ShowSection("profile");
            };
            actions.Children.Add(duplicate);

            Button delete = Ui.SmallButton(Strings.Get("common.delete"));
            delete.IsEnabled = _config.Profiles.Count > 1;
            delete.Click += delegate
            {
                if (_config.Profiles.Count <= 1) return;

                MessageBoxResult answer = MessageBox.Show(
                    Strings.Format("profiles.deleteQuestion", profile.Name),
                    Strings.Get("profiles.deleteTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
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
            dialog.Title = Strings.Get("profiles.renameTitle");
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
            body.Children.Add(FieldBlock(Strings.Get("profiles.name"), field, 0));

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 18, 0, 0);

            Button cancel = Ui.Button(Strings.Get("common.cancel"));
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += delegate { dialog.Close(); };
            buttons.Children.Add(cancel);

            Button save = Ui.PrimaryButton(Strings.Get("common.apply"));
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
            body.Children.Add(PageHeader(Strings.Get("devices.title"),
                Strings.Get("devices.subtitle"), null));

            // Angeschlossene Geräte
            StackPanel attached = new StackPanel();

            _deviceStatus = Ui.Text(Strings.Get("devices.searching"), 11.5, Ui.InkMuted);
            _deviceStatus.Margin = new Thickness(0, 0, 0, 8);
            attached.Children.Add(_deviceStatus);

            _attachedRows = new StackPanel();
            attached.Children.Add(_attachedRows);

            body.Children.Add(Card(Strings.Get("devices.attached"), null, attached));

            // Eingerichtete Geräte
            StackPanel configured = new StackPanel();

            _configuredRows = new StackPanel();
            configured.Children.Add(_configuredRows);

            Button addPreset = Ui.Button(Strings.Get("devices.addStandard"));
            addPreset.HorizontalAlignment = HorizontalAlignment.Left;
            addPreset.Margin = new Thickness(0, 12, 0, 0);
            addPreset.Click += delegate { AddStandardDevice(); };
            configured.Children.Add(addPreset);

            body.Children.Add(Card(Strings.Get("devices.configured"),
                Strings.Get("devices.configuredNote"), configured));

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

            if (_deviceStatus != null) _deviceStatus.Text = Strings.Get("devices.searching");

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
                    _deviceStatus.Text = Strings.Format("devices.searchFailed", _discoverError);
                    _deviceStatus.Foreground = Ui.Warn;
                }
                else if (_discovered == null || _discovered.Count == 0)
                {
                    _deviceStatus.Text = Strings.Get("devices.foundNone");
                    _deviceStatus.Foreground = Ui.InkMuted;
                }
                else
                {
                    _deviceStatus.Text = _discovered.Count == 1
                        ? Strings.Get("devices.foundOne")
                        : Strings.Format("devices.foundMany", _discovered.Count);
                    _deviceStatus.Foreground = Ui.InkMuted;
                }
            }

            if (_discovered == null || _discovered.Count == 0)
            {
                TextBlock hint = Ui.Text(Strings.Get("devices.hint"), 12, Ui.InkMuted);
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

            Button rescan = Ui.SmallButton(Strings.Get("devices.rescan"));
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

            AddCell(grid, 0, Strings.Get("devices.headerName"), true);
            AddCell(grid, 1, Strings.Get("devices.headerVendor"), true);
            AddCell(grid, 2, Strings.Get("devices.headerProduct"), true);
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

            string name = string.IsNullOrEmpty(device.Name)
                ? Strings.Get("devices.unnamed") : device.Name;
            TextBlock nameCell = Ui.Text(name, 12.5, Ui.Ink);
            nameCell.TextTrimming = TextTrimming.CharacterEllipsis;
            nameCell.VerticalAlignment = VerticalAlignment.Center;
            // Ohne Abstand stossen lange Geraetenamen an die Herstellerspalte.
            nameCell.Margin = new Thickness(0, 0, 8, 0);
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

            Button action = Ui.SmallButton(configured ? Strings.Get("devices.configuredState")
                : (addressable ? Strings.Get("common.add") : Strings.Get("devices.notSelectable")));
            action.IsEnabled = !configured && addressable;
            if (!addressable)
                action.ToolTip = Strings.Get("devices.notSelectableTip");
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
                TextBlock empty = Ui.Text(Strings.Get("devices.none"), 12, Ui.InkMuted);
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

            TextBlock name = Ui.Text(string.IsNullOrEmpty(binding.Name)
                ? Strings.Get("devices.unnamed") : binding.Name, 12.5, Ui.Ink);
            info.Children.Add(name);

            bool attached = IsAttached(binding);

            StackPanel meta = new StackPanel();
            meta.Orientation = Orientation.Horizontal;
            meta.Margin = new Thickness(0, 2, 0, 0);

            TextBlock match = Ui.Text(
                string.IsNullOrEmpty(binding.Match) ? Strings.Get("devices.noMatch")
                    : binding.Match, 11, Ui.InkMuted);
            meta.Children.Add(match);

            TextBlock state = Ui.Text(attached ? Strings.Get("devices.attachedState")
                : Strings.Get("devices.detachedState"), 11, attached ? Ui.InkMuted : Ui.Warn);
            meta.Children.Add(state);

            info.Children.Add(meta);
            grid.Children.Add(info);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;

            CheckBox enabled = Check(Strings.Get("devices.enabled"), binding.Enabled, null);
            enabled.Margin = new Thickness(0, 0, 12, 0);
            enabled.Click += delegate
            {
                binding.Enabled = enabled.IsChecked == true;
                Push();
            };
            actions.Children.Add(enabled);

            Button rename = Ui.SmallButton(Strings.Get("common.rename"));
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

            Button remove = Ui.SmallButton(Strings.Get("common.remove"));
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
                    MessageBox.Show(Strings.Get("devices.standardAlready"),
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

        static readonly string[] PositionKeys =
        {
            "appearance.pos.bottomRight", "appearance.pos.bottomLeft",
            "appearance.pos.topRight", "appearance.pos.topLeft",
            "appearance.pos.rightTop", "appearance.pos.rightCenter", "appearance.pos.rightBottom",
            "appearance.pos.leftTop", "appearance.pos.leftCenter", "appearance.pos.leftBottom",
            "appearance.pos.topCenter", "appearance.pos.bottomCenter",
            "appearance.pos.center", "appearance.pos.pointer"
        };

        FrameworkElement BuildAppearancePage()
        {
            StackPanel body = PageBody();
            body.Children.Add(PageHeader(Strings.Get("appearance.title"),
                Strings.Get("appearance.subtitle"), null));

            // Position
            StackPanel positionBlock = new StackPanel();

            SelectControl position = new SelectControl();
            List<string> labels = new List<string>();
            for (int i = 0; i < PositionKeys.Length; i++) labels.Add(Strings.Get(PositionKeys[i]));
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

            positionBlock.Children.Add(FieldBlock(Strings.Get("appearance.position"), position, 0));

            TextBlock positionNote = Ui.Text(Strings.Get("appearance.positionNote"), 11, Ui.InkMuted);
            positionNote.TextWrapping = TextWrapping.Wrap;
            positionNote.Margin = new Thickness(0, 5, 0, 0);
            positionBlock.Children.Add(positionNote);

            body.Children.Add(Card(Strings.Get("appearance.position"), null, positionBlock));

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

            Button preview = Ui.SmallButton(Strings.Get("app.preview"));
            preview.Click += delegate { _app.PreviewOverlay(); };
            scaleFoot.Children.Add(preview);

            TextBlock scaleNote = Ui.Text("70 – 180 %", 11, Ui.InkMuted);
            scaleNote.VerticalAlignment = VerticalAlignment.Center;
            scaleNote.Margin = new Thickness(10, 0, 0, 0);
            scaleFoot.Children.Add(scaleNote);

            scaleBlock.Children.Add(scaleFoot);
            body.Children.Add(Card(Strings.Get("appearance.scale"), null, scaleBlock));

            body.Children.Add(BuildBrightnessCard());
            body.Children.Add(BuildRingCard());

            // Beschriftungen
            StackPanel labelsBlock = new StackPanel();

            CheckBox showLabels = Check(Strings.Get("appearance.labels"), _config.ShowLabels, null);
            showLabels.Click += delegate
            {
                _config.ShowLabels = showLabels.IsChecked == true;
                MarkDirty();
            };
            labelsBlock.Children.Add(showLabels);

            TextBlock labelsNote = Ui.Text(Strings.Get("appearance.labelsNote"), 11, Ui.InkMuted);
            labelsNote.TextWrapping = TextWrapping.Wrap;
            labelsNote.Margin = new Thickness(24, 5, 0, 0);
            labelsBlock.Children.Add(labelsNote);

            body.Children.Add(Card(Strings.Get("appearance.labels"), null, labelsBlock));

            return ScrollPage(body);
        }

        static int IndexOfPosition(string position)
        {
            if (!string.IsNullOrEmpty(position))
                for (int i = 0; i < Positions.Length; i++)
                    if (string.Equals(Positions[i], position, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        // ------------------------------------------------ Helligkeit und Ring

        static readonly string[] ModeKeys = { "auto", "dark", "light" };

        static readonly string[] ModeKeyNames =
        {
            "appearance.modeAuto", "appearance.modeDark", "appearance.modeLight"
        };

        // Drei gleich breite Felder. Der gewaehlte Zustand ist allein am
        // dunkleren, etwas staerkeren Rahmen erkennbar - keine Farbflaeche.
        FrameworkElement BuildBrightnessCard()
        {
            StackPanel block = new StackPanel();

            Grid row = new Grid();
            _modeChips = new Border[ModeKeys.Length];

            for (int i = 0; i < ModeKeys.Length; i++)
            {
                string key = ModeKeys[i];

                row.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Border chip = AppearanceChip(key, Strings.Get(ModeKeyNames[i]));
                chip.Margin = new Thickness(0, 0, i == ModeKeys.Length - 1 ? 0 : 8, 0);

                chip.MouseLeftButtonUp += delegate
                {
                    _config.Appearance = key;
                    Push();
                    SyncAppearanceModes();
                    SyncRingTiles();
                };

                Grid.SetColumn(chip, i);
                row.Children.Add(chip);
                _modeChips[i] = chip;
            }

            block.Children.Add(row);

            TextBlock note = Ui.Text(Strings.Get("appearance.brightnessNote"), 11, Ui.InkMuted);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 10, 0, 0);
            block.Children.Add(note);

            SyncAppearanceModes();
            return Card(Strings.Get("appearance.mode"), null, block);
        }

        Border AppearanceChip(string key, string label)
        {
            Border chip = new Border();
            chip.Height = 38;
            chip.CornerRadius = new CornerRadius(6);
            chip.BorderThickness = new Thickness(1);
            chip.BorderBrush = Ui.Line;
            chip.Background = Ui.Subtle;
            chip.Cursor = Cursors.Hand;
            chip.Tag = key;
            chip.Focusable = false;

            TextBlock text = Ui.Text(label, 12.5, Ui.InkSoft);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.TextAlignment = TextAlignment.Center;
            text.TextTrimming = TextTrimming.None;
            chip.Child = text;

            chip.MouseEnter += delegate
            {
                if (!IsModeSelected(key)) chip.Background = Tokens.Hover;
            };
            chip.MouseLeave += delegate
            {
                if (!IsModeSelected(key)) chip.Background = Ui.Subtle;
            };

            return chip;
        }

        bool IsModeSelected(string key)
        {
            string current = string.IsNullOrEmpty(_config.Appearance) ? "auto" : _config.Appearance;
            return string.Equals(current, key, StringComparison.OrdinalIgnoreCase);
        }

        void SyncAppearanceModes()
        {
            if (_modeChips == null) return;

            for (int i = 0; i < _modeChips.Length; i++)
            {
                Border chip = _modeChips[i];
                if (chip == null) continue;

                bool active = IsModeSelected(chip.Tag as string);
                chip.BorderThickness = new Thickness(active ? 1.5 : 1);
                chip.BorderBrush = active ? Ui.Ink : Ui.Line;
                chip.Background = active ? Brushes.White : Ui.Subtle;

                TextBlock text = chip.Child as TextBlock;
                if (text != null)
                {
                    text.Foreground = active ? Ui.Ink : Ui.InkSoft;
                    text.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }
        }

        // Vier Kacheln, je ein Thema. Die Vorschau zeigt immer die Fassung,
        // die der Ring bei der gewaehlten Helligkeit wirklich benutzt.
        FrameworkElement BuildRingCard()
        {
            StackPanel block = new StackPanel();

            Grid row = new Grid();
            int count = RingThemes.All.Count;
            _themeTiles = new Border[count];
            _themeSwatchHosts = new ContentControl[count];
            _themeNames = new TextBlock[count];

            for (int i = 0; i < count; i++)
            {
                RingTheme theme = RingThemes.All[i];

                row.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Border tile = new Border();
                tile.Padding = new Thickness(10, 12, 10, 14);
                tile.CornerRadius = new CornerRadius(8);
                tile.BorderThickness = new Thickness(1);
                tile.BorderBrush = Ui.Line;
                tile.Background = Brushes.Transparent;
                tile.Cursor = Cursors.Hand;
                tile.Margin = new Thickness(0, 0, i == count - 1 ? 0 : 10, 0);
                tile.VerticalAlignment = VerticalAlignment.Stretch;

                StackPanel inner = new StackPanel();

                ContentControl host = new ContentControl();
                host.Width = SwatchSize;
                host.Height = SwatchSize;
                host.HorizontalAlignment = HorizontalAlignment.Center;
                inner.Children.Add(host);

                TextBlock name = Ui.Text(ThemeName(theme), 12.5, Ui.Ink);
                name.HorizontalAlignment = HorizontalAlignment.Center;
                name.TextAlignment = TextAlignment.Center;
                name.Margin = new Thickness(0, 10, 0, 0);
                inner.Children.Add(name);

                TextBlock note = Ui.Text(ThemeNote(theme), 11, Ui.InkMuted);
                note.TextWrapping = TextWrapping.Wrap;
                note.TextAlignment = TextAlignment.Center;
                note.HorizontalAlignment = HorizontalAlignment.Stretch;
                note.Margin = new Thickness(0, 3, 0, 0);
                inner.Children.Add(note);

                tile.Child = inner;

                string id = theme.Id;
                tile.MouseLeftButtonUp += delegate
                {
                    _config.RingTheme = id;
                    Push();
                    SyncRingTiles();
                };
                tile.MouseEnter += delegate
                {
                    tile.Background = Ui.Subtle;
                };
                tile.MouseLeave += delegate
                {
                    // Die Auswahl zeigt sich am Rahmen, nicht an der Flaeche -
                    // deshalb faellt jede Kachel beim Verlassen zurueck.
                    tile.Background = Brushes.Transparent;
                };

                Grid.SetColumn(tile, i);
                row.Children.Add(tile);

                _themeTiles[i] = tile;
                _themeSwatchHosts[i] = host;
                _themeNames[i] = name;
            }

            block.Children.Add(row);

            TextBlock tilesNote = Ui.Text(Strings.Get("appearance.ringNote"), 11, Ui.InkMuted);
            tilesNote.TextWrapping = TextWrapping.Wrap;
            tilesNote.Margin = new Thickness(0, 12, 0, 0);
            block.Children.Add(tilesNote);

            SyncRingTiles();
            return Card(Strings.Get("appearance.ring"), null, block);
        }

        bool IsThemeSelected(string id)
        {
            return string.Equals(_config.RingTheme, id, StringComparison.OrdinalIgnoreCase);
        }

        // Name und Erlaeuterung eines Themas kommen aus der Sprachtabelle;
        // unbekannte Themen fallen auf ihren eigenen Text zurueck.
        static string ThemeName(RingTheme theme)
        {
            string key = "ring." + theme.Id + ".name";
            return Strings.Has(key) ? Strings.Get(key) : theme.Name;
        }

        static string ThemeNote(RingTheme theme)
        {
            string key = "ring." + theme.Id + ".note";
            return Strings.Has(key) ? Strings.Get(key) : theme.Note;
        }

        void SyncRingTiles()
        {
            if (_themeTiles == null || _themeSwatchHosts == null) return;

            bool light = Appearance.IsLight(_config.Appearance);

            for (int i = 0; i < _themeTiles.Length; i++)
            {
                RingTheme baseTheme = RingThemes.All[i];
                RingTheme theme = RingThemes.Get(baseTheme.Id, light);
                bool active = IsThemeSelected(baseTheme.Id);

                _themeSwatchHosts[i].Content = RingSwatch(theme);

                Border tile = _themeTiles[i];
                tile.BorderThickness = new Thickness(active ? 1.5 : 1);
                tile.BorderBrush = active ? Ui.Ink : Ui.Line;
                tile.Background = Brushes.Transparent;

                if (_themeNames[i] != null)
                    _themeNames[i].FontWeight = active
                        ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        const double SwatchSize = 72;

        // Der Ring als kleines Bild: acht Segmente auf einem Grund in der
        // Farbe des Themas, ein Segment aktiv, in der Mitte die Narbe.
        static FrameworkElement RingSwatch(RingTheme theme)
        {
            Border well = new Border();
            well.Width = SwatchSize;
            well.Height = SwatchSize;
            well.CornerRadius = new CornerRadius(10);
            well.Padding = new Thickness(3);
            well.Background = Solid(theme.Ground);
            well.BorderBrush = Ui.FieldLine;
            well.BorderThickness = new Thickness(1);

            double canvasSize = SwatchSize - 8;
            double centre = canvasSize / 2.0;
            double outer = centre - 2;
            double inner = outer * 0.44;
            System.Windows.Point middle = new System.Windows.Point(centre, centre);

            Canvas canvas = new Canvas();
            canvas.Width = canvasSize;
            canvas.Height = canvasSize;

            const int segments = 8;
            const double gap = 5.0;
            int active = 1;

            for (int i = 0; i < segments; i++)
            {
                double start = -90 + i * (360.0 / segments) + gap / 2.0;
                double end = -90 + (i + 1) * (360.0 / segments) - gap / 2.0;

                bool hot = i == active;

                Path segment = new Path();
                segment.Data = RingSegment(middle, inner, outer, start, end);
                segment.Fill = Vertical(
                    hot ? theme.ActiveTop : theme.IdleTop,
                    hot ? theme.ActiveBottom : theme.IdleBottom);
                segment.Stroke = Solid(hot ? theme.ActiveStroke : theme.IdleStrokeTop);
                segment.StrokeThickness = hot ? 1.1 : 0.8;
                segment.SnapsToDevicePixels = false;
                canvas.Children.Add(segment);
            }

            Ellipse hub = new Ellipse();
            hub.Width = inner * 2;
            hub.Height = inner * 2;
            hub.Fill = Vertical(theme.CenterOuter, theme.CenterInner);
            hub.Stroke = Solid(theme.CenterRimTop);
            hub.StrokeThickness = 1;
            Canvas.SetLeft(hub, centre - inner);
            Canvas.SetTop(hub, centre - inner);
            canvas.Children.Add(hub);

            Ellipse dot = new Ellipse();
            dot.Width = 5;
            dot.Height = 5;
            dot.Fill = Solid(theme.IconIdle);
            Canvas.SetLeft(dot, centre - 2.5);
            Canvas.SetTop(dot, centre - 2.5);
            canvas.Children.Add(dot);

            well.Child = canvas;
            return well;
        }

        // Ein Ringsegment zwischen zwei Radien, im Uhrzeigersinn.
        static Geometry RingSegment(System.Windows.Point centre, double inner,
            double outer, double startDegrees, double endDegrees)
        {
            bool large = Math.Abs(endDegrees - startDegrees) > 180;

            PathFigure figure = new PathFigure();
            figure.StartPoint = Polar(centre, outer, startDegrees);
            figure.IsClosed = true;
            figure.IsFilled = true;

            ArcSegment outside = new ArcSegment();
            outside.Point = Polar(centre, outer, endDegrees);
            outside.Size = new System.Windows.Size(outer, outer);
            outside.IsLargeArc = large;
            outside.SweepDirection = SweepDirection.Clockwise;
            figure.Segments.Add(outside);

            LineSegment edge = new LineSegment();
            edge.Point = Polar(centre, inner, endDegrees);
            figure.Segments.Add(edge);

            ArcSegment inside = new ArcSegment();
            inside.Point = Polar(centre, inner, startDegrees);
            inside.Size = new System.Windows.Size(inner, inner);
            inside.IsLargeArc = large;
            inside.SweepDirection = SweepDirection.Counterclockwise;
            figure.Segments.Add(inside);

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        static System.Windows.Point Polar(System.Windows.Point centre, double radius, double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            return new System.Windows.Point(
                centre.X + radius * Math.Cos(radians),
                centre.Y + radius * Math.Sin(radians));
        }

        static LinearGradientBrush Vertical(Color top, Color bottom)
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new System.Windows.Point(0, 0);
            brush.EndPoint = new System.Windows.Point(0, 1);
            brush.GradientStops.Add(new GradientStop(top, 0));
            brush.GradientStops.Add(new GradientStop(bottom, 1));
            brush.Freeze();
            return brush;
        }

        static SolidColorBrush Solid(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        // ==================================================================
        // Seite: Verhalten
        // ==================================================================

        FrameworkElement BuildBehaviorPage()
        {
            StackPanel body = PageBody();
            body.Children.Add(PageHeader(Strings.Get("behavior.title"),
                Strings.Get("behavior.subtitle"), null));

            // Allgemein
            StackPanel general = new StackPanel();

            CheckBox enabled = Check(Strings.Get("behavior.enabled"), _config.Enabled, null);
            enabled.Click += delegate
            {
                _config.Enabled = enabled.IsChecked == true;
                MarkDirty();
            };
            general.Children.Add(enabled);

            TextBlock enabledNote = Ui.Text(Strings.Get("behavior.enabledNote"), 11, Ui.InkMuted);
            enabledNote.TextWrapping = TextWrapping.Wrap;
            enabledNote.Margin = new Thickness(24, 5, 0, 0);
            general.Children.Add(enabledNote);

            // Sprache: die Auswahl wirkt sofort, der Aufbau laeuft danach neu.
            TextBlock languageCaption = Ui.Text(Strings.Get("behavior.language"), 11, Ui.InkMuted);
            languageCaption.Margin = new Thickness(0, 16, 0, 4);
            general.Children.Add(languageCaption);

            List<string> codes = Strings.Available;
            List<string> names = new List<string>();
            for (int i = 0; i < codes.Count; i++) names.Add(Strings.Name(codes[i]));

            SelectControl language = new SelectControl();
            language.SetItems(names);
            for (int i = 0; i < codes.Count; i++)
                if (codes[i] == _config.Language) language.SelectedIndex = i;
            language.Changed += delegate
            {
                int index = language.SelectedIndex;
                if (index < 0 || index >= codes.Count) return;
                if (codes[index] == _config.Language) return;
                SwitchLanguage(codes[index]);
            };
            general.Children.Add(language);

            TextBlock languageNote = Ui.Text(Strings.Get("behavior.languageNote"), 11, Ui.InkMuted);
            languageNote.TextWrapping = TextWrapping.Wrap;
            languageNote.Margin = new Thickness(0, 5, 0, 0);
            general.Children.Add(languageNote);

            body.Children.Add(Card(Strings.Get("behavior.general"), null, general));

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

            hide.Children.Add(FieldBlock(Strings.Get("behavior.hideAfter"), hideRow, 0));
            body.Children.Add(Card(Strings.Get("behavior.hideTitle"),
                Strings.Get("behavior.hideNote"), hide));

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

            longPress.Children.Add(FieldBlock(Strings.Get("behavior.longPress"), longRow, 0));
            body.Children.Add(Card(Strings.Get("behavior.longPressTitle"),
                Strings.Get("behavior.longPressNote"), longPress));

            // Autostart
            StackPanel startup = new StackPanel();

            CheckBox autostart = Check(Strings.Get("behavior.autostart"), _config.StartWithWindows, null);
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
                        detail.StartsWith(Strings.Get("autostart.active"), StringComparison.Ordinal)
                            || detail.StartsWith(Strings.Get("autostart.inactive"), StringComparison.Ordinal)
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
            TextBlock commandLabel = Ui.Text(Strings.Get("behavior.commandLabel"), 11, Ui.InkMuted);
            commandLabel.Margin = new Thickness(24, 12, 0, 3);
            startup.Children.Add(commandLabel);

            TextBox commandField = Ui.TextField();
            commandField.Text = string.IsNullOrEmpty(command)
                ? Strings.Get("behavior.commandMissing") : command;
            commandField.IsReadOnly = true;
            commandField.Margin = new Thickness(24, 0, 0, 0);
            commandField.Background = Ui.Subtle;
            commandField.TextWrapping = TextWrapping.Wrap;
            startup.Children.Add(commandField);

            body.Children.Add(Card(Strings.Get("behavior.startTitle"),
                Strings.Get("behavior.startNote"),
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
                    ? Strings.Get("autostart.pathMissing")
                    : exists ? Strings.Format("autostart.exe", exe)
                    : Strings.Format("autostart.exeMissing", exe);
                _autostartPath.Foreground = exists ? Ui.InkMuted : Ui.Warn;
            }
        }

        // ==================================================================
        // Seite: Ueber
        // ==================================================================

        FrameworkElement BuildAboutPage()
        {
            StackPanel body = PageBody();
            body.Children.Add(PageHeader(Strings.Get("about.title"), null, null));

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

            TextBlock tagline = Ui.Text(Strings.Get("about.tagline"), 12, Ui.InkMuted);
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

            TextBlock configLabel = Ui.Text(Strings.Get("about.config"), 11, Ui.InkMuted);
            files.Children.Add(configLabel);

            TextBox configPath = Ui.TextField();
            configPath.Text = Config.FilePath;
            configPath.IsReadOnly = true;
            configPath.Background = Ui.Subtle;
            configPath.Margin = new Thickness(0, 4, 0, 0);
            files.Children.Add(configPath);

            TextBlock logLabel = Ui.Text(Strings.Get("about.log"), 11, Ui.InkMuted);
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

            Button openFolder = Ui.SmallButton(Strings.Get("about.openFolder"));
            openFolder.Click += delegate { OpenPath(Config.Folder); };
            fileButtons.Children.Add(openFolder);

            Button openLog = Ui.SmallButton(Strings.Get("about.openLog"));
            openLog.Margin = new Thickness(6, 0, 0, 0);
            openLog.Click += delegate { OpenPath(Log.FilePath); };
            fileButtons.Children.Add(openLog);

            Button copyConfig = Ui.SmallButton(Strings.Get("about.copyPath"));
            copyConfig.Margin = new Thickness(6, 0, 0, 0);
            copyConfig.Click += delegate
            {
                try { Clipboard.SetText(Config.FilePath); }
                catch (Exception ex) { Log.Write("Zwischenablage: " + ex.Message); }
            };
            fileButtons.Children.Add(copyConfig);

            files.Children.Add(fileButtons);
            body.Children.Add(Card(Strings.Get("about.files"), null, files));

            // Projekt
            StackPanel project = new StackPanel();

            Grid linkRow = new Grid();
            linkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel linkText = new StackPanel();
            TextBlock linkTitle = Ui.Text(Strings.Get("about.projectLink"), 12.5, Ui.Ink);
            linkText.Children.Add(linkTitle);
            TextBlock linkUrl = Ui.Text("github.com/dialmenu", 11.5, Ui.InkMuted);
            linkUrl.Margin = new Thickness(0, 2, 0, 0);
            linkText.Children.Add(linkUrl);
            linkRow.Children.Add(linkText);

            Button openProject = Ui.SmallButton(Strings.Get("about.open"));
            openProject.IsEnabled = false;
            openProject.ToolTip = Strings.Get("about.linkPending");
            Grid.SetColumn(openProject, 1);
            linkRow.Children.Add(openProject);

            project.Children.Add(linkRow);

            TextBlock note = Ui.Text(Strings.Get("about.projectNote"), 11, Ui.InkMuted);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 14, 0, 0);
            project.Children.Add(note);

            body.Children.Add(Card(Strings.Get("about.project"), null, project));

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
        // Ringvorschau auf der Seite "Befehle"
        // ==================================================================

        // Feste Breite der rechten Spalte. Sie zeigt denselben Ring wie das
        // Overlay, nur klein und unbeweglich - zum Ansehen, nicht zum Klicken.
        const double PreviewColumnWidth = 260;
        // Unterhalb dieser Breite weicht die Vorschau, damit der Editor seine
        // 320 px behaelt: 220 Liste + 12 + 320 Editor + 12 + 260 Vorschau.
        const double PreviewMinBodyWidth = 824;

        // Aussenradius im Zeichenfeld; das Overlay rechnet mit 122 bei
        // Groesse 100 %, das sind hier rund 85 %.
        const double PreviewOuter = 100;
        const double PreviewPad = 12;
        const int PreviewMaxSegments = 12;

        FrameworkElement BuildRingPreviewPane()
        {
            StackPanel pane = new StackPanel();
            pane.VerticalAlignment = VerticalAlignment.Top;

            double inner = PreviewColumnWidth - PreviewPad * 2 - 2;

            _previewWell = new Border();
            _previewWell.Width = PreviewColumnWidth;
            _previewWell.Height = PreviewColumnWidth;
            _previewWell.Padding = new Thickness(PreviewPad);
            _previewWell.CornerRadius = new CornerRadius(10);
            _previewWell.BorderBrush = Ui.Line;
            _previewWell.BorderThickness = new Thickness(1);
            _previewWell.Background = Ui.Subtle;

            _previewCanvas = new Canvas();
            _previewCanvas.Width = inner;
            _previewCanvas.Height = inner;
            _previewCanvas.IsHitTestVisible = false;
            _previewWell.Child = _previewCanvas;
            pane.Children.Add(_previewWell);

            TextBlock caption = Ui.Text(Strings.Get("commands.preview"), 11.5, Ui.InkSoft);
            caption.Margin = new Thickness(2, 10, 0, 0);
            caption.TextWrapping = TextWrapping.Wrap;
            pane.Children.Add(caption);

            _previewCount = Ui.Text("", 11, Ui.InkMuted);
            _previewCount.Margin = new Thickness(2, 3, 0, 0);
            _previewCount.TextTrimming = TextTrimming.CharacterEllipsis;
            pane.Children.Add(_previewCount);

            return pane;
        }

        // Zeichnet die Vorschau neu. Wird von RefreshActionList und von
        // AfterEdit gerufen, also nach jeder Änderung an der Liste oder am
        // bearbeiteten Befehl.
        void RefreshRingPreview()
        {
            Canvas canvas = _previewCanvas;
            if (canvas == null) return;

            canvas.Children.Clear();

            RingTheme theme = RingThemes.Get(_config.RingTheme, Appearance.IsLight(_config.Appearance));
            if (_previewWell != null)
            {
                _previewWell.Background = Solid(theme.Ground);
                _previewWell.BorderBrush = Solid(theme.IdleStrokeTop);
            }

            List<ActionItem> items = _profile != null && _profile.Actions != null
                ? _profile.Actions : new List<ActionItem>();
            int total = items.Count;
            int n = Math.Min(total, PreviewMaxSegments);

            double size = canvas.Width;
            double cx = size / 2.0;
            double cy = size / 2.0;
            double rOut = PreviewOuter;
            // Dicke und Radien wie im Overlay: 74 zu 122.
            double rIn = rOut * (74.0 / 122.0);
            double rMid = (rIn + rOut) / 2.0;

            // Narbe: im Overlay ist sie rIn - 5 bei rIn = 74, hier im
            // gleichen Verhältnis.
            double hubR = rIn * (69.0 / 74.0);
            double iconSize = Math.Max(12, 24 * 0.82 * (rOut - rIn) / 48.0);

            // Grundring: die dunkle Fuge zwischen den Segmenten.
            Path ground = new Path();
            ground.Data = PreviewDonut(cx, cy, rIn - 3, rOut + 3);
            ground.Fill = Solid(theme.Ground);
            ground.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 16,
                ShadowDepth = 4,
                Direction = 270,
                Opacity = theme.IsLight ? 0.26 : 0.60
            };
            canvas.Children.Add(ground);

            // Lichtkanten aussen und innen.
            Path rimOuter = new Path();
            rimOuter.Data = new EllipseGeometry(new System.Windows.Point(cx, cy), rOut + 1.6, rOut + 1.6);
            rimOuter.Stroke = Solid(theme.RimOuter);
            rimOuter.StrokeThickness = 0.8;
            canvas.Children.Add(rimOuter);

            Path rimInner = new Path();
            rimInner.Data = new EllipseGeometry(new System.Windows.Point(cx, cy), rIn - 1.4, rIn - 1.4);
            rimInner.Stroke = Solid(theme.RimInner);
            rimInner.StrokeThickness = 0.7;
            canvas.Children.Add(rimInner);

            Brush idleFill = Vertical(theme.IdleTop, theme.IdleBottom);
            Brush idleStroke = Vertical(theme.IdleStrokeTop, theme.IdleStrokeBottom);
            Brush gloss = Vertical(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF),
                                   Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));

            if (n > 0)
            {
                double step = 360.0 / n;
                // Feste Fugenbreite auf dem mittleren Radius, wie im Overlay;
                // bei vielen Segmenten bleibt ein Rest Abstand.
                double gapDeg = Math.Min(step * 0.26, 2.4 / (rMid * Math.PI / 180.0));

                for (int i = 0; i < n; i++)
                {
                    double a0 = -90 + i * step + gapDeg / 2.0;
                    double a1 = -90 + (i + 1) * step - gapDeg / 2.0;

                    Geometry geo = n == 1
                        ? PreviewDonut(cx, cy, rIn, rOut)
                        : PreviewSector(cx, cy, rIn, rOut, a0, a1, 3.4);

                    bool active = i == 0;

                    Path body = new Path();
                    body.Data = geo;
                    body.Fill = active ? Vertical(theme.ActiveTop, theme.ActiveBottom) : idleFill;
                    body.Stroke = active ? Solid(theme.ActiveStroke) : idleStroke;
                    body.StrokeThickness = active ? 1.4 : 0.9;
                    if (active)
                    {
                        // Der Schein des ausgewaehlten Segments: im Dunkeln
                        // weiss, im Hellen ein weicher Schatten.
                        body.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = theme.GlowColor,
                            BlurRadius = 14,
                            ShadowDepth = 0,
                            Opacity = theme.GlowOpacity
                        };
                    }
                    canvas.Children.Add(body);

                    // Glasreflex über der Ruhefläche; auf dem aktiven
                    // Segment liegt er unter der Auswahl und bleibt unsichtbar.
                    if (!active)
                    {
                        Path sheen = new Path();
                        sheen.Data = geo;
                        sheen.Fill = gloss;
                        sheen.IsHitTestVisible = false;
                        canvas.Children.Add(sheen);
                    }

                    // Symbol in der Mitte des Segments.
                    double mid = -90 + (i + 0.5) * step;
                    double radians = mid * Math.PI / 180.0;
                    double px = cx + rMid * Math.Cos(radians);
                    double py = cy + rMid * Math.Sin(radians);

                    Viewbox box = new Viewbox();
                    box.Width = iconSize;
                    box.Height = iconSize;
                    box.Child = PreviewIcon(
                        IconLibrary.For(items[i]),
                        Solid(active ? theme.IconActive : theme.IconIdle),
                        active ? 1.75 : 1.6);
                    Canvas.SetLeft(box, px - iconSize / 2.0);
                    Canvas.SetTop(box, py - iconSize / 2.0);
                    canvas.Children.Add(box);
                }
            }

            BuildPreviewHub(canvas, theme, cx, cy, hubR, items, total);
            UpdatePreviewCaption(total, n);
        }

        // Die Narbe: eingelassener Kreis mit Symbol und Namen des ersten
        // Befehls, darunter der Zaehler - wie im Overlay.
        void BuildPreviewHub(Canvas canvas, RingTheme theme, double cx, double cy,
                             double hubR, List<ActionItem> items, int total)
        {
            double d = hubR * 2.0;

            Grid host = new Grid();
            host.Width = d;
            host.Height = d;
            host.IsHitTestVisible = false;

            Ellipse body = new Ellipse();
            RadialGradientBrush fill = new RadialGradientBrush(theme.CenterOuter, theme.CenterInner);
            fill.Freeze();
            body.Fill = fill;
            host.Children.Add(body);

            Ellipse sheen = new Ellipse();
            sheen.Fill = Vertical(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF),
                                  Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
            host.Children.Add(sheen);

            Ellipse rim = new Ellipse();
            rim.Stroke = Vertical(theme.CenterRimTop, theme.CenterRimBottom);
            rim.StrokeThickness = 1.1;
            host.Children.Add(rim);

            Ellipse inner = new Ellipse();
            inner.Margin = new Thickness(5);
            inner.Stroke = new SolidColorBrush(Color.FromArgb(0x2E, 0x00, 0x00, 0x00));
            inner.StrokeThickness = 2.6;
            host.Children.Add(inner);

            StackPanel content = new StackPanel();
            content.VerticalAlignment = VerticalAlignment.Center;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.Margin = new Thickness(10, 0, 10, 0);

            ActionItem first = items.Count > 0 ? items[0] : null;
            string iconData = first != null ? IconLibrary.For(first) : IconLibrary.Gear;
            double iconSide = Math.Max(18, hubR * 0.52);

            Viewbox box = new Viewbox();
            box.Width = iconSide;
            box.Height = iconSide;
            box.Child = PreviewIcon(iconData, Solid(first != null ? theme.IconActive : theme.TextMuted), 1.55);
            box.HorizontalAlignment = HorizontalAlignment.Center;
            box.Margin = new Thickness(0, 0, 0, 2);
            content.Children.Add(box);

            TextBlock name = new TextBlock();
            name.FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI");
            name.FontSize = Math.Max(9.5, hubR * 0.215);
            name.FontWeight = FontWeights.SemiBold;
            name.Foreground = Solid(theme.Text);
            name.TextAlignment = TextAlignment.Center;
            name.TextWrapping = TextWrapping.Wrap;
            name.MaxWidth = hubR * 1.62;
            name.LineHeight = name.FontSize * 1.25;
            name.Text = first != null
                ? (string.IsNullOrEmpty(first.Label) ? Strings.Get("commands.unnamed") : first.Label)
                : Strings.Get("commands.previewEmpty");
            content.Children.Add(name);

            if (total > 0)
            {
                TextBlock counter = new TextBlock();
                counter.FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
                counter.FontSize = Math.Max(8, hubR * 0.135);
                counter.Foreground = Solid(theme.TextMuted);
                counter.TextAlignment = TextAlignment.Center;
                counter.Margin = new Thickness(0, 4, 0, 0);
                counter.Text = string.Format(CultureInfo.InvariantCulture,
                    "{0:00} / {1:00}", 1, total);
                content.Children.Add(counter);
            }

            host.Children.Add(content);

            Canvas.SetLeft(host, cx - hubR);
            Canvas.SetTop(host, cy - hubR);
            canvas.Children.Add(host);
        }

        void UpdatePreviewCaption(int total, int shown)
        {
            if (_previewCount == null) return;

            // Ohne Befehle steht der Hinweis schon in der Mitte - die Zeile
            // darunter bleibt dann leer statt "leer" zu doppeln.
            if (total == 0)
            {
                _previewCount.Text = "";
                return;
            }

            string text = total == 1 ? Strings.Get("commands.countOne")
                : Strings.Format("commands.countMany", total);

            int hidden = total - shown;
            if (hidden > 0)
                text += "  ·  " + Strings.Format("commands.previewMore", hidden);

            _previewCount.Text = text;
        }

        // Kreisausschnitt mit abgerundeten Ecken - dieselbe Konstruktion wie
        // im Overlay, hier als eigene, kleine Fassung.
        static Geometry PreviewSector(double cx, double cy, double rIn, double rOut,
                                      double a0, double a1, double cr)
        {
            double sweep = a1 - a0;
            double maxRadial = (rOut - rIn) / 2.0;
            double maxArc = (rOut * Math.Abs(sweep) * Math.PI / 180.0) / 2.0;
            cr = Math.Max(0, Math.Min(cr, Math.Min(maxRadial, maxArc) * 0.92));

            System.Windows.Point A = PreviewPolar(cx, cy, rOut, a0);
            System.Windows.Point B = PreviewPolar(cx, cy, rOut, a1);
            System.Windows.Point C = PreviewPolar(cx, cy, rIn, a1);
            System.Windows.Point D = PreviewPolar(cx, cy, rIn, a0);

            Vector t0 = PreviewTangent(a0), t1 = PreviewTangent(a1);
            Vector rad0 = PreviewRadial(a0), rad1 = PreviewRadial(a1);

            System.Windows.Point A1 = A - rad0 * cr;
            System.Windows.Point A2 = A + t0 * cr;
            System.Windows.Point B1 = B - t1 * cr;
            System.Windows.Point B2 = B - rad1 * cr;
            System.Windows.Point C1 = C + rad1 * cr;
            System.Windows.Point C2 = C - t1 * cr;
            System.Windows.Point D1 = D + t0 * cr;
            System.Windows.Point D2 = D + rad0 * cr;

            bool large = Math.Abs(sweep) > 180;

            PathFigure figure = new PathFigure();
            figure.StartPoint = A2;
            figure.IsClosed = true;
            figure.Segments.Add(new ArcSegment(B1, new System.Windows.Size(rOut, rOut), 0, large, SweepDirection.Clockwise, true));
            figure.Segments.Add(new QuadraticBezierSegment(B, B2, true));
            figure.Segments.Add(new LineSegment(C1, true));
            figure.Segments.Add(new QuadraticBezierSegment(C, C2, true));
            figure.Segments.Add(new ArcSegment(D1, new System.Windows.Size(rIn, rIn), 0, large, SweepDirection.Counterclockwise, true));
            figure.Segments.Add(new QuadraticBezierSegment(D, D2, true));
            figure.Segments.Add(new LineSegment(A1, true));
            figure.Segments.Add(new QuadraticBezierSegment(A, A2, true));

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        static Geometry PreviewDonut(double cx, double cy, double rIn, double rOut)
        {
            GeometryGroup group = new GeometryGroup();
            group.FillRule = FillRule.EvenOdd;
            group.Children.Add(new EllipseGeometry(new System.Windows.Point(cx, cy), rOut, rOut));
            group.Children.Add(new EllipseGeometry(new System.Windows.Point(cx, cy), rIn, rIn));
            group.Freeze();
            return group;
        }

        static System.Windows.Point PreviewPolar(double cx, double cy, double r, double deg)
        {
            double a = deg * Math.PI / 180.0;
            return new System.Windows.Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }

        static Vector PreviewRadial(double deg)
        {
            double a = deg * Math.PI / 180.0;
            return new Vector(Math.Cos(a), Math.Sin(a));
        }

        static Vector PreviewTangent(double deg)
        {
            double a = deg * Math.PI / 180.0;
            return new Vector(-Math.Sin(a), Math.Cos(a));
        }

        static Path PreviewIcon(string data, Brush stroke, double thickness)
        {
            Path path = new Path();
            try
            {
                path.Data = Geometry.Parse(data);
            }
            catch
            {
                try { path.Data = Geometry.Parse(IconLibrary.Gear); }
                catch { path.Data = Geometry.Empty; }
            }
            path.Stroke = stroke;
            path.StrokeThickness = thickness;
            path.StrokeLineJoin = PenLineJoin.Round;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            path.Fill = Brushes.Transparent;
            return path;
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

            Title = Strings.Get("icon.title");
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
            head.Children.Add(Ui.Heading(Strings.Get("commands.icon"), 15));

            TextBlock sub = Ui.Text(Strings.Get("icon.subtitle"), 12, Ui.InkMuted);
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

            _name = Ui.Text(string.IsNullOrEmpty(_selected)
                ? Strings.Get("icon.automatic") : _selected, 12, Ui.InkSoft);
            _name.Margin = new Thickness(10, 0, 0, 0);
            _name.VerticalAlignment = VerticalAlignment.Center;
            chosen.Children.Add(_name);

            foot.Children.Add(chosen);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;

            Button clear = Ui.Button(Strings.Get("icon.auto"));
            clear.Margin = new Thickness(0, 0, 8, 0);
            clear.ToolTip = Strings.Get("icon.autoTip");
            clear.Click += delegate
            {
                _selected = "";
                DialogResult = true;
            };
            buttons.Children.Add(clear);

            Button cancel = Ui.Button(Strings.Get("common.cancel"));
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += delegate { DialogResult = false; };
            buttons.Children.Add(cancel);

            Button apply = Ui.PrimaryButton(Strings.Get("common.apply"));
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
                TextBlock empty = Ui.Text(Strings.Get("icon.empty"), 12, Ui.InkMuted);
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
                if (_name != null) _name.Text = string.IsNullOrEmpty(name)
                    ? Strings.Get("icon.automatic") : name;
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
