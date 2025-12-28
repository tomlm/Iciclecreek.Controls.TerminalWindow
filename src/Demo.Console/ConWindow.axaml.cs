using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Iciclecreek.Avalonia.WindowManager;

namespace Demo.Console
{
    public partial class ConWindow : ManagedWindow
    {
        public ConWindow()
        {
        }

        private void Terminal_ProcessExited(object? sender, Iciclecreek.Terminal.ProcessExitedEventArgs e)
        {
            this.Close();
        }

        private void ManagedWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            this.Terminal.Kill();
        }
    }
}