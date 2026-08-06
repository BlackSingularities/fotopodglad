using System.Windows;
using Fotopodglad.ViewModels;

namespace Fotopodglad.Views;

public partial class MainViewWindow : Window
{
    public MainViewWindow(MainViewWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
