using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DiskSpace
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var window = new Window();
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Title = "Disk Space";
            window.Loaded += Window_Loaded;
            var app = new Application();
            app.Run(window);
        }

        private static void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Window window = (Window)sender;
            var textBlock = new TextBlock();
            textBlock.Margin = new Thickness(32);
            window.Content = textBlock;

            var dispatcherTimer = new DispatcherTimer(
                TimeSpan.FromSeconds(1),
                DispatcherPriority.Background,
                (s, e) => { textBlock.Text = GetText(); },
                window.Dispatcher);
            dispatcherTimer.Start();
        }

        private static string GetText()
        {
            var driveInfo = new DriveInfo("C:");
            return $"Free disk space on C: {driveInfo.AvailableFreeSpace:N0}";
        }
    }
}
