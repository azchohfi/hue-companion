using Microsoft.UI.Xaml.Controls;
using HueWindows.Core.Services.Interfaces;

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
        _frame?.Navigate(typeof(TPage));
    }

    /// <inheritdoc/>
    public void NavigateTo<TPage>(object parameter) where TPage : class
    {
        _frame?.Navigate(typeof(TPage), parameter);
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
        if (parameter != null)
        {
            _frame?.Navigate(pageType, parameter);
        }
        else
        {
            _frame?.Navigate(pageType);
        }
    }
}
