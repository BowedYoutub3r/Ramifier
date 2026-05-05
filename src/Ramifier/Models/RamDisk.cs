using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ramifier.Models;

public class RamDisk : INotifyPropertyChanged
{
    private string _driveLetter = "";
    private long _sizeBytes;
    private string _fileSystem = "NTFS";
    private string _label = "";
    private bool _isActive;
    private long _usedBytes;
    private int _unitNumber = -1;

    public string DriveLetter
    {
        get => _driveLetter;
        set => SetField(ref _driveLetter, value);
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (SetField(ref _sizeBytes, value))
                OnPropertyChanged(nameof(SizeDisplay));
        }
    }

    public string FileSystem
    {
        get => _fileSystem;
        set => SetField(ref _fileSystem, value);
    }

    public string Label
    {
        get => _label;
        set => SetField(ref _label, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public long UsedBytes
    {
        get => _usedBytes;
        set
        {
            if (SetField(ref _usedBytes, value))
            {
                OnPropertyChanged(nameof(FreeBytes));
                OnPropertyChanged(nameof(UsagePercent));
                OnPropertyChanged(nameof(UsedDisplay));
                OnPropertyChanged(nameof(FreeDisplay));
            }
        }
    }

    public int UnitNumber
    {
        get => _unitNumber;
        set => SetField(ref _unitNumber, value);
    }

    public long FreeBytes => SizeBytes - UsedBytes;
    public double UsagePercent => SizeBytes > 0 ? (double)UsedBytes / SizeBytes * 100 : 0;
    public string SizeDisplay => FormatBytes(SizeBytes);
    public string UsedDisplay => FormatBytes(UsedBytes);
    public string FreeDisplay => FormatBytes(FreeBytes);

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }
        return $"{value:F1} {units[unitIndex]}";
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
