using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Iciclecreek.Avalonia.Terminal;
using Pty;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using XT = global::XTerm;

namespace Iciclecreek.Terminal
{
    public class TerminalControl : Control
    {
        private XT.Terminal _terminal = new XT.Terminal(null);
        private FormattedText _measureText;
        private double _charWidth;
        private double _charHeight;
        private int _bufferSize = 100;

        // Process management
        private System.Diagnostics.Process _process;
        private CancellationTokenSource _processCts;

        public static readonly DirectProperty<TerminalControl, int> BufferSizeProperty =
            AvaloniaProperty.RegisterDirect<TerminalControl, int>(
                nameof(BufferSize),
                o => o._bufferSize,
                (o, v) => o._bufferSize = v);

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<TerminalControl, FontFamily>(
                nameof(FontFamily),
                defaultValue: FontFamily.Default);

        public static readonly StyledProperty<double> FontSizeProperty =
            AvaloniaProperty.Register<TerminalControl, double>(
                nameof(FontSize),
                defaultValue: 12);

        public static readonly StyledProperty<FontStyle> FontStyleProperty =
            AvaloniaProperty.Register<TerminalControl, FontStyle>(
                nameof(FontStyle),
                defaultValue: FontStyle.Normal);

        public static readonly StyledProperty<FontWeight> FontWeightProperty =
            AvaloniaProperty.Register<TerminalControl, FontWeight>(
                nameof(FontWeight),
                defaultValue: FontWeight.Normal);

        public static readonly StyledProperty<TextDecorationLocation?> TextDecorationsProperty =
            AvaloniaProperty.Register<TerminalControl, TextDecorationLocation?>(
                nameof(TextDecorations),
                defaultValue: null);

        public static readonly StyledProperty<Color> ForegroundColorProperty =
            AvaloniaProperty.Register<TerminalControl, Color>(
                nameof(ForegroundColor),
                defaultValue: Colors.White);

        public static readonly StyledProperty<Color> BackgroundColorProperty =
            AvaloniaProperty.Register<TerminalControl, Color>(
                nameof(BackgroundColor),
                defaultValue: Colors.Transparent);

        public static readonly StyledProperty<IBrush> SelectionBrushProperty =
            AvaloniaProperty.Register<TerminalControl, IBrush>(
                nameof(SelectionBrush),
                defaultValue: new SolidColorBrush(Color.FromArgb(128, 0, 120, 215)));

        public static readonly StyledProperty<string> ProcessProperty =
            AvaloniaProperty.Register<TerminalControl, string>(
                nameof(Process),
                defaultValue: "cmd.exe");

        public static readonly StyledProperty<IList<string>> ArgsProperty =
            AvaloniaProperty.Register<TerminalControl, IList<string>>(
                nameof(Args),
                defaultValue: Array.Empty<string>());


        static TerminalControl()
        {
            AffectsRender<TerminalControl>(
                FontFamilyProperty,
                FontSizeProperty,
                FontStyleProperty,
                FontWeightProperty,
                TextDecorationsProperty,
                //ForegroundColorProperty,
                //BackgroundColorProperty,
                SelectionBrushProperty);

            AffectsMeasure<TerminalControl>(
                FontFamilyProperty,
                FontSizeProperty,
                FontStyleProperty,
                FontWeightProperty,
                BufferSizeProperty);

            FocusableProperty.OverrideDefaultValue<TerminalControl>(true);
        }

