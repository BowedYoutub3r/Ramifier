using System.Windows;
using Ramifier.Models;
using Ramifier.Services;

namespace Ramifier;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public bool SettingsSaved { get; private set; }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        PersistentCheck.IsChecked = settings.PersistentDisks;
        StartupCheck.IsChecked = settings.StartOnBoot;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.PersistentDisks = PersistentCheck.IsChecked == true;
        _settings.StartOnBoot = StartupCheck.IsChecked == true;

        SettingsService.SetStartOnBoot(_settings.StartOnBoot);

        SettingsSaved = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
