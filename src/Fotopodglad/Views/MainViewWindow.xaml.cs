using System.Windows;
using System.Windows.Input;
using Fotopodglad.Helpers;
using Fotopodglad.ViewModels;

namespace Fotopodglad.Views;

public partial class MainViewWindow : Window
{
    public MainViewWindow(MainViewWindowViewModel viewModel)
    {
        InitializeComponent();
        Title = ApplicationVersion.CreateWindowTitle("podgląd");
        DataContext = viewModel;
    }

    // PreviewKeyDown (tunelowanie) zamiast KeyDown: strzałki i Home muszą trafić do skrótów aplikacji,
    // zanim przechwyci je kontrolka z fokusem — np. ScrollViewer galerii przewijający zawartość.
    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
        => await ((App)Application.Current).HandleShortcutAsync(this, e);
}
