using System;

namespace DesktopOrganizer.Core.Interfaces;

public interface IFileWatcher
{
    event EventHandler<string> FileCreated;
    event EventHandler<string> FileChanged;
    event EventHandler<string> FileRenamed;
    
    void StartMonitoring(string path);
    void StopMonitoring();
}
