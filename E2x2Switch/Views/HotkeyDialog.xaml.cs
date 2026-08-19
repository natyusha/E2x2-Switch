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
    public HotkeyBinding ResultBinding { get; private set; }

    public HotkeyDialog(string actionTitle, HotkeyBinding currentBinding)
    {
        InitializeComponent();
        TitleText.Text = actionTitle;
        ResultBinding = new HotkeyBinding(currentBinding.Modifiers, currentBinding.Key);

        RenderBadges();
    }

    protected override void OnPreviewKeyDown(WpfKeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        uint mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            mods |= HotkeyService.ModControl;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            mods |= HotkeyService.ModAlt;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            mods |= HotkeyService.ModShift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            mods |= HotkeyService.ModWin;

        if (mods != 0)
        {
            ResultBinding = new HotkeyBinding(mods, key);
            RenderBadges();
            e.Handled = true;
        }
    }

    private void RenderBadges()
    {
        KeyBadgesPanel.Children.Clear();

        foreach (var label in ResultBinding.GetKeyLabels())
        {
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
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
