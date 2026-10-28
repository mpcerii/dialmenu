using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace DialMenu
{
    // Bearbeitet die Schrittliste eines Makros.
    public class MacroEditor : Window
    {
        static readonly Brush Bg = new SolidColorBrush(Color.FromRgb(0xFA, 0xFB, 0xFC));
        static readonly Brush Card = Brushes.White;
        static readonly Brush Line = new SolidColorBrush(Color.FromRgb(0xE6, 0xE8, 0xEC));
        static readonly Brush Ink = Tokens.Ink;
        static readonly Brush InkSoft = Tokens.InkSoft;
        static readonly Brush InkMuted = Tokens.InkMuted;
        static readonly Brush FieldLine = Tokens.LineStrong;
        static readonly Brush Accent = Tokens.Primary;
        static readonly Brush AccentSoft = Tokens.Selected;

        internal static readonly string[] Types = { "hotkey", "text", "delay", "program" };
        internal static readonly string[] TypeKeys =
        {
            "macro.type.hotkey", "macro.type.text", "macro.type.delay", "macro.type.program"
        };

        readonly List<MacroStep> _steps;
        readonly List<MacroStep> _original;
        readonly Action _onChanged;

        ListBox _list;
        TextBox _value;
        ComboBox _type;
        NumericBox _delay;
        TextBlock _valueCaption;
        TextBlock _summary;
        bool _loading;
        int _index = -1;

        public MacroEditor(List<MacroStep> steps, string actionLabel, Action onChanged)
        {
            _steps = steps;
            _onChanged = onChanged;
            _original = new List<MacroStep>();
            foreach (MacroStep s in steps) _original.Add(s.Clone());

            Title = Strings.Get("macro.title");
            Width = 620;
            Height = 520;
            MinWidth = 540;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Bg;
            FontFamily = UiFont.Family;
            FontSize = 13;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            BuildUi(actionLabel);
            RefreshList();

            // RefreshList setzt die Auswahl bereits waehrend _loading; das
            // SelectionChanged-Ereignis faellt dadurch aus. Ohne diesen
            // Aufruf bliebe das Formular rechts leer.
            if (_steps.Count > 0)
            {
                _index = 0;
                _list.SelectedIndex = 0;
                ShowStep(_steps[0]);
            }
            else ShowStep(null);
        }

        // ------------------------------------------------------------------
        // Aufbau
        // ------------------------------------------------------------------

        void BuildUi(string actionLabel)
        {
            DockPanel root = new DockPanel();
            root.LastChildFill = true;

            // Kopf
            Border header = new Border();
            header.Background = Card;
            header.BorderBrush = Line;
            header.BorderThickness = new Thickness(0, 0, 0, 1);
            header.Padding = new Thickness(22, 16, 22, 14);

            StackPanel headStack = new StackPanel();
            TextBlock title = new TextBlock();
            title.Text = string.IsNullOrEmpty(actionLabel) ? Strings.Get("macro.macro") : actionLabel;
            title.FontSize = 15;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = Ink;
            headStack.Children.Add(title);

            TextBlock sub = new TextBlock();
            sub.Text = Strings.Get("macro.subtitle");
            sub.FontSize = 12;
            sub.Foreground = InkMuted;
            sub.Margin = new Thickness(0, 2, 0, 0);
            sub.TextWrapping = TextWrapping.Wrap;
            headStack.Children.Add(sub);

            header.Child = headStack;
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            // Fuss
            Border footer = new Border();
            footer.Background = Card;
            footer.BorderBrush = Line;
            footer.BorderThickness = new Thickness(0, 1, 0, 0);
            footer.Padding = new Thickness(22, 12, 22, 12);

            Grid foot = new Grid();
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button done = Ui.PrimaryButton(Strings.Get("macro.done"));
            done.Click += delegate { Close(); };
            Grid.SetColumn(done, 1);
            foot.Children.Add(done);

            Button reset = Ui.Button(Strings.Get("macro.reload"));
            reset.HorizontalAlignment = HorizontalAlignment.Left;
            reset.ToolTip = Strings.Get("macro.reloadTip");
            reset.Click += delegate { ReloadFromOriginal(); };
            foot.Children.Add(reset);

            footer.Child = foot;
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(footer);

            // Hauptbereich
            Grid body = new Grid();
            body.Margin = new Thickness(22, 16, 22, 16);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Linke Spalte: Schritte
            DockPanel left = new DockPanel();
            left.LastChildFill = true;

            TextBlock stepsCaption = Ui.Caption(Strings.Get("macro.steps"));
            stepsCaption.Margin = new Thickness(0, 0, 0, 6);
            DockPanel.SetDock(stepsCaption, Dock.Top);
            left.Children.Add(stepsCaption);

            StackPanel tools = new StackPanel();
            tools.Orientation = Orientation.Horizontal;
            tools.Margin = new Thickness(0, 8, 0, 0);
            DockPanel.SetDock(tools, Dock.Bottom);

            Button add = Ui.SmallButton(Strings.Get("macro.add"));
            add.Click += delegate { AddStep(); };
            tools.Children.Add(add);

            Button up = Ui.SmallButton(Strings.Get("macro.up"));
            up.Margin = new Thickness(6, 0, 0, 0);
            up.Click += delegate { MoveStep(-1); };
            tools.Children.Add(up);

            Button down = Ui.SmallButton(Strings.Get("macro.down"));
            down.Margin = new Thickness(6, 0, 0, 0);
            down.Click += delegate { MoveStep(1); };
            tools.Children.Add(down);

            _list = new ListBox();
            _list.BorderBrush = Line;
            _list.BorderThickness = new Thickness(1);
            _list.Background = Card;
            _list.SelectionChanged += delegate
            {
                if (_loading) return;
                int i = _list.SelectedIndex;
                if (i >= 0 && i < _steps.Count)
                {
                    _index = i;
                    ShowStep(_steps[i]);
                }
            };
            left.Children.Add(_list);

            Grid.SetColumn(left, 0);
            body.Children.Add(left);

            // Rechte Spalte: Schritt
            StackPanel right = new StackPanel();

            TextBlock detailCaption = Ui.Caption(Strings.Get("macro.step"));
            detailCaption.Margin = new Thickness(0, 0, 0, 8);
            right.Children.Add(detailCaption);

            Border card = new Border();
            card.Background = Card;
            card.BorderBrush = Line;
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(6);
            card.Padding = new Thickness(16, 14, 16, 16);

            StackPanel form = new StackPanel();

            _type = Ui.Combo();
            for (int i = 0; i < TypeKeys.Length; i++) _type.Items.Add(Strings.Get(TypeKeys[i]));
            _type.SelectionChanged += delegate
            {
                if (_loading || _index < 0 || _index >= _steps.Count) return;
                MacroStep step = _steps[_index];
                int i = _type.SelectedIndex;
                if (i < 0) return;
                string next = Types[i];
                if (step.Type != next)
                {
                    step.Type = next;
                    if (next == "delay" && string.IsNullOrEmpty(step.Value)) step.Value = "300";
                    ShowStep(step);
                    RefreshList();
                    Changed();
                }
            };
            form.Children.Add(Ui.Field(Strings.Get("macro.kind"), _type));

            _valueCaption = Ui.Caption(Strings.Get("macro.value"));
            _valueCaption.Margin = new Thickness(0, 13, 0, 4);

            _value = Ui.TextField();
            _value.TextChanged += delegate
            {
                if (_loading || _index < 0 || _index >= _steps.Count) return;
                _steps[_index].Value = _value.Text;
                RefreshRow(_index);
                UpdateSummary();
                Changed();
            };

            StackPanel valueBlock = new StackPanel();
            valueBlock.Children.Add(_valueCaption);
            valueBlock.Children.Add(_value);
            form.Children.Add(valueBlock);

            _delay = new NumericBox(0, 10000, 10);
            _delay.Margin = new Thickness(0, 13, 0, 0);
            _delay.ValueChanged += delegate
            {
                if (_loading || _index < 0 || _index >= _steps.Count) return;
                _steps[_index].DelayMs = _delay.Value;
                RefreshRow(_index);
                UpdateSummary();
                Changed();
            };

            StackPanel delayBlock = new StackPanel();
            TextBlock delayCaption = Ui.Caption(Strings.Get("macro.delayAfter"));
            delayCaption.Margin = new Thickness(0, 0, 0, 4);
            delayBlock.Children.Add(delayCaption);
            delayBlock.Children.Add(_delay);
            form.Children.Add(delayBlock);

            card.Child = form;
            right.Children.Add(card);

            _summary = new TextBlock();
            _summary.FontSize = 12;
            _summary.Foreground = InkSoft;
            _summary.TextWrapping = TextWrapping.Wrap;
            _summary.Margin = new Thickness(0, 12, 0, 0);
            right.Children.Add(_summary);

            Button remove = Ui.Button(Strings.Get("macro.remove"));
            remove.HorizontalAlignment = HorizontalAlignment.Left;
            remove.Margin = new Thickness(0, 16, 0, 0);
            remove.Click += delegate { RemoveStep(); };
            right.Children.Add(remove);

            Grid.SetColumn(right, 2);
            body.Children.Add(right);

            root.Children.Add(body);
            Content = root;
        }

        // ------------------------------------------------------------------
        // Liste
        // ------------------------------------------------------------------

        void RefreshList()
        {
            int keep = _list.SelectedIndex;
            _loading = true;
            _list.Items.Clear();
            for (int i = 0; i < _steps.Count; i++)
            {
                ListBoxItem item = new ListBoxItem();
                item.Content = RowFor(i, _steps[i]);
                item.Padding = new Thickness(9, 5, 9, 5);
                item.Tag = i;
                _list.Items.Add(item);
            }
            if (keep >= 0 && keep < _list.Items.Count) _list.SelectedIndex = keep;
            else if (_list.Items.Count > 0 && _index < 0) _list.SelectedIndex = 0;
            _loading = false;
        }

        void RefreshRow(int index)
        {
            if (index < 0 || index >= _list.Items.Count || index >= _steps.Count) return;
            ListBoxItem item = _list.Items[index] as ListBoxItem;
            if (item == null) return;
            item.Content = RowFor(index, _steps[index]);
        }

        FrameworkElement RowFor(int index, MacroStep step)
        {
            Grid row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock number = new TextBlock();
            number.Text = (index + 1).ToString();
            number.FontSize = 11;
            number.Foreground = InkMuted;
            number.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(number);

            StackPanel text = new StackPanel();
            text.Margin = new Thickness(2, 0, 0, 0);

            TextBlock type = new TextBlock();
            type.Text = TypeLabel(step.Type);
            type.FontSize = 12.5;
            type.Foreground = Ink;
            type.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(type);

            TextBlock detail = new TextBlock();
            detail.Text = Detail(step);
            detail.FontSize = 10.5;
            detail.Foreground = InkMuted;
            detail.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(detail);

            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return row;
        }

        static string TypeLabel(string type)
        {
            for (int i = 0; i < Types.Length; i++)
                if (Types[i] == type) return Strings.Get(TypeKeys[i]);
            if (type == "wait") return Strings.Get("macro.wait");
            return string.IsNullOrEmpty(type) ? Strings.Get("macro.stepFallback") : type;
        }

        static string Detail(MacroStep step)
        {
            string value = step.Value == null ? "" : step.Value.Trim();
            string pause = step.DelayMs > 0 ? "  ·  " + step.DelayMs + " ms" : "";

            switch (step.Type)
            {
                case "hotkey":
                    return (value.Length == 0 ? Strings.Get("macro.noCombination") : value) + pause;
                case "text":
                    return (value.Length == 0 ? Strings.Get("macro.noText")
                        : value.Length > 26 ? value.Substring(0, 26) + "…" : value) + pause;
                case "delay":
                    return Strings.Get("macro.interrupts") + pause;
                case "program":
                    return (value.Length == 0 ? Strings.Get("macro.noProgram") : Shorten(value, 30)) + pause;
            }
            return value + pause;
        }

        static string Shorten(string value, int max)
        {
            if (value.Length <= max) return value;
            return value.Substring(0, max - 1) + "…";
        }

        // ------------------------------------------------------------------
        // Schritt bearbeiten
        // ------------------------------------------------------------------

        void ShowStep(MacroStep step)
        {
            _loading = true;
            try
            {
                bool has = step != null;
                _type.IsEnabled = has;
                _value.IsEnabled = has;
                _delay.IsEnabled = has;

                if (!has)
                {
                    _type.SelectedIndex = -1;
                    _value.Text = "";
                    _delay.Value = 0;
                    _summary.Text = Strings.Get("macro.empty");
                    _valueCaption.Text = Strings.Get("macro.value");
                    return;
                }

                int typeIndex = 0;
                for (int i = 0; i < Types.Length; i++)
                    if (Types[i] == step.Type) { typeIndex = i; break; }
                if (step.Type == "wait") typeIndex = 2;

                _type.SelectedIndex = typeIndex;
                _value.Text = step.Value == null ? "" : step.Value;
                _delay.Value = step.DelayMs;

                switch (step.Type)
                {
                    case "hotkey":
                        _valueCaption.Text = Strings.Get("macro.caption.hotkey");
                        _value.ToolTip = Strings.Get("macro.tip.hotkey");
                        break;
                    case "text":
                        _valueCaption.Text = Strings.Get("macro.caption.text");
                        _value.ToolTip = Strings.Get("macro.tip.text");
                        break;
                    case "delay":
                        _valueCaption.Text = Strings.Get("macro.caption.delay");
                        _value.ToolTip = Strings.Get("macro.tip.delay");
                        break;
                    case "program":
                        _valueCaption.Text = Strings.Get("macro.caption.program");
                        _value.ToolTip = Strings.Get("macro.tip.program");
                        break;
                    default:
                        _valueCaption.Text = Strings.Get("macro.value");
                        break;
                }
            }
            finally { _loading = false; }

            UpdateSummary();
        }

        void UpdateSummary()
        {
            if (_index < 0 || _index >= _steps.Count)
            {
                _summary.Text = "";
                return;
            }

            MacroStep step = _steps[_index];
            string what;
            switch (step.Type)
            {
                case "hotkey": what = Strings.Format("macro.send", Value(step)); break;
                case "text": what = Strings.Format("macro.typesText", Value(step)); break;
                case "delay": what = Strings.Get("macro.waits"); break;
                case "program": what = Strings.Format("macro.starts", Value(step)); break;
                default: what = Strings.Format("macro.runs", Value(step)); break;
            }

            if (step.DelayMs > 0)
                _summary.Text = Strings.Format("macro.summaryWithPause", _index + 1, what, step.DelayMs);
            else
                _summary.Text = Strings.Format("macro.summaryNoPause", _index + 1, what);
        }

        static string Value(MacroStep step)
        {
            if (string.IsNullOrEmpty(step.Value)) return "–";
            return step.Value;
        }

        // ------------------------------------------------------------------
        // Aktionen
        // ------------------------------------------------------------------

        void AddStep()
        {
            MacroStep step = new MacroStep();
            step.Type = "hotkey";
            step.Value = "";
            step.DelayMs = 150;

            int at = _index >= 0 ? _index + 1 : _steps.Count;
            _steps.Insert(at, step);
            RefreshList();
            _list.SelectedIndex = at;
            Changed();
        }

        void RemoveStep()
        {
            if (_index < 0 || _index >= _steps.Count) return;
            int at = _index;
            _steps.RemoveAt(at);
            RefreshList();
            if (_steps.Count == 0)
            {
                _index = -1;
                ShowStep(null);
            }
            else
            {
                _list.SelectedIndex = Math.Min(at, _steps.Count - 1);
            }
            Changed();
        }

        void MoveStep(int delta)
        {
            if (_index < 0 || _index >= _steps.Count) return;
            int target = _index + delta;
            if (target < 0 || target >= _steps.Count) return;

            MacroStep step = _steps[_index];
            _steps.RemoveAt(_index);
            _steps.Insert(target, step);
            _index = target;
            RefreshList();
            _list.SelectedIndex = target;
            Changed();
        }

        void Changed()
        {
            if (_onChanged != null) _onChanged();
        }

        void ReloadFromOriginal()
        {
            _steps.Clear();
            foreach (MacroStep s in _original) _steps.Add(s.Clone());
            _index = _steps.Count > 0 ? 0 : -1;
            RefreshList();
            if (_index >= 0) _list.SelectedIndex = 0;
            else ShowStep(null);
            Changed();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }
    }

    // ------------------------------------------------------------------
    // Kleine Bausteine, die sich SettingsWindow und MacroEditor teilen.
    // ------------------------------------------------------------------

    internal static class UiFont
    {
        static FontFamily _family;

        public static FontFamily Family
        {
            get
            {
                if (_family != null) return _family;
                try
                {
                    FontFamily candidate = new FontFamily("Segoe UI Variable Text");
                    _family = candidate;
                }
                catch
                {
                    _family = new FontFamily("Segoe UI");
                }
                return _family;
            }
        }
    }

    internal static class Ui
    {
        public static readonly Brush Bg = Tokens.Canvas;
        public static readonly Brush Card = Tokens.Surface;
        public static readonly Brush Line = Tokens.Line;
        public static readonly Brush FieldLine = Tokens.LineStrong;
        public static readonly Brush Ink = Tokens.Ink;
        public static readonly Brush InkSoft = Tokens.InkSoft;
        public static readonly Brush InkMuted = Tokens.InkMuted;
        public static readonly Brush Accent = Tokens.Primary;
        public static readonly Brush AccentSoft = Tokens.Selected;
        public static readonly Brush Warn = Tokens.Warn;
        public static readonly Brush WarnSoft = Tokens.WarnSoft;
        public static readonly Brush Subtle = Tokens.Recess;

        public static TextBlock Text(string value, double size, Brush brush)
        {
            TextBlock t = new TextBlock();
            t.Text = value;
            // Unter 12 ist am Bildschirm muehsam zu lesen. Hinweise werden
            // deshalb angehoben, ohne die Abstufung ganz aufzugeben.
            t.FontSize = size < 12 ? 12 : size;
            t.Foreground = brush;
            return t;
        }

        public static TextBlock Caption(string value)
        {
            TextBlock t = Text(value, 11, InkMuted);
            return t;
        }

        public static TextBlock Heading(string value, double size)
        {
            TextBlock t = Text(value, size, Ink);
            t.FontWeight = FontWeights.SemiBold;
            return t;
        }

        public static TextBlock Body(string value)
        {
            TextBlock t = Text(value, 12.5, InkSoft);
            t.TextWrapping = TextWrapping.Wrap;
            return t;
        }

        public static FrameworkElement Field(string label, FrameworkElement control)
        {
            StackPanel p = new StackPanel();
            TextBlock l = Caption(label);
            l.Margin = new Thickness(0, 0, 0, 4);
            p.Children.Add(l);
            p.Children.Add(control);
            return p;
        }

        public static TextBox TextField()
        {
            TextBox t = new TextBox();
            t.Padding = new Thickness(8, 6, 8, 6);
            t.BorderBrush = FieldLine;
            t.BorderThickness = new Thickness(1);
            t.Background = Brushes.White;
            t.Foreground = Ink;
            t.FontSize = 12.5;
            t.FontFamily = UiFont.Family;
            t.VerticalContentAlignment = VerticalAlignment.Center;
            return t;
        }

        public static ComboBox Combo()
        {
            ComboBox c = new ComboBox();
            c.Padding = new Thickness(6, 4, 6, 4);
            c.FontSize = 12.5;
            c.FontFamily = UiFont.Family;
            c.Foreground = Ink;
            return c;
        }

        public static Button Button(string text)
        {
            Button b = new Button();
            b.Content = text;
            b.Padding = new Thickness(13, 6, 13, 6);
            b.FontSize = 12;
            b.FontFamily = UiFont.Family;
            b.Cursor = Cursors.Hand;
            b.Background = Card;
            b.Foreground = Ink;
            b.BorderBrush = FieldLine;
            b.BorderThickness = new Thickness(1);
            return b;
        }

        public static Button PrimaryButton(string text)
        {
            Button b = Button(text);
            b.Background = Accent;
            b.Foreground = Brushes.White;
            b.BorderBrush = Accent;
            b.FontWeight = FontWeights.SemiBold;
            return b;
        }

        public static Button SmallButton(string text)
        {
            Button b = Button(text);
            b.Padding = new Thickness(9, 4, 9, 4);
            b.FontSize = 11.5;
            return b;
        }

        public static Button IconButton(string glyph, string tooltip)
        {
            Button b = new Button();
            b.Content = glyph;
            b.ToolTip = tooltip;
            b.Width = 28;
            b.Height = 26;
            b.Padding = new Thickness(0);
            b.FontSize = 11;
            b.Cursor = Cursors.Hand;
            b.Background = Card;
            b.Foreground = InkSoft;
            b.BorderBrush = FieldLine;
            b.BorderThickness = new Thickness(1);
            return b;
        }

        public static Border CardBorder()
        {
            Border b = new Border();
            b.Background = Card;
            b.BorderBrush = Line;
            b.BorderThickness = new Thickness(1);
            b.CornerRadius = new CornerRadius(6);
            return b;
        }

        public static Border Divider()
        {
            Border b = new Border();
            b.Height = 1;
            b.Background = Line;
            return b;
        }

        // Symbol aus SVG-Pfaddaten, sauber auf eine Zielgroesse skaliert.
        public static FrameworkElement Glyph(string data, double size, Brush brush)
        {
            Canvas canvas = new Canvas();
            canvas.Width = 24;
            canvas.Height = 24;

            Path path = new Path();
            try
            {
                path.Data = Geometry.Parse(data);
            }
            catch
            {
                return new Border();
            }
            path.Stroke = brush;
            path.StrokeThickness = 1.7;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            path.StrokeLineJoin = PenLineJoin.Round;
            path.Fill = Brushes.Transparent;
            path.Width = 24;
            path.Height = 24;
            canvas.Children.Add(path);

            Viewbox box = new Viewbox();
            box.Width = size;
            box.Height = size;
            box.Child = canvas;
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.VerticalAlignment = VerticalAlignment.Center;
            return box;
        }

        // Zeilenstil fuer Listen: ruhig, Auswahl als Flaeche, keine Fokusrahmen.
        public static Style ListRowStyle()
        {
            Style style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0, 0, 0, 1)));
            style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(ListBoxItem.FocusVisualStyleProperty, null));

            ControlTemplate template = new ControlTemplate(typeof(ListBoxItem));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Row";
            border.SetValue(Border.BackgroundProperty,
                new TemplateBindingExtension(ListBoxItem.BackgroundProperty));
            border.SetValue(Border.PaddingProperty, new Thickness(10, 7, 10, 7));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            border.AppendChild(content);

            template.VisualTree = border;

            Trigger hover = new Trigger();
            hover.Property = ListBoxItem.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Subtle));
            template.Triggers.Add(hover);

            Trigger selected = new Trigger();
            selected.Property = ListBoxItem.IsSelectedProperty;
            selected.Value = true;
            selected.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, AccentSoft));
            template.Triggers.Add(selected);

            style.Setters.Add(new Setter(ListBoxItem.TemplateProperty, template));
            return style;
        }
    }

    // Ein Auswahlfeld im eigenen Gewand: ruhige Flaeche, Chevron, Systemmenue.
    public class SelectControl : Button
    {
        readonly List<string> _items = new List<string>();
        TextBlock _text;
        int _index = -1;

        public event EventHandler Changed;

        public SelectControl()
        {
            Height = 31;
            Padding = new Thickness(9, 0, 8, 0);
            FontSize = 12.5;
            FontFamily = UiFont.Family;
            Foreground = Ui.Ink;
            Background = Brushes.White;
            BorderBrush = Ui.FieldLine;
            BorderThickness = new Thickness(1);
            Cursor = Cursors.Hand;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            FocusVisualStyle = null;

            _text = Ui.Text("", 12.5, Ui.Ink);
            _text.VerticalAlignment = VerticalAlignment.Center;
            _text.TextTrimming = TextTrimming.CharacterEllipsis;

            Grid content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.Children.Add(_text);

            FrameworkElement chevron = Ui.Glyph("M5,8 L12,15 L19,8", 13, Ui.InkMuted);
            chevron.VerticalAlignment = VerticalAlignment.Center;
            chevron.HorizontalAlignment = HorizontalAlignment.Right;
            chevron.Margin = new Thickness(8, 1, 0, 0);
            Grid.SetColumn(chevron, 1);
            content.Children.Add(chevron);

            Content = content;
            Click += delegate { Open(); };
        }

        public void SetItems(IEnumerable<string> items)
        {
            _items.Clear();
            foreach (string s in items) _items.Add(s);
            if (_index >= _items.Count) _index = _items.Count - 1;
            Sync();
        }

        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                int next = value;
                if (next < -1) next = -1;
                if (next >= _items.Count) next = _items.Count - 1;
                _index = next;
                Sync();
            }
        }

        public string SelectedItem
        {
            get { return _index >= 0 && _index < _items.Count ? _items[_index] : null; }
            set
            {
                _index = -1;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i], value, StringComparison.Ordinal))
                    {
                        _index = i;
                        break;
                    }
                }
                Sync();
            }
        }

        void Sync()
        {
            if (_text == null) return;
            _text.Text = SelectedItem == null ? "" : SelectedItem;
            _text.Foreground = SelectedItem == null ? Ui.InkMuted : Ui.Ink;
        }

        void Open()
        {
            if (_items.Count == 0) return;

            ContextMenu menu = new ContextMenu();
            menu.FontFamily = UiFont.Family;
            menu.FontSize = 12.5;
            menu.PlacementTarget = this;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.MinWidth = ActualWidth;

            for (int i = 0; i < _items.Count; i++)
            {
                int index = i;
                MenuItem entry = new MenuItem();
                entry.Header = _items[i];
                entry.FontFamily = UiFont.Family;
                entry.FontSize = 12.5;
                if (i == _index) entry.FontWeight = FontWeights.SemiBold;
                entry.Click += delegate
                {
                    _index = index;
                    Sync();
                    if (Changed != null) Changed(this, EventArgs.Empty);
                };
                menu.Items.Add(entry);
            }

            menu.IsOpen = true;
        }
    }

    // Zahleneingabe mit Schrittweite und Grenzen.
    public class NumericBox : TextBox
    {
        int _min;
        int _max;

        public event EventHandler ValueChanged;

        public NumericBox(int min, int max, int step)
        {
            _min = min;
            _max = max;

            Padding = new Thickness(8, 6, 8, 6);
            BorderBrush = Ui.FieldLine;
            BorderThickness = new Thickness(1);
            Background = Brushes.White;
            Foreground = Ui.Ink;
            FontSize = 12.5;
            FontFamily = UiFont.Family;
            VerticalContentAlignment = VerticalAlignment.Center;
            Text = "0";

            LostFocus += delegate { Normalize(); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { Normalize(); e.Handled = true; }
                else if (e.Key == Key.Up) { Offset(step); e.Handled = true; }
                else if (e.Key == Key.Down) { Offset(-step); e.Handled = true; }
            };
        }

        public int Value
        {
            get
            {
                int parsed;
                if (int.TryParse(Text.Trim(), out parsed)) return Clamp(parsed);
                return _min;
            }
            set
            {
                int clamped = Clamp(value);
                string text = clamped.ToString();
                if (Text != text) Text = text;
            }
        }

        int Clamp(int value)
        {
            if (value < _min) return _min;
            if (value > _max) return _max;
            return value;
        }

        void Offset(int delta)
        {
            Value = Value + delta;
            Text = Value.ToString();
            Raise();
        }

        void Normalize()
        {
            Text = Value.ToString();
            Raise();
        }

        void Raise()
        {
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }
}
