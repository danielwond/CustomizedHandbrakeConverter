﻿using Handbrake;
using Handbrake.Properties;
using MediaInfo;
using System.Diagnostics;
using System.Timers;

class Program
{
    static string  handbrakePath = "C:\\handbrake\\HandBrakeCLI.exe";
    internal static readonly HashSet<string> extensions = [".mp4", ".wmv", ".avi", ".mkv", ".mov"];

    static async Task Main(string[] args)
    {

        if(!CheckHandbrakePath(handbrakePath))
        {
            Console.WriteLine("HandbrakeCLI does not exist.. please download the latest version from https://handbrake.fr/downloads2.php");
            return;
        }


        var rootPath = "M:\\NOPE\\BYDAY\\New folder (7)";

        var videos = GetTotalVideos(rootPath);
        var convertedVideos = GetConvertedVideos(rootPath);

        //Check if all conversion is finished in this folder!
        if (CheckEncodeStatusOnFolder(rootPath)) 
        {
            Console.WriteLine("Finished encoding all in this folder!");
            return;
        }

        Console.WriteLine(string.Format("Total Videos Found: {0}", videos));
        ShowProgress(rootPath);

        if (convertedVideos.Count != 0)
        {
            Console.WriteLine(string.Format("Last Converted File name: {0}", Path.GetFileName(convertedVideos.Last())));

            Console.WriteLine("------------------------------------------------------------------------------------------------------");

            Console.WriteLine("\nChecking if there are Corrupt Files");

            var corruptVideos = GetCorruptedVideos(convertedVideos);
            if (corruptVideos.Count != 0)
            {
                Console.WriteLine("\nFound Corrupt Files: ");
                foreach (var vid in corruptVideos)
                {
                    Console.WriteLine(Path.GetFileName(vid));
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
                }
                Console.Write("\b");
            }
            else
            {
                Console.Write(new String(' ', Console.BufferWidth));
                Console.WriteLine("\nNo Corrupt files Found.");
            }
            Console.WriteLine("------------------------------------------------------------------------------------------------------");
        }

        //var filteredFiles = files.Where(x => !convertedFilesRenamed.Contains(x)).ToList();
        var filteredFiles = videos.Where(x => !convertedVideos.Select(x => x.Replace("Converted_", "")).Contains(x)).ToList();

        if (filteredFiles.Count == 0 && File.Exists(Path.Combine(rootPath, "Converted.txt")))
        {
            Console.WriteLine("All files are converted.. delete all the old videos? (Y)es or (N)o");

            if (Console.ReadLine().ToLower() != "y")
            {
                return;
            }

            DeleteFiles(convertedVideos.Select(x => x.Replace("Converted_", "")).ToList(), rootPath);

            Console.Clear();

            Console.WriteLine("Old files are deleted! :)");
        }
        else
        {
            var directory = new DirectoryInfo(rootPath);
            var BeforefolderSize = directory.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
            var BeforefolderSizeInMB = BeforefolderSize / 1048576;
            var BeforefolderSizeInGB = BeforefolderSizeInMB / 1024;

            if (!File.Exists(Path.Combine(rootPath, "info.txt")))
            {
                File.AppendAllText(Path.Combine(rootPath, "info.txt"), string.Format("Before Folder Size = {1} GB, {0} MB", BeforefolderSizeInMB, BeforefolderSizeInGB));
            }

            Console.WriteLine("Found {0} file(s) to convert, proceed? (Y)es, (N)o, (F)inalize", filteredFiles.Count);

            var pressed = Console.ReadLine();

            if(pressed == "F")
            {
                directory = new DirectoryInfo(rootPath);

                DeleteFiles(convertedVideos.Select(x => x.Replace("Converted_", "")).ToList(), rootPath);
                LogFinished(directory, rootPath);
                Console.Clear();

                Console.WriteLine("Finalized! :)");

                return;
            }
            else if (pressed != "Y")
            {
                return;
            }

            await ConvertFiles(filteredFiles, rootPath, extensions);

            File.WriteAllText(Path.Combine(rootPath, "Converted.txt"), "1");

            LogFinished(directory, rootPath);
        }
    }

    private async static Task ConvertFiles(IEnumerable<string> files, string rootPath, HashSet<string> extensions)
    {
        foreach (var inputFile in files)
        {
            try
            {
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

                    await Task.Delay(30000);
                }
            }
            catch (Exception)
            {

                throw;
            }
        }
    }

    private static void DeleteFiles(List<string> files, string rootPath)
    {
        { 
            foreach (var item in files)
            {
                if (File.Exists(item))
                {
                    File.SetAttributes(item, FileAttributes.Normal);
                    File.Delete(item);
                    var path = Path.GetDirectoryName(item);
                    var file = Path.GetFileName(item);
                    var newFile = "Converted_" + file;
                    File.Move(Path.Combine(path, newFile), Path.Combine(path, file));
                }
            }

            File.AppendAllText(Path.Combine(rootPath, "Finished.txt"), "1");
        }
    }

    private static void LogFinished(DirectoryInfo directory, string rootPath)
    {
        var AfterfolderSize = directory.EnumerateFiles("*", SearchOption.AllDirectories).Where(x => x.Name.StartsWith("Converted_")).Sum(fi => fi.Length);
        var AfterfolderSizeInMB = AfterfolderSize / 1048576;
        var AfterfolderSizeInGB = AfterfolderSizeInMB / 1024;

        File.AppendAllText(Path.Combine(rootPath, "info.txt"), string.Format("\nAfter Folder Size = {1} GB, {0} MB", AfterfolderSizeInMB, AfterfolderSizeInGB));
        Console.WriteLine("Conversion completed!");

        File.AppendAllText(@"C:\LOGS\finished.txt", rootPath + $" Finished On {DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")}" + "--------------------------------------------------------------------\n");
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

        double percentage = (double)convertedVideos.Count / totalVideos.Count;

        Console.WriteLine(string.Format("Converted: {0}\nNon Converted: {1}", convertedVideos.Count, totalVideos.Count - convertedVideos.Count));
        Console.WriteLine(string.Format("{0}% Progress", Math.Floor(percentage * 100)));
    }

    private static bool CheckHandbrakePath(string path)
    {
        if (File.Exists(path))
        {
            return true;
        }
        return false;
    }
    private static bool CheckEncodeStatusOnFolder(string rootPath)
    {
        return File.Exists(Path.Combine(rootPath, "Finished.txt"));
    }
    private static List<string> GetTotalVideos(string rootPath)
    {
        return  Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                             .Where(x => extensions.Contains(Path.GetExtension(x)))
                             .Where(x => !Path.GetFileName(x).StartsWith("Converted_"))
                             .ToList();
    }
    private static List<string> GetConvertedVideos(string rootPath)
    {
        return Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                                        .Where(x => extensions.Contains(Path.GetExtension(x)))
                                        .Where(x => Path.GetFileName(x).StartsWith("Converted_"))
                                        .ToList();
    }
}