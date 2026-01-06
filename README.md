# Hue Windows

A modern Windows 11 app for controlling Philips Hue smart lights, built with WinUI 3 and Fluent Design.

## Features

- **Dashboard with Room Cards** - Widget-like tiles for quick room control
- **Drag to Dim** - Press and drag on room cards to adjust brightness
- **Scene Selection** - Quickly activate saved Hue scenes
- **Color Control** - Full color picker with color wheel and temperature slider
- **Individual Light Control** - Fine-grained control of each light
- **Dark/Light Theme** - Follows system theme or manual selection
- **Mica Backdrop** - Modern Windows 11 visual style

## Requirements

- Windows 10 version 1809 (build 17763) or later
- Windows 11 recommended for full visual experience
- Philips Hue Bridge on the same local network
- .NET 8.0 Runtime

## Building

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- Visual Studio 2022 (optional, for IDE development)

### Build from Command Line

```bash
# Restore and build
dotnet build src/HueWindows/HueWindows.csproj

# Run the app
dotnet run --project src/HueWindows/HueWindows.csproj
```

### Build for Specific Architecture

```bash
# x64
dotnet build src/HueWindows/HueWindows.csproj -p:Platform=x64

# ARM64
dotnet build src/HueWindows/HueWindows.csproj -p:Platform=ARM64
```

## Project Structure

```
hue-windows/
├── src/
│   ├── HueWindows/                 # WinUI 3 App (MSIX packaged)
│   │   ├── Views/                  # XAML pages
│   │   ├── Controls/               # Custom controls (RoomCard)
│   │   ├── Converters/             # Value converters
│   │   ├── Helpers/                # Navigation and utilities
│   │   └── Assets/                 # App icons and images
│   │
│   ├── HueWindows.Core/            # Business logic library
│   │   ├── Models/                 # Data models
│   │   ├── ViewModels/             # MVVM ViewModels
│   │   └── Services/               # Hue API and settings services
│   │
│   └── HueWindows.Tests/           # Unit tests
│
└── Directory.Build.props           # Shared build settings
```

## Technology Stack

- **Framework**: WinUI 3 / Windows App SDK 1.6
- **Language**: C# / .NET 8
- **Architecture**: MVVM with CommunityToolkit.Mvvm
- **Hue Integration**: HueApi NuGet package (CLIP v2 API)
- **Packaging**: MSIX for Windows Store

## First Run Setup

1. Launch the app
2. The app will automatically discover Hue bridges on your network
3. Select your bridge from the list
4. Press the link button on your Hue bridge when prompted
5. The app will connect and display your rooms

## License

MIT License - See LICENSE file for details.
