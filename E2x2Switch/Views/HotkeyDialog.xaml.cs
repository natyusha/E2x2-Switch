using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using E2x2Switch.Models;
using E2x2Switch.Services;
using Wpf.Ui.Controls;
using WpfBrush = System.Windows.Media.Brush;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace E2x2Switch.Views;

/// <summary>Modal dialog for recording and changing shortcut key combinations.</summary>
public partial class HotkeyDialog : FluentWindow
{
    #region Props & Constructor

    /// <summary>Gets the resulting shortcut binding selected by the user.</summary>
    public HotkeyBinding ResultBinding { get; private set; }

    /// <summary>Initializes the shortcut editor dialog for the specified action.</summary>
    /// <param name="actionTitle">Display title describing the shortcut action.</param>
    /// <param name="currentBinding">Existing shortcut binding to populate initially.</param>
    public HotkeyDialog(string actionTitle, HotkeyBinding currentBinding)
    {
        InitializeComponent();
        TitleText.Text = actionTitle;
        ResultBinding = new HotkeyBinding(currentBinding.Modifiers, currentBinding.Key);

        RenderBadges();
    }

    #endregion

    #region Key Capture

    /// <summary>Captures modifier + key combinations or handles Esc/Enter dialog actions.</summary>
    /// <param name="eventArgs">Keyboard event arguments.</param>
    protected override void OnPreviewKeyDown(WpfKeyEventArgs eventArgs)
    {
        Key pressedKey = eventArgs.Key == Key.System ? eventArgs.SystemKey : eventArgs.Key;

        // Allow standalone Esc to cancel and Enter to save when no modifiers are held
        if (Keyboard.Modifiers == ModifierKeys.None && pressedKey is Key.Escape or Key.Enter)
        {
            CloseWithResult(pressedKey == Key.Enter);
            eventArgs.Handled = true;
            return;
        }

        // Ignore standalone modifier presses
        if (pressedKey is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        uint modifiers = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            modifiers |= HotkeyService.ModControl;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers |= HotkeyService.ModAlt;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            modifiers |= HotkeyService.ModShift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            modifiers |= HotkeyService.ModWin;

        if (modifiers != 0)
        {
            ResultBinding = new HotkeyBinding(modifiers, pressedKey);
            RenderBadges();
            eventArgs.Handled = true;
        }
    }

    private void RenderBadges()
    {
        KeyBadgesPanel.Children.Clear();

        foreach (string label in ResultBinding.GetKeyLabels())
            KeyBadgesPanel.Children.Add(
                new Border
                {
                    Background = (WpfBrush)FindResource("AccentFillColorDefaultBrush"),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 8, 14, 8),
                    Margin = new Thickness(4, 0, 4, 0),
                    Child = new WpfTextBlock
                    {
                        Text = label,
                        Foreground = (WpfBrush)FindResource("TextOnAccentFillColorPrimaryBrush"),
                        FontWeight = FontWeights.Bold,
                        FontSize = 14,
                    },
                }
            );
    }

    #endregion

    #region Event Handlers

    private void Save_Click(object sender, RoutedEventArgs eventArgs) => CloseWithResult(true);

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs) => CloseWithResult(false);

    private void CloseWithResult(bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }

    #endregion
}
