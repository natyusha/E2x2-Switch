using System.Windows;
using System.Windows.Threading;
using E2x2Switch.Views;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace E2x2Switch;

/// <summary>Interaction logic for App.xaml</summary>
public partial class App : WpfApplication
{
    #region Fields

    private static readonly string s_appEventName = $@"Local\{E2x2SwitchConstants.Name.Replace(" ", "")}_SingleInstance_Event";
    private static EventWaitHandle? s_instanceEvent;
    private static Thread? s_eventThread;

    #endregion

    #region Lifecycle

    /// <summary>Initializes single-instance coordination, exception handlers, and the main window.</summary>
    /// <param name="eventArgs">Command-line arguments passed at application startup.</param>
    protected override void OnStartup(StartupEventArgs eventArgs)
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

        base.OnStartup(eventArgs);

        bool startInTray = eventArgs.Args.Any(argument => argument.Equals("--tray", StringComparison.OrdinalIgnoreCase) || argument.Equals("-tray", StringComparison.OrdinalIgnoreCase));
        var mainWindow = new MainWindow();

        SessionEnding += (sender, sessionEventArgs) => mainWindow.PrepareForShutdown();

        if (!startInTray)
            mainWindow.Show();

        // Listen for launch signals from secondary instances with safe disposal handling
        EventWaitHandle activeEvent = s_instanceEvent;
        s_eventThread = new Thread(() =>
        {
            try
            {
                while (activeEvent.WaitOne())
                    Dispatcher.BeginInvoke(mainWindow.RestoreFromTray);
            }
            catch (ObjectDisposedException) { }
        })
        {
            IsBackground = true,
        };
        s_eventThread.Start();
    }

    /// <summary>Releases single-instance synchronization resources on application exit.</summary>
    /// <param name="eventArgs">Exit event data.</param>
    protected override void OnExit(ExitEventArgs eventArgs)
    {
        s_instanceEvent?.Dispose();
        s_instanceEvent = null;
        base.OnExit(eventArgs);
    }

    #endregion

    #region Exception Handlers

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        WpfMessageBox.Show(
            $"Dispatcher Exception:\n\n{eventArgs.Exception.Message}\n\nStack Trace:\n{eventArgs.Exception.StackTrace}",
            $"{E2x2SwitchConstants.Name} Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
        eventArgs.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
            WpfMessageBox.Show($"Fatal Exception:\n\n{exception.Message}\n\nStack Trace:\n{exception.StackTrace}", $"{E2x2SwitchConstants.Name} Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    #endregion
}
