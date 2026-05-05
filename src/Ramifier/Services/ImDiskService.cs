using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Ramifier.Models;

namespace Ramifier.Services;

public class ImDiskService
{
    private static string _imDiskPath = FindImDisk();

    private static string FindImDisk()
    {
        string[] candidates =
        [
            @"C:\Windows\System32\imdisk.exe",
            @"C:\Program Files\ImDisk\imdisk.exe",
            @"C:\Program Files (x86)\ImDisk\imdisk.exe",
        ];

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        try
        {
            var result = RunProcess("where", "imdisk");
            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
                return result.Output.Trim().Split('\n')[0].Trim();
        }
        catch { }

        return "imdisk";
    }

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public bool IsInstalled()
    {
        _imDiskPath = FindImDisk();
        return File.Exists(_imDiskPath);
    }

    public (bool Success, string Message) CreateRamDisk(string driveLetter, long sizeBytes, string fileSystem, string label)
    {
        if (!IsElevated())
            return RunElevated($"-a -s {sizeBytes / (1024 * 1024)}M -m {driveLetter.TrimEnd(':')}: -p \"/fs:{fileSystem} /q /y /v:{label}\"");

        string sizeMb = (sizeBytes / (1024 * 1024)).ToString();
        string letter = driveLetter.TrimEnd(':');

        var createResult = RunProcess(_imDiskPath, $"-a -s {sizeMb}M -m {letter}: -p \"/fs:{fileSystem} /q /y /v:{label}\"");

        if (createResult.ExitCode != 0)
            createResult = RunProcess(_imDiskPath, $"-a -s {sizeMb}M -m {letter}: -p \"/fs:{fileSystem} /q /y\"");

        if (createResult.ExitCode != 0)
            return (false, $"Failed to create RAM disk: {createResult.Output} {createResult.Error}");

        return (true, $"RAM disk {letter}: created ({sizeMb} MB, {fileSystem})");
    }

    public (bool Success, string Message) RemoveRamDisk(string driveLetter, int unitNumber = -1)
    {
        // Always force-dismount — gentle -d fails when the volume is in use
        string target = unitNumber >= 0
            ? $"-u {unitNumber}"
            : $"-m {driveLetter.TrimEnd(':')}:";
        string args = $"-D {target}";

        if (!IsElevated())
            return RunElevated(args);

        var result = RunProcess(_imDiskPath, args);

        if (result.ExitCode != 0)
            return (false, $"Failed to remove RAM disk: {result.Output} {result.Error}");

        return (true, $"RAM disk {driveLetter}: removed");
    }

    public List<RamDisk> ListRamDisks()
    {
        var disks = new List<RamDisk>();

        // imdisk -l outputs lines like: \Device\ImDisk0
        // It exits with code 1 even on success, so only check for output
        var result = RunProcess(_imDiskPath, "-l");
        if (string.IsNullOrWhiteSpace(result.Output))
            return disks;

        var unitMatches = Regex.Matches(result.Output, @"\\Device\\ImDisk(\d+)", RegexOptions.IgnoreCase);

        foreach (Match match in unitMatches)
        {
            int unit = int.Parse(match.Groups[1].Value);
            var detail = RunProcess(_imDiskPath, $"-l -u {unit}");
            if (detail.ExitCode != 0) continue;

            var disk = ParseDiskDetail(detail.Output, unit);
            if (disk != null)
                disks.Add(disk);
        }

        return disks;
    }

