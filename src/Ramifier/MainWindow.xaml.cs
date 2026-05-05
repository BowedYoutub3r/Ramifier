using System.Windows;
using System.Windows.Media.Imaging;

namespace Ramifier;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Icon = new BitmapImage(new System.Uri("pack://application:,,,/logo/ramifier_256.ico"));
    }
}
