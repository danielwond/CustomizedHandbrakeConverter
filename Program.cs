﻿using Handbrake;
using Handbrake.Data;
using Handbrake.Properties;
using MediaInfo;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Diagnostics;
using System.Linq;
using System.Timers;

class Program
{
    static string handbrakePath = "C:\\handbrake\\HandBrakeCLI.exe";

    internal static readonly HashSet<string> extensions = new HashSet<string>() { ".mp4", ".wmv", ".avi", ".mkv", ".mov", ".m4v" };
    internal static readonly HashSet<string> unconvertables = new HashSet<string>() { };

    static bool shutdown = false;

    static async Task Main(string[] args)
    {

        if (!CheckFilePath(handbrakePath))
        {
            Console.WriteLine($"HandbrakeCLI does not exist.. please download the latest version from https://handbrake.fr/downloads2.php \nand put it in path \"{handbrakePath}\"");
            return;
        }


        var rootPath = "";
        var videos = GetTotalVideos(rootPath);
        var convertedVideos = GetConvertedVideos(rootPath);
        var unconvertableVideos = videos.Where(file => unconvertables.Any(x => file.ToLower().Contains(x.ToLower()))).ToList();
        var ConvertedButUnconvertableVideos = convertedVideos.Where(file => unconvertables.Any(x => file.ToLower().Contains(x.ToLower()))).ToList();

        var db = new MyDatabase(rootPath);

        //Check if all conversion is finished in this folder!
        if (db.HasFinalizedConversion())
        {
            Console.WriteLine("Finished all encoding in this folder!");
            return;
        }
        Console.WriteLine("------------------------------------------------------------------------------------------------------");
        Console.WriteLine(string.Format("Total Videos Found: {0}", videos.Count));
        if (unconvertableVideos.Count > 0)
        {
            Console.WriteLine(string.Format("Total Unconvertable Videos Found: {0}, Total size: {1}", unconvertableVideos.Count, GetFileSizes(unconvertableVideos)));
            videos = videos.Except(unconvertableVideos).ToList();
        }
        if (ConvertedButUnconvertableVideos.Count > 0)
        {
            Console.WriteLine(string.Format("Total Converted But Unconvertable Videos Found: {0}, Total size: {1}", ConvertedButUnconvertableVideos.Count, GetFileSizes(ConvertedButUnconvertableVideos)));
            convertedVideos = ConvertedButUnconvertableVideos;
        }
        Console.WriteLine("------------------------------------------------------------------------------------------------------");


        ShowProgress(rootPath);

        if (convertedVideos.Count != 0)
        {
            Console.WriteLine(string.Format("Last Converted File name: {0}", Path.GetFileName(convertedVideos.Last())));

            Console.WriteLine("------------------------------------------------------------------------------------------------------");

            Console.WriteLine("\nChecking if there are Converted but Corrupted Files..");

            var corruptVideos = GetCorruptedVideos(convertedVideos);
            if (corruptVideos.Count != 0)
            {
                Console.WriteLine("\nFound Corrupt File(s): ");
                foreach (var vid in corruptVideos)
                {
                    Console.WriteLine($"- {Path.GetFileName(vid)}");
                }
                Console.WriteLine("\nDelete corrupt files? (Y)es or (N)o");
                if (Console.ReadLine().ToLower() == "y")
                {
                    using var spinner = new Spinner(10, 10);
                    spinner.Start();

                    foreach (var item in corruptVideos)
                    {
                        File.SetAttributes(item, FileAttributes.Normal);
                        File.Delete(item);
                    }
                    spinner.Stop();

                    Console.Write(new string(' ', Console.BufferWidth));
                    Console.WriteLine("\nAll corrupt videos are now deleted :)");

                    //Refresh Files after deletion

                    videos = GetTotalVideos(rootPath);
                    convertedVideos = GetConvertedVideos(rootPath);

                    if (unconvertableVideos.Count > 0)
                    {
                        videos = videos.Except(unconvertableVideos).ToList();
                    }
                    if (ConvertedButUnconvertableVideos.Count > 0)
                    {
                        convertedVideos = ConvertedButUnconvertableVideos;
                    }
                }
                Console.Write("\b");
            }
            else
            {
                Console.Write(new string(' ', Console.BufferWidth));
                Console.WriteLine("\nNo Corrupt files Found.");
            }
            Console.WriteLine("------------------------------------------------------------------------------------------------------");
        }

        //var filteredFiles = files.Where(x => !convertedFilesRenamed.Contains(x)).ToList();
        var filteredFiles = videos.Where(x => !convertedVideos.Select(x => x.Replace("Converted_", "")).Contains(x)).ToList();

        if (filteredFiles.Count == 0 && db.HasFolderCompletedConversion())
        {
            Console.WriteLine("All files are converted.. delete all the old videos? (Y)es or (N)o");

            if (Console.ReadLine().ToLower() != "y")
            {
                return;
            }

            DeleteFiles(convertedVideos.Select(x => x.Replace("Converted_", "")).ToList());

            Console.Clear();

            Console.WriteLine("Old files are deleted! :)");

            db.FinalizeFolderConversion();

            FolderHelper.SetFolderIcon(rootPath);
        }
        else
        {
            var directory = new DirectoryInfo(rootPath);
            var BeforefolderSize = directory.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
            long BeforefolderSizeInMB = BeforefolderSize / 1048576;
            long BeforefolderSizeInGB = BeforefolderSizeInMB / 1024;

            if (db.IsBeforeFolderSizeSet())
            {
                db.SetBeforeFolderSize(BeforefolderSizeInGB == 0 ? BeforefolderSizeInMB : BeforefolderSizeInGB, BeforefolderSizeInGB == 0 ? "MB" : "GB");
            }

            Console.WriteLine("Found {0} file(s) to convert, proceed? (Y)es, (N)o, (F)inalize", filteredFiles.Count);

            var pressed = Console.ReadLine();

            if (pressed == "F")
            {
                Console.WriteLine("------------------------------------------------------------------------------------------------------");
                Console.WriteLine("are you sure?? this will delete the source files..\n you should only do this if the source files are corrupt and not convertable!\nproceed? (Y)es, (N)o");
                Console.WriteLine("------------------------------------------------------------------------------------------------------");
                var confirmation = Console.ReadLine();
                if (confirmation.ToLower() == "y")
                {
                    Console.WriteLine("Okay, you asked for it fellar");

                    //rename the source files to converted_something..
                    foreach (var item in filteredFiles)
                    {
                        var path = Path.GetDirectoryName(item);
                        var file = Path.GetFileName(item);
                        var newFile = "Converted_" + file;

                        File.Move(Path.Combine(path, file), Path.Combine(path, newFile), true);
                    }

                    DeleteFiles(convertedVideos.Select(x => x.Replace("Converted_", "")).ToList());


                    //log finished
                    directory = new DirectoryInfo(rootPath);

                    db.FinalizeFolderConversion();

                    LogFinished(directory, db);
                    Console.Clear();

                    FolderHelper.SetFolderIcon(rootPath);

                    Console.WriteLine("Finalized! :)");

                    return;
                }
                else
                {
                    Console.WriteLine("Phew, close! good thing i inserted this confirmation then.. ");
                    return;
                }
            }
            else if (pressed != "Y")
            {
                return;
            }

            await ConvertFiles(filteredFiles, rootPath, db);

            db.FolderCompletedConversion();

            LogFinished(directory, db);

            if (shutdown)
            {
                Process.Start("shutdown", "/f");
            }
        }
    }

