using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Fotopodglad.ViewModels;

namespace Fotopodglad.Controls;

/// <summary>
/// Widok zdjęcia z danymi EXIF i zoomem używany w Oknie A. Działa automatycznie dla najnowszego
/// zdjęcia albo tymczasowo pokazuje fotografię klikniętą w siatce Okna B.
/// Crossfade między kolejnymi zdjęciami realizowany przez naprzemienne użycie dwóch warstw ZoomableImage,
/// żeby uniknąć migotania przy podmianie źródła.
/// </summary>
public partial class FullscreenPhotoView : UserControl
{
    private bool _showingLayerA = true;

    public FullscreenPhotoView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is FullscreenPhotoViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is FullscreenPhotoViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            ApplyImage(newVm.CurrentImageSource, animate: false);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FullscreenPhotoViewModel.CurrentImageSource) &&
            DataContext is FullscreenPhotoViewModel vm)
        {
            ApplyImage(vm.CurrentImageSource, animate: true);
        }
    }

    private void ApplyImage(System.Windows.Media.Imaging.BitmapImage? source, bool animate)
    {
        var incoming = _showingLayerA ? LayerB : LayerA;
        var outgoing = _showingLayerA ? LayerA : LayerB;

        incoming.Source = source;
        incoming.ResetZoom();
        outgoing.ResetZoom();

        if (animate)
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
            incoming.BeginAnimation(OpacityProperty, fadeIn);
            outgoing.BeginAnimation(OpacityProperty, fadeOut);
        }
        else
        {
            incoming.Opacity = 1;
            outgoing.Opacity = 0;
        }

        _showingLayerA = !_showingLayerA;
    }
}
