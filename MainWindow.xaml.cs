using System.Windows;
using TaskSum.ViewModels;

namespace TaskSum;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
