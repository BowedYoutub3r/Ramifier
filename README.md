# Ramifier

A Windows RAM disk manager built with WPF. Creates virtual drives backed entirely by RAM using ImDisk, so a 1 GB disk uses 1 GB of physical memory.

## Features

- Create RAM disks with configurable size, drive letter, and file system (NTFS, FAT32, exFAT)
- Live monitoring of disk usage, refreshed every 3 seconds
- System memory overview showing total and available RAM
- Force-dismount removal of active disks
- UAC elevation on demand — runs without admin, elevates only for disk operations
- Per-monitor DPI aware UI with dark theme
- One-click ImDisk Toolkit installer — downloads and installs automatically if missing

## Download

Grab the latest release from [Releases](../../releases). The zip includes:

- **Ramifier.exe** — self-contained, no .NET SDK needed
- **imdiskinst.exe** — ImDisk Toolkit installer (required driver)

On first launch, if ImDisk isn't installed, the app will download and install it for you automatically.

## Building from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet run --project src/Ramifier/Ramifier.csproj
```

Or publish a standalone executable:

```
dotnet publish src/Ramifier/Ramifier.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Usage

1. Select a drive letter, size, unit (MB/GB), and file system
2. Click **Create RAM Disk**
3. Accept the UAC prompt
4. The disk appears in the active list and in Windows Explorer
5. Click **X** on a disk to remove it

## Releasing

Push a version tag to trigger a GitHub Actions build that publishes a release:

```
git tag v1.0.0
git push origin v1.0.0
```

## Important

RAM disks are volatile — all data is lost when the disk is removed or the system is restarted. Do not store anything you can't afford to lose.

## Project Structure

```
src/Ramifier/
  Models/          Data model for RAM disk instances
  Services/        ImDisk CLI wrapper with UAC elevation and auto-installer
  ViewModels/      MVVM view model and commands
  MainWindow.xaml  UI layout
  App.xaml         Theme and styles
```

## License

MIT
