namespace HueWindows.Core.Utilities;

/// <summary>
/// Extension methods for Task to handle fire-and-forget patterns safely.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Safely executes a fire-and-forget task, logging any exceptions to Debug output.
    /// </summary>
    public static async void FireAndForget(this Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fire-and-forget error: {ex.Message}");
        }
    }
}
