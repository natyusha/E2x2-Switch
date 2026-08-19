using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using E2x2Switch.Models;
using E2x2Switch.Services;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.Brush;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace E2x2Switch.Views;

/// <summary>Main window view and interaction logic.</summary>
public partial class MainWindow : FluentWindow
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly ToppingService _topping = new();
    private readonly HotkeyService _hotkeys = new();
    private readonly AppConfig _config = AppConfig.Load();
    private readonly DispatcherTimer _heartbeatTimer = new();
    private NotifyIcon? _trayIcon;
    private Icon? _currentTrayIcon;

    private int _idHp;
    private int _idSpk;
    private int _idGain;
    private int _idBoth;

    public MainWindow()
    {
        InitializeComponent();

        var systemAccent = ApplicationAccentColorManager.GetColorizationColor();
        ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica, updateAccent: true);
        ApplicationAccentColorManager.Apply(systemAccent, ApplicationTheme.Dark);
        SystemThemeWatcher.Watch(this);

        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
        _hotkeys.Initialize(hwnd);
        _hotkeys.HotkeyPressed += OnHotkeyPressed;

        RegisterHotkeys();
        UpdateAllPills();

        StartWithWindowsToggle.IsChecked = StartupService.IsStartWithWindowsEnabled();
        SelectStartupBehaviorItem(_config.StartupState);

        _topping.GainChanged += OnHardwareGainChanged;
        _topping.OutputModeChanged += OnHardwareOutputModeChanged;
        _topping.ConnectionChanged += OnHardwareConnectionChanged;
        _topping.StartMonitoring();

        ApplyInitialStartupState();

        _heartbeatTimer.Interval = TimeSpan.FromSeconds(2);
        _heartbeatTimer.Tick += (s, e) => UpdateConnectionStatus();
        _heartbeatTimer.Start();
        UpdateConnectionStatus();

        Closing += MainWindow_Closing;
        SetupTray();
        UpdateTrayState();

        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    protected override void OnPreviewKeyDown(WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (StartupBehaviorComboBox.IsDropDownOpen)
            {
                StartupBehaviorComboBox.IsDropDownOpen = false;
                e.Handled = true;
                return;
            }

            // Clear keyboard focus from any control to remove the focus highlight
            Keyboard.ClearFocus();
            Focus();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void ApplyInitialStartupState()
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
            default:
                _topping.InitializeState(_config.LastOutputMode, _config.LastGainIsHigh);
                break;
        }

        PersistStateAndRefresh();
    }

    private void SelectStartupBehaviorItem(StartupBehavior behavior)
    {
        string tag = behavior.ToString();
        foreach (ComboBoxItem item in StartupBehaviorComboBox.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                StartupBehaviorComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void StartupBehaviorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StartupBehaviorComboBox.SelectedItem is ComboBoxItem item && Enum.TryParse<StartupBehavior>(item.Tag?.ToString(), out var behavior))
        {
            _config.StartupState = behavior;
            _config.Save();
        }
    }

    private void OnHardwareGainChanged(bool gainIsHigh)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _config.LastGainIsHigh = gainIsHigh;
            _config.Save();
            UpdateTrayState();
        });
    }

    private void OnHardwareOutputModeChanged(AudioOutputMode mode)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _config.LastOutputMode = mode;
            _config.Save();
            UpdateTrayState();
        });
    }

    private void OnHardwareConnectionChanged(bool isConnected)
    {
        Dispatcher.BeginInvoke(UpdateConnectionStatus);
    }

    private void UpdateConnectionStatus()
    {
        if (_topping.IsConnected)
        {
            StatusFooterText.Text = $"● Connected (VID: 0x{ToppingService.Vid:X4}, PID: 0x{ToppingService.Pid:X4})";
            StatusFooterText.Foreground = (WpfBrush)FindResource("SystemFillColorSuccessBrush");
        }
        else
        {
            StatusFooterText.Text = "○ Disconnected (TOPPING E2x2 not detected)";
            StatusFooterText.Foreground = (WpfBrush)FindResource("SystemFillColorCriticalBrush");
        }
    }

    private void StartWithWindowsToggle_Click(object sender, RoutedEventArgs e)
    {
        StartupService.SetStartWithWindows(StartWithWindowsToggle.IsChecked == true);
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateTrayState();
            UpdateContextMenuTheme();
        });
    }

    private void RegisterHotkeys()
    {
        _hotkeys.UnregisterAll();
        _idHp = _hotkeys.Register(_config.HeadphonesOnly.Modifiers, _config.HeadphonesOnly.Key);
        _idSpk = _hotkeys.Register(_config.SpeakersOnly.Modifiers, _config.SpeakersOnly.Key);
        _idGain = _hotkeys.Register(_config.GainToggle.Modifiers, _config.GainToggle.Key);
        _idBoth = _hotkeys.Register(_config.BothEnabled.Modifiers, _config.BothEnabled.Key);
    }

    private void UpdateAllPills()
    {
        RenderPillPlate(PillsHp, _config.HeadphonesOnly);
        RenderPillPlate(PillsSpk, _config.SpeakersOnly);
        RenderPillPlate(PillsGain, _config.GainToggle);
        RenderPillPlate(PillsBoth, _config.BothEnabled);
    }

    private void RenderPillPlate(StackPanel panel, HotkeyBinding binding)
    {
        panel.Children.Clear();

        foreach (var label in binding.GetKeyLabels())
        {
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
        }

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

    private void OnHotkeyPressed(int id)
    {
        if (id == _idHp)
            SetHeadphonesMode();
        if (id == _idSpk)
            SetSpeakersMode();
        if (id == _idGain)
            ToggleGainMode();
        if (id == _idBoth)
            SetBothMode();
    }

    private void SetHeadphonesMode()
    {
        _topping.SetHeadphonesOnly();
        PersistStateAndRefresh();
    }

    private void SetSpeakersMode()
    {
        _topping.SetSpeakersOnly();
        PersistStateAndRefresh();
    }

    private void SetBothMode()
    {
        _topping.SetBoth();
        PersistStateAndRefresh();
    }

    private void ToggleGainMode()
    {
        _topping.ToggleGain();
        PersistStateAndRefresh();
    }

    private void PersistStateAndRefresh()
    {
        _config.LastOutputMode = _topping.CurrentMode;
        _config.LastGainIsHigh = _topping.GainIsHigh;
        _config.Save();

        UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        if (_trayIcon == null)
            return;

        Icon? oldIcon = _currentTrayIcon;
        _currentTrayIcon = TrayIconService.GetTrayIcon(_topping.CurrentMode, _topping.GainIsHigh);
        _trayIcon.Icon = _currentTrayIcon;

        oldIcon?.Dispose();

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

    private void OpenShortcutDialog(string title, Func<AppConfig, HotkeyBinding> getter, Action<AppConfig, HotkeyBinding> setter)
    {
        var dialog = new HotkeyDialog(title, getter(_config)) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            setter(_config, dialog.ResultBinding);
            _config.Save();
            RegisterHotkeys();
            UpdateAllPills();
        }
    }

    private void CardHp_Click(object sender, MouseButtonEventArgs e) => SetHeadphonesMode();

    private void CardSpk_Click(object sender, MouseButtonEventArgs e) => SetSpeakersMode();

    private void CardGain_Click(object sender, MouseButtonEventArgs e) => ToggleGainMode();

    private void CardBoth_Click(object sender, MouseButtonEventArgs e) => SetBothMode();

    private void EditHp_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenShortcutDialog("Headphones only shortcut", c => c.HeadphonesOnly, (c, b) => c.HeadphonesOnly = b);
    }

    private void EditSpk_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenShortcutDialog("Speakers only shortcut", c => c.SpeakersOnly, (c, b) => c.SpeakersOnly = b);
    }

    private void EditGain_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenShortcutDialog("Gain toggle shortcut", c => c.GainToggle, (c, b) => c.GainToggle = b);
    }

    private void EditBoth_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenShortcutDialog("Both outputs shortcut", c => c.BothEnabled, (c, b) => c.BothEnabled = b);
    }

    private void SetupTray()
    {
        try
        {
            _trayIcon = new NotifyIcon { Visible = true, Text = E2x2SwitchConstants.Name };
            _trayIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };

            var menu = new ContextMenuStrip { ShowImageMargin = false, DropShadowEnabled = true };

            menu.Items.Add(new ToolStripMenuItem("Headphones Only", null, (s, e) => SetHeadphonesMode()));
            menu.Items.Add(new ToolStripMenuItem("Speakers Only", null, (s, e) => SetSpeakersMode()));
            menu.Items.Add(new ToolStripMenuItem("Both Outputs", null, (s, e) => SetBothMode()));
            menu.Items.Add(new ToolStripMenuItem("Gain Toggle", null, (s, e) => ToggleGainMode()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(
                new ToolStripMenuItem(
                    $"Open {E2x2SwitchConstants.Name}",
                    null,
                    (s, e) =>
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                    }
                )
            );
            menu.Items.Add(
                new ToolStripMenuItem(
                    "Exit",
                    null,
                    (s, e) =>
                    {
                        _trayIcon.Visible = false;
                        System.Windows.Application.Current.Shutdown();
                    }
                )
            );

            menu.Opened += (s, e) => UpdateContextMenuTheme();
            _trayIcon.ContextMenuStrip = menu;
            UpdateContextMenuTheme();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tray setup warning: {ex.Message}");
        }
    }

    private void UpdateContextMenuTheme()
    {
        if (_trayIcon?.ContextMenuStrip == null)
            return;

        bool isLight = TrayIconService.IsTaskbarLight();
        _trayIcon.ContextMenuStrip.Renderer = new ThemeAwareContextMenuRenderer(isLight);

        GdiColor textColor = isLight ? GdiColor.FromArgb(25, 25, 25) : GdiColor.White;
        foreach (ToolStripItem item in _trayIcon.ContextMenuStrip.Items)
        {
            item.ForeColor = textColor;
        }

        if (_trayIcon.ContextMenuStrip.IsHandleCreated)
        {
            int darkMode = isLight ? 0 : 1;
            DwmSetWindowAttribute(_trayIcon.ContextMenuStrip.Handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _heartbeatTimer.Stop();
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        _hotkeys.Dispose();
        _trayIcon?.Dispose();
        _currentTrayIcon?.Dispose();
        _topping.Dispose();
        base.OnClosed(e);
    }
}
