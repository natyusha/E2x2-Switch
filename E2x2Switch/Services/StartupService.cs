using Microsoft.Win32;

namespace E2x2Switch.Services;

/// <summary>Manages Windows user startup registry configuration.</summary>
public static class StartupService
{
    #region Constants & Fields

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly string s_appRegistryName = E2x2SwitchConstants.Name.Replace(" ", "");

    #endregion

    #region Public Methods

    /// <summary>Checks whether launch on Windows startup is enabled in the registry.</summary>
    /// <returns><c>true</c> if the startup entry exists; otherwise, <c>false</c>.</returns>
    public static bool IsStartWithWindowsEnabled()
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return runKey?.GetValue(s_appRegistryName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Enables or disables automatic launch on Windows startup.</summary>
    /// <param name="enable"><c>true</c> to register startup launch; <c>false</c> to remove it.</param>
    public static void SetStartWithWindows(bool enable)
    {
        try
        {
            using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (runKey == null)
                return;

            if (enable && Environment.ProcessPath is { Length: > 0 } executablePath)
                runKey.SetValue(s_appRegistryName, $"\"{executablePath}\" --tray");
            else if (!enable)
                runKey.DeleteValue(s_appRegistryName, false);
        }
        catch { }
    }

    #endregion
}
