using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaTerminal;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Demo.Views;

public partial class MainView : UserControl
{
    private Process _process;
    private StreamWriter _streamWriter;
    private CancellationTokenSource _cancellationTokenSource = new();

    public TerminalControlModel model = new();

    public MainView()
    {
        InitializeComponent();
        StartProcess();

        model.UserInput += Input;
    }

    private void StartProcess()
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
        };

        _process.Start();

        _streamWriter = _process.StandardInput;
        _streamWriter.AutoFlush = true;

        _ = Task.Run(() => ReadOutputAsync(_process.StandardOutput.BaseStream), _cancellationTokenSource.Token);
        _ = Task.Run(() => ReadOutputAsync(_process.StandardError.BaseStream), _cancellationTokenSource.Token);
    }

    private async Task ReadOutputAsync(Stream stream)
    {
        const int bufferSize = 256;
        byte[] buffer = new byte[bufferSize];

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested && stream.CanRead)
            {
                var nRead = await stream.ReadAsync(buffer, _cancellationTokenSource.Token);
                if (nRead > 0)
                {
                    byte[] data = new byte[nRead];
                    Array.Copy(buffer, data, nRead);

                    Dispatcher.UIThread.Post(() => model.Feed(data, nRead), DispatcherPriority.Send);
                }
                else
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }

    private void Input(byte[] input)
    {
        try
        {
            if (_streamWriter?.BaseStream?.CanWrite == true)
            {
                _streamWriter.BaseStream.Write(input, 0, input.Length);
                _streamWriter.BaseStream.Flush();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Input error: {ex.Message}");
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        
        _cancellationTokenSource?.Cancel();
        _streamWriter?.Dispose();
        
        try
        {
            _process?.Kill();
        }
        catch { }
        
        _process?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
