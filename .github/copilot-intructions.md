# Desktop Organizer Application - GitHub Copilot Instructions

## Project Overview

**Desktop Organizer** is a Windows desktop application built with WPF and C# that automatically organizes files and folders on a user's desktop based on customizable rules and categories. The application monitors file system changes in real-time and provides intelligent categorization, visual organization, and productivity features.

---

## Core Architecture & Technology Stack

### Framework & Language
- **Framework:** WPF (Windows Presentation Foundation) with .NET 6/7+
- **Language:** C# 10+
- **UI Pattern:** MVVM (Model-View-ViewModel)
- **Database:** SQLite with Entity Framework Core

### Key Technologies
- **File Monitoring:** System.IO.FileSystemWatcher API
- **System Integration:** Win32 APIs via P/Invoke
- **Data Persistence:** Entity Framework Core with SQLite
- **Configuration Storage:** JSON and AppSettings
- **Threading:** System.Threading for background operations

---

## Project Structure & Organization

```
DesktopOrganizer/
├── .github/
│   └── copilot-instructions.md          (this file)
├── src/
│   ├── DesktopOrganizer.Core/           (Business Logic Layer)
│   │   ├── Models/                       (Data models: FileInfo, Rule, Category)
│   │   ├── Services/                     (Business services: RuleEngine, FileOrganizer)
│   │   ├── Rules/                        (Rule implementations: IRule interface)
│   │   └── Interfaces/                   (Contracts: IFileWatcher, IRepository)
│   ├── DesktopOrganizer.Data/           (Data Access Layer)
│   │   ├── Context/                      (DbContext, EF configuration)
│   │   ├── Repositories/                 (Repository pattern implementations)
│   │   └── Migrations/                   (EF Core migrations)
│   ├── DesktopOrganizer.UI/             (Presentation Layer - WPF)
│   │   ├── Views/                        (XAML files: MainWindow, UserControls)
│   │   ├── ViewModels/                   (ViewModel classes)
│   │   ├── Converters/                   (Value converters for bindings)
│   │   ├── Resources/                    (Styles, templates, colors)
│   │   └── Utils/                        (UI helpers, commands)
│   └── DesktopOrganizer.Integration/    (System Integration Layer)
│       ├── FileSystemWatcher/            (File monitoring implementation)
│       ├── Win32/                        (P/Invoke declarations)
│       └── SystemHooks/                  (Desktop shortcuts, hotkeys)
├── tests/
│   ├── DesktopOrganizer.Core.Tests/     (Unit tests for business logic)
│   └── DesktopOrganizer.Integration.Tests/ (Integration tests)
├── docs/
│   └── API.md                            (API documentation)
└── README.md
```

---

## Key Components & Responsibilities

### 1. File System Monitoring (FileSystemWatcher)
- **Location:** `DesktopOrganizer.Integration/FileSystemWatcher/`
- **Purpose:** Monitors desktop and user-specified directories for file changes
- **Features:**
  - Detect file creation, deletion, renaming, modification
  - Support for subdirectories
  - Filter by file type (extensions)
  - Debounce rapid file changes
  - Thread-safe event handling
- **Pattern:** Observer pattern with event subscriptions

### 2. Rule Engine (Business Logic)
- **Location:** `DesktopOrganizer.Core/Services/RuleEngine.cs`
- **Purpose:** Evaluates files against defined rules and determines target categories
- **Rule Types:**
  - ExtensionRule: Match by file extension (.pdf, .docx, etc.)
  - DateRule: Match by creation/modification date
  - SizeRule: Match by file size ranges
  - NamePatternRule: Regex-based matching on filename
  - CustomRule: User-defined composite rules
- **Pattern:** Strategy pattern with rule composition
- **Execution:** Rules execute in defined priority order with short-circuit evaluation

### 3. File Organization Service
- **Location:** `DesktopOrganizer.Core/Services/FileOrganizer.cs`
- **Purpose:** Performs actual file operations (move, copy, create folders)
- **Responsibilities:**
  - Create folder hierarchies
  - Move files to categorized locations
  - Handle conflicts and existing files
  - Log all operations for audit trail
  - Support undo operations

### 4. View Models (MVVM)
- **MainWindowViewModel:** Manages main application state
- **RuleEditorViewModel:** Handles rule creation/editing
- **DashboardViewModel:** Displays statistics and recent activity
- **SettingsViewModel:** Configuration options
- **Pattern:** INotifyPropertyChanged for property binding, RelayCommand for commands

### 5. Data Access & Repositories
- **Location:** `DesktopOrganizer.Data/`
- **Entities:**
  - Category: Folder categories and destinations
  - Rule: Categorization rules
  - FileLog: Audit trail of file operations
  - UserPreferences: App settings and configuration
