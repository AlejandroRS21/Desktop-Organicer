using System;
using System.IO;
using DesktopOrganizer.Core.Interfaces;

namespace DesktopOrganizer.Integration.FileSystemWatcher;

public class FileWatcherService : IFileWatcher, IDisposable
{
    private readonly System.IO.FileSystemWatcher _watcher;

    public event EventHandler<string>? FileCreated;
    public event EventHandler<string>? FileChanged;
    public event EventHandler<string>? FileRenamed;

    public FileWatcherService()
    {
        _watcher = new System.IO.FileSystemWatcher();
        _watcher.NotifyFilter = NotifyFilters.FileName 
                              | NotifyFilters.LastWrite 
                              | NotifyFilters.CreationTime;
        
        _watcher.Created += OnCreated;
        _watcher.Changed += OnChanged;
        _watcher.Renamed += OnRenamed;
    }

    public void StartMonitoring(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
        }

        _watcher.Path = path;
        _watcher.EnableRaisingEvents = true;
    }

    public void StopMonitoring()
    {
        _watcher.EnableRaisingEvents = false;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        FileCreated?.Invoke(this, e.FullPath);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        FileChanged?.Invoke(this, e.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        FileRenamed?.Invoke(this, e.FullPath);
    }

    public void Dispose()
    {
        _watcher.Created -= OnCreated;
        _watcher.Changed -= OnChanged;
        _watcher.Renamed -= OnRenamed;
        _watcher.Dispose();
    }
}
