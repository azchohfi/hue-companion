namespace HueWindows.Core.Services.Interfaces;

/// <summary>
/// Interface for dispatching work to the UI thread.
/// Implemented by the UI layer and used by ViewModels to marshal property changes.
/// </summary>
public interface IUIDispatcher
{
    /// <summary>
    /// Executes the specified action on the UI thread.
    /// </summary>
    void RunOnUIThread(Action action);
}

/// <summary>
/// Static accessor for the UI dispatcher.
/// Set by the UI layer during app initialization.
/// </summary>
public static class UIDispatcher
{
    private static IUIDispatcher? _instance;

    /// <summary>
    /// Sets the UI dispatcher instance.
    /// </summary>
    public static void SetDispatcher(IUIDispatcher dispatcher)
    {
        _instance = dispatcher;
    }

    /// <summary>
    /// Executes the specified action on the UI thread.
    /// If no dispatcher is set, executes the action directly.
    /// </summary>
    public static void RunOnUIThread(Action action)
    {
        if (_instance != null)
        {
            _instance.RunOnUIThread(action);
        }
        else
        {
            action();
        }
    }
}
