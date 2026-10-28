using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;

namespace DialMenu
{
    // Auswahl beim Hinzufuegen eines Befehls.
    //
    // Oben die kuratierten Befehle nach Gruppen, darunter echte, auf diesem
    // Rechner installierte Programme. Die Suche laeuft im Hintergrund und
    // fuellt die Liste nach und nach, damit das Fenster sofort bedienbar ist.
    public class PresetWindow : Window
    {
        class Item
        {
            public string Group;
            public string Label;
            public string Kind;
            public string Target;
            public string Args;
            public string Hint;
            public bool Installed;
            public string ExePath;
            public ImageSource Icon;
            public bool IsHeader;
            public bool IsCustom;
        }

        // --- Daten ----------------------------------------------------

        readonly List<Item> _presets = new List<Item>();
        readonly List<Item> _installed = new List<Item>();
        readonly object _installedLock = new object();
        readonly Action<ActionItem> _onPick;

        bool _installedLoaded;
        bool _closed;
        bool _picked;
        string _mode = "presets";

        // --- Steuerelemente -------------------------------------------

        TextBox _search;
        ListBox _list;
        TextBlock _count;
        TextBlock _status;
        TextBlock _searchHint;

        public PresetWindow(Action<ActionItem> onPick)
        {
            _onPick = onPick;

            Title = Strings.Get("presets.title");
            Width = 560;
            Height = 620;
            MinWidth = 460;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = Ui.Bg;
            FontFamily = UiFont.Family;
            FontSize = 13;
            Foreground = Ui.Ink;
            UseLayoutRounding = true;

            BuildPresets();
            BuildUi();
            Fill("");

            Loaded += delegate
            {
                _search.Focus();
                StartInstalledSearch();
            };

            Closed += delegate
            {
                _closed = true;
                lock (_installedLock) Monitor.PulseAll(_installedLock);
            };
        }

        // ==================================================================
        // Aufbau
        // ==================================================================

        void BuildUi()
        {
            DockPanel root = new DockPanel();
            root.LastChildFill = true;

            // --- Kopf ---
            Border header = new Border();
            header.Background = Ui.Card;
            header.BorderBrush = Ui.Line;
            header.BorderThickness = new Thickness(0, 0, 0, 1);
            header.Padding = new Thickness(20, 16, 20, 14);

            StackPanel head = new StackPanel();
            head.Children.Add(Ui.Heading(Strings.Get("presets.title"), 16));

            TextBlock sub = Ui.Text(Strings.Get("presets.subtitle"), 12, Ui.InkMuted);
            sub.Margin = new Thickness(0, 2, 0, 12);
            head.Children.Add(sub);

            Grid searchRow = new Grid();
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _search = Ui.TextField();
            _search.FontSize = 13;
            _search.Padding = new Thickness(10, 7, 10, 7);
            _search.ToolTip = Strings.Get("presets.searchTip");
            _search.TextChanged += delegate { Fill(_search.Text); };
            searchRow.Children.Add(_search);

            _searchHint = Ui.Text(Strings.Get("common.search"), 11, Ui.InkMuted);
            _searchHint.VerticalAlignment = VerticalAlignment.Center;
            _searchHint.Margin = new Thickness(10, 0, 0, 0);
            _searchHint.Cursor = Cursors.Hand;
            _searchHint.ToolTip = Strings.Get("presets.showInstalled");
            _searchHint.MouseLeftButtonUp += delegate
            {
                if (_mode == "installed") ShowPresetMode();
                else ShowInstalledMode();
            };
            Grid.SetColumn(_searchHint, 1);
            searchRow.Children.Add(_searchHint);

            head.Children.Add(searchRow);

            Grid metaRow = new Grid();
            metaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            metaRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            metaRow.Margin = new Thickness(0, 9, 0, 0);

            _count = Ui.Text("", 11, Ui.InkMuted);
            _count.VerticalAlignment = VerticalAlignment.Center;
            metaRow.Children.Add(_count);

            _status = Ui.Text("", 11, Ui.InkMuted);
            _status.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_status, 1);
            metaRow.Children.Add(_status);

            head.Children.Add(metaRow);

            header.Child = head;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // --- Fuss ---
            Border footer = new Border();
            footer.Background = Ui.Card;
            footer.BorderBrush = Ui.Line;
            footer.BorderThickness = new Thickness(0, 1, 0, 0);
            footer.Padding = new Thickness(20, 12, 20, 12);