    private static RamDisk? ParseDiskDetail(string output, int unit)
    {
        var disk = new RamDisk { UnitNumber = unit, IsActive = true };

        // Size line: "Size: 2147483648 bytes (2 GB), Virtual Memory, HDD, Modified."
        var sizeMatch = Regex.Match(output, @"Size:\s+(\d+)\s+bytes", RegexOptions.IgnoreCase);
        if (sizeMatch.Success)
            disk.SizeBytes = long.Parse(sizeMatch.Groups[1].Value);

        // Drive letter line: "Drive letter: Y"
        var driveMatch = Regex.Match(output, @"Drive letter:\s+([A-Za-z])", RegexOptions.IgnoreCase);
        if (driveMatch.Success)
            disk.DriveLetter = driveMatch.Groups[1].Value.ToUpper();

        // Fallback: also check "Mount point:" for non-letter mounts
        if (string.IsNullOrEmpty(disk.DriveLetter))
        {
            var mountMatch = Regex.Match(output, @"Mount point:\s+(.+)", RegexOptions.IgnoreCase);
            if (mountMatch.Success)
            {
                var mountPoint = mountMatch.Groups[1].Value.Trim().TrimEnd('\\', ':');
                if (mountPoint.Length >= 1)
                    disk.DriveLetter = mountPoint[..1].ToUpper();
            }
        }

        if (!string.IsNullOrEmpty(disk.DriveLetter))
        {
            try
            {
                var driveInfo = new DriveInfo(disk.DriveLetter);
                if (driveInfo.IsReady)
                {
                    disk.SizeBytes = driveInfo.TotalSize;
                    disk.UsedBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;
                    disk.FileSystem = driveInfo.DriveFormat;
                    disk.Label = driveInfo.VolumeLabel;
                }
            }
            catch { }
        }

        return disk;
    }

    private static (bool Success, string Message) RunElevated(string arguments)
    {
        try
        {
            // Use cmd.exe as the elevated host so "runas" verb works reliably
            // and we get a real exit code back
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{_imDiskPath}\" {arguments}\"",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            process.Start();
            process.WaitForExit(30000);
            return process.ExitCode == 0
                ? (true, "Operation completed successfully")
                : (false, $"Operation failed (exit code {process.ExitCode})");
        }
        catch (Exception ex)
        {
            return (false, $"Elevation cancelled or failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DownloadAndInstallAsync(Action<string>? onProgress = null)
    {
        try
        {
            // Look for installer bundled next to Ramifier.exe first
            string? appDir = Path.GetDirectoryName(Environment.ProcessPath);
            string? localInstaller = appDir != null
                ? Path.Combine(appDir, "imdiskinst.exe")
                : null;

            string installerPath;

            if (localInstaller != null && File.Exists(localInstaller))
            {
                installerPath = localInstaller;
                onProgress?.Invoke("Found bundled ImDisk installer...");
            }
            else
            {
                // Fall back to downloading from GitHub releases
                const string installerUrl = "https://github.com/BowedYoutub3r/Ramifier/releases/latest/download/imdiskinst.exe";
                installerPath = Path.Combine(Path.GetTempPath(), "imdiskinst.exe");

                onProgress?.Invoke("Downloading ImDisk Toolkit...");
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                using var response = await http.GetAsync(installerUrl);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(installerPath, bytes);
                onProgress?.Invoke("Download complete...");
            }

            onProgress?.Invoke("Installing ImDisk — follow the installer prompts...");
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Verb = "runas",
                UseShellExecute = true,
            };
            process.Start();
            await process.WaitForExitAsync();

            if (installerPath.StartsWith(Path.GetTempPath()))
                try { File.Delete(installerPath); } catch { }

            if (IsInstalled())
                return (true, "ImDisk Toolkit installed successfully");

            return (false, "Installation may have been cancelled — restart Ramifier after installing manually");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to install ImDisk: {ex.Message}");
        }
    }

    public static List<string> GetAvailableDriveLetters()
    {
        var used = DriveInfo.GetDrives().Select(d => d.Name[0]).ToHashSet();
        return Enumerable.Range('A', 26)
            .Select(c => ((char)c).ToString())
            .Where(c => !used.Contains(c[0]) && c[0] != 'A' && c[0] != 'B')
            .ToList();
    }

    public static long GetAvailableRam()
    {
        try
        {
            var result = RunProcess("wmic", "OS get FreePhysicalMemory /value");
            var match = Regex.Match(result.Output, @"FreePhysicalMemory=(\d+)");
            if (match.Success)
                return long.Parse(match.Groups[1].Value) * 1024;
        }
        catch { }

        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    }

    public static long GetTotalRam()
    {
        try
        {
            var result = RunProcess("wmic", "OS get TotalVisibleMemorySize /value");
            var match = Regex.Match(result.Output, @"TotalVisibleMemorySize=(\d+)");
            if (match.Success)
                return long.Parse(match.Groups[1].Value) * 1024;
        }
        catch { }

        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    }

    private static (int ExitCode, string Output, string Error) RunProcess(string fileName, string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit(30000);

        return (process.ExitCode, output, error);
    }
}
