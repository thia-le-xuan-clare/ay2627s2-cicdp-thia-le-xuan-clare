# Waddle

[![.NET Core Desktop](https://github.com/thia-le-xuan-clare/ay2627s2-cicdp-thia-le-xuan-clare/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/thia-le-xuan-clare/ay2627s2-cicdp-thia-le-xuan-clare/actions/workflows/dotnet-desktop.yml)
[![Security](https://github.com/thia-le-xuan-clare/ay2627s2-cicdp-thia-le-xuan-clare/actions/workflows/security.yml/badge.svg)](https://github.com/thia-le-xuan-clare/ay2627s2-cicdp-thia-le-xuan-clare/actions/workflows/security.yml)

Waddle is a Windows desktop app that reminds you to sit up straight. It watches your posture through your webcam, nudges you with escalating warnings when you slouch, and tracks how much good posture time you rack up each day. A duck mascot reacts to how you're doing.

Everything runs locally on your computer.

## Features

- **Posture detection:** uses your webcam and OpenCV face detection to compare your face against a calibrated baseline
- **Calibration:** sit up straight once and Waddle remembers it as your baseline
- **Escalating warnings:** a banner first, then a full-screen overlay if you keep slouching. Press `Ctrl + Shift + E` to dismiss the overlay
- **Duck mascot:** a desktop widget with happy, sad, angry and sleeping moods
- **Time tracking:** counts seconds of good posture per day, and pauses automatically when you step away or the camera is covered
- **History dashboard:** monthly calendar, history chart and pie charts of how you responded to warnings
- **Start with Windows:** optional launch on login, plus an option to start the timer automatically
- **System tray icon:** keeps Waddle running quietly in the background

## Tech stack

| Area | Technology |
| --- | --- |
| Language / runtime | C# on .NET 10 |
| UI | WPF (MVVM with CommunityToolkit.Mvvm) |
| Computer vision | OpenCvSharp4 (Haar cascade face detection) |
| Storage | SQLite via Microsoft.Data.Sqlite |
| Dependency injection | Microsoft.Extensions.DependencyInjection |
| Tray icon | H.NotifyIcon.Wpf |
| Testing | xUnit, FlaUI (UI automation), coverlet |
| CI/CD | GitHub Actions, CodeQL, Dependabot |

## Getting started

### Prerequisites

- Windows 10 or 11 (WPF is Windows-only)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A webcam

### Run it

```powershell
git clone https://github.com/thia-le-xuan-clare/ay2627s2-cicdp-thia-le-xuan-clare.git
cd ay2627s2-cicdp-thia-le-xuan-clare
dotnet restore Waddle.slnx
dotnet run --project Waddle.Wpf
```

### Build a release

```powershell
dotnet publish Waddle.Wpf/Waddle.Wpf.csproj --configuration Release --output ./publish
```

## Project structure

```
Waddle.slnx
├── Waddle.Core            Interfaces and models (no dependencies on UI or database)
├── Waddle.Infrastructure  SQLite stores, OpenCV camera service, file logging
├── Waddle.Wpf             WPF app: windows, view models, monitors, tray icon
├── Waddle.Tests           xUnit unit and integration tests
└── Waddle.UITests         FlaUI end-to-end tests that launch the real app
```

Waddle follows a layered design: `Waddle.Wpf` depends on the interfaces in `Waddle.Core`, and `Waddle.Infrastructure` implements them. See [DIAGRAMS.md](DIAGRAMS.md) for the data flow, architecture, component and class diagrams.

## Testing

```powershell
# Unit and integration tests (includes security tests in Waddle.Tests/Security)
dotnet test Waddle.Tests/Waddle.Tests.csproj

# UI automation tests (build the solution first, they launch the real app)
dotnet build Waddle.slnx --configuration Release
dotnet test Waddle.UITests/Waddle.UITests.csproj --configuration Release --no-build
```

The UI tests point Waddle at throwaway data using environment variables (`WADDLE_DB_PATH`, `WADDLE_STARTUP_RUN_KEY`, `WADDLE_STARTUP_ASKED_FLAG`, `WADDLE_AUTOSTART_TIMER_FLAG`), so they never touch your real database or Windows startup entries.

## CI/CD

Two GitHub Actions workflows run on every push and pull request to `main`:

| Workflow | What it does |
| --- | --- |
| `dotnet-desktop.yml` | Checks formatting (`dotnet format`, which is the lint step), builds in Debug and Release, runs unit and UI tests, reports test results and coverage, and uploads the published app as an artifact |
| `security.yml` | Scans NuGet packages for known vulnerabilities, scans the repo history for leaked secrets (gitleaks), and runs CodeQL static analysis. It also runs weekly |

Dependabot opens pull requests for outdated or vulnerable NuGet packages and GitHub Actions.

### Running the lint check locally

```powershell
dotnet format Waddle.slnx --verify-no-changes   # check only
dotnet format Waddle.slnx                       # fix automatically
```

Formatting rules live in [`.editorconfig`](.editorconfig).

## Privacy and security

- Camera frames are processed in memory for posture detection and are not saved to disk
- Waddle doesn't use any network APIs. Your data stays on your machine
- Posture history and calibration are stored in a local SQLite database at `%LocalAppData%\Waddle\waddle.db`
- All database queries are parameterised, and the schema has `CHECK` and foreign-key constraints as a second line of defence
- Camera activity (on, off, covered, failed) is written to `%LocalAppData%\Waddle\logs\security.log`, so you can always see when the camera was used
- Log messages are escaped so they can't be used to forge log entries
- The "start with Windows" entry is written to the current user's registry only (no admin rights needed), with the executable path quoted

## Contributing

1. Create a branch for your change
2. Run `dotnet format` and `dotnet test` before pushing
3. Open a pull request to `main`. CI must pass before merging