            Grid foot = new Grid();
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button custom = Ui.Button(Strings.Get("presets.custom"));
            custom.HorizontalAlignment = HorizontalAlignment.Left;
            custom.ToolTip = Strings.Get("presets.customTip");
            custom.IsDefault = false;
            custom.Click += delegate { PickCustom(); };
            foot.Children.Add(custom);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;

            Button cancel = Ui.Button(Strings.Get("common.cancel"));
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += delegate { Close(); };
            buttons.Children.Add(cancel);

            Button add = Ui.PrimaryButton(Strings.Get("common.add"));
            add.IsDefault = true;
            add.Click += delegate { PickSelected(); };
            buttons.Children.Add(add);

            Grid.SetColumn(buttons, 1);
            foot.Children.Add(buttons);

            footer.Child = foot;
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            // --- Liste ---
            _list = new ListBox();
            _list.BorderThickness = new Thickness(0);
            _list.Background = Ui.Card;
            _list.Padding = new Thickness(8, 6, 8, 6);
            _list.ItemContainerStyle = RowStyle();
            _list.MouseDoubleClick += delegate { PickSelected(); };
            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
            root.Children.Add(_list);

            Content = root;
        }

        void ScrollToGroup(string group)
        {
            for (int i = 0; i < _list.Items.Count; i++)
            {
                ListBoxItem row = _list.Items[i] as ListBoxItem;
                if (row == null) continue;
                Item item = row.Tag as Item;
                if (item != null && item.IsHeader && item.Label == group)
                {
                    _list.ScrollIntoView(row);
                    return;
                }
            }
        }

        void ShowInstalledMode()
        {
            _mode = "installed";
            if (_search != null) _search.Text = "";
            Fill("");
            if (_search != null) _search.Focus();
        }

        void ShowPresetMode()
        {
            _mode = "presets";
            Fill(_search == null ? "" : _search.Text);
        }

