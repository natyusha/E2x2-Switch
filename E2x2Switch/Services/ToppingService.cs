using HidSharp;

namespace E2x2Switch.Services;

/// <summary>Handles USB HID report communications and hardware event listening with the TOPPING E2x2 interface.</summary>
public class ToppingService : IDisposable
{
    public const int Vid = 0x152A;
    public const int Pid = 0x8752;
    private const string ControlInterfaceSubstring = "mi_04";

    private static readonly Lock s_hidLock = new();

    // Packets from Wireshark capture (prefixed with 0x00 Report ID for Windows HID)
    private static readonly byte[] s_cmdHpOn = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x34, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_cmdHpMute = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x34, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_cmdSpkOn = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x34, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_cmdSpkMute = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x34, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_cmdGainHigh = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x11, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_cmdGainLow = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x11, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_cmdRoute1 = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x31, 0x01, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];
    private static readonly byte[] s_cmdRoute2 = [0x00, 0x22, 0x33, 0x20, 0x01, 0x01, 0x31, 0x02, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x66, 0x77, 0x00];

    private HidStream? _stream;
    private CancellationTokenSource? _cts;
    private Thread? _readThread;

    private bool? _lastHpState;
    private bool? _lastSpkState;

    public bool GainIsHigh { get; private set; }
    public bool IsConnected { get; private set; }
    public AudioOutputMode CurrentMode { get; private set; } = AudioOutputMode.Headphones;

    public event Action<bool>? GainChanged;
    public event Action<AudioOutputMode>? OutputModeChanged;
    public event Action<bool>? ConnectionChanged;

    /// <summary>Starts the background HID report listener thread.</summary>
    public void StartMonitoring()
    {
        _cts = new CancellationTokenSource();
        _readThread = new Thread(ReadLoop) { IsBackground = true };
        _readThread.Start();
    }

    /// <summary>Initializes software tracking state without transmitting packets.</summary>
    public void InitializeState(AudioOutputMode mode, bool gainIsHigh)
    {
        CurrentMode = mode;
        GainIsHigh = gainIsHigh;

        _lastHpState = mode is AudioOutputMode.Headphones or AudioOutputMode.Both;
        _lastSpkState = mode is AudioOutputMode.Speakers or AudioOutputMode.Both;
    }

    /// <summary>Applies the specified output routing and gain state directly to the hardware.</summary>
    public void ApplyState(AudioOutputMode mode, bool gainIsHigh)
    {
        CurrentMode = mode;
        GainIsHigh = gainIsHigh;
        _lastHpState = mode is AudioOutputMode.Headphones or AudioOutputMode.Both;
        _lastSpkState = mode is AudioOutputMode.Speakers or AudioOutputMode.Both;

        byte[] hpPacket = _lastHpState.Value ? s_cmdHpOn : s_cmdHpMute;
        byte[] spkPacket = _lastSpkState.Value ? s_cmdSpkOn : s_cmdSpkMute;
        byte[] gainPacket = gainIsHigh ? s_cmdGainHigh : s_cmdGainLow;

        SendPackets(hpPacket, spkPacket, gainPacket, s_cmdRoute1, s_cmdRoute2);
    }

    private void ReadLoop()
    {
        byte[] buffer = new byte[64];

        while (_cts is { IsCancellationRequested: false })
        {
            try
            {
                lock (s_hidLock)
                {
                    if (_stream == null)
                    {
                        var device = DeviceList.Local.GetHidDevices(Vid, Pid).FirstOrDefault(d => d.DevicePath.Contains(ControlInterfaceSubstring, StringComparison.OrdinalIgnoreCase));

                        if (device != null && device.TryOpen(out _stream))
                        {
                            SetConnectionState(true);
                        }
                        else
                        {
                            SetConnectionState(false);
                        }
                    }
                }

                if (_stream == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    ProcessIncomingReport(buffer, bytesRead);
                }
            }
            catch
            {
                lock (s_hidLock)
                {
                    _stream?.Dispose();
                    _stream = null;
                }
                SetConnectionState(false);
                Thread.Sleep(1000);
            }
        }
    }

    private void ProcessIncomingReport(byte[] buffer, int length)
    {
        for (int i = 0; i <= length - 8; i++)
        {
            // 1. Gain Register (11 02)
            if (buffer[i] == 0x11 && buffer[i + 1] == 0x02)
            {
                if (i + 5 < length)
                {
                    bool newGainState = buffer[i + 5] == 0x01;
                    if (GainIsHigh != newGainState)
                    {
                        GainIsHigh = newGainState;
                        GainChanged?.Invoke(GainIsHigh);
                    }
                }
            }
            // 2. Headphones Output Register (34 01)
            else if (buffer[i] == 0x34 && buffer[i + 1] == 0x01)
            {
                if (i + 5 < length)
                {
                    _lastHpState = buffer[i + 5] == 0x01;
                    EvaluateOutputMode();
                }
            }
            // 3. Speakers / Line Out Register (34 02)
            else if (buffer[i] == 0x34 && buffer[i + 1] == 0x02)
            {
                if (i + 5 < length)
                {
                    _lastSpkState = buffer[i + 5] == 0x01;
                    EvaluateOutputMode();
                }
            }
        }
    }

    private void EvaluateOutputMode()
    {
        if (_lastHpState.HasValue && _lastSpkState.HasValue)
        {
            AudioOutputMode? evaluatedMode = (_lastHpState.Value, _lastSpkState.Value) switch
            {
                (true, false) => AudioOutputMode.Headphones,
                (false, true) => AudioOutputMode.Speakers,
                (true, true) => AudioOutputMode.Both,
                _ => null,
            };

            if (evaluatedMode.HasValue && CurrentMode != evaluatedMode.Value)
            {
                CurrentMode = evaluatedMode.Value;
                OutputModeChanged?.Invoke(CurrentMode);
            }
        }
    }

    private void SetConnectionState(bool connected)
    {
        if (IsConnected != connected)
        {
            IsConnected = connected;
            ConnectionChanged?.Invoke(IsConnected);
        }
    }

    private void SendPackets(params byte[][] packets)
    {
        lock (s_hidLock)
        {
            try
            {
                if (_stream == null)
                {
                    var device = DeviceList.Local.GetHidDevices(Vid, Pid).FirstOrDefault(d => d.DevicePath.Contains(ControlInterfaceSubstring, StringComparison.OrdinalIgnoreCase));

                    if (device == null || !device.TryOpen(out _stream))
                    {
                        return;
                    }
                    SetConnectionState(true);
                }

                foreach (var pkt in packets)
                {
                    _stream.Write(pkt);
                    Thread.Sleep(8);
                }
            }
            catch (Exception ex)
            {
                _stream?.Dispose();
                _stream = null;
                SetConnectionState(false);
                Console.WriteLine($"HID Error: {ex.Message}");
            }
        }
    }

    public void SetHeadphonesOnly()
    {
        CurrentMode = AudioOutputMode.Headphones;
        _lastHpState = true;
        _lastSpkState = false;
        SendPackets(s_cmdHpOn, s_cmdSpkMute, s_cmdRoute1, s_cmdRoute2);
    }

    public void SetSpeakersOnly()
    {
        CurrentMode = AudioOutputMode.Speakers;
        _lastHpState = false;
        _lastSpkState = true;
        SendPackets(s_cmdHpMute, s_cmdSpkOn, s_cmdRoute1, s_cmdRoute2);
    }

    public void SetBoth()
    {
        CurrentMode = AudioOutputMode.Both;
        _lastHpState = true;
        _lastSpkState = true;
        SendPackets(s_cmdHpOn, s_cmdSpkOn, s_cmdRoute1, s_cmdRoute2);
    }

    public void ToggleGain()
    {
        GainIsHigh = !GainIsHigh;
        SendPackets(GainIsHigh ? s_cmdGainHigh : s_cmdGainLow);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        lock (s_hidLock)
        {
            _stream?.Dispose();
            _stream = null;
        }
        _cts?.Dispose();
    }
}
