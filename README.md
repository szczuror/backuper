# Backup Manager

**Backup Manager** is an interactive C# console application designed for real-time directory synchronization and backup management. 

## Features
* **Real-time Monitoring:** Uses `FileSystemWatcher` to track and immediately replicate file creations, modifications, deletions, and renames from the source to the target directory.
* **Smart Restore:** Restores files by comparing file sizes and timestamps to skip copying unmodified files. It also cleans up orphaned files in the source that no longer exist in the backup.
* **Advanced File Operations:** Properly handles symbolic links and preserves the `LastWriteTime` of files. It also natively preserves Unix file modes when running on Linux.
* **Graceful Shutdown:** Safely cleans up resources and terminates active monitoring jobs when receiving a SIGINT (Ctrl+C) signal.

## Interactive Commands
Once the application is running, it opens an interactive prompt (`> `) where you can use the following commands:
* `add <source> <target>...` - Performs an initial copy and starts a real-time backup job from the source to one or multiple target directories. **Note:** The target directory must be empty or non-existent prior to running this command.
* `list` - Displays all currently active backup jobs.
* `end <source> [target1]...` - Stops monitoring the specified source. You can optionally specify target paths to only stop specific sync routes.
* `restore <source> <backup>` - Restores the source directory using the provided backup folder.
* `exit` - Cancels all operations and cleanly exits the program.