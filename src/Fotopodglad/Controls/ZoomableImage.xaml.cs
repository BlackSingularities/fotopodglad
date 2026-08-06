using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Fotopodglad.Controls;

/// <summary>
/// Obraz z możliwością przybliżania kółkiem myszy (zoom do punktu kursora) i przesuwania (pan) po
/// przybliżeniu. Cały czas pozostaje w obrębie tego samego, pełnoekranowego layoutu — zoom nie
/// otwiera żadnego dodatkowego okna ani chrome.
/// </summary>
public partial class ZoomableImage : UserControl
{
    private const double MinZoom = 1.0;
    private const double MaxZoom = 5.0;

    private bool _isDragging;
    private Point _dragStartMouse;
    private double _dragStartTranslateX;
    private double _dragStartTranslateY;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ImageSource), typeof(ZoomableImage),
        new PropertyMetadata(null, OnSourceChanged));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ZoomableImage()
    {
        InitializeComponent();
        PreviewMouseWheel += OnPreviewMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        LostMouseCapture += (_, _) => _isDragging = false;
    }

    public void ResetZoom()
    {
        ScaleTransform.ScaleX = MinZoom;
        ScaleTransform.ScaleY = MinZoom;
        TranslateTransform.X = 0;
        TranslateTransform.Y = 0;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ZoomableImage)d;
        control.PhotoImage.Source = e.NewValue as ImageSource;
        control.ResetZoom();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        var currentZoom = ScaleTransform.ScaleX;
        var zoomFactor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        var newZoom = Math.Clamp(currentZoom * zoomFactor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - currentZoom) < 0.0001)
        {
            return;
        }

        var cursor = e.GetPosition(RootGrid);
        var center = new Point(RootGrid.ActualWidth / 2, RootGrid.ActualHeight / 2);

        // Przelicz translację tak, by punkt pod kursorem pozostał w tym samym miejscu ekranu po zmianie skali.
        var offsetX = cursor.X - center.X - TranslateTransform.X;
        var offsetY = cursor.Y - center.Y - TranslateTransform.Y;
        var scaleRatio = newZoom / currentZoom;

        TranslateTransform.X -= offsetX * (scaleRatio - 1);
        TranslateTransform.Y -= offsetY * (scaleRatio - 1);

        ScaleTransform.ScaleX = newZoom;
        ScaleTransform.ScaleY = newZoom;

        ClampTranslation();

        if (newZoom <= MinZoom)
        {
            TranslateTransform.X = 0;
            TranslateTransform.Y = 0;
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ScaleTransform.ScaleX <= MinZoom)
        {
            return;
        }

        _isDragging = true;
        _dragStartMouse = e.GetPosition(this);
        _dragStartTranslateX = TranslateTransform.X;
        _dragStartTranslateY = TranslateTransform.Y;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var current = e.GetPosition(this);
        TranslateTransform.X = _dragStartTranslateX + (current.X - _dragStartMouse.X);
        TranslateTransform.Y = _dragStartTranslateY + (current.Y - _dragStartMouse.Y);
        ClampTranslation();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private void ClampTranslation()
    {
        var zoom = ScaleTransform.ScaleX;
        if (zoom <= MinZoom || RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
        {
            return;
        }

        var maxOffsetX = RootGrid.ActualWidth * (zoom - 1) / 2;
        var maxOffsetY = RootGrid.ActualHeight * (zoom - 1) / 2;

        TranslateTransform.X = Math.Clamp(TranslateTransform.X, -maxOffsetX, maxOffsetX);
        TranslateTransform.Y = Math.Clamp(TranslateTransform.Y, -maxOffsetY, maxOffsetY);
    }
}
