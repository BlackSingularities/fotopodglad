using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Fotopodglad.Controls;

public partial class ExifBadge : UserControl
{
    public static readonly DependencyProperty IconResourceKeyProperty = DependencyProperty.Register(
        nameof(IconResourceKey), typeof(string), typeof(ExifBadge),
        new PropertyMetadata(null, OnIconResourceKeyChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(ExifBadge),
        new PropertyMetadata(null, OnTextChanged));

    public string? IconResourceKey
    {
        get => (string?)GetValue(IconResourceKeyProperty);
        set => SetValue(IconResourceKeyProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ExifBadge()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyIcon();
    }

    private static void OnIconResourceKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ExifBadge)d).ApplyIcon();
    }

    private void ApplyIcon()
    {
        if (IconResourceKey is string key && TryFindResource(key) is Geometry geometry)
        {
            IconPath.Data = geometry;
        }
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var badge = (ExifBadge)d;
        badge.TextBlockValue.Text = e.NewValue as string;
    }
}
