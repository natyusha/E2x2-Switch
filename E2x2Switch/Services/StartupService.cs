using Microsoft.Win32;

namespace E2x2Switch.Services;

/// <summary>Manages Windows user startup registry configuration.</summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly string s_appRegistryName = E2x2SwitchConstants.Name.Replace(" ", "");

    /// <summary>Checks whether launch on Windows startup is enabled in the registry.</summary>
    public static bool IsStartWithWindowsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(s_appRegistryName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Enables or disables automatic launch on Windows startup.</summary>
    public static void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null)
                return;

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(s_appRegistryName, $"\"{exePath}\" --tray");
                }
            }
            else
            {
                key.DeleteValue(s_appRegistryName, false);
            }
        }
        catch { }
    }
}
