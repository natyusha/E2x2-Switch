using System.IO;
using System.Text.Json;
using System.Windows.Input;
using E2x2Switch.Services;

namespace E2x2Switch.Models;

/// <summary>Represents a single modifier + key shortcut binding.</summary>
public class HotkeyBinding
{
    public uint Modifiers { get; set; }
    public Key Key { get; set; }

    public HotkeyBinding() { }

    public HotkeyBinding(uint modifiers, Key key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    /// <summary>Enumerates key labels in display order.</summary>
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
            yield return Key.ToString();
    }
}

/// <summary>Persistent application configuration model.</summary>
public class AppConfig
{
    public HotkeyBinding HeadphonesOnly { get; set; } = new(HotkeyService.ModControl | HotkeyService.ModShift, Key.F1);
    public HotkeyBinding SpeakersOnly { get; set; } = new(HotkeyService.ModControl | HotkeyService.ModShift, Key.F2);
    public HotkeyBinding GainToggle { get; set; } = new(HotkeyService.ModControl | HotkeyService.ModShift, Key.F3);
    public HotkeyBinding BothEnabled { get; set; } = new(HotkeyService.ModControl | HotkeyService.ModShift, Key.F4);

    public AudioOutputMode LastOutputMode { get; set; } = AudioOutputMode.Headphones;
    public bool LastGainIsHigh { get; set; } = false;

    private static readonly string s_configPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    /// <summary>Loads configuration from disk or returns defaults.</summary>
    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(s_configPath))
            {
                string json = File.ReadAllText(s_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }

        var config = new AppConfig();
        config.Save();
        return config;
    }

    /// <summary>Saves current configuration to disk.</summary>
    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(s_configPath, json);
        }
        catch { }
    }
}
