using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HueCompanion.Core.Services;
using HueCompanion.Core.Services.Interfaces;
using HueCompanion.Mcp.Services;
using ModelContextProtocol.Server;

const int DefaultPort = 5680;

if (args.Contains("--stdio"))
{
    await RunStdio(args);
}
else
{
    var useHttps = !args.Contains("--http");
    var port = DefaultPort;
    var portIndex = Array.IndexOf(args, "--port");
    if (portIndex >= 0 && portIndex + 1 < args.Length)
        int.TryParse(args[portIndex + 1], out port);

    await RunHttp(args, port, useHttps);
}

async Task RunStdio(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    RegisterCoreServices(builder.Services);

    builder.Services
        .AddMcpServer(ConfigureMcp)
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    var host = builder.Build();

    if (!await LoadAndCheck(host.Services))
        return;

    await host.RunAsync();
}

async Task RunHttp(string[] args, int port, bool useHttps)
{
    var builder = WebApplication.CreateBuilder(args);

    var scheme = useHttps ? "https" : "http";

    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.Listen(IPAddress.Loopback, port, listenOptions =>
        {
            if (useHttps)
                listenOptions.UseHttps();
        });
    });

    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    RegisterCoreServices(builder.Services);

    builder.Services
        .AddMcpServer(ConfigureMcp)
        .WithHttpTransport()
        .WithToolsFromAssembly();

    var app = builder.Build();

    if (!await LoadAndCheck(app.Services))
        return;

    app.MapMcp();

    var url = $"{scheme}://localhost:{port}";
    Console.Error.WriteLine($"Hue Companion MCP server listening on {url}");
    Console.Error.WriteLine($"Use this URL in Claude Desktop: {url}/mcp");

    app.Run();
}

void RegisterCoreServices(IServiceCollection services)
{
    services.AddSingleton<ISettingsService, FileSettingsService>();
    services.AddSingleton<IMultiBridgeService, MultiBridgeService>();
    services.AddSingleton<ISceneStorageService, SceneStorageService>();
    services.AddSingleton<IAnimationService, AnimationService>();
}

void ConfigureMcp(McpServerOptions options)
{
    options.ServerInfo = new()
    {
        Name = "Hue Companion",
        Version = "1.0.0"
    };
}

async Task<bool> LoadAndCheck(IServiceProvider services)
{
    var settings = services.GetRequiredService<ISettingsService>();
    await settings.LoadAsync();

    if (!settings.Settings.McpEnabled)
    {
        Console.Error.WriteLine("Hue Companion MCP server is disabled. Enable it in Settings > MCP Server.");
        return false;
    }

    var multiBridge = services.GetRequiredService<IMultiBridgeService>();
    await multiBridge.ConnectAllAsync();
    return true;
}