        static Style RowStyle()
        {
            Style style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0)));
            style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(ListBoxItem.FocusVisualStyleProperty, null));

            ControlTemplate template = new ControlTemplate(typeof(ListBoxItem));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Row";
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ListBoxItem.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            template.VisualTree = border;

            Trigger hover = new Trigger();
            hover.Property = ListBoxItem.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Ui.Subtle));
            template.Triggers.Add(hover);

            Trigger selected = new Trigger();
            selected.Property = ListBoxItem.IsSelectedProperty;
            selected.Value = true;
            selected.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Ui.AccentSoft));
            template.Triggers.Add(selected);

            style.Setters.Add(new Setter(ListBoxItem.TemplateProperty, template));
            return style;
        }

        // ==================================================================
        // Kuratierte Befehle
        // ==================================================================

        void BuildPresets()
        {
            List<Presets.Entry> entries = Presets.All();
            for (int i = 0; i < entries.Count; i++)
            {
                Presets.Entry e = entries[i];
                Item item = new Item();
                item.Group = LocalizeGroup(e.Group);
                item.Label = Localize("presets.label.", e.Label);
                item.Kind = e.Kind;
                item.Target = e.Target;
                item.Args = e.Args;
                _presets.Add(item);
            }
        }

        // Die kuratierten Befehle stehen in Presets.cs auf Deutsch; hier
        // bekommen Gruppe und Bezeichnung ihre Fassung in der aktuellen
        // Sprache. Fehlt ein Eintrag, bleibt der deutsche Text stehen.
        static string Localize(string prefix, string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            string key = prefix + value;
            return Strings.Has(key) ? Strings.Get(key) : value;
        }

        static string LocalizeGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return group;
            string key;
            switch (group)
            {
                case "Fenster": key = "presets.group.fenster"; break;
                case "System": key = "presets.group.system"; break;
                case "Programme": key = "presets.group.programme"; break;
                case "Medien": key = "presets.group.medien"; break;
                case "Bearbeiten": key = "presets.group.bearbeiten"; break;
                case "Discord": key = "presets.group.discord"; break;
                case "Makros": key = "presets.group.makros"; break;
                default: return group;
            }
            return Strings.Get(key);
        }

        // ==================================================================
        // Installierte Programme
        // ==================================================================

        void StartInstalledSearch()
        {
            if (_installedLoaded) return;
            if (_status != null) _status.Text = Strings.Get("presets.searching");

            Thread worker = new Thread(delegate()
            {
                List<Item> found = new List<Item>();
                try
                {
                    List<InstalledApp> apps = InstalledPrograms.Scan();
                    for (int i = 0; i < apps.Count; i++)
                    {
                        InstalledApp app = apps[i];
                        Item item = new Item();
                        item.Label = app.Name;
                        item.Target = app.ExePath;
                        item.ExePath = app.ExePath;
                        item.Icon = app.Icon;
                        item.Kind = "program";
                        item.Installed = true;
                        found.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("Programmsuche fehlgeschlagen: " + ex.Message);
                }

                lock (_installedLock)
                {
                    if (_closed) return;
                    _installed.Clear();
                    _installed.AddRange(found);
                    _installedLoaded = true;
                }

                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (_closed) return;
                        if (_status != null)
                            _status.Text = _installed.Count == 0
                                ? Strings.Get("presets.noneInstalled")
                                : _installed.Count == 1
                                    ? Strings.Get("presets.foundOne")
                                    : Strings.Format("presets.foundMany", _installed.Count);
                        Fill(_search == null ? "" : _search.Text);
                    }));
                }
                catch { }
            });
            worker.IsBackground = true;
            worker.Name = "DialMenu.InstalledPrograms";
            worker.Start();
        }

        // ==================================================================
        // Liste fuellen
        // ==================================================================

        void Fill(string filter)
        {
            if (_list == null) return;

            _list.Items.Clear();

            string query = (filter ?? "").Trim().ToLowerInvariant();
            int shown = 0;

            if (_mode == "installed")
            {
                shown += FillInstalled(query);
                if (shown == 0)
                {
                    bool ready;
                    lock (_installedLock) ready = _installedLoaded;

                    TextBlock empty = Ui.Text(
                        ready ? Strings.Get("presets.emptyInstalled")
                            : Strings.Get("presets.stillSearching"),
                        12, Ui.InkMuted);
                    empty.Margin = new Thickness(12, 14, 12, 14);
                    _list.Items.Add(WrapRow(empty));
                }
            }
            else
            {
                shown += FillPresets(query);

                // Treffer aus den installierten Programmen direkt darunter,
                // damit die Suche beides gleichzeitig findet.
                if (query.Length > 0) shown += FillInstalled(query);

                if (query.Length > 0 && shown == 0)
                {
                    TextBlock empty = Ui.Text(
                        Strings.Get("presets.emptySearch"), 12, Ui.InkMuted);
                    empty.Margin = new Thickness(12, 14, 12, 14);
                    _list.Items.Add(WrapRow(empty));
                }
            }

            // Eigener Befehl als letzter Eintrag.
            _list.Items.Add(RowForSpecial());

            if (_count != null)
            {
                _count.Text = shown == 1
                    ? Strings.Get(_mode == "installed" ? "presets.countProgramOne" : "presets.countEntryOne")
                    : Strings.Format(_mode == "installed" ? "presets.countProgramMany" : "presets.countEntryMany",
                        shown);
            }

            UpdateModeLabel();
            SelectFirstSelectable();
        }

        void UpdateModeLabel()
        {
            if (_searchHint == null) return;
            _searchHint.Text = _mode == "installed"
                ? Strings.Get("presets.modePresets") : Strings.Get("presets.modeInstalled");
            _searchHint.ToolTip = _mode == "installed"
                ? Strings.Get("presets.backToPresets")
                : Strings.Get("presets.showInstalled");
        }

        static ListBoxItem WrapRow(FrameworkElement content)
        {
            ListBoxItem row = new ListBoxItem();
            row.Content = content;
            row.IsHitTestVisible = false;
            row.Focusable = false;
            row.Tag = HeaderItem("");
            return row;
        }

        int FillPresets(string query)
        {
            int shown = 0;
            string lastGroup = null;

            for (int i = 0; i < _presets.Count; i++)
            {
                Item item = _presets[i];
                if (!Matches(item, query)) continue;

                if (!string.Equals(item.Group, lastGroup, StringComparison.Ordinal))
                {
                    lastGroup = item.Group;
                    _list.Items.Add(RowForHeader(item.Group.ToUpperInvariant()));
                }

                _list.Items.Add(RowForItem(item));
                shown++;
            }

            return shown;
        }

        int FillInstalled(string query)
        {
            List<Item> snapshot;
            lock (_installedLock)
            {
                if (!_installedLoaded || _installed.Count == 0) return 0;
                snapshot = new List<Item>(_installed);
            }

            int shown = 0;
            bool header = false;

            for (int i = 0; i < snapshot.Count; i++)
            {
                Item item = snapshot[i];
                if (!Matches(item, query)) continue;

                if (!header)
                {
                    header = true;
                    _list.Items.Add(RowForHeader(Strings.Get("presets.installedHeader")));
                }

                _list.Items.Add(RowForItem(item));
                shown++;
            }

            return shown;
        }

        static bool Matches(Item item, string query)
        {
            if (query.Length == 0) return true;
            if (item.Label != null && item.Label.ToLowerInvariant().Contains(query)) return true;
            if (item.Group != null && item.Group.ToLowerInvariant().Contains(query)) return true;
            if (item.Target != null && item.Target.ToLowerInvariant().Contains(query)) return true;
            return false;
        }

        // ==================================================================
        // Zeilen
        // ==================================================================

        FrameworkElement RowForHeader(string text)
        {
            Border border = new Border();
            border.Padding = new Thickness(10, 12, 10, 5);
            border.IsHitTestVisible = false;

            TextBlock label = Ui.Text(text, 10, Ui.InkMuted);
            label.FontWeight = FontWeights.SemiBold;
            border.Child = label;

            ListBoxItem row = new ListBoxItem();
            row.Content = border;
            row.IsHitTestVisible = false;
            row.Focusable = false;
            row.Tag = HeaderItem(text);
            return row;
        }

        static Item HeaderItem(string text)
        {
            Item item = new Item();
            item.IsHeader = true;
            item.Label = text;
            return item;
        }

        FrameworkElement RowForItem(Item item)
        {
            Border border = new Border();
            border.Padding = new Thickness(10, 7, 10, 7);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            ContentControl icon = new ContentControl();
            icon.Width = 22;
            icon.Height = 22;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            icon.Content = IconFor(item, 19);
            grid.Children.Add(icon);

            StackPanel text = new StackPanel();
            text.VerticalAlignment = VerticalAlignment.Center;

            TextBlock label = Ui.Text(item.Label, 12.5, Ui.Ink);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(label);

            string detail = DetailFor(item);
            if (!string.IsNullOrEmpty(detail))
            {
                TextBlock sub = Ui.Text(detail, 10.5, Ui.InkMuted);
                sub.TextTrimming = TextTrimming.CharacterEllipsis;
                sub.Margin = new Thickness(0, 1, 0, 0);
                text.Children.Add(sub);
            }

            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            if (item.Installed)
            {
                TextBlock tag = Ui.Text(Strings.Get("presets.installed"), 10, Ui.InkMuted);
                tag.VerticalAlignment = VerticalAlignment.Center;
                tag.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(tag, 2);
                grid.Children.Add(tag);
            }

            border.Child = grid;

            ListBoxItem row = new ListBoxItem();
            row.Content = border;
            row.Tag = item;
            return row;
        }

        FrameworkElement RowForSpecial()
        {
            Border border = new Border();
            border.Padding = new Thickness(10, 10, 10, 10);
            border.BorderBrush = Ui.Line;
            border.BorderThickness = new Thickness(0, 1, 0, 0);

            StackPanel line = new StackPanel();
            line.Orientation = Orientation.Horizontal;

            ContentControl icon = new ContentControl();
            icon.Width = 22;
            icon.Height = 22;
            icon.Content = Ui.Glyph(
                "M12,5 V19 M5,12 H19", 17, Ui.InkSoft);
            line.Children.Add(icon);

            TextBlock label = Ui.Text(Strings.Get("presets.custom"), 12.5, Ui.InkSoft);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(8, 0, 0, 0);
            line.Children.Add(label);

            border.Child = line;

            ListBoxItem row = new ListBoxItem();
            row.Content = border;

            Item tag = new Item();
            tag.IsCustom = true;
            tag.Label = Strings.Get("presets.customCommand");
            row.Tag = tag;
            return row;
        }

        static FrameworkElement IconFor(Item item, double size)
        {
            if (item.Icon != null)
            {
                Image image = new Image();
                image.Source = item.Icon;
                image.Width = size;
                image.Height = size;
                image.Stretch = Stretch.Uniform;
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                return image;
            }

            if (item.Installed)
                return Ui.Glyph(IconLibrary.Window, size, Ui.InkMuted);

            ActionItem probe = new ActionItem();
            probe.Kind = item.Kind;
            probe.Target = item.Target;
            probe.Label = item.Label;
            return Ui.Glyph(SafeIconFor(probe), size, Ui.InkSoft);
        }

        static string SafeIconFor(ActionItem item)
        {
            try
            {
                string data = IconLibrary.For(item);
                if (!string.IsNullOrEmpty(data)) return data;
            }
            catch { }
            return IconLibrary.Gear;
        }

        static string DetailFor(Item item)
        {
            if (item.Installed) return item.Target;

            switch (item.Kind)
            {
                case "hotkey": return Strings.Format("presets.detailHotkey", item.Target);
                case "program": return Strings.Format("presets.detailProgram", item.Target);
                case "url": return Strings.Format("presets.detailOpen", item.Target);
                case "media": return Strings.Get("presets.detailMedia");
                case "volume": return Strings.Get("presets.detailVolume");
                case "system": return Strings.Get("presets.detailSystem");
                case "macro": return Strings.Get("presets.detailMacro");
            }
            return item.Target;
        }

        // ==================================================================
        // Auswahl
        // ==================================================================

        void SelectFirstSelectable()
        {
            for (int i = 0; i < _list.Items.Count; i++)
            {
                ListBoxItem row = _list.Items[i] as ListBoxItem;
                if (row == null) continue;
                Item item = row.Tag as Item;
                if (item == null || item.IsHeader) continue;
                _list.SelectedIndex = i;
                return;
            }
        }

        void PickSelected()
        {
            if (_picked) return;

            ListBoxItem row = _list.SelectedItem as ListBoxItem;
            if (row == null) return;

            Item item = row.Tag as Item;
            if (item == null) return;

            if (item.IsHeader) return;
            if (item.IsCustom) { PickCustom(); return; }

            ActionItem action = ToAction(item);
            if (action == null) return;

            _picked = true;
            if (_onPick != null) _onPick(action);
            Close();
        }

        static ActionItem ToAction(Item item)
        {
            ActionItem action = new ActionItem();
            action.Label = item.Label ?? "";
            action.Kind = item.Kind ?? "program";
            action.Target = item.Target ?? "";
            action.Args = item.Args ?? "";
            action.Hint = item.Hint ?? "";

            if (action.Kind == "macro" && action.Steps.Count == 0)
                action.Steps = Presets.SampleMacro().Steps;

            return action;
        }

        void PickCustom()
        {
            if (_picked) return;
            _picked = true;

            ActionItem action = new ActionItem();
            action.Label = Strings.Get("commands.newCommand");
            action.Kind = "program";

            if (_onPick != null) _onPick(action);
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
            if (e.Key == Key.Enter) { PickSelected(); e.Handled = true; return; }
            if (e.Key == Key.Down && _search != null && _search.IsKeyboardFocusWithin)
            {
                MoveSelection(1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up && _search != null && _search.IsKeyboardFocusWithin)
            {
                MoveSelection(-1);
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        void MoveSelection(int delta)
        {
            int index = _list.SelectedIndex + delta;
            while (index >= 0 && index < _list.Items.Count)
            {
                ListBoxItem row = _list.Items[index] as ListBoxItem;
                Item item = row == null ? null : row.Tag as Item;
                if (item != null && !item.IsHeader)
                {
                    _list.SelectedIndex = index;
                    return;
                }
                index += delta;
            }
        }
    }

    // ------------------------------------------------------------------
    // Echte, installierte Programme finden.
    //
    // Zwei Quellen: Startmenue-Verknuepfungen (Ziel ueber WScript.Shell)
    // und die Uninstall-Schluessel der Registry (DisplayIcon). Beides kann
    // langsam sein, deshalb laeuft es ausschliesslich im Hintergrund.
    // ------------------------------------------------------------------

    public class InstalledApp
    {
        public string Name;
        public string ExePath;
        public ImageSource Icon;
    }

    public static class InstalledPrograms
    {
        static readonly string[] IgnoredNames =
        {
            "uninstall", "deinstallieren", "readme", "liesmich", "help", "hilfe",
            "website", "webseite", "documentation", "dokumentation", "release notes",
            "changelog", "license", "lizenz", "update", "updater"
        };

        static readonly string[] IgnoredExes =
        {
            "unins", "uninstall", "setup", "install", "update", "updater",
            "readme", "help", "crashreport", "crashpad"
        };

        public static List<InstalledApp> Scan()
        {
            Dictionary<string, InstalledApp> byPath =
                new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, InstalledApp> byName =
                new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

            try { ScanStartMenu(byPath, byName); }
            catch (Exception ex) { Log.Write("Startmenü: " + ex.Message); }

            try { ScanUninstallKeys(byPath, byName); }
            catch (Exception ex) { Log.Write("Registry: " + ex.Message); }

            List<InstalledApp> list = new List<InstalledApp>();
            list.AddRange(byPath.Values);
            list.Sort(delegate(InstalledApp a, InstalledApp b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        // --- Startmenue -------------------------------------------------

        static void ScanStartMenu(Dictionary<string, InstalledApp> byPath,
            Dictionary<string, InstalledApp> byName)
        {
            List<string> roots = new List<string>();

            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            string user = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);

            AddProgramsRoot(roots, common);
            AddProgramsRoot(roots, user);

            List<string> shortcuts = new List<string>();
            for (int i = 0; i < roots.Count; i++)
            {
                try
                {
                    if (!Directory.Exists(roots[i])) continue;
                    shortcuts.AddRange(Directory.GetFiles(roots[i], "*.lnk", SearchOption.AllDirectories));
                }
                catch (Exception ex)
                {
                    Log.Write("Startmenü-Ordner: " + ex.Message);
                }
            }

            if (shortcuts.Count == 0) return;

            object shell = null;
            try { shell = CreateShell(); }
            catch (Exception ex) { Log.Write("WScript.Shell: " + ex.Message); }

            for (int i = 0; i < shortcuts.Count; i++)
            {
                string shortcut = shortcuts[i];
                string name = Path.GetFileNameWithoutExtension(shortcut);
                if (string.IsNullOrEmpty(name) || IsIgnoredName(name)) continue;

                string target = null;
                if (shell != null)
                {
                    try { target = ResolveShortcut(shell, shortcut); }
                    catch { }
                }

                if (string.IsNullOrEmpty(target)) continue;
                if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(target)) continue;
                if (IsIgnoredExe(target)) continue;

                // Programme, die tief im System liegen, sind selten gemeint.
                if (IsSystemPath(target)) continue;

                Add(byPath, byName, name, target, true);
            }
        }

        static void AddProgramsRoot(List<string> roots, string startMenu)
        {
            if (string.IsNullOrEmpty(startMenu)) return;
            string programs = Path.Combine(startMenu, "Programs");
            if (Directory.Exists(programs)) roots.Add(programs);
            else if (Directory.Exists(startMenu)) roots.Add(startMenu);
        }

        static object CreateShell()
        {
            Type type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return null;
            return Activator.CreateInstance(type);
        }

        static string ResolveShortcut(object shell, string path)
        {
            object shortcut = null;
            try
            {
                shortcut = shell.GetType().InvokeMember(
                    "CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null, shell, new object[] { path });

                if (shortcut == null) return null;

                object target = shortcut.GetType().InvokeMember(
                    "TargetPath",
                    System.Reflection.BindingFlags.GetProperty,
                    null, shortcut, null);

                return target as string;
            }
            catch
            {
                return null;
            }
        }

        static bool IsIgnoredName(string name)
        {
            string lower = name.ToLowerInvariant();
            for (int i = 0; i < IgnoredNames.Length; i++)
                if (lower.Contains(IgnoredNames[i])) return true;
            return false;
        }

        static bool IsIgnoredExe(string path)
        {
            string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            for (int i = 0; i < IgnoredExes.Length; i++)
                if (file.Contains(IgnoredExes[i])) return true;
            return false;
        }

        static bool IsSystemPath(string path)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(windows)) return false;
            if (!path.StartsWith(windows, StringComparison.OrdinalIgnoreCase)) return false;

            // Manche Systemwerkzeuge sind trotzdem sinnvoll.
            string file = Path.GetFileName(path).ToLowerInvariant();
            return file != "explorer.exe" && file != "notepad.exe"
                && file != "mspaint.exe" && file != "calc.exe"
                && file != "taskmgr.exe" && file != "snippingtool.exe";
        }

        // --- Registry ---------------------------------------------------

        static void ScanUninstallKeys(Dictionary<string, InstalledApp> byPath,
            Dictionary<string, InstalledApp> byName)
        {
            string[] roots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            for (int r = 0; r < roots.Length; r++)
            {
                RegistryKey hives = null;
                try { hives = Registry.LocalMachine.OpenSubKey(roots[r]); }
                catch { hives = null; }
                ReadUninstall(hives, byPath, byName);
                if (hives != null) hives.Close();

                RegistryKey userHive = null;
                try { userHive = Registry.CurrentUser.OpenSubKey(roots[r]); }
                catch { userHive = null; }
                ReadUninstall(userHive, byPath, byName);
                if (userHive != null) userHive.Close();
            }
        }

        static void ReadUninstall(RegistryKey root,
            Dictionary<string, InstalledApp> byPath,
            Dictionary<string, InstalledApp> byName)
        {
            if (root == null) return;

            string[] keys;
            try { keys = root.GetSubKeyNames(); }
            catch { return; }

            for (int i = 0; i < keys.Length; i++)
            {
                RegistryKey entry = null;
                try
                {
                    entry = root.OpenSubKey(keys[i]);
                    if (entry == null) continue;

                    object systemComponent = entry.GetValue("SystemComponent");
                    if (systemComponent != null && Convert.ToInt32(systemComponent) == 1) continue;

                    if (entry.GetValue("ParentKeyName") != null) continue;

                    string name = entry.GetValue("DisplayName") as string;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (IsIgnoredName(name)) continue;

                    string exe = ExeFromDisplayIcon(entry.GetValue("DisplayIcon") as string);
                    if (exe == null) continue;
                    if (!File.Exists(exe)) continue;
                    if (IsIgnoredExe(exe)) continue;

                    // Eintraege mit InstallLocation sind meist echte Programme,
                    // reine Icon-Angaben (z. B. aus Treiberpaketen) fliegen raus.
                    Add(byPath, byName, name.Trim(), exe, true);
                }
                catch
                {
                    // Ein defekter Eintrag darf die Suche nicht beenden.
                }
                finally
                {
                    if (entry != null) entry.Close();
                }
            }
        }

        static string ExeFromDisplayIcon(string displayIcon)
        {
            if (string.IsNullOrEmpty(displayIcon)) return null;

            string value = displayIcon.Trim().Trim('"');

            // "C:\Pfad\app.exe,0" -> Pfad ohne Index.
            int comma = value.LastIndexOf(',');
            if (comma > 2)
            {
                string tail = value.Substring(comma + 1).Trim();
                int parsed;
                if (int.TryParse(tail, out parsed)) value = value.Substring(0, comma).Trim();
            }

            if (!value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
            return value;
        }

        // --- gemeinsam --------------------------------------------------

        static void Add(Dictionary<string, InstalledApp> byPath,
            Dictionary<string, InstalledApp> byName,
            string name, string exe, bool withIcon)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(exe)) return;

            string key = exe;
            if (byPath.ContainsKey(key)) return;
            if (byName.ContainsKey(name)) return;

            InstalledApp app = new InstalledApp();
            app.Name = CommonPrefix.Clean(name);
            app.ExePath = exe;
            if (string.IsNullOrEmpty(app.Name)) return;
            if (withIcon) app.Icon = LoadIcon(exe);

            byPath[key] = app;
            byName[app.Name] = app;
        }

        static ImageSource LoadIcon(string exe)
        {
            Icon icon = null;
            try
            {
                icon = Icon.ExtractAssociatedIcon(exe);
                if (icon == null) return null;

                BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle, Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(20, 20));
                source.Freeze();
                return source;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (icon != null) icon.Dispose();
            }
        }
    }

    // Raeumt typische Zusaetze wie "– 64 Bit" aus Anzeigenamen.
    internal static class CommonPrefix
    {
        public static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string value = name.Trim();

            int cut = value.IndexOf(" (x64)", StringComparison.OrdinalIgnoreCase);
            if (cut > 0) value = value.Substring(0, cut);
            cut = value.IndexOf(" (x86)", StringComparison.OrdinalIgnoreCase);
            if (cut > 0) value = value.Substring(0, cut);

            return value.Trim();
        }
    }
}