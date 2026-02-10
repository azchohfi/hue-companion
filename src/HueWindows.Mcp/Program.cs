using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HueWindows.Core.Services;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Mcp.Services;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// All logging to stderr so stdout stays clean for MCP JSON-RPC
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Core services
builder.Services.AddSingleton<ISettingsService, FileSettingsService>();
builder.Services.AddSingleton<IMultiBridgeService, MultiBridgeService>();
builder.Services.AddSingleton<ISceneStorageService, SceneStorageService>();
builder.Services.AddSingleton<IAnimationService, AnimationService>();

// MCP server with stdio transport
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "HueWindows",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

// Load settings and connect to bridges before serving
var settings = host.Services.GetRequiredService<ISettingsService>();
await settings.LoadAsync();

var multiBridge = host.Services.GetRequiredService<IMultiBridgeService>();
await multiBridge.ConnectAllAsync();

await host.RunAsync();
