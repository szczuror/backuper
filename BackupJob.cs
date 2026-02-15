using System;
using System.IO;

public class BackupJob : IDisposable
{
    public string SourcePath { get; }
    public string TargetPath { get; }
    private FileSystemWatcher? _watcher;

    public BackupJob(string source, string target)
    {
        SourcePath = Path.GetFullPath(source);
        TargetPath = Path.GetFullPath(target);

        if (!Directory.Exists(TargetPath))
        {
            Directory.CreateDirectory(TargetPath);
        }
        else if (Directory.GetFileSystemEntries(TargetPath).Length > 0)
        {
            throw new InvalidOperationException($"Cel {TargetPath} nie jest pusty!");
        }

        Console.WriteLine($"[SYNC] Kopiowanie początkowe: {SourcePath} -> {TargetPath}");
        FileOps.CopyDirectory(SourcePath, TargetPath);
        
        StartWatching();
    }

    private void StartWatching()
    {
        _watcher = new FileSystemWatcher(SourcePath);
        _watcher.IncludeSubdirectories = true;
        
        _watcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.CreationTime | 
                                NotifyFilters.DirectoryName | NotifyFilters.FileName | 
                                NotifyFilters.LastWrite | NotifyFilters.Size;

        _watcher.Created += OnChanged;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;

        _watcher.EnableRaisingEvents = true;
        Console.WriteLine($"[START] Monitorowanie: {SourcePath} -> {TargetPath}");
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        string relativePath = Path.GetRelativePath(SourcePath, e.FullPath);
        string destPath = Path.Combine(TargetPath, relativePath);

        Console.WriteLine($"[ZM] {e.ChangeType}: {relativePath}");

        if (Directory.Exists(e.FullPath))
        {
            Directory.CreateDirectory(destPath);
        }
        else
        {
            FileOps.CopyFileOrSymlink(e.FullPath, destPath, SourcePath, TargetPath);
        }
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        string relativePath = Path.GetRelativePath(SourcePath, e.FullPath);
        string destPath = Path.Combine(TargetPath, relativePath);

        Console.WriteLine($"[DEL] Usunięto: {relativePath}");

        if (Directory.Exists(destPath)) Directory.Delete(destPath, true);
        else if (File.Exists(destPath)) File.Delete(destPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        string relativeOld = Path.GetRelativePath(SourcePath, e.OldFullPath);
        string destOld = Path.Combine(TargetPath, relativeOld);

        string relativeNew = Path.GetRelativePath(SourcePath, e.FullPath);
        string destNew = Path.Combine(TargetPath, relativeNew);

        Console.WriteLine($"[MV] {relativeOld} -> {relativeNew}");

        if (Directory.Exists(destOld)) Directory.Move(destOld, destNew);
        else if (File.Exists(destOld)) File.Move(destOld, destNew);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}