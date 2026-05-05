using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Ramifier.Models;
using Ramifier.Services;

namespace Ramifier.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ImDiskService _imDisk = new();
    private readonly SettingsService _settingsService = new();
    private readonly DispatcherTimer _refreshTimer;
    private AppSettings _settings;

    private string _selectedDriveLetter = "";
    private int _selectedSizeValue = 1;
    private string _selectedSizeUnit = "GB";
    private string _selectedFileSystem = "NTFS";
    private string _volumeLabel = "RAMDisk";
    private string _statusMessage = "Ready";
    private bool _isBusy;
    private bool _isImDiskInstalled;
    private long _totalRam;
    private long _availableRam;

    public MainViewModel()
    {
        _settings = _settingsService.Load();

        DriveLetters = new ObservableCollection<string>(ImDiskService.GetAvailableDriveLetters());
        ActiveDisks = new ObservableCollection<RamDisk>();
        SizeUnits = ["MB", "GB"];
        FileSystems = ["NTFS", "FAT32", "exFAT"];

        SelectedDriveLetter = DriveLetters.FirstOrDefault() ?? "R";
        SelectedSizeUnit = "GB";
        SelectedSizeValue = 1;
        SelectedFileSystem = "NTFS";

        CreateCommand = new RelayCommand(_ => CreateDisk(), _ => CanCreate);
        RemoveCommand = new RelayCommand(disk => RemoveDisk(disk as RamDisk), disk => disk is RamDisk);
        RemoveAllCommand = new RelayCommand(_ => RemoveAll(), _ => ActiveDisks.Count > 0);
        RefreshCommand = new RelayCommand(_ => Refresh());
        InstallImDiskCommand = new RelayCommand(_ => InstallImDisk(), _ => !IsBusy && !IsImDiskInstalled);
        SettingsCommand = new RelayCommand(_ => OpenSettings());

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += (_, _) => RefreshDiskUsage();
        _refreshTimer.Start();

        CheckImDisk();
        Refresh();
        UpdateRamInfo();
        RestorePersistentDisks();
    }

    public ObservableCollection<string> DriveLetters { get; }
    public ObservableCollection<RamDisk> ActiveDisks { get; }
    public List<string> SizeUnits { get; }
    public List<string> FileSystems { get; }

    public ICommand CreateCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RemoveAllCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand InstallImDiskCommand { get; }
    public ICommand SettingsCommand { get; }

    public string SelectedDriveLetter
    {
        get => _selectedDriveLetter;
        set => SetField(ref _selectedDriveLetter, value);
    }

    public int SelectedSizeValue
    {
        get => _selectedSizeValue;
        set
        {
            if (SetField(ref _selectedSizeValue, value))
                OnPropertyChanged(nameof(SizeBytesDisplay));
        }
    }

    public string SelectedSizeUnit
    {
        get => _selectedSizeUnit;
        set
        {
            if (SetField(ref _selectedSizeUnit, value))
                OnPropertyChanged(nameof(SizeBytesDisplay));
        }
    }

    public string SelectedFileSystem
    {
        get => _selectedFileSystem;
        set => SetField(ref _selectedFileSystem, value);
    }

    public string VolumeLabel
    {
        get => _volumeLabel;
        set => SetField(ref _volumeLabel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
                OnPropertyChanged(nameof(CanCreate));
        }
    }

    public bool IsImDiskInstalled
    {
        get => _isImDiskInstalled;
        set => SetField(ref _isImDiskInstalled, value);
    }

    public long TotalRam
    {
        get => _totalRam;
        set
        {
            if (SetField(ref _totalRam, value))
                OnPropertyChanged(nameof(TotalRamDisplay));
        }
    }

    public long AvailableRam
    {
        get => _availableRam;
        set
        {
            if (SetField(ref _availableRam, value))
            {
                OnPropertyChanged(nameof(AvailableRamDisplay));
                OnPropertyChanged(nameof(RamUsagePercent));
            }
        }
    }

    public string TotalRamDisplay => RamDisk.FormatBytes(TotalRam);
    public string AvailableRamDisplay => RamDisk.FormatBytes(AvailableRam);
    public double RamUsagePercent => TotalRam > 0 ? (double)(TotalRam - AvailableRam) / TotalRam * 100 : 0;
    public bool CanCreate => !IsBusy && IsImDiskInstalled && !string.IsNullOrEmpty(SelectedDriveLetter);

    public string SizeBytesDisplay
    {
        get
        {
            long bytes = CalculateSizeBytes();
            return RamDisk.FormatBytes(bytes);
        }
    }

    private long CalculateSizeBytes()
    {
        long multiplier = SelectedSizeUnit switch
        {
            "GB" => 1024L * 1024 * 1024,
            "MB" => 1024L * 1024,
            _ => 1024L * 1024 * 1024,
        };
        return SelectedSizeValue * multiplier;
    }

    private void CheckImDisk()
    {
        IsImDiskInstalled = _imDisk.IsInstalled();
        if (!IsImDiskInstalled)
            StatusMessage = "ImDisk not found — click Install ImDisk to set it up automatically";
    }

    private async void InstallImDisk()
    {
        IsBusy = true;
        try
        {
            var result = await _imDisk.DownloadAndInstallAsync(msg =>
                Application.Current.Dispatcher.Invoke(() => StatusMessage = msg));

            StatusMessage = result.Message;
            CheckImDisk();
            OnPropertyChanged(nameof(CanCreate));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void CreateDisk()
    {
        long sizeBytes = CalculateSizeBytes();
        long available = ImDiskService.GetAvailableRam();

        if (sizeBytes > available)
        {
            var sizeFmt = RamDisk.FormatBytes(sizeBytes);
            var availFmt = RamDisk.FormatBytes(available);
            var result = MessageBox.Show(
                $"The requested disk size ({sizeFmt}) exceeds available RAM ({availFmt}).\n\n" +
                "This may cause system instability or force Windows to use the page file heavily.\n\n" +
                "Continue anyway?",
                "Insufficient RAM",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                StatusMessage = "Disk creation cancelled";
                return;
            }
        }

        IsBusy = true;
        StatusMessage = $"Creating {SelectedSizeValue} {SelectedSizeUnit} RAM disk on {SelectedDriveLetter}:...";

        try
        {
            var result = await Task.Run(() =>
                _imDisk.CreateRamDisk(SelectedDriveLetter, sizeBytes, SelectedFileSystem, VolumeLabel));

            StatusMessage = result.Message;

            if (result.Success)
            {
                SaveDiskToSettings(SelectedDriveLetter, sizeBytes, SelectedFileSystem, VolumeLabel);
                Refresh();
                UpdateRamInfo();
                RefreshDriveLetters();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void RemoveDisk(RamDisk? disk)
    {
        if (disk == null) return;

        IsBusy = true;
        StatusMessage = $"Removing RAM disk {disk.DriveLetter}:...";

        try
        {
            var result = await Task.Run(() => _imDisk.RemoveRamDisk(disk.DriveLetter, disk.UnitNumber));
            StatusMessage = result.Message;
            RemoveDiskFromSettings(disk.DriveLetter);
            Refresh();
            UpdateRamInfo();
            RefreshDriveLetters();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void RemoveAll()
    {
        IsBusy = true;
        StatusMessage = "Removing all RAM disks...";

        try
        {
            var disks = ActiveDisks.ToList();
            foreach (var disk in disks)
            {
                await Task.Run(() => _imDisk.RemoveRamDisk(disk.DriveLetter, disk.UnitNumber));
                RemoveDiskFromSettings(disk.DriveLetter);
            }

            StatusMessage = $"Removed {disks.Count} RAM disk(s)";
            Refresh();
            UpdateRamInfo();
            RefreshDriveLetters();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Refresh()
    {
        try
        {
            var disks = _imDisk.ListRamDisks();
            ActiveDisks.Clear();
            foreach (var disk in disks)
                ActiveDisks.Add(disk);
        }
        catch { }
    }

    private void RefreshDiskUsage()
    {
        foreach (var disk in ActiveDisks)
        {
            try
            {
                var driveInfo = new System.IO.DriveInfo(disk.DriveLetter);
                if (driveInfo.IsReady)
                {
                    disk.UsedBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
                    disk.SizeBytes = driveInfo.TotalSize;
                }
            }
            catch { }
        }
        UpdateRamInfo();
    }

    private void UpdateRamInfo()
    {
        TotalRam = ImDiskService.GetTotalRam();
        AvailableRam = ImDiskService.GetAvailableRam();
    }

    private void RefreshDriveLetters()
    {
        var available = ImDiskService.GetAvailableDriveLetters();
        DriveLetters.Clear();
        foreach (var letter in available)
            DriveLetters.Add(letter);
        if (DriveLetters.Count > 0 && !DriveLetters.Contains(SelectedDriveLetter))
            SelectedDriveLetter = DriveLetters[0];
    }

    private void SaveDiskToSettings(string driveLetter, long sizeBytes, string fileSystem, string label)
    {
        if (!_settings.PersistentDisks) return;

        _settings.SavedDisks.RemoveAll(d => d.DriveLetter.Equals(driveLetter, StringComparison.OrdinalIgnoreCase));
        _settings.SavedDisks.Add(new SavedDisk
        {
            DriveLetter = driveLetter,
            SizeBytes = sizeBytes,
            FileSystem = fileSystem,
            Label = label,
        });
        _settingsService.Save(_settings);
    }

    private void RemoveDiskFromSettings(string driveLetter)
    {
        _settings.SavedDisks.RemoveAll(d => d.DriveLetter.Equals(driveLetter, StringComparison.OrdinalIgnoreCase));
        _settingsService.Save(_settings);
    }

    private async void RestorePersistentDisks()
    {
        if (!_settings.PersistentDisks || _settings.SavedDisks.Count == 0) return;
        if (!_imDisk.IsInstalled()) return;

        var existing = _imDisk.ListRamDisks();
        var toRestore = _settings.SavedDisks
            .Where(s => !existing.Any(e => e.DriveLetter.Equals(s.DriveLetter, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (toRestore.Count == 0) return;

        StatusMessage = $"Restoring {toRestore.Count} persistent disk(s)...";
        foreach (var saved in toRestore)
        {
            await Task.Run(() => _imDisk.CreateRamDisk(saved.DriveLetter, saved.SizeBytes, saved.FileSystem, saved.Label));
        }

        Refresh();
        UpdateRamInfo();
        RefreshDriveLetters();
        StatusMessage = $"Restored {toRestore.Count} persistent disk(s)";
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settings)
        {
            Owner = Application.Current.MainWindow,
        };

        if (window.ShowDialog() == true)
        {
            _settingsService.Save(_settings);

            if (!_settings.PersistentDisks)
                _settings.SavedDisks.Clear();
            else
                SyncSavedDisksFromActive();

            _settingsService.Save(_settings);
            StatusMessage = "Settings saved";
        }
    }

    private void SyncSavedDisksFromActive()
    {
        _settings.SavedDisks.Clear();
        foreach (var disk in ActiveDisks)
        {
            _settings.SavedDisks.Add(new SavedDisk
            {
                DriveLetter = disk.DriveLetter,
                SizeBytes = disk.SizeBytes,
                FileSystem = disk.FileSystem,
                Label = disk.Label,
            });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
