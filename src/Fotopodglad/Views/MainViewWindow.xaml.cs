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

    private async void OnKeyDown(object sender, KeyEventArgs e)
        => await ((App)Application.Current).HandleShortcutAsync(this, e);
}
