using HidSharp;

namespace E2x2Switch.Services;

/// <summary>Handles USB HID report communications and hardware event listening with the TOPPING E2x2 interface.</summary>
public class ToppingService : IDisposable
{
    #region Constants & Fields

    /// <summary>USB Vendor ID for TOPPING devices.</summary>
    public const int VendorId = 0x152A;

    /// <summary>USB Product ID for the TOPPING E2x2.</summary>
    public const int ProductId = 0x8752;

    private const string ControlInterfaceSubstring = "mi_04";

    private static readonly Lock s_hidLock = new();

    // Packets from Wireshark capture (prefixed with 0x00 Report ID for Windows HID)
    private static readonly byte[] s_commandHeadphonesOn = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x34, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_commandHeadphonesMute = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x34, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_commandSpeakersOn = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x34, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_commandSpeakersMute = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x34, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_commandGainHigh = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x11, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_commandGainLow = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x11, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_commandRouteOutput1 = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x31, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_commandRouteOutput2 = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x31, 0x02, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];

    private HidStream? _stream;
    private CancellationTokenSource? _cancellationTokenSource;
    private Thread? _readThread;
    private bool _disposed;

    private bool? _lastHeadphonesState;
    private bool? _lastSpeakersState;

    #endregion

    #region Properties & Events

    /// <summary>Gets a value indicating whether high headphone gain (+17dBu) is active.</summary>
    public bool GainIsHigh { get; private set; }

    /// <summary>Gets a value indicating whether the hardware control interface is currently connected.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Gets the active audio output routing mode.</summary>
    public AudioOutputMode CurrentMode { get; private set; } = AudioOutputMode.Headphones;

    /// <summary>Raised when the hardware gain state changes externally.</summary>
    public event Action<bool>? GainChanged;

    /// <summary>Raised when the hardware output routing mode changes externally.</summary>
    public event Action<AudioOutputMode>? OutputModeChanged;

    /// <summary>Raised when the USB device connection state changes.</summary>
    public event Action<bool>? ConnectionChanged;

    #endregion

    #region Monitoring & State

    /// <summary>Starts the background HID interrupt listener thread.</summary>
    public void StartMonitoring()
    {
        if (_readThread != null)
            return;
        _cancellationTokenSource = new CancellationTokenSource();
        _readThread = new Thread(ReadLoop) { IsBackground = true };
        _readThread.Start();
    }

    /// <summary>Initializes software state tracking without transmitting HID packets.</summary>
    /// <param name="mode">Initial output routing mode.</param>
    /// <param name="gainIsHigh">Initial headphone gain state.</param>
    public void InitializeState(AudioOutputMode mode, bool gainIsHigh)
    {
        lock (s_hidLock)
        {
            CurrentMode = mode;
            GainIsHigh = gainIsHigh;
            _lastHeadphonesState = mode is AudioOutputMode.Headphones or AudioOutputMode.Both;
            _lastSpeakersState = mode is AudioOutputMode.Speakers or AudioOutputMode.Both;
        }
    }

    /// <summary>Applies the specified output routing and gain state directly to the hardware.</summary>
    /// <param name="mode">Target output routing mode.</param>
    /// <param name="gainIsHigh">Target headphone gain state.</param>
    public void ApplyState(AudioOutputMode mode, bool gainIsHigh)
    {
        bool enableHeadphones = mode is AudioOutputMode.Headphones or AudioOutputMode.Both;
        bool enableSpeakers = mode is AudioOutputMode.Speakers or AudioOutputMode.Both;

        lock (s_hidLock)
        {
            CurrentMode = mode;
            GainIsHigh = gainIsHigh;
            _lastHeadphonesState = enableHeadphones;
            _lastSpeakersState = enableSpeakers;
        }

        byte[] headphonesPacket = enableHeadphones ? s_commandHeadphonesOn : s_commandHeadphonesMute;
        byte[] speakersPacket = enableSpeakers ? s_commandSpeakersOn : s_commandSpeakersMute;
        byte[] gainPacket = gainIsHigh ? s_commandGainHigh : s_commandGainLow;

        SendPackets(headphonesPacket, speakersPacket, gainPacket, s_commandRouteOutput1, s_commandRouteOutput2);
    }

    private void ReadLoop()
    {
        byte[] buffer = new byte[64];

        while (_cancellationTokenSource is { IsCancellationRequested: false })
        {
            try
            {
                HidStream? activeStream;
                lock (s_hidLock)
                {
                    if (_stream == null && !_disposed)
                        TryOpenStreamLocked();
                    activeStream = _stream;
                }

                if (activeStream == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                int bytesRead = activeStream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                    ProcessIncomingReport(buffer, bytesRead);
            }
            catch (TimeoutException) { }
            catch
            {
                CloseStream();
                Thread.Sleep(1000);
            }
        }
    }

    private void ProcessIncomingReport(byte[] buffer, int length)
    {
        for (int index = 0; index <= length - 10; index++)
        {
            // Verify TOPPING packet footer (66 77) and boolean state byte (00 or 01) to avoid false matches in unrelated report bytes
            if (buffer[index + 8] != 0x66 || buffer[index + 9] != 0x77 || buffer[index + 5] is not (0x00 or 0x01))
                continue;

            // 1. Gain Register (11 02)
            if (buffer[index] == 0x11 && buffer[index + 1] == 0x02)
            {
                bool newGainState = buffer[index + 5] == 0x01;
                bool raisedChange = false;
                lock (s_hidLock)
                {
                    if (GainIsHigh != newGainState)
                    {
                        GainIsHigh = newGainState;
                        raisedChange = true;
                    }
                }
                if (raisedChange)
                    GainChanged?.Invoke(newGainState);
            }
            // 2. Headphones Output Register (34 01)
            else if (buffer[index] == 0x34 && buffer[index + 1] == 0x01)
            {
                AudioOutputMode? changedMode;
                lock (s_hidLock)
                {
                    _lastHeadphonesState = buffer[index + 5] == 0x01;
                    changedMode = EvaluateOutputModeLocked();
                }
                if (changedMode.HasValue)
                    OutputModeChanged?.Invoke(changedMode.Value);
            }
            // 3. Speakers / Line Out Register (34 02)
            else if (buffer[index] == 0x34 && buffer[index + 1] == 0x02)
            {
                AudioOutputMode? changedMode;
                lock (s_hidLock)
                {
                    _lastSpeakersState = buffer[index + 5] == 0x01;
                    changedMode = EvaluateOutputModeLocked();
                }
                if (changedMode.HasValue)
                    OutputModeChanged?.Invoke(changedMode.Value);
            }
        }
    }

    private AudioOutputMode? EvaluateOutputModeLocked()
    {
        if (!_lastHeadphonesState.HasValue || !_lastSpeakersState.HasValue)
            return null;

        AudioOutputMode? evaluatedMode = (_lastHeadphonesState.Value, _lastSpeakersState.Value) switch
        {
            (true, false) => AudioOutputMode.Headphones,
            (false, true) => AudioOutputMode.Speakers,
            (true, true) => AudioOutputMode.Both,
            _ => null,
        };

        if (!evaluatedMode.HasValue || CurrentMode == evaluatedMode.Value)
            return null;

        CurrentMode = evaluatedMode.Value;
        return CurrentMode;
    }

    private void SetConnectionState(bool connected)
    {
        if (IsConnected == connected)
            return;
        IsConnected = connected;
        ConnectionChanged?.Invoke(IsConnected);
    }

    #endregion

    #region Output Cmds/HID I/O

    /// <summary>Routes audio exclusively to the headphone output.</summary>
    public void SetHeadphonesOnly()
    {
        lock (s_hidLock)
        {
            CurrentMode = AudioOutputMode.Headphones;
            _lastHeadphonesState = true;
            _lastSpeakersState = false;
        }
        SendPackets(s_commandHeadphonesOn, s_commandSpeakersMute, s_commandRouteOutput1, s_commandRouteOutput2);
    }

    /// <summary>Routes audio exclusively to the speaker line output.</summary>
    public void SetSpeakersOnly()
    {
        lock (s_hidLock)
        {
            CurrentMode = AudioOutputMode.Speakers;
            _lastHeadphonesState = false;
            _lastSpeakersState = true;
        }
        SendPackets(s_commandHeadphonesMute, s_commandSpeakersOn, s_commandRouteOutput1, s_commandRouteOutput2);
    }

    /// <summary>Enables both headphone and speaker outputs simultaneously.</summary>
    public void SetBoth()
    {
        lock (s_hidLock)
        {
            CurrentMode = AudioOutputMode.Both;
            _lastHeadphonesState = true;
            _lastSpeakersState = true;
        }
        SendPackets(s_commandHeadphonesOn, s_commandSpeakersOn, s_commandRouteOutput1, s_commandRouteOutput2);
    }

    /// <summary>Toggles headphone amplifier gain between low (0dBu) and high (+17dBu).</summary>
    public void ToggleGain()
    {
        bool targetGain;
        lock (s_hidLock)
        {
            GainIsHigh = !GainIsHigh;
            targetGain = GainIsHigh;
        }
        SendPackets(targetGain ? s_commandGainHigh : s_commandGainLow);
    }

    private bool TryOpenStreamLocked()
    {
        HidDevice? device = DeviceList.Local.GetHidDevices(VendorId, ProductId).FirstOrDefault(candidate => candidate.DevicePath.Contains(ControlInterfaceSubstring, StringComparison.OrdinalIgnoreCase));

        bool opened = device != null && device.TryOpen(out _stream);
        if (opened && _stream != null)
            _stream.ReadTimeout = Timeout.Infinite;

        SetConnectionState(opened);
        return opened;
    }

    private void CloseStream()
    {
        lock (s_hidLock)
        {
            _stream?.Dispose();
            _stream = null;
        }
        SetConnectionState(false);
    }

    private void SendPackets(params byte[][] packets)
    {
        lock (s_hidLock)
        {
            if (_disposed)
                return;

            try
            {
                if (_stream == null && !TryOpenStreamLocked())
                    return;

                foreach (byte[] packet in packets)
                {
                    _stream!.Write(packet);
                    Thread.Sleep(8);
                }
            }
            catch (Exception exception)
            {
                _stream?.Dispose();
                _stream = null;
                SetConnectionState(false);
                Console.WriteLine($"HID Error: {exception.Message}");
            }
        }
    }

    #endregion

    #region Lifecycle

    /// <summary>Stops hardware monitoring and releases the open HID stream.</summary>
    public void Dispose()
    {
        lock (s_hidLock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _cancellationTokenSource?.Cancel();
            _stream?.Dispose();
            _stream = null;
        }
        _cancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