- **Pattern:** Repository pattern abstracted through IRepository<T>

### 6. Database Configuration
- **Tool:** Entity Framework Core with SQLite
- **Location:** `AppData/Local/DesktopOrganizer/database.db`
- **Initialization:** Automatic migration on first run
- **Backup:** Automatic daily backups to AppData/Local/DesktopOrganizer/backups/

---

## Code Guidelines & Best Practices

### C# Standards
- **Language Features:** Use modern C# syntax (nullable reference types, records where appropriate, pattern matching)
- **Null Safety:** Enable nullable reference types globally in project file
- **Naming Conventions:**
  - PascalCase for classes, methods, properties
  - camelCase for private fields and local variables
  - Use descriptive names; avoid abbreviations except for common terms (e.g., `fileSystem`, `ruleEngine`)
  - Interface names start with `I` (e.g., `IFileWatcher`)

### Architecture Principles
- **Separation of Concerns:** Keep business logic, data access, and UI presentation strictly separated
- **Dependency Injection:** Use constructor injection for all dependencies (configured in App.xaml.cs)
- **SOLID Principles:**
  - Single Responsibility: Each class has one reason to change
  - Open/Closed: Open for extension, closed for modification
  - Liskov Substitution: Subtypes must be substitutable for base types
  - Interface Segregation: Clients depend on specific interfaces
  - Dependency Inversion: Depend on abstractions, not concrete types

### Code Style
- **Indentation:** 4 spaces (no tabs)
- **Line Length:** Maximum 120 characters
- **Bracing:** Allman style (opening brace on new line)
- **Comments:** Use XML documentation comments for public members
- **Async/Await:** Use async methods for I/O operations; avoid blocking calls with `.Result` or `.Wait()`

### WPF & XAML Standards
- **Bindings:** Use relative source bindings and data templates appropriately
- **Converters:** Keep converters focused on simple value transformations
- **Resources:** Define colors, fonts, and brushes in ResourceDictionaries for consistency
- **Event Handling:** Minimize code-behind; delegate to ViewModel via attached behaviors or commands
- **Data Context:** Set DataContext in ViewModel-first approach

### Testing Requirements
- **Unit Tests:** Required for all business logic in Core project
- **Test Framework:** xUnit with Moq for mocking
- **Test Naming:** `MethodName_Scenario_ExpectedResult`
- **Coverage:** Minimum 80% code coverage for Core project
- **Async Tests:** Use async test methods with proper await patterns

### Error Handling & Logging
- **Exception Handling:** Catch specific exceptions; use custom exception types
- **Logging:** Use Serilog with structured logging for troubleshooting
- **User Feedback:** Display user-friendly error messages in UI via notifications
- **Graceful Degradation:** Application continues operating if non-critical operation fails

---

## File Organization Rules & Categorization

### Rule Definition Format
```csharp
// Example: Create a rule that organizes PDF and Word documents
var documentRule = new ExtensionRule
{
    Name = "Business Documents",
    Priority = 1,
    Extensions = new[] { ".pdf", ".docx", ".xlsx", ".pptx" },
    TargetCategory = "Documents",
    IsActive = true
};
```

### Category Structure
- **Default Categories:** Documents, Images, Videos, Music, Archives, Executables, Other
- **Custom Categories:** User-defined with specific folder paths
- **Subcategories:** Supported via folder nesting (e.g., Documents/Work, Documents/Personal)

### Built-in Rules (Cannot be deleted)
- **System Files:** .sys, .ini, .tmp → System folder (hidden)
- **Executable Files:** .exe, .msi → Programs folder
- **Archives:** .zip, .rar, .7z → Compressed folder

---

## Performance & Optimization

### FileSystemWatcher Optimization
- **Buffer Size:** Set to 64KB for moderate desktop activity
- **Debouncing:** 500ms delay to coalesce rapid file changes
- **Filtering:** Use NotifyFilter to monitor only relevant changes
- **Async Processing:** Queue file operations to background thread

### Database Optimization
- **Indexing:** Indexes on Rule.IsActive, FileLog.CreatedDate, Category.Name
- **Lazy Loading:** Disabled; use explicit Include() for eager loading
- **Query Caching:** Cache rule definitions in memory with invalidation on changes
- **Batch Operations:** Group file operations for efficient database writes

### UI Responsiveness
- **Threading:** File operations run on background ThreadPool thread
- **Dispatcher:** Use Dispatcher.Invoke for UI thread updates
- **Virtualization:** ListBox/DataGrid items virtualized for large datasets
- **Lazy Loading:** Dashboard statistics load progressively

