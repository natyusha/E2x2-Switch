using System.Windows;
using System.Windows.Threading;
using E2x2Switch.Views;

namespace E2x2Switch;

/// <summary>Interaction logic for App.xaml</summary>
public partial class App : System.Windows.Application
{
    private static readonly string s_appEventName = $"{E2x2SwitchConstants.Name.Replace(" ", "")}_SingleInstance_Event";
    private static EventWaitHandle? s_instanceEvent;
    private static Thread? s_eventThread;

    protected override void OnStartup(StartupEventArgs e)
    {
        s_instanceEvent = new EventWaitHandle(false, EventResetMode.AutoReset, s_appEventName, out bool isNewInstance);

        if (!isNewInstance)
        {
            s_instanceEvent.Set();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        base.OnStartup(e);

        bool startInTray = e.Args.Any(arg => arg.Equals("--tray", StringComparison.OrdinalIgnoreCase) || arg.Equals("-tray", StringComparison.OrdinalIgnoreCase));

        var mainWindow = new MainWindow();

        if (!startInTray)
        {
            mainWindow.Show();
        }

        s_eventThread = new Thread(() =>
        {
            while (s_instanceEvent.WaitOne())
            {
                Dispatcher.BeginInvoke(() =>
                {
                    mainWindow.Show();
                    if (mainWindow.WindowState == WindowState.Minimized)
                    {
                        mainWindow.WindowState = WindowState.Normal;
                    }
                    mainWindow.Activate();
                    mainWindow.Focus();
                });
            }
        })
        {
            IsBackground = true,
        };
        s_eventThread.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        s_instanceEvent?.Dispose();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Windows.MessageBox.Show(
            $"Dispatcher Exception:\n\n{e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}",
            $"{E2x2SwitchConstants.Name} Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            System.Windows.MessageBox.Show($"Fatal Exception:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", $"{E2x2SwitchConstants.Name} Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
