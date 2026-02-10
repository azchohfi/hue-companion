using Microsoft.UI.Dispatching;
using HueCompanion.Core.Services.Interfaces;

namespace HueCompanion.Helpers;

/// <summary>
/// Helper class for dispatching work to the UI thread.
/// Must be initialized from App.xaml.cs after the main window is created.
/// </summary>
public static class DispatcherHelper
{
    private static DispatcherQueue? _dispatcherQueue;
    private static readonly DispatcherAdapter _adapter = new();

    /// <summary>
    /// Initializes the dispatcher helper with the UI thread's DispatcherQueue.
    /// Call this from App.xaml.cs after creating the main window.
    /// Also registers with the Core UIDispatcher for ViewModel use.
    /// </summary>
    public static void Initialize(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        UIDispatcher.SetDispatcher(_adapter);
    }

    /// <summary>
    /// Adapter that implements IUIDispatcher using DispatcherHelper.
    /// </summary>
    private class DispatcherAdapter : IUIDispatcher
    {
        public void RunOnUIThread(Action action) => DispatcherHelper.RunOnUIThread(action);
    }

    /// <summary>
    /// Gets whether the helper has been initialized.
    /// </summary>
    public static bool IsInitialized => _dispatcherQueue != null;

    /// <summary>
    /// Executes the specified action on the UI thread.
    /// If already on the UI thread, executes immediately.
    /// </summary>
    public static void RunOnUIThread(Action action)
    {
        if (_dispatcherQueue == null)
        {
            // Not initialized yet, just run directly (may cause issues but won't crash)
            action();
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            // Already on UI thread
            action();
        }
        else
        {
            // Dispatch to UI thread
            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
        }
    }

    /// <summary>
    /// Executes the specified action on the UI thread asynchronously.
    /// Returns a task that completes when the action has finished executing.
    /// </summary>
    public static Task RunOnUIThreadAsync(Action action)
    {
        if (_dispatcherQueue == null)
        {
            action();
            return Task.CompletedTask;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
