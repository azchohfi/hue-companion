namespace HueCompanion.Core.Services.Interfaces;

/// <summary>
/// Service for handling page navigation within the application.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets whether navigation can go back.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Initializes the navigation service with the content frame.
    /// </summary>
    /// <param name="frame">The frame to use for navigation.</param>
    void Initialize(object frame);

    /// <summary>
    /// Navigates to a page.
    /// </summary>
    /// <typeparam name="TPage">The type of page to navigate to.</typeparam>
    void NavigateTo<TPage>() where TPage : class;

    /// <summary>
    /// Navigates to a page with a parameter.
    /// </summary>
    /// <typeparam name="TPage">The type of page to navigate to.</typeparam>
    /// <param name="parameter">The parameter to pass to the page.</param>
    void NavigateTo<TPage>(object parameter) where TPage : class;

    /// <summary>
    /// Navigates back to the previous page.
    /// </summary>
    void GoBack();

    /// <summary>
    /// Navigates to a page by type.
    /// </summary>
    /// <param name="pageType">The type of page to navigate to.</param>
    /// <param name="parameter">Optional parameter to pass to the page.</param>
    void NavigateTo(Type pageType, object? parameter = null);
}
