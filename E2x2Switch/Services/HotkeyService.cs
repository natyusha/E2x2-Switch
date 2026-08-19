using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace E2x2Switch.Services;

/// <summary>Manages native Win32 global hotkey registrations.</summary>
public class HotkeyService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;

    private const int WmHotkey = 0x0312;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private int _currentId = 1;

    public event Action<int>? HotkeyPressed;

    /// <summary>Initializes message hook for the specified window handle.</summary>
    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(HwndHook);
    }

    /// <summary>Registers a global hotkey and returns its unique ID.</summary>
    public int Register(uint modifiers, Key key)
    {
        int id = _currentId++;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        RegisterHotKey(_hwnd, id, modifiers, vk);
        return id;
    }

    /// <summary>Unregisters all currently assigned hotkeys.</summary>
    public void UnregisterAll()
    {
        for (int i = 1; i < _currentId; i++)
            UnregisterHotKey(_hwnd, i);
        _currentId = 1;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            HotkeyPressed?.Invoke(wParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(HwndHook);
    }
}
