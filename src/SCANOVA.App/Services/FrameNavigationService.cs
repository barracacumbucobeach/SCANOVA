using Microsoft.UI.Xaml.Controls;
using SCANOVA.App.Views;

namespace SCANOVA.App.Services;

public sealed class FrameNavigationService : INavigationService
{
    private Frame? _frame;

    public void SetFrame(Frame frame) => _frame = frame;

    public void NavigateTo(Type pageType, object? parameter = null)
    {
        if (_frame is null)
        {
            throw new InvalidOperationException($"{nameof(FrameNavigationService)}.{nameof(SetFrame)} precisa ser chamado antes de navegar.");
        }

        _frame.Navigate(pageType, parameter);
    }

    public void NavigateToPlaceholder(string title, string description) =>
        NavigateTo(typeof(PlaceholderPage), new PlaceholderPageParameter(title, description));
}
