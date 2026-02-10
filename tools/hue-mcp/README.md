# Hue MCP Server

An MCP (Model Context Protocol) server that exposes Philips Hue light control via stdio transport. Works with Claude Desktop, VS Code Copilot, and any MCP-compatible client.

## Features

- **Light Control**: Set individual lights or entire rooms — brightness, color (hex/RGB/named), color temperature
- **Scene Management**: Create, activate, edit, and delete scenes (compatible with HueWindows app scenes)
- **Animated Scenes**: Create keyframe-based animations with per-light tracks
- **Event Effects**: Lightning flash, sparkle, candle flicker effects
- **Multi-Bridge**: Supports multiple Hue bridges simultaneously
- **Fuzzy Matching**: Find lights and rooms by partial name match

## Tools

| Tool | Description |
|------|-------------|
| `hue_list_rooms` | List all rooms/zones with light states |
| `hue_get_light` | Get a specific light's state |
| `hue_set_light` | Control a light (on/off, brightness, color, temperature) |
| `hue_set_room` | Control all lights in a room |
| `hue_turn_off_all` | Turn off everything |
| `hue_list_scenes` | List saved scenes |
| `hue_activate_scene` | Activate a scene in a room |
| `hue_create_scene` | Create a static scene |
| `hue_delete_scene` | Delete a scene |
| `hue_create_animated_scene` | Create an animated scene with keyframes |
| `hue_play_animation` | Start an animated scene |
| `hue_stop_animation` | Stop animations |
| `hue_edit_scene` | Edit scene keyframes/effects |
| `hue_list_event_presets` | List available effect presets |
| `hue_list_bridges` | List configured bridges |

## Setup

### Prerequisites

- .NET 8 Runtime (or use self-contained build)
- At least one Philips Hue bridge configured

### Bridge Configuration

The MCP server reads bridge credentials from `%LOCALAPPDATA%/HueWindows/bridges.json`:

```json
[
  {
    "BridgeId": "001788FFFE123456",
    "IpAddress": "192.168.1.50",
    "AppKey": "your-app-key-here",
    "FriendlyName": "Living Room Bridge"
  }
]
```

If you've already configured bridges in the HueWindows app, the server will also read from `%LOCALAPPDATA%/HueWindows/settings.json` as a fallback.

### Building

```bash
# Framework-dependent (requires .NET 8 runtime)
dotnet build src/HueCompanion.Mcp -c Release

# Self-contained single file (no runtime needed)
dotnet publish src/HueCompanion.Mcp -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o dist/mcp
```

### Claude Desktop Configuration

Add to `%APPDATA%/Claude/claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "hue": {
      "command": "C:/path/to/HueCompanion.Mcp.exe",
      "args": []
    }
  }
}
```

Or if using `dotnet run`:

```json
{
  "mcpServers": {
    "hue": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/src/HueCompanion.Mcp"]
    }
  }
}
```

### VS Code / GitHub Copilot Configuration

Add to `.vscode/mcp.json` in your workspace:

```json
{
  "servers": {
    "hue": {
      "type": "stdio",
      "command": "C:/path/to/HueCompanion.Mcp.exe"
    }
  }
}
```

## Example Usage

Once configured, you can ask your AI assistant things like:

- "Turn the bedroom lights to a warm orange at 50% brightness"
- "What lights are on right now?"
- "Create a sunset animation that goes from warm white to deep red over 30 seconds"
- "Activate the Movie Night scene in the living room"
- "Turn off all the lights"

## Color Formats

The server accepts colors in several formats:

- **Hex**: `#FF0000`, `FF0000`
- **RGB**: `rgb(255, 0, 0)`
- **Named**: `red`, `blue`, `warm white`, `coral`, `lavender`, etc.

## Scene Compatibility

Scenes are stored as JSON in `%LOCALAPPDATA%/HueWindows/Scenes/` and are fully compatible with the HueWindows desktop app's Scene Builder.