    private async static Task ConvertFiles(IEnumerable<string> files, string rootPath, MyDatabase db)
    {
        foreach (var inputFile in files)
        {
            try
            {
                var startTime = DateTime.Now;

                var folderPath = Path.GetDirectoryName(inputFile);
                var fileName = Path.GetFileName(inputFile);
                var outputFile = Path.Combine(folderPath, $"Converted_{fileName}");
                var fileInfo = new FileInfo(inputFile);

                //if (fileInfo.Length >= 943718400 && !fileName.StartsWith("Converted_"))
                if (!fileName.StartsWith("Converted_"))
                {
                    Console.WriteLine("Converting file {0}", inputFile);

                    string additionalOptions = "--preset=\"Fast 1080p30\"";
                    string arguments = $"-i \"{inputFile}\" -o \"{outputFile}\" {additionalOptions}";

                    var handbrake = Process.Start(handbrakePath, arguments);
                    handbrake.WaitForExit();

                    Console.WriteLine("\n\n---------------------------------------------------------------------------------------------------------------\n\n");
                    Console.WriteLine($"Conversion of {fileName} finished!\n");
                    ShowProgress(rootPath);
                    Console.WriteLine("\n\n---------------------------------------------------------------------------------------------------------------\n\n");

                    db.FinishedConvertingFile(inputFile);

                    var elapsed = DateTime.Now - startTime;

                    #region cool off the CPU after completing the conversion..
                    //if the time elapsed greater than 4 minutes or 240 Seconds, wait longer to cool off..
                    if (elapsed.TotalSeconds >= 300)
                    {
                        var waitingTime = (elapsed.TotalSeconds / 3.5) * 1000;
                        var flooredWaitingTime = (int)Math.Floor(waitingTime);
                        await Task.Delay(flooredWaitingTime);
                    }
                    //if time elapsed is between 1 minute or 60 seconds AND 4 minutes or 240 seconds, wait one minute to cool off
                    else if (elapsed.TotalSeconds >= 60)
                    {
                        var waitingTime = 60000;
                        await Task.Delay(waitingTime);
                    }
                    else
                    {
                        await Task.Delay(30000);
                    }
                    #endregion
                }
            }
            catch (Exception)
            {

                throw;
            }
        }
    }