---

## Security & Privacy

### File System Access
- **Permissions:** Request necessary permissions at startup
- **Restricted Paths:** Prevent operations in System32, Windows, Program Files folders
- **Confirmation Dialogs:** Prompt user before moving files from sensitive locations
- **Audit Trail:** Log all file operations with timestamp, source, destination, user

### Data Privacy
- **Configuration Storage:** User settings stored locally, never transmitted
- **Temporary Files:** Cleaned up after operation completion
- **Database Encryption:** SQLite database stored with user-restricted permissions
- **No Telemetry:** Application does not collect or transmit user data

---

## UI/UX Standards

### User Interactions
- **Drag & Drop:** Support drag-and-drop for folder/file configuration
- **Undo/Redo:** Maintain operation history for recent actions
- **Preview:** Show preview of operations before execution
- **Progress Indication:** Display progress bars for batch operations
- **Notifications:** Toast notifications for operation completion/errors

### Accessibility
- **Keyboard Navigation:** All features accessible via keyboard shortcuts
- **Screen Readers:** Support for NVDA and JAWS
- **Color Contrast:** WCAG AA compliance minimum
- **Tab Order:** Logical tab order through all interactive elements

---

## Deployment & Distribution

### Build Process
- **Output:** Standalone .exe installer using WiX or NSIS
- **Dependencies:** Framework-dependent (requires .NET 6/7+ runtime)
- **Signing:** Code-signed with production certificate
- **Version Management:** Semantic versioning (Major.Minor.Patch)

### Installation
- **Target Path:** %ProgramFiles%/DesktopOrganizer or portable AppData
- **Registry:** Minimal registry usage; configuration in AppData
- **Shortcuts:** Desktop shortcut and Start Menu entry created
- **Uninstall:** Clean removal of all files and registry entries

### Updates
- **Mechanism:** Built-in update checker with auto-download
- **Versioning:** Compare installed vs. latest release on GitHub
- **Rollback:** Backup of previous version before update

---

## Common Development Tasks

### Adding a New Rule Type
1. Create class `NewRule` implementing `IRule` interface in `Core/Rules/`
2. Implement `Evaluate(FileInfo file)` method returning bool
3. Add UI controls in `UI/Views/RuleEditorView.xaml`
4. Add ViewModel binding in `RuleEditorViewModel.cs`
5. Update database migration if schema changes needed
6. Add unit tests in `Core.Tests/Services/RuleEngine.Tests.cs`

### Monitoring Additional Directories
1. Update `FileWatcherService.cs` to register new paths
2. Modify `DesktopOrganizer.Core/Models/UserPreferences.cs` to persist paths
3. Update UI in `SettingsView.xaml` with folder picker
4. Add integration test for multi-directory monitoring

### Creating Database Reports
1. Define LINQ query in repository method
2. Create data model for results in `Models/Reports/`
3. Add report generation service
4. Bind to new ViewModel and add Report view
5. Include export functionality (CSV, Excel)

---

## Debugging & Troubleshooting

### Enable Detailed Logging
- Set `LogLevel` to Debug in `appsettings.json`
- Logs written to `AppData/Local/DesktopOrganizer/logs/`
- Use Serilog sinks for file, console, and structured logging

### Common Issues
- **FileSystemWatcher not detecting changes:** Verify path accessibility and permissions
- **Database locked errors:** Check for multiple instances running
- **XAML binding errors:** Check DataContext and binding paths in Output window
- **Performance degradation:** Verify rule count and FileSystemWatcher buffer size

---

## Resources & References

- **WPF Documentation:** https://learn.microsoft.com/en-us/dotnet/desktop/wpf/
- **Entity Framework Core:** https://learn.microsoft.com/en-us/ef/core/
- **MVVM Best Practices:** https://docs.microsoft.com/en-us/archive/msdn-magazine/2009/february/patterns-inside-the-mvvm-pattern
- **C# Coding Guidelines:** https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
- **File System Watcher:** https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher

---

## Project Goals

- Provide automatic desktop organization without manual intervention
- Maintain high performance with minimal system resource usage
- Ensure data integrity with comprehensive audit trails
- Deliver intuitive, accessible user experience
- Support extensibility through custom rules and categories

---

## Limitations & Future Scope

**Current Limitations:**
- Windows-only (requires .NET Desktop Runtime)
- Single-user desktop monitoring only
- No cloud synchronization

**Future Enhancements:**
- Network share monitoring
- Advanced ML-based categorization
- Integration with cloud storage services
- Multi-device synchronization
- Custom rule scripting engine