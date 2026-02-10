namespace HueCompanion.Helpers;

/// <summary>
/// Parses command-line arguments for deep linking navigation.
/// </summary>
public static class CommandLineParser
{
    private static readonly HashSet<string> ValidPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "dashboard", "mydashboard", "rooms", "zones",
        "room", "zone", "light", "settings", "setup"
    };

    private static readonly HashSet<string> PagesRequiringId = new(StringComparer.OrdinalIgnoreCase)
    {
        "room", "zone", "light"
    };

    /// <summary>
    /// Parses command-line arguments into a CommandLineArgs record.
    /// </summary>
    /// <param name="args">The command-line arguments from Environment.GetCommandLineArgs().</param>
    /// <returns>Parsed command-line arguments.</returns>
    public static CommandLineArgs Parse(string[] args)
    {
        // Skip first arg (executable path)
        var argsToProcess = args.Length > 1 ? args.Skip(1).ToArray() : Array.Empty<string>();

        string? page = null;
        Guid? id = null;
        string? name = null;
        bool screenshotMode = false;
        int screenshotDelayMs = 5000;

        for (int i = 0; i < argsToProcess.Length; i++)
        {
            var arg = argsToProcess[i];

            if (arg.Equals("--page", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-p", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= argsToProcess.Length)
                {
                    return InvalidArgs("--page requires a page name");
                }
                page = argsToProcess[++i];
            }
            else if (arg.Equals("--id", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= argsToProcess.Length)
                {
                    return InvalidArgs("--id requires a GUID value");
                }
                var idStr = argsToProcess[++i];
                if (!Guid.TryParse(idStr, out var parsedId))
                {
                    return InvalidArgs($"Invalid GUID format: {idStr}");
                }
                id = parsedId;
            }
            else if (arg.Equals("--name", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-n", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= argsToProcess.Length)
                {
                    return InvalidArgs("--name requires a value");
                }
                name = argsToProcess[++i];
            }
            else if (arg.Equals("--screenshot", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
            {
                screenshotMode = true;
            }
            else if (arg.Equals("--delay", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= argsToProcess.Length)
                {
                    return InvalidArgs("--delay requires a value in milliseconds");
                }
                if (!int.TryParse(argsToProcess[++i], out screenshotDelayMs) ||
                    screenshotDelayMs < 0)
                {
                    return InvalidArgs("--delay must be a non-negative integer");
                }
            }
        }

        // Validate page name
        if (page != null && !ValidPages.Contains(page))
        {
            return InvalidArgs($"Unknown page: {page}. Valid pages: {string.Join(", ", ValidPages)}");
        }

        // Validate ID or name requirement
        if (page != null && PagesRequiringId.Contains(page) && !id.HasValue && string.IsNullOrEmpty(name))
        {
            return InvalidArgs($"Page '{page}' requires --id or --name parameter");
        }

        return new CommandLineArgs
        {
            Page = page,
            Id = id,
            Name = name,
            ScreenshotMode = screenshotMode,
            ScreenshotDelayMs = screenshotDelayMs,
            IsValid = true
        };
    }

    private static CommandLineArgs InvalidArgs(string message)
    {
        return new CommandLineArgs
        {
            IsValid = false,
            ErrorMessage = message
        };
    }
}
