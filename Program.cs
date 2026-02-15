using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static List<BackupJob> activeBackups = new List<BackupJob>();
    static object activeBackupsLock = new object();
    static CancellationTokenSource cts = new CancellationTokenSource();

    static async Task Main(string[] args)
    {
        Console.WriteLine("Backup Manager");
        PrintHelp();

        Console.CancelKeyPress += (s, e) => {
            e.Cancel = true;
            Console.WriteLine("\nOtrzymano sygnał SIGINT");
            cts.Cancel();
        };

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                Console.Write("> ");

                string? line = await ReadLineAsyncWithCancellation(cts.Token);

                if (line == null) break;

                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = ParseCommand(line);
                if (parts.Count == 0) continue;

                string command = parts[0].ToLower();

                try
                {
                    switch (command)
                    {
                        case "add":
                            HandleAdd(parts);
                            break;
                        case "list":
                            HandleList();
                            break;
                        case "end":
                            HandleEnd(parts);
                            break;
                        case "restore":
                            HandleRestore(parts);
                            break;
                        case "exit":
                            cts.Cancel();
                            break;
                        default:
                            PrintHelp();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        Cleanup();
    }

    static async Task<string?> ReadLineAsyncWithCancellation(CancellationToken token)
    {
        var inputTask = Task.Run(() => Console.ReadLine(), token);
        
        try
        {
            return await inputTask;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    static void HandleAdd(List<string> args)
    {
        if (args.Count < 3) { Console.WriteLine("Użycie: add <source> <target>..."); return; }
        string source = Path.GetFullPath(args[1]);

        if (!Directory.Exists(source))
        {
            Console.WriteLine("Folder źródłowy nie istnieje.");
            return;
        }

        for (int i = 2; i < args.Count; i++)
        {
            string target = Path.GetFullPath(args[i]);
            
            try
            {
                BackupJob? job = null;
                lock (activeBackupsLock)
                {
                    if (activeBackups.Any(b => b.SourcePath == source && b.TargetPath == target))
                    {
                        Console.WriteLine($"Kopia {source} -> {target} już istnieje.");
                        continue;
                    }
                    
                    job = new BackupJob(source, target);
                    activeBackups.Add(job);
                }
            }
            catch (Exception ex) { Console.WriteLine($"Błąd: {ex.Message}"); }
        }
    }

    static void HandleList()
    {
        lock (activeBackupsLock)
        {
            if (activeBackups.Count == 0) Console.WriteLine("Brak aktywnych kopii.");
            foreach (var job in activeBackups)
                Console.WriteLine($"Źródło: {job.SourcePath} | Cel: {job.TargetPath}");
        }
    }

    static void HandleEnd(List<string> args)
    {
        if (args.Count < 2) 
        {
            Console.WriteLine("Użycie: end <source> [target1] [target2] ...");
            return;
        }
        
        string source = Path.GetFullPath(args[1]);
        
        if (args.Count == 2)
        {
            List<BackupJob> toRemove;
            lock (activeBackupsLock)
            {
                toRemove = activeBackups.Where(b => b.SourcePath == source).ToList();
            }
            
            foreach (var job in toRemove)
            {
                job.Dispose();
                lock (activeBackupsLock)
                {
                    activeBackups.Remove(job);
                }
                Console.WriteLine($"Zakończono: {job.SourcePath} -> {job.TargetPath}");
            }
        }
        else
        {
            for (int i = 2; i < args.Count; i++)
            {
                string target = Path.GetFullPath(args[i]);
                
                BackupJob? toRemove = null;
                lock (activeBackupsLock)
                {
                    toRemove = activeBackups.FirstOrDefault(b => b.SourcePath == source && b.TargetPath == target);
                }
                
                if (toRemove != null)
                {
                    toRemove.Dispose();
                    lock (activeBackupsLock)
                    {
                        activeBackups.Remove(toRemove);
                    }
                    Console.WriteLine($"Zakończono: {source} -> {target}");
                }
                else
                {
                    Console.WriteLine($"Nie znaleziono kopii: {source} -> {target}");
                }
            }
        }
    }

    static void HandleRestore(List<string> args)
    {
        if (args.Count != 3) 
        {
            Console.WriteLine("Użycie: restore <source> <backup>");
            return;
        }
        
        string source = Path.GetFullPath(args[1]);
        string backup = Path.GetFullPath(args[2]);
        
        if (!Directory.Exists(backup))
        {
            Console.WriteLine($"Folder backupu {backup} nie istnieje.");
            return;
        }
        
        Console.WriteLine($"Przywracanie {source} z {backup}...");
        
        FileOps.RestoreRecursive(backup, source, backup, source);
        
        FileOps.CleanRecursive(backup, source);
        
        Console.WriteLine("Przywracanie zakończone.");
    }

    static void Cleanup()
    {
        Console.WriteLine("\nCzyszczenie zasobów i zamykanie...");
        lock (activeBackupsLock)
        {
            foreach (var job in activeBackups) job.Dispose();
            activeBackups.Clear();
        }
        Console.WriteLine("Koniec programu.");
    }

    static void PrintHelp()
    {
        Console.WriteLine("Komendy: add, list, end, restore, exit");
    }

    static List<string> ParseCommand(string input)
    {
        var args = new List<string>();
        var regex = new Regex(@"[\""](?<arg>[^\""]+)[\""]|(?<arg>\S+)");
        foreach (Match match in regex.Matches(input))
        {
            args.Add(match.Groups["arg"].Value);
        }
        return args;
    }
}