using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using E2x2Switch.Services;

namespace E2x2Switch.Models;

#region Enums

/// <summary>Configurable startup state application behavior.</summary>
public enum StartupBehavior
{
    /// <summary>Restore the last active output routing and gain state.</summary>
    RestoreLastState,

    /// <summary>Switch to headphones only with low gain on launch.</summary>
    HeadphonesLowGain,

    /// <summary>Switch to headphones only with high gain on launch.</summary>
    HeadphonesHighGain,

    /// <summary>Switch to speakers only on launch.</summary>
    SpeakersOnly,

    /// <summary>Enable both headphones and speakers on launch.</summary>
    BothOutputs,

    /// <summary>Do not transmit state changes on launch.</summary>
    None,
}

#endregion

#region Models

/// <summary>Represents a single modifier and key shortcut binding.</summary>
public class HotkeyBinding
{
    /// <summary>Gets or sets the Win32 modifier key bitmask.</summary>
    public uint Modifiers { get; set; }

    /// <summary>Gets or sets the primary virtual key.</summary>
    public Key Key { get; set; }

    /// <summary>Initializes an empty shortcut binding for deserialization.</summary>
    public HotkeyBinding() { }

    /// <summary>Initializes a shortcut binding with the specified modifiers and key.</summary>
    /// <param name="modifiers">Win32 modifier key bitmask.</param>
    /// <param name="key">Primary key for the shortcut.</param>
    public HotkeyBinding(uint modifiers, Key key)
    {
        (Modifiers, Key) = (modifiers, key);
    }

    /// <summary>Enumerates key labels in display order.</summary>
    /// <returns>An ordered sequence of user-friendly key labels.</returns>
    public IEnumerable<string> GetKeyLabels()
    {
        if ((Modifiers & HotkeyService.ModWin) != 0)
            yield return "Win";
        if ((Modifiers & HotkeyService.ModControl) != 0)
            yield return "Ctrl";
        if ((Modifiers & HotkeyService.ModAlt) != 0)
            yield return "Alt";
        if ((Modifiers & HotkeyService.ModShift) != 0)
            yield return "Shift";

        if (Key != Key.None)
            yield return Key switch
            {
                >= Key.D0 and <= Key.D9 => ((char)('0' + (Key - Key.D0))).ToString(),
                >= Key.NumPad0 and <= Key.NumPad9 => $"Num {(char)('0' + (Key - Key.NumPad0))}",
                _ => Key.ToString(),
            };
    }
}

#endregion

#region Configuration

/// <summary>Persistent application configuration model.</summary>
public class AppConfig
{
    private static readonly string s_configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
    private static readonly Lock s_fileLock = new();
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Gets or sets the shortcut binding for headphones-only routing.</summary>
    public HotkeyBinding HeadphonesOnly { get; set; } = new(HotkeyService.ModControl | HotkeyService.ModShift, Key.F1);

    /// <summary>Gets or sets the shortcut binding for speakers-only routing.</summary>
    public HotkeyBinding SpeakersOnly { get; set; } = new(HotkeyService.ModControl | HotkeyService.ModShift, Key.F2);

    /// <summary>Gets or sets the shortcut binding for toggling headphone gain.</summary>
    public HotkeyBinding GainToggle { get; set; } = new(HotkeyService.ModControl | HotkeyService.ModShift, Key.F3);

    /// <summary>Gets or sets the shortcut binding for enabling both outputs simultaneously.</summary>
    public HotkeyBinding BothEnabled { get; set; } = new(HotkeyService.ModControl | HotkeyService.ModShift, Key.F4);

    /// <summary>Gets or sets the last active audio output mode.</summary>
    public AudioOutputMode LastOutputMode { get; set; } = AudioOutputMode.Headphones;

    /// <summary>Gets or sets a value indicating whether high gain was last active.</summary>
    public bool LastGainIsHigh { get; set; }

    /// <summary>Gets or sets the startup state enforcement behavior.</summary>
    public StartupBehavior StartupState { get; set; } = StartupBehavior.RestoreLastState;

    /// <summary>Loads configuration from disk or creates a default configuration file.</summary>
    /// <returns>A validated <see cref="AppConfig"/> instance.</returns>
    public static AppConfig Load()
    {
        lock (s_fileLock)
        {
            try
            {
                if (File.Exists(s_configPath))
                {
                    string jsonContent = File.ReadAllText(s_configPath);
                    AppConfig? loadedConfig = JsonSerializer.Deserialize<AppConfig>(jsonContent, s_jsonOptions);
                    if (loadedConfig != null)
                    {
                        loadedConfig.HeadphonesOnly ??= new HotkeyBinding(HotkeyService.ModControl | HotkeyService.ModShift, Key.F1);
                        loadedConfig.SpeakersOnly ??= new HotkeyBinding(HotkeyService.ModControl | HotkeyService.ModShift, Key.F2);
                        loadedConfig.GainToggle ??= new HotkeyBinding(HotkeyService.ModControl | HotkeyService.ModShift, Key.F3);
                        loadedConfig.BothEnabled ??= new HotkeyBinding(HotkeyService.ModControl | HotkeyService.ModShift, Key.F4);
                        return loadedConfig;
                    }
                }
            }
            catch { }
        }

        var defaultConfig = new AppConfig();
        defaultConfig.Save();
        return defaultConfig;
    }

    /// <summary>Atomically writes the current configuration to disk.</summary>
    public void Save()
    {
        lock (s_fileLock)
        {
            try
            {
                string jsonContent = JsonSerializer.Serialize(this, s_jsonOptions);
                string tempPath = $"{s_configPath}.tmp";
                File.WriteAllText(tempPath, jsonContent);
                File.Move(tempPath, s_configPath, true);
            }
            catch { }
        }
    }
}

#endregion
