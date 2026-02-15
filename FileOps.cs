using System;
using System.IO;
using System.Linq;

public static class FileOps
{
    private const double TimestampToleranceSeconds = 2.0;

    public static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            CopyFileOrSymlink(file.FullName, targetFilePath, sourceDir, destinationDir);
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    public static void CopyFileOrSymlink(string srcPath, string destPath, string rootSrc, string rootDest)
    {
        var fileInfo = new FileInfo(srcPath);

        if (fileInfo.LinkTarget != null)
        {
            string target = fileInfo.LinkTarget;
            
            if (Path.IsPathFullyQualified(target) && target.StartsWith(rootSrc))
            {
                target = target.Replace(rootSrc, rootDest);
            }

            if (File.Exists(destPath) || Directory.Exists(destPath)) 
                File.Delete(destPath);
                
            File.CreateSymbolicLink(destPath, target);
            return;
        }

        try
        {
            File.Copy(srcPath, destPath, true);
            
            File.SetLastWriteTime(destPath, File.GetLastWriteTime(srcPath));

            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(destPath, File.GetUnixFileMode(srcPath));
            }
        }
        catch (IOException e)
        {
            Console.WriteLine($"[Błąd I/O] {e.Message}");
        }
    }

    public static void RestoreRecursive(string backupDir, string sourceDir, string rootBackup, string rootSource)
    {
        var dir = new DirectoryInfo(backupDir);
        if (!dir.Exists) return;

        if (!Directory.Exists(sourceDir))
        {
            Directory.CreateDirectory(sourceDir);
        }

        foreach (FileInfo file in dir.GetFiles())
        {
            string sourceFilePath = Path.Combine(sourceDir, file.Name);
            bool shouldCopy = true;

            if (File.Exists(sourceFilePath))
            {
                var sourceInfo = new FileInfo(sourceFilePath);
                if (sourceInfo.Length == file.Length && 
                    Math.Abs((sourceInfo.LastWriteTime - file.LastWriteTime).TotalSeconds) < TimestampToleranceSeconds)
                {
                    shouldCopy = false;
                }
            }

            if (shouldCopy)
            {
                CopyFileOrSymlinkRestore(file.FullName, sourceFilePath, rootBackup, rootSource);
            }
        }

        foreach (DirectoryInfo subDir in dir.GetDirectories())
        {
            string sourceSubDir = Path.Combine(sourceDir, subDir.Name);
            RestoreRecursive(subDir.FullName, sourceSubDir, rootBackup, rootSource);
        }
    }

    public static void CleanRecursive(string backupDir, string sourceDir)
    {
        var sourceInfo = new DirectoryInfo(sourceDir);
        if (!sourceInfo.Exists) return;

        var backupInfo = new DirectoryInfo(backupDir);
        
        if (!backupInfo.Exists)
        {
            Directory.Delete(sourceDir, true);
            return;
        }

        var backupFiles = backupInfo.GetFiles().Select(f => f.Name).ToHashSet();
        var backupDirs = backupInfo.GetDirectories().Select(d => d.Name).ToHashSet();

        foreach (FileInfo file in sourceInfo.GetFiles())
        {
            if (!backupFiles.Contains(file.Name))
            {
                Console.WriteLine($"[RESTORE-DEL] Usuwanie: {file.FullName}");
                file.Delete();
            }
        }

        foreach (DirectoryInfo dir in sourceInfo.GetDirectories())
        {
            if (!backupDirs.Contains(dir.Name))
            {
                Console.WriteLine($"[RESTORE-DEL] Usuwanie katalogu: {dir.FullName}");
                dir.Delete(true);
            }
            else
            {
                string backupSubDir = Path.Combine(backupDir, dir.Name);
                CleanRecursive(backupSubDir, dir.FullName);
            }
        }
    }

    private static void CopyFileOrSymlinkRestore(string backupPath, string sourcePath, string rootBackup, string rootSource)
    {
        var fileInfo = new FileInfo(backupPath);

        if (fileInfo.LinkTarget != null)
        {
            string target = fileInfo.LinkTarget;
            
            if (Path.IsPathFullyQualified(target) && target.StartsWith(rootBackup))
            {
                string relativePath = Path.GetRelativePath(rootBackup, target);
                target = Path.Combine(rootSource, relativePath);
            }

            if (File.Exists(sourcePath) || Directory.Exists(sourcePath)) 
                File.Delete(sourcePath);
                
            File.CreateSymbolicLink(sourcePath, target);
            Console.WriteLine($"[RESTORE] Symlink: {sourcePath} -> {target}");
            return;
        }

        try
        {
            File.Copy(backupPath, sourcePath, true);
            
            File.SetLastWriteTime(sourcePath, File.GetLastWriteTime(backupPath));

            if (OperatingSystem.IsLinux())
            {
                File.SetUnixFileMode(sourcePath, File.GetUnixFileMode(backupPath));
            }

            Console.WriteLine($"[RESTORE] Plik: {sourcePath}");
        }
        catch (IOException e)
        {
            Console.WriteLine($"[Błąd RESTORE I/O] {e.Message}");
        }
    }
}