# Desktop Organizer

**Desktop Organizer** is a Windows desktop application built with WPF and C# that automatically organizes files and folders on a user's desktop based on customizable rules and categories. The application monitors file system changes in real-time and provides intelligent categorization, visual organization, and productivity features.

## Project Structure

The project follows a clean architecture approach:

- **DesktopOrganizer.Core**: Business Logic Layer (Models, Services, Rules)
- **DesktopOrganizer.Data**: Data Access Layer (EF Core, SQLite)
- **DesktopOrganizer.UI**: Presentation Layer (WPF, MVVM)
- **DesktopOrganizer.Integration**: System Integration (FileSystemWatcher, Win32 APIs)

## Getting Started

### Prerequisites
- .NET 6/7+ SDK
- Visual Studio 2022 or compatible IDE

### Build
Run `dotnet build` in the root directory.

## Documentation
See [docs/API.md](docs/API.md) for API documentation.
