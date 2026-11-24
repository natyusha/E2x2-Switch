using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using E2x2Switch.Models;
using E2x2Switch.Services;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using GdiColor = System.Drawing.Color;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace E2x2Switch.Views;

/// <summary>Main window view and interaction logic.</summary>
public partial class MainWindow : FluentWindow
{
    #region Win32 Interop

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int attributeValue, int attributeSize);

    #endregion

    #region Constants & Fields

    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly ToppingService _topping = new();
    private readonly HotkeyService _hotkeys = new();
    private readonly AppConfig _config = AppConfig.Load();
    private NotifyIcon? _trayIcon;
    private Icon? _currentTrayIcon;

    private int _headphonesHotkeyId;
    private int _speakersHotkeyId;
    private int _gainHotkeyId;
    private int _bothHotkeyId;
    private bool _allowClose;
    private bool _startupStateApplied;

    #endregion

    #region Constructor

    /// <summary>Initializes the main window, applies Windows theme resources, registers hotkeys, and starts device monitoring.</summary>
    public MainWindow()
    {
        InitializeComponent();

        WpfColor systemAccent = ApplicationAccentColorManager.GetColorizationColor();
        ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica, updateAccent: true);
        ApplicationAccentColorManager.Apply(systemAccent, ApplicationTheme.Dark);
        SystemThemeWatcher.Watch(this);

        IntPtr windowHandle = new WindowInteropHelper(this).EnsureHandle();
        _hotkeys.Initialize(windowHandle);
        _hotkeys.HotkeyPressed += OnHotkeyPressed;

        RegisterHotkeys();
        UpdateAllPills();

        StartWithWindowsToggle.IsChecked = StartupService.IsStartWithWindowsEnabled();
        SelectStartupBehaviorItem(_config.StartupState);
        StartupBehaviorComboBox.SelectionChanged += StartupBehaviorComboBox_SelectionChanged;

        _topping.InitializeState(_config.LastOutputMode, _config.LastGainIsHigh);
        _topping.GainChanged += OnHardwareGainChanged;
        _topping.OutputModeChanged += OnHardwareOutputModeChanged;
        _topping.ConnectionChanged += OnHardwareConnectionChanged;
        _topping.StartMonitoring();

        Closing += MainWindow_Closing;
        SetupTray();
        UpdateTrayState();
        UpdateConnectionStatus();

        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    #endregion

    #region Init & Hardware Sync

    private void ApplyInitialStartupState()
    {
        if (_startupStateApplied || !_topping.IsConnected)
            return;

        _startupStateApplied = true;

        if (_config.StartupState == StartupBehavior.None)
            return;

        Task.Run(() =>
        {
            switch (_config.StartupState)
            {
                case StartupBehavior.RestoreLastState:
                    _topping.ApplyState(_config.LastOutputMode, _config.LastGainIsHigh);
                    break;
                case StartupBehavior.HeadphonesLowGain:
                    _topping.ApplyState(AudioOutputMode.Headphones, false);
                    break;
                case StartupBehavior.HeadphonesHighGain:
                    _topping.ApplyState(AudioOutputMode.Headphones, true);
                    break;
                case StartupBehavior.SpeakersOnly:
                    _topping.ApplyState(AudioOutputMode.Speakers, false);
                    break;
                case StartupBehavior.BothOutputs:
                    _topping.ApplyState(AudioOutputMode.Both, false);
                    break;
                case StartupBehavior.None:
                    break;
                default:
                    break;
            }

            Dispatcher.BeginInvoke(PersistStateAndRefresh);
        });
    }

    private void SelectStartupBehaviorItem(StartupBehavior behavior)
    {
        string behaviorTag = behavior.ToString();
        foreach (ComboBoxItem item in StartupBehaviorComboBox.Items)
            if (item.Tag?.ToString() == behaviorTag)
            {
                StartupBehaviorComboBox.SelectedItem = item;
                break;
            }
    }

    private void OnHardwareGainChanged(bool gainIsHigh) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (_allowClose)
                return;
            _config.LastGainIsHigh = gainIsHigh;
            _config.Save();
            UpdateTrayState();
        });

    private void OnHardwareOutputModeChanged(AudioOutputMode mode) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (_allowClose)
                return;
            _config.LastOutputMode = mode;
            _config.Save();
            UpdateTrayState();
        });

    private void OnHardwareConnectionChanged(bool isConnected) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (_allowClose)
                return;
            UpdateConnectionStatus();
            if (isConnected)
                ApplyInitialStartupState();
        });

    private void RegisterHotkeys()
    {
        _hotkeys.UnregisterAll();
        _headphonesHotkeyId = _hotkeys.Register(_config.HeadphonesOnly.Modifiers, _config.HeadphonesOnly.Key);
        _speakersHotkeyId = _hotkeys.Register(_config.SpeakersOnly.Modifiers, _config.SpeakersOnly.Key);
        _gainHotkeyId = _hotkeys.Register(_config.GainToggle.Modifiers, _config.GainToggle.Key);
        _bothHotkeyId = _hotkeys.Register(_config.BothEnabled.Modifiers, _config.BothEnabled.Key);
    }

    #endregion

    #region UI & Tray Updates

    /// <summary>Restores the window from the system tray and brings it to the foreground.</summary>
    public void RestoreFromTray()
    {
        if (_allowClose)
            return;

        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        bool wasTopmost = Topmost;
        Topmost = true;
        Activate();
        Topmost = wasTopmost;
        Focus();
    }

    /// <summary>Permits the window to close permanently during application shutdown or OS session ending.</summary>
    public void PrepareForShutdown() => _allowClose = true;

    private void UpdateConnectionStatus()
    {
        StatusFooterText.Text = _topping.IsConnected ? $"● Connected (VID: 0x{ToppingService.VendorId:X4}, PID: 0x{ToppingService.ProductId:X4})" : "○ Disconnected (TOPPING E2x2 not detected)";
        StatusFooterText.Foreground = (WpfBrush)FindResource(_topping.IsConnected ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush");
    }

    private void UpdateAllPills()
    {
        RenderPillPlate(HeadphonesPillsPanel, _config.HeadphonesOnly);
        RenderPillPlate(SpeakersPillsPanel, _config.SpeakersOnly);
        RenderPillPlate(GainPillsPanel, _config.GainToggle);
        RenderPillPlate(BothPillsPanel, _config.BothEnabled);
    }

    private void RenderPillPlate(StackPanel panel, HotkeyBinding binding)
    {
        panel.Children.Clear();

        foreach (string label in binding.GetKeyLabels())
            panel.Children.Add(
                new Border
                {
                    Background = (WpfBrush)FindResource("AccentFillColorDefaultBrush"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 4, 0),
                    Child = new WpfTextBlock
                    {
                        Text = label,
                        Foreground = (WpfBrush)FindResource("TextOnAccentFillColorPrimaryBrush"),
                        FontWeight = FontWeights.Bold,
                        FontSize = 11,
                    },
                }
            );

        panel.Children.Add(
            new SymbolIcon
            {
                Symbol = SymbolRegular.Edit24,
                FontSize = 14,
                Foreground = (WpfBrush)FindResource("TextFillColorSecondaryBrush"),
                Margin = new Thickness(4, 0, 2, 0),
            }
        );
    }

    private void PersistStateAndRefresh()
    {
        if (_allowClose)
            return;

        _config.LastOutputMode = _topping.CurrentMode;
        _config.LastGainIsHigh = _topping.GainIsHigh;
        _config.Save();

        UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        if (_trayIcon == null)
            return;

        Icon? previousIcon = _currentTrayIcon;
        _currentTrayIcon = TrayIconService.GetTrayIcon(_topping.CurrentMode, _topping.GainIsHigh);
        _trayIcon.Icon = _currentTrayIcon;

        previousIcon?.Dispose();

        string modeName = _topping.CurrentMode switch
        {
            AudioOutputMode.Headphones => "Headphones",
            AudioOutputMode.Speakers => "Speakers",
            AudioOutputMode.Both => "Both (HP + Speakers)",
            _ => E2x2SwitchConstants.Name,
        };
        string gainText = _topping.GainIsHigh ? "+17dBu High Gain" : "Low Gain";
        _trayIcon.Text = $"{E2x2SwitchConstants.Name} - {modeName} ({gainText})";
    }

    private void UpdateContextMenuTheme()
    {
        if (_trayIcon?.ContextMenuStrip == null)
            return;

        bool isLight = TrayIconService.IsTaskbarLight();
        _trayIcon.ContextMenuStrip.Renderer = new ThemeAwareContextMenuRenderer(isLight);

        GdiColor textColor = isLight ? GdiColor.FromArgb(25, 25, 25) : GdiColor.White;
        foreach (ToolStripItem menuItem in _trayIcon.ContextMenuStrip.Items)
            menuItem.ForeColor = textColor;

        if (_trayIcon.ContextMenuStrip.IsHandleCreated)
        {
            int darkMode = isLight ? 0 : 1;
            DwmSetWindowAttribute(_trayIcon.ContextMenuStrip.Handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
        }
    }

    #endregion

    #region Hotkey/Card Handlers

    private void OnHotkeyPressed(int hotkeyId)
    {
        if (hotkeyId == _headphonesHotkeyId)
            SetHeadphonesMode();
        else if (hotkeyId == _speakersHotkeyId)
            SetSpeakersMode();
        else if (hotkeyId == _gainHotkeyId)
            ToggleGainMode();
        else if (hotkeyId == _bothHotkeyId)
            SetBothMode();
    }

    private void SetHeadphonesMode()
    {
        _startupStateApplied = true;
        _topping.SetHeadphonesOnly();
        PersistStateAndRefresh();
    }

    private void SetSpeakersMode()
    {
        _startupStateApplied = true;
        _topping.SetSpeakersOnly();
        PersistStateAndRefresh();
    }

    private void SetBothMode()
    {
        _startupStateApplied = true;
        _topping.SetBoth();
        PersistStateAndRefresh();
    }

    private void ToggleGainMode()
    {
        _startupStateApplied = true;
        _topping.ToggleGain();
        PersistStateAndRefresh();
    }

    private void OpenShortcutDialog(string title, Func<AppConfig, HotkeyBinding> getter, Action<AppConfig, HotkeyBinding> setter)
    {
        _hotkeys.UnregisterAll();
        try
        {
            var dialog = new HotkeyDialog(title, getter(_config)) { Owner = this };
            if (dialog.ShowDialog() != true)
                return;

            setter(_config, dialog.ResultBinding);
            _config.Save();
            UpdateAllPills();
        }
        finally
        {
            RegisterHotkeys();
        }
    }

    private void HeadphonesCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs) => SetHeadphonesMode();

    private void SpeakersCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs) => SetSpeakersMode();

    private void GainCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs) => ToggleGainMode();

    private void BothCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs) => SetBothMode();

    private void EditHeadphones_Click(object sender, RoutedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        OpenShortcutDialog("Headphones only shortcut", config => config.HeadphonesOnly, (config, binding) => config.HeadphonesOnly = binding);
    }

    private void EditSpeakers_Click(object sender, RoutedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        OpenShortcutDialog("Speakers only shortcut", config => config.SpeakersOnly, (config, binding) => config.SpeakersOnly = binding);
    }

    private void EditGain_Click(object sender, RoutedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        OpenShortcutDialog("Gain toggle shortcut", config => config.GainToggle, (config, binding) => config.GainToggle = binding);
    }

    private void EditBoth_Click(object sender, RoutedEventArgs eventArgs)
    {
        eventArgs.Handled = true;
        OpenShortcutDialog("Both outputs shortcut", config => config.BothEnabled, (config, binding) => config.BothEnabled = binding);
    }

    private void StartupBehaviorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (StartupBehaviorComboBox.SelectedItem is ComboBoxItem item && Enum.TryParse(item.Tag?.ToString(), out StartupBehavior behavior))
        {
            _config.StartupState = behavior;
            _config.Save();
        }
    }

    private void StartWithWindowsToggle_Click(object sender, RoutedEventArgs eventArgs) => StartupService.SetStartWithWindows(StartWithWindowsToggle.IsChecked == true);

    #endregion

    #region Wndw/Tray Lifecycle

    /// <summary>Handles Esc key presses to close open dropdowns or clear control focus highlights.</summary>
    /// <param name="eventArgs">Keyboard event arguments.</param>
    protected override void OnPreviewKeyDown(WpfKeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            if (StartupBehaviorComboBox.IsDropDownOpen)
            {
                StartupBehaviorComboBox.IsDropDownOpen = false;
                eventArgs.Handled = true;
                return;
            }

            // Clear keyboard focus from any control to remove the focus highlight
            Keyboard.ClearFocus();
            Focus();
            eventArgs.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(eventArgs);
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs eventArgs) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (_allowClose)
                return;
            UpdateTrayState();
            UpdateContextMenuTheme();
        });

    private void SetupTray()
    {
        try
        {
            _trayIcon = new NotifyIcon { Visible = true, Text = E2x2SwitchConstants.Name };
            _trayIcon.DoubleClick += (sender, eventArgs) => RestoreFromTray();

            var menu = new ContextMenuStrip { ShowImageMargin = false, DropShadowEnabled = true };

            menu.HandleCreated += (sender, eventArgs) => UpdateContextMenuTheme();
            menu.Opened += (sender, eventArgs) => UpdateContextMenuTheme();
            menu.Items.Add(new ToolStripMenuItem("Headphones Only", null, (sender, eventArgs) => SetHeadphonesMode()));
            menu.Items.Add(new ToolStripMenuItem("Speakers Only", null, (sender, eventArgs) => SetSpeakersMode()));
            menu.Items.Add(new ToolStripMenuItem("Both Outputs", null, (sender, eventArgs) => SetBothMode()));
            menu.Items.Add(new ToolStripMenuItem("Gain Toggle", null, (sender, eventArgs) => ToggleGainMode()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem($"Open {E2x2SwitchConstants.Name}", null, (sender, eventArgs) => RestoreFromTray()));
            menu.Items.Add(new ToolStripMenuItem("Exit", null, (sender, eventArgs) =>
                {
                    PrepareForShutdown();
                    WpfApplication.Current.Shutdown();
                }));

            _trayIcon.ContextMenuStrip = menu;
            UpdateContextMenuTheme();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Tray setup warning: {exception.Message}");
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs eventArgs)
    {
        if (_allowClose)
            return;

        eventArgs.Cancel = true;
        Hide();
    }

    /// <summary>Stops active timers, unhooks events, and disposes native tray and HID resources.</summary>
    /// <param name="eventArgs">Event data for window closure.</param>
    protected override void OnClosed(EventArgs eventArgs)
    {
        _allowClose = true;
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        _hotkeys.Dispose();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _currentTrayIcon?.Dispose();
        _currentTrayIcon = null;
        _topping.Dispose();
        base.OnClosed(eventArgs);
    }

    #endregion
}
