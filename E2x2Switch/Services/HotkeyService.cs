using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace E2x2Switch.Services;

/// <summary>Manages native Win32 global hotkey registrations.</summary>
public class HotkeyService : IDisposable
{
    #region Win32 Interop

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int hotkeyId, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int hotkeyId);

    #endregion

    #region Constants & Fields

    /// <summary>Alt modifier flag.</summary>
    public const uint ModAlt = 0x0001;

    /// <summary>Control modifier flag.</summary>
    public const uint ModControl = 0x0002;

    /// <summary>Shift modifier flag.</summary>
    public const uint ModShift = 0x0004;

    /// <summary>Windows key modifier flag.</summary>
    public const uint ModWin = 0x0008;

    /// <summary>Suppresses repeated WM_HOTKEY messages when holding down a shortcut.</summary>
    public const uint ModNoRepeat = 0x4000;

    private const int WmHotkey = 0x0312;

    private readonly List<int> _registeredIds = [];
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private int _nextHotkeyId = 1;

    #endregion

    #region Events

    /// <summary>Raised when a registered global hotkey is pressed.</summary>
    public event Action<int>? HotkeyPressed;

    #endregion

    #region Public Methods

    /// <summary>Initializes the Win32 message hook for the specified window handle.</summary>
    /// <param name="windowHandle">Native HWND to receive hotkey messages.</param>
    public void Initialize(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _hwndSource = HwndSource.FromHwnd(windowHandle);
        _hwndSource?.AddHook(HwndHook);
    }

    /// <summary>Registers a global hotkey with auto-repeat suppression.</summary>
    /// <param name="modifiers">Win32 modifier flags.</param>
    /// <param name="key">Primary key to register.</param>
    /// <returns>The assigned hotkey identifier, or <c>0</c> if registration failed.</returns>
    public int Register(uint modifiers, Key key)
    {
        if (key == Key.None || _windowHandle == IntPtr.Zero)
            return 0;

        int hotkeyId = _nextHotkeyId;
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!RegisterHotKey(_windowHandle, hotkeyId, modifiers | ModNoRepeat, virtualKey))
            return 0;

        _nextHotkeyId++;
        _registeredIds.Add(hotkeyId);
        return hotkeyId;
    }

    /// <summary>Unregisters all currently registered hotkeys.</summary>
    public void UnregisterAll()
    {
        foreach (int hotkeyId in _registeredIds)
            UnregisterHotKey(_windowHandle, hotkeyId);
        _registeredIds.Clear();
        _nextHotkeyId = 1;
    }

    /// <summary>Unregisters all hotkeys and detaches the Win32 window hook.</summary>
    public void Dispose()
    {
        UnregisterAll();
        _hwndSource?.RemoveHook(HwndHook);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Private Methods

    private IntPtr HwndHook(IntPtr windowHandle, int message, IntPtr wordParam, IntPtr longParam, ref bool handled)
    {
        if (message == WmHotkey)
        {
            HotkeyPressed?.Invoke(wordParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    #endregion
}
