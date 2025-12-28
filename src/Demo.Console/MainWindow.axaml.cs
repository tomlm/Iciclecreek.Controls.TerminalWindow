using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;

namespace Demo.Console
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Terminal_ProcessExited(object? sender, Iciclecreek.Terminal.ProcessExitedEventArgs e)
        {
            var lifetime = Application.Current!.ApplicationLifetime as IControlledApplicationLifetime;
            lifetime!.Shutdown();
        }

        private void AddWindow_Click(object? sender, RoutedEventArgs e)
        {
            var consoleWindow = new ConWindow()
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width=80, 
                Height=25
            };
            consoleWindow.Show(this);
        }

    }
}