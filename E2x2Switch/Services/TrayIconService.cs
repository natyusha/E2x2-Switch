using System.IO;
using System.Windows.Resources;
using Microsoft.Win32;
using WpfApplication = System.Windows.Application;

namespace E2x2Switch.Services;

#region Enums

/// <summary>Supported hardware audio output routing states.</summary>
public enum AudioOutputMode
{
    /// <summary>Audio routed exclusively to headphones.</summary>
    Headphones,

    /// <summary>Audio routed exclusively to speaker line out.</summary>
    Speakers,

    /// <summary>Audio routed to both headphones and speakers simultaneously.</summary>
    Both,
}

#endregion

#region Service

/// <summary>Provides taskbar-contrast aware tray icon resolution.</summary>
public static class TrayIconService
{
    /// <summary>Checks whether Windows is currently using a light taskbar theme.</summary>
    /// <returns><c>true</c> if the taskbar uses light mode; otherwise, <c>false</c>.</returns>
    public static bool IsTaskbarLight()
    {
        try
        {
            using RegistryKey? personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (personalizeKey?.GetValue("SystemUsesLightTheme") is int themeValue)
                return themeValue == 1;
        }
        catch { }

        return false;
    }

    /// <summary>Retrieves the matching icon resource based on active mode, gain state, and taskbar contrast.</summary>
    /// <param name="mode">Current hardware audio output routing mode.</param>
    /// <param name="gainIsHigh">Whether high headphone gain is currently active.</param>
    /// <returns>An <see cref="Icon"/> matching the current device state and taskbar theme.</returns>
    public static Icon GetTrayIcon(AudioOutputMode mode, bool gainIsHigh)
    {
        string themeSuffix = IsTaskbarLight() ? "dark" : "light";

        string iconName = (mode, gainIsHigh) switch
        {
            (AudioOutputMode.Headphones, true) => $"hp_gain_{themeSuffix}.ico",
            (AudioOutputMode.Headphones, false) => $"hp_{themeSuffix}.ico",
            (AudioOutputMode.Speakers, true) => $"spk_gain_{themeSuffix}.ico",
            (AudioOutputMode.Speakers, false) => $"spk_{themeSuffix}.ico",
            (AudioOutputMode.Both, true) => $"both_gain_{themeSuffix}.ico",
            (AudioOutputMode.Both, false) => $"both_{themeSuffix}.ico",
            _ => $"app_{themeSuffix}.ico",
        };

        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/{iconName}");
            StreamResourceInfo? streamInfo = WpfApplication.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using Stream resourceStream = streamInfo.Stream;
                return new Icon(resourceStream);
            }
        }
        catch { }

        return SystemIcons.Application;
    }
}

#endregion
