using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Iciclecreek.Avalonia.Terminal;
using Pty.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private Pty.Net.IPtyConnection _ptyConnection;
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
                defaultValue: RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sh");

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

        private async void LaunchProcess()
        {
            CleanupProcess();

            try
            {
                _processCts = new CancellationTokenSource();

                // Determine the process to launch based on OS if not explicitly set
                string processToLaunch = Process;
                if (string.IsNullOrEmpty(processToLaunch))
                {
                    processToLaunch = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sh";
                }

                var options = new PtyOptions
                {
                    Name = processToLaunch,
                    Cols = _terminal.Cols,
                    Rows = _terminal.Rows,
                    Cwd = Environment.CurrentDirectory,
                    App = processToLaunch
                };

                // Add arguments if provided
                if (Args != null && Args.Count > 0)
                {
                    options.CommandLine = Args.ToArray();
                }

                _ptyConnection = await PtyProvider.SpawnAsync(options, _processCts.Token);

                // Start reading from the PTY connection
                _ = Task.Run(async () => await ReadPtyOutputAsync(_processCts.Token), _processCts.Token);
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _terminal.WriteLine($"Error launching process: {ex.Message}\n");
                });
            }
        }

        private async Task ReadPtyOutputAsync(CancellationToken cancellationToken)
        {
            try
            {
                var buffer = new byte[8192];
                while (!cancellationToken.IsCancellationRequested && _ptyConnection != null)
                {
                    var bytesRead = await _ptyConnection.ReaderStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        // Process has exited
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _terminal.WriteLine($"\nProcess exited with code: {_ptyConnection?.ExitCode ?? 0}\n");
                        });
                        break;
                    }

                    var output = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _terminal.Write(output);
                        InvalidateVisual();
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _terminal.WriteLine($"\nError reading from process: {ex.Message}\n");
                });
            }
        }

        private void CleanupProcess()
        {
            _processCts?.Cancel();

            if (_ptyConnection != null)
            {
                try
                {
                    _ptyConnection.Kill();
                    _ptyConnection.Dispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
                finally
                {
                    _ptyConnection = null;
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
