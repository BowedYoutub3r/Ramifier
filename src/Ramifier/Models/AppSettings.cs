namespace Ramifier.Models;

public class AppSettings
{
    public bool PersistentDisks { get; set; }
    public bool StartOnBoot { get; set; }
    public List<SavedDisk> SavedDisks { get; set; } = [];
}

public class SavedDisk
{
    public string DriveLetter { get; set; } = "";
    public long SizeBytes { get; set; }
    public string FileSystem { get; set; } = "NTFS";
    public string Label { get; set; } = "";
}
