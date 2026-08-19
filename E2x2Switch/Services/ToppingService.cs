using HidSharp;

namespace E2x2Switch.Services;

/// <summary>Handles USB HID report communications with the TOPPING E2x2 interface.</summary>
public class ToppingService
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

    public bool GainIsHigh { get; private set; }

    /// <summary>Checks whether the TOPPING E2x2 HID control interface is actively connected.</summary>
    public static bool IsConnected()
    {
        try
        {
            return DeviceList.Local.GetHidDevices(Vid, Pid).Any(d => d.DevicePath.Contains(ControlInterfaceSubstring, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static void SendPackets(params byte[][] packets)
    {
        lock (s_hidLock)
        {
            try
            {
                var device = DeviceList.Local.GetHidDevices(Vid, Pid).FirstOrDefault(d => d.DevicePath.Contains(ControlInterfaceSubstring, StringComparison.OrdinalIgnoreCase));

                if (device != null && device.TryOpen(out var stream))
                {
                    using (stream)
                    {
                        foreach (var pkt in packets)
                        {
                            stream.Write(pkt);
                            Thread.Sleep(8);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HID Error: {ex.Message}");
            }
        }
    }

    public void SetHeadphonesOnly() => SendPackets(s_cmdHpOn, s_cmdSpkMute, s_cmdRoute1, s_cmdRoute2);

    public void SetSpeakersOnly() => SendPackets(s_cmdHpMute, s_cmdSpkOn, s_cmdRoute1, s_cmdRoute2);

    public void SetBoth() => SendPackets(s_cmdHpOn, s_cmdSpkOn, s_cmdRoute1, s_cmdRoute2);

    public void ToggleGain()
    {
        GainIsHigh = !GainIsHigh;
        SendPackets(GainIsHigh ? s_cmdGainHigh : s_cmdGainLow);
    }
}