    private static void DeleteFiles(List<string> files)
    {
        {
            foreach (var item in files)
            {
                var path = Path.GetDirectoryName(item);
                var file = Path.GetFileName(item);
                var newFile = "Converted_" + file;

                if (File.Exists(item))
                {
                    File.SetAttributes(item, FileAttributes.Normal);
                    File.Delete(item);

                    if (file.Contains("2160p"))
                    {
                        file = file.Replace("2160p", "1080p");
                    }
                    File.Move(Path.Combine(path, newFile), Path.Combine(path, file));
                }


                else if (File.Exists(Path.Combine(path, newFile)))
                {
                    File.Move(Path.Combine(path, newFile), Path.Combine(path, file));
                }
            }

            //File.AppendAllText(Path.Combine(rootPath, "Finished.txt"), "1");
        }
    }

    private static void LogFinished(DirectoryInfo directory, MyDatabase db)
    {
        var AfterfolderSize = directory.EnumerateFiles("*", SearchOption.AllDirectories).Where(x => x.Name.StartsWith("Converted_")).Sum(fi => fi.Length);
        var AfterfolderSizeInMB = AfterfolderSize / 1048576;
        var AfterfolderSizeInGB = AfterfolderSizeInMB / 1024;

        if (db.IsAfterFolderSizeSet())
        {
            db.SetAfterFolderSize(AfterfolderSizeInGB == 0 ? 
                AfterfolderSizeInMB : 
                AfterfolderSizeInGB, 
                AfterfolderSizeInGB == 0 ? "MB" : "GB");
        }

        Console.WriteLine("Conversion completed!");
    }

    private static List<string> GetCorruptedVideos(List<string> videos)
    {
        var corruptVideos = new List<string>();
        foreach (var video in videos)
        {
            var fileName = Path.GetFileName(video);

            if (fileName.StartsWith("Converted_"))
            {
                using MediaInfo.MediaInfo mi = new MediaInfo.MediaInfo();

                mi.Open(video);
                mi.Option("Complete");

                var ss = mi.Get(StreamKind.Video, 0, "CodecID");

                if (string.IsNullOrEmpty(ss))
                {
                    corruptVideos.Add(video);
                }
            }
        }

        return corruptVideos;
    }

    private static void ShowProgress(string rootPath)
    {

        var totalVideos = GetTotalVideos(rootPath);
        var convertedVideos = GetConvertedVideos(rootPath);
        var unconvertableVideos = totalVideos.Where(file => unconvertables.Any(x => file.ToLower().Contains(x.ToLower()))).ToList();
        var ConvertedButUnconvertableVideos = convertedVideos.Where(file => unconvertables.Any(x => file.ToLower().Contains(x.ToLower()))).ToList();

        if (unconvertableVideos.Count > 0)
        {
            totalVideos = totalVideos.Except(unconvertableVideos).ToList();
        }
        if (ConvertedButUnconvertableVideos.Count > 0)
        {
            convertedVideos = ConvertedButUnconvertableVideos;
        }

        double percentage = (double)convertedVideos.Count / totalVideos.Count;

        Console.WriteLine(string.Format("Converted: {0}\nNon Converted: {1}", convertedVideos.Count, totalVideos.Count - convertedVideos.Count));
        Console.WriteLine(string.Format("{0}% Progress", Math.Floor(percentage * 100)));
    }

    private static bool CheckFilePath(string path)
    {
        return File.Exists(path);
    }

    private static List<string> GetTotalVideos(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            var error = "The directory does not exist.. or maybe its not a directory. ¯\\_(ツ)_/¯";
            Console.WriteLine(error);
            throw new Exception(error);
        }
        var files = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                             .Where(x => extensions.Contains(Path.GetExtension(x).ToLower()))
                             .Where(x => !Path.GetFileName(x).StartsWith("Converted_"))
                             .ToList();
        return files;
    }
    private static List<string> GetConvertedVideos(string rootPath)
    {
        return Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                                        .Where(x => extensions.Contains(Path.GetExtension(x)))
                                        .Where(x => Path.GetFileName(x).StartsWith("Converted_"))
                                        .ToList();
    }
    private static string GetFileSizes(List<string> files)
    {
        double totalSize = 0;
        foreach (var file in files)
        {
            var size = new FileInfo(file).Length;
            totalSize += size;
        }
        var sizeInMB = totalSize / 1048576;
        var sizeInGB = sizeInMB / 1024;

        return sizeInGB == 0 ? sizeInMB.ToString("#.##") + " MB" : sizeInGB.ToString("#.##") + " GB";
    }
}