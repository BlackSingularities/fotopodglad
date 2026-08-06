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
    {
        if (e.Key == Key.P && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await ((App)Application.Current).OpenSettingsAsync(this);
        }
    }
}
