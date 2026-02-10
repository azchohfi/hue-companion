using System.Diagnostics;

namespace HueCompanion.Services;

/// <summary>
/// Manages the MCP server process lifecycle.
/// Starts/stops the HTTP server as a background child process.
/// </summary>
public sealed class McpServerManager : IDisposable
{
    private Process? _process;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Whether the MCP server process is currently running.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _process is { HasExited: false };
            }
        }
    }

    /// <summary>
    /// Starts the MCP server if not already running.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_disposed) return;
            if (_process is { HasExited: false }) return;

            var exePath = FindMcpExe();
            if (exePath == null)
            {
                Debug.WriteLine("[McpServerManager] Could not find HueCompanion.Mcp.exe");
                return;
            }

            try
            {
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    },
                    EnableRaisingEvents = true,
                };

                _process.Exited += (_, _) =>
                    Debug.WriteLine("[McpServerManager] MCP server process exited");

                _process.Start();
                _process.BeginErrorReadLine();

                Debug.WriteLine($"[McpServerManager] Started MCP server (PID {_process.Id})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McpServerManager] Failed to start MCP server: {ex.Message}");
                _process?.Dispose();
                _process = null;
            }
        }
    }

    /// <summary>
    /// Stops the MCP server if running.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_process is not { HasExited: false }) return;

            try
            {
                Debug.WriteLine($"[McpServerManager] Stopping MCP server (PID {_process.Id})");
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[McpServerManager] Error stopping MCP server: {ex.Message}");
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }
    }

    /// <summary>
    /// Finds the MCP server executable.
    /// </summary>
    private static string? FindMcpExe()
    {
        // Published/stable: exe sits next to the main app
        var appDir = AppContext.BaseDirectory;
        var colocated = Path.Combine(appDir, "HueCompanion.Mcp.exe");
        if (File.Exists(colocated))
            return colocated;

        // Dev build: walk up from the WinUI output to the repo root,
        // then into the MCP project's build output
        var dir = new DirectoryInfo(appDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "HueCompanion.Mcp", "bin", "Debug", "net8.0", "HueCompanion.Mcp.exe");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }
        Stop();
    }
}