        public TerminalControl()
        {
            Focusable = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public int BufferSize
        {
            get => _bufferSize;
            set
            {
                _terminal.Resize(_terminal.Cols, value);
                SetAndRaise(BufferSizeProperty, ref _bufferSize, value);
                InvalidateVisual();
            }
        }

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public FontStyle FontStyle
        {
            get => GetValue(FontStyleProperty);
            set => SetValue(FontStyleProperty, value);
        }

        public FontWeight FontWeight
        {
            get => GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        public TextDecorationLocation? TextDecorations
        {
            get => GetValue(TextDecorationsProperty);
            set => SetValue(TextDecorationsProperty, value);
        }

        public Color ForegroundColor
        {
            get => GetValue(ForegroundColorProperty);
            set => SetValue(ForegroundColorProperty, value);
        }

        public Color BackgroundColor
        {
            get => GetValue(BackgroundColorProperty);
            set => SetValue(BackgroundColorProperty, value);
        }

        public IBrush SelectionBrush
        {
            get => GetValue(SelectionBrushProperty);
            set => SetValue(SelectionBrushProperty, value);
        }

        public string Process
        {
            get => GetValue(ProcessProperty);
            set => SetValue(ProcessProperty, value);
        }

        public IList<string> Args
        {
            get => GetValue(ArgsProperty);
            set => SetValue(ArgsProperty, value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Process))
            {
                LaunchProcess();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CleanupProcess();
        }

        private void LaunchProcess()
        {
            CleanupProcess();

            try
            {
                _processCts = new CancellationTokenSource();

                var startInfo = new ProcessStartInfo
                {
                    FileName = Process,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (Args != null && Args.Count > 0)
                {
                    foreach (var arg in Args)
                    {
                        startInfo.ArgumentList.Add(arg);
                    }
                }

                _process = new System.Diagnostics.Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                _process.OutputDataReceived += OnOutputDataReceived;
                _process.ErrorDataReceived += OnErrorDataReceived;
                _process.Exited += OnProcessExited;

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                _terminal.WriteLine($"Error launching process: {ex.Message}\n");
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && !_processCts.Token.IsCancellationRequested)
            {
                _terminal.WriteLine(e.Data);
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && !_processCts.Token.IsCancellationRequested)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    // Optionally use a different color for errors
                    _terminal.WriteLine(e.Data);
                });
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _terminal.WriteLine($"\nProcess exited with code: {_process?.ExitCode}\n");
            });
        }

        private void CleanupProcess()
        {
            _processCts?.Cancel();

            if (_process != null)
            {
                try
                {
                    _process.OutputDataReceived -= OnOutputDataReceived;
                    _process.ErrorDataReceived -= OnErrorDataReceived;
                    _process.Exited -= OnProcessExited;

                    if (!_process.HasExited)
                    {
                        _process.Kill();
                    }

                    _process.Dispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
                finally
                {
                    _process = null;
                }
            }

            _processCts?.Dispose();
            _processCts = null;
        }


        private void UpdateTextMetrics()
        {
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight);
            _measureText = new FormattedText(
                "W",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Brushes.Black);

            _charWidth = _measureText.Width;
            _charHeight = _measureText.Height;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            UpdateTextMetrics();

            var desiredWidth = availableSize.Width;
            var desiredHeight = _bufferSize * _charHeight;

            return new Size(desiredWidth, desiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize) => finalSize;


        public override void Render(DrawingContext context)
        {
            StringBuilder sb = new StringBuilder();
            for (int y = _terminal.Buffer.ScrollTop; y < _terminal.Buffer.ScrollBottom; y++)
            {
                var line = _terminal.Buffer.GetLine(y)!;
                for (int x = 0; x < _terminal.Cols;)
                {
                    var cell = line[x];

                    var text = String.Join(String.Empty, line.Skip(x)
                        .TakeWhile(cell2 => cell.Attributes == cell2.Attributes)
                        .Select(cell2 => cell2.Content).ToArray());

                    double posX = x * _charWidth;
                    double posY = y * _charHeight;

                    // draw rectangle for line
                    var bgBrush = new SolidColorBrush(cell.GetBackground() ?? this.BackgroundColor);
                    context.FillRectangle(bgBrush, new Rect(posX, posY, text.Length * _charWidth, _charHeight));

                    // draw text
                    var typeface = new Typeface(FontFamily, cell.GetFontStyle(), cell.GetFontWeight());
                    var fgBrush = new SolidColorBrush(cell.GetForegroundColor() ?? this.ForegroundColor);
                    var formattedText = new FormattedText(
                        text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        FontSize,
                        fgBrush);
                    context.DrawText(formattedText, new Point(posX, posY));
                    x += text.Length;
                }
            }
        }

    }

}
