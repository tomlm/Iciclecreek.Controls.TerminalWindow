using Avalonia.Controls;
using Avalonia.Interactivity;
using Iciclecreek.Terminal;

namespace Demo.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Terminal.ProcessExited += OnProcessExited;
    }

    private void OnProcessExited(object? sender, ProcessExitedEventArgs e)
    {
        // Close the parent window when the terminal process exits
        if (this.VisualRoot is Window window)
        {
            window.Close();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        Terminal.ProcessExited -= OnProcessExited;
    }
}
