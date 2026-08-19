# E2x2 Switch

![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?&logo=windows)
![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)

A lightweight Windows utility for controlling the [TOPPING E2x2 Audio Interface](https://topping.pro/e2x2/) via global keyboard shortcuts.

E2x2 Switch communicates directly with the hardware microcontroller, bypassing the need to open or interact with the official TOPPING Professional Control Center (which does not have any hotkeys for switching the 1+2 output). The output's current state will also be reflected in the system tray icon.

![Preview](https://raw.githubusercontent.com/natyusha/E2x2-Switch/master/E2x2Switch/Assets/e2x2-switch-preview.png)

## Installation

- Extract the latest [release](https://github.com/natyusha/E2x2-Switch/releases) to a convenient location
- Run `E2x2-Switch.exe` and configure the hotkeys as desired
- Close the window to minimize it to the system tray

## Building from Source

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later

### Build Commands

```bash
# Clone the repository
git clone https://github.com/natyusha/E2x2-Switch.git
cd E2x2-Switch

# Run development build
dotnet run --project E2x2Switch

# Publish standalone single-file binary
dotnet publish E2x2Switch -c Release
```

The compiled binary will be located in `E2x2Switch/bin/Release/net10.0-windows/win-x64/publish/E2x2-Switch.exe`

## Protocol Documentation

Direct USB HID communication packets and capture logs are documented in [WiresharkDump.md](./Docs/WiresharkDump.md).
