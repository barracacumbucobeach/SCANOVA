using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SCANOVA.App.ViewModels;
using SCANOVA.Core.Models;

namespace SCANOVA.App.Views;

/// <summary>Parâmetro de navegação para <see cref="DocumentViewerPage"/>.</summary>
public sealed record DocumentViewerParameter(string? SourcePath, RasterImage Image);

/// <summary>
/// Visualizador/editor básico de imagem (Fase 2): abrir, visualizar, zoom, girar, cortar
/// (seleção manual por arrastar) e exportar (seção 12/44 da especificação).
/// </summary>
public sealed partial class DocumentViewerPage : Page
{
    private Windows.Foundation.Point? _cropDragStart;

    public DocumentViewerViewModel ViewModel { get; }

    public DocumentViewerPage()
    {
        ViewModel = App.Services.GetRequiredService<DocumentViewerViewModel>();
        InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is DocumentViewerParameter parameter)
        {
            await ViewModel.LoadAsync(parameter.SourcePath, parameter.Image);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewerViewModel.HasPendingCrop) && !ViewModel.HasPendingCrop)
        {
            CropRectangle.Visibility = Visibility.Collapsed;
        }
    }

    private void ImageHost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.IsCropMode)
        {
            return;
        }

        var point = e.GetCurrentPoint(ImageHost).Position;
        _cropDragStart = point;

        Canvas.SetLeft(CropRectangle, point.X);
        Canvas.SetTop(CropRectangle, point.Y);
        CropRectangle.Width = 0;
        CropRectangle.Height = 0;
        CropRectangle.Visibility = Visibility.Visible;

        ImageHost.CapturePointer(e.Pointer);
    }

    private void ImageHost_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_cropDragStart is not { } start)
        {
            return;
        }

        var current = e.GetCurrentPoint(ImageHost).Position;

        var x = Math.Min(start.X, current.X);
        var y = Math.Min(start.Y, current.Y);
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(CropRectangle, x);
        Canvas.SetTop(CropRectangle, y);
        CropRectangle.Width = width;
        CropRectangle.Height = height;
    }

    private void ImageHost_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_cropDragStart is null)
        {
            return;
        }

        ImageHost.ReleasePointerCapture(e.Pointer);
        _cropDragStart = null;

        var left = Canvas.GetLeft(CropRectangle);
        var top = Canvas.GetTop(CropRectangle);
        ViewModel.SetPendingCropSelection(left, top, CropRectangle.Width, CropRectangle.Height);

        if (!ViewModel.HasPendingCrop)
        {
            CropRectangle.Visibility = Visibility.Collapsed;
        }
    }

    private void CropToggle_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsCropMode = sender is AppBarToggleButton { IsChecked: true };
    }

    private void FitToScreen_Click(object sender, RoutedEventArgs e)
    {
        if (MainImage.ActualWidth <= 0 || MainImage.ActualHeight <= 0)
        {
            return;
        }

        var zoomX = Viewer.ViewportWidth / MainImage.ActualWidth;
        var zoomY = Viewer.ViewportHeight / MainImage.ActualHeight;
        var zoom = (float)Math.Max(0.05, Math.Min(zoomX, zoomY));

        Viewer.ChangeView(0, 0, zoom);
    }

    private void ActualSize_Click(object sender, RoutedEventArgs e)
    {
        Viewer.ChangeView(null, null, 1f);
    }
}
