using Microsoft.Win32;

namespace E2x2Switch.Services;

/// <summary>Supported hardware audio output routing states.</summary>
public enum AudioOutputMode
{
    Headphones,
    Speakers,
    Both,
}

/// <summary>Provides taskbar-contrast aware tray icon resolution.</summary>
public static class TrayIconService
{
    /// <summary>Checks whether Windows is currently using a light taskbar theme.</summary>
    public static bool IsTaskbarLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("SystemUsesLightTheme") is int val)
            {
                return val == 1;
            }
        }
        catch { }

        return false;
    }

    /// <summary>Retrieves the matching icon resource based on active mode, gain state, and taskbar contrast.</summary>
    public static Icon GetTrayIcon(AudioOutputMode mode, bool gainIsHigh)
    {
        string themeSuffix = IsTaskbarLight() ? "dark" : "light";

        string iconName = (mode, gainIsHigh) switch
        {
            (AudioOutputMode.Headphones, true) => $"hp_gain_{themeSuffix}.ico",
            (AudioOutputMode.Headphones, false) => $"hp_{themeSuffix}.ico",
            (AudioOutputMode.Speakers, _) => $"spk_{themeSuffix}.ico",
            (AudioOutputMode.Both, true) => $"both_gain_{themeSuffix}.ico",
            (AudioOutputMode.Both, false) => $"both_{themeSuffix}.ico",
            _ => $"app_{themeSuffix}.ico",
        };

        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/{iconName}");
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                return new Icon(streamInfo.Stream);
            }
        }
        catch { }

        return SystemIcons.Application;
    }
}
