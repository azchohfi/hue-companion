using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Views;

namespace HueWindows.Helpers;

/// <summary>
/// Service for handling page navigation within the WinUI application.
/// </summary>
public class NavigationService : INavigationService
{
    private Frame? _frame;

    /// <inheritdoc/>
    public bool CanGoBack => _frame?.CanGoBack ?? false;

    /// <inheritdoc/>
    public void Initialize(object frame)
    {
        if (frame is Frame f)
        {
            _frame = f;
        }
    }

    /// <inheritdoc/>
    public void NavigateTo<TPage>() where TPage : class
    {
        var transitionInfo = GetTransitionInfo(typeof(TPage));
        _frame?.Navigate(typeof(TPage), null, transitionInfo);
    }

    /// <inheritdoc/>
    public void NavigateTo<TPage>(object parameter) where TPage : class
    {
        var transitionInfo = GetTransitionInfo(typeof(TPage));
        _frame?.Navigate(typeof(TPage), parameter, transitionInfo);
    }

    /// <inheritdoc/>
    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
        {
            _frame.GoBack();
        }
    }

    /// <inheritdoc/>
    public void NavigateTo(Type pageType, object? parameter = null)
    {
        var transitionInfo = GetTransitionInfo(pageType);
        _frame?.Navigate(pageType, parameter, transitionInfo);
    }

    /// <summary>
    /// Gets the appropriate navigation transition for the target page type.
    /// Detail pages use DrillIn, list pages use default entrance.
    /// </summary>
    private static NavigationTransitionInfo GetTransitionInfo(Type pageType)
    {
        // Detail pages drill in from source
        if (pageType == typeof(RoomDetailPage) || pageType == typeof(LightDetailPage))
        {
            return new DrillInNavigationTransitionInfo();
        }

        // Default entrance animation for other pages
        return new EntranceNavigationTransitionInfo();
    }
}
