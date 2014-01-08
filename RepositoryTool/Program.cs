using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using RepositoryManifest;
using System.Resources;
using Utilities;


namespace RepositoryTool
{
    class Program
    {
        static void Main(string[] args)
        {
            DateTime startTime = DateTime.Now;

            int exitCode = 0;
            ManifestConsole console = new ManifestConsole();

            int argIndex = 0;

            // Give the user some help if they need it
            string commandArg = "help";
            if (args.Length > 0)
            {
                commandArg = args[argIndex++];
            }

            // Initial screen for valid command
            switch (commandArg)
            {
                case "create":
                case "validate":
                case "status":
                case "update":
                case "edit":
                case "groom":
                case "clear":
                case "info":
                case "help":
                    break;

                default:
                    console.WriteLine("Unrecognized command \"" + commandArg + "\"");
                    exitCode = 1;
                    Environment.Exit(exitCode);
                    break;
            }

            RepositoryTool tool = new RepositoryTool();

            tool.WriteLogDelegate =
                delegate(String message)
                {
                    console.Write(message);
                };

            // Default manifest file name located in current directory.
            String manifestFilePathNotRecursive =
                PathUtilities.NativeFromNativeAndStandard(
                    System.IO.Directory.GetCurrentDirectory(),
                    Manifest.DefaultManifestStandardFilePath);

            bool ignoreDate = false;
            bool ignoreNew = false;
            bool time = false;
            bool all = false;
            bool force = false;
            bool ignoreDefault = false;
            bool recursive = false;
            bool cascade = false;
            bool manifestInfoChanged = false;
            bool noTouch = false;
            bool confirmUpdate = false;

            string repositoryName = null;
            string repositoryDescription = null;
            string hashMethod = null;

            List<String> ignoreList = new List<string>();
            List<String> dontIgnoreList = new List<string>();

            // Parse flags
            while (argIndex < args.Length)
            {
                string nextArg = args[argIndex++];

                switch (nextArg)
                {
                    case "-all":
                        all = true;
                        break;

                    case "-silent":
                        console.Silent = true;
                        tool.WriteLogDelegate = null;
                        break;

                    case "-detail":
                        console.Detail = true;
                        break;

                    case "-showProgress":
                        tool.ShowProgress = true;
                        break;

                    case "-ignoreDate":
                        ignoreDate = true;
                        break;

                    case "-ignoreNew":
                        ignoreNew = true;
                        break;

                    case "-reHash":
                        tool.AlwaysCheckHash = true;
                        break;

                    case "-newHash":
                        tool.MakeNewHash = true;
                        break;

                    case "-time":
                        time = true;
                        break;
                        
                    case "-name":
                        if (ArgUtilities.HasAnotherArgument(
                            args, argIndex, console) == true)
                        {
                            repositoryName = args[argIndex++];
                        }
                        break;

                    case "-description":
                        if (ArgUtilities.HasAnotherArgument(
                            args, argIndex, console) == true)
                        {
                            repositoryDescription = args[argIndex++];
                        }
                        break;

                    case "-hashMethod":
                        if (ArgUtilities.HasAnotherArgument(
                            args, argIndex, console) == true)
                        {
                            hashMethod = args[argIndex++];
                        }
                        break;

                    case "-backDate":
                        tool.BackDate = true;
                        break;
                        
                    case "-trackMoves":
                        tool.TrackMoves = true;
                        break;

                    case "-trackDuplicates":
                        tool.TrackDuplicates = true;
                        break;

                    case "-ignore":
                        if (ArgUtilities.HasAnotherArgument(
                            args, argIndex, console) == true)
                        {
                            ignoreList.Add(args[argIndex++]);
                        }
                        break;

                    case "-dontIgnore":
                        if (ArgUtilities.HasAnotherArgument(
                            args, argIndex, console) == true)
                        {
                            dontIgnoreList.Add(args[argIndex++]);
                        }
                        break;

                    case "-manifestFile":
                        if (ArgUtilities.HasAnotherArgument(
                            args, argIndex, console) == true)
                        {
                            manifestFilePathNotRecursive = args[argIndex++];
                        }
                        break;

                    case "-force":
                        force = true;
                        break;
                        
                    case "-ignoreDefault":
                        ignoreDefault = true;
                        break;

                    case "-recursive":
                        recursive = true;
                        break;

                    case "-cascade":
                        cascade = true;
                        break;

                    case "-noTouch":
                        noTouch = true;
                        break;

                    case "-confirmUpdate":
                        confirmUpdate = true;
                        break;

                    default:
                        console.WriteLine("Unrecognized parameter \" " + nextArg + "\"");
                        commandArg = "";
                        exitCode = 1;
                        break;
                }
            }

            if (time)
            {
                console.WriteLine("Started: " + startTime.ToString());
            }

            // Prepare a list of paths to be processed
            List<String> manifestFilePaths = new List<string>();
            if (recursive)
            {
                FindManifests(
                    new DirectoryInfo(Directory.GetCurrentDirectory()),
                    cascade,
                    manifestFilePaths);
            }
            else
            {
                manifestFilePaths.Add(manifestFilePathNotRecursive);
            }

            foreach (String manifestFilePath in manifestFilePaths)
            {
                if (recursive)
                {
                    console.WriteLine(Path.GetDirectoryName(manifestFilePath) + ":");
                }

                // Initialize the tool for this manifest
                tool.Clear();
                tool.Manifest = null;

                // Second copy of the manifest which will remain unmodified
                // and possibly rewritten after a validate.
                Manifest manifestForValidateDateUpdate = null;

                FileInfo fileInfo = new FileInfo(manifestFilePath);
                tool.RootDirectory = fileInfo.Directory;

                switch (commandArg)
                {
                    case "create":
                        {
                            bool doCreate = true;

                            if (force == false)
                            {
                                if (File.Exists(manifestFilePath))
                                {
                                    doCreate = false;
                                    console.Write("Replace existing manifest file? ");
                                    doCreate = console.CheckConfirm();
                                }
                            }

                            if (doCreate == true)
                            {
                                tool.Manifest = tool.MakeManifest();
                            }

                            break;
                        }

                    case "validate":
                    case "status":
                    case "update":
                    case "edit":
                    case "groom":
                        {
                            if (commandArg == "validate")
                            {
                                tool.AlwaysCheckHash = true;
                            }
                            else if (commandArg == "update")
                            {
                                tool.Update = true;
                            }

                            bool different = false;

                            try
                            {
                                tool.Manifest = Manifest.ReadManifestFile(manifestFilePath);

                                if (commandArg == "validate" && noTouch == false)
                                {
                                    // Read a second copy which will remain unmodified
                                    manifestForValidateDateUpdate =
                                        Manifest.ReadManifestFile(manifestFilePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                console.ReportException(ex);
                                console.WriteLine("Could not read manifest.");
                                Environment.Exit(1);
                            }

                            if (tool.Manifest == null)
                            {
                                exitCode = 1;
                            }
                            else if (commandArg != "edit")
                            {
                                tool.DoUpdate();

                                if (tool.MissingFiles.Count > 0)
                                {
                                    console.WriteLine(tool.MissingFiles.Count.ToString() + " files are missing.");
                                    console.DetailFiles(tool.MissingFiles);
                                    different = true;
                                }

                                if (tool.ChangedFiles.Count > 0)
                                {
                                    console.WriteLine(tool.ChangedFiles.Count.ToString() + " files have changed content.");
                                    console.DetailFiles(tool.ChangedFiles);
                                    different = true;
                                }

                                if (tool.NewFiles.Count > 0)
                                {
                                    console.WriteLine(tool.NewFiles.Count.ToString() + " files are new.");
                                    console.DetailFiles(tool.NewFiles);

                                    if (ignoreNew == false)
                                    {
                                        different = true;
                                    }
                                }

                                if (tool.LastModifiedDateFiles.Count > 0)
                                {
                                    console.WriteLine(tool.LastModifiedDateFiles.Count.ToString() +
                                        " files have last-modified dates which are different.");

                                    console.DetailFiles(tool.LastModifiedDateFiles);

                                    if (ignoreDate == false)
                                    {
                                        different = true;
                                    }
                                }

                                if (tool.ErrorFiles.Count > 0)
                                {
                                    console.WriteLine(tool.ErrorFiles.Count.ToString() + " files have errors.");
                                    console.DetailFiles(tool.ErrorFiles);
                                    different = true;
                                }

                                if (tool.MovedFiles.Count > 0)
                                {
                                    console.WriteLine(tool.MovedFiles.Count.ToString() + " files were moved.");
                                    console.DetailFiles(tool.MovedFileOrder, tool.MovedFiles);
                                    different = true;
                                }

                                if (tool.DuplicateFiles.Keys.Count > 0)
                                {
                                    console.WriteLine(tool.DuplicateFiles.Keys.Count.ToString() + " file hashes were duplicates.");
                                    console.DetailFiles(tool.DuplicateFiles);
                                }

                                if (tool.NewlyIgnoredFiles.Count > 0)
                                {
                                    console.WriteLine(tool.NewlyIgnoredFiles.Count.ToString() + " files are newly ignored.");
                                    console.DetailFiles(tool.NewlyIgnoredFiles);
                                }

                                if (tool.IgnoredFiles.Count > 1)
                                {
                                    console.WriteLine(tool.IgnoredFiles.Count.ToString() + " files were ignored.");

                                    if (all == true)
                                    {
                                        console.DetailFiles(tool.IgnoredFiles);
                                    }
                                }


                                console.WriteLine(tool.FileCheckedCount.ToString() + " files were checked.");

                                if (commandArg == "validate")
                                {
                                    if (different)
                                    {
                                        console.WriteLine("Problems found.");
                                        exitCode = 1;
                                    }
                                    else
                                    {
                                        console.WriteLine("No problems.");
                                    }
                                }
                            }
                            break;
                        }

                    case "clear":

                        try
                        {
                            tool.Manifest = Manifest.ReadManifestFile(manifestFilePath);
                        }
                        catch (Exception ex)
                        {
                            console.ReportException(ex);
                            console.WriteLine("Could not read manifest.");
                        }

                        if (tool.Manifest != null)
                        {
                            bool doClear = true;

                            if (force == false)
                            {
                                console.Write("Clear " +
                                    tool.Manifest.CountFiles().ToString() +
                                    " files from the manifest? ");

                                doClear = console.CheckConfirm();
                            }

                            if (doClear == true)
                            {
                                if (tool.Manifest == null)
                                {
                                    exitCode = 1;
                                }
                                else
                                {
                                    tool.Manifest.RootDirectory.Files.Clear();
                                    tool.Manifest.RootDirectory.Subdirectories.Clear();
                                }
                            }
                        }

                        break;

                    case "info":
                        try
                        {
                            tool.Manifest = Manifest.ReadManifestFile(manifestFilePath);
                        }
                        catch (Exception ex)
                        {
                            console.ReportException(ex);
                            console.WriteLine("Could not read manifest.");
                        }

                        if (tool.Manifest == null)
                        {
                            exitCode = 1;
                        }
                        else
                        {
                            if (tool.Manifest.Name != null)
                            {
                                console.WriteLine("Name:                          " + tool.Manifest.Name);
                            }

                            console.WriteLine("GUID:                          " + tool.Manifest.Guid.ToString());

                            if (tool.Manifest.DefaultHashMethod != null)
                            {
                                console.WriteLine("Default hash method:           " + tool.Manifest.DefaultHashMethod);
                            }

                            console.WriteLine("Date of creation:              " +
                                (tool.Manifest.InceptionDateUtc.ToLocalTime()).ToString());

                            console.WriteLine("Date of last update:           " +
                                (tool.Manifest.LastUpdateDateUtc.ToLocalTime()).ToString());

                            console.WriteLine("Last change of manifest info:  " +
                                (tool.Manifest.ManifestInfoLastModifiedUtc.ToLocalTime()).ToString());

                            console.WriteLine("Date of last validation:       " +
                                (tool.Manifest.LastValidateDateUtc.ToLocalTime()).ToString());

                            NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
                            nfi.NumberDecimalDigits = 0;


                            console.WriteLine("Total number of files:         " + tool.Manifest.CountFiles().ToString("N", nfi));
                            console.WriteLine("Total number of bytes:         " + tool.Manifest.CountBytes().ToString("N", nfi));
                            if (tool.Manifest.IgnoreList.Count > 0)
                            {
                                console.WriteLine("Ignoring these file patterns:");
                                foreach (String nextIgnore in tool.Manifest.IgnoreList)
                                {
                                    console.WriteLine("   " + nextIgnore);
                                }
                            }
                        }

                        if (tool.Manifest.Description != null)
                        {
                            console.WriteLine();
                            console.WriteLine("Description: ");
                            console.WriteLine(tool.Manifest.Description);
                        }

                        break;

                    case "help":
                        console.Write(Properties.Resources.RepositoryToolHelp);
                        break;

                    case "":
                        break;
                }

                switch (commandArg)
                {
                    case "create":
                    case "update":
                    case "edit":
                    case "clear":

                        if (tool.Manifest != null)
                        {
                            if (repositoryName != null)
                            {
                                tool.Manifest.Name = repositoryName;
                                manifestInfoChanged = true;
                            }

                            if (repositoryDescription != null)
                            {
                                tool.Manifest.Description = repositoryDescription;
                                manifestInfoChanged = true;
                            }

                            if (hashMethod != null)
                            {
                                tool.Manifest.DefaultHashMethod = hashMethod;
                                manifestInfoChanged = true;
                            }

                            if (ignoreList.Count > 0)
                            {
                                foreach (String nextIgnore in ignoreList)
                                {
                                    if (tool.Manifest.IgnoreList.Contains(nextIgnore) == false)
                                    {
                                        tool.Manifest.IgnoreList.Add(nextIgnore);
                                    }
                                }
                                manifestInfoChanged = true;
                            }

                            if (dontIgnoreList.Count > 0)
                            {
                                foreach (String nextIgnore in dontIgnoreList)
                                {
                                    if (tool.Manifest.IgnoreList.Contains(nextIgnore) == true)
                                    {
                                        tool.Manifest.IgnoreList.Remove(nextIgnore);
                                    }
                                }
                                manifestInfoChanged = true;
                            }

                            if (ignoreDefault == true)
                            {
                                tool.Manifest.IgnoreList.Clear();

                                Manifest defaultPrototype = tool.MakeManifest();
                                foreach (String nextIgnore in defaultPrototype.IgnoreList)
                                {
                                    tool.Manifest.IgnoreList.Add(nextIgnore);
                                }
                                manifestInfoChanged = true;
                            }

                            if (manifestInfoChanged)
                            {
                                tool.Manifest.ManifestInfoLastModifiedUtc =
                                    DateTime.Now.ToUniversalTime();
                            }

                            if (confirmUpdate)
                            {
                                console.Write("Update manifest? ");

                                bool writeManifest = console.CheckConfirm();
                                if (writeManifest == false)
                                {
                                    break;
                                }
                            }

                            try
                            {
                                tool.Manifest.WriteManifestFile(manifestFilePath);
                            }
                            catch (Exception ex)
                            {
                                console.ReportException(ex);
                                console.WriteLine("Could not write manifest.");
                                exitCode = 1;
                            }
                        }
                        break;

                    case "validate":
                        if (noTouch == false)
                        {
                            manifestForValidateDateUpdate.LastValidateDateUtc =
                                DateTime.UtcNow;

                            try
                            {
                                manifestForValidateDateUpdate.WriteManifestFile(
                                    manifestFilePath);
                            }
                            catch (Exception ex)
                            {
                                console.ReportException(ex);
                                console.WriteLine("Could not write manifest.");
                                exitCode = 1;
                            }
                        }
                        break;

                    case "groom":

                        if (tool.NewFilesForGroom.Count > 0)
                        {
                            bool doGroom = true;

                            if (force == false)
                            {
                                console.Write("Delete " +
                                    tool.NewFilesForGroom.Count.ToString() +
                                    " new files? ");

                                doGroom = console.CheckConfirm();
                            }

                            if (doGroom == true)
                            {
                                foreach (FileInfo delFile in tool.NewFilesForGroom)
                                {
                                    delFile.Delete();
                                }
                            }
                        }

                        if (all == true && tool.IgnoredFilesForGroom.Count > 0)
                        {
                            bool doGroomAll = true;

                            if (force == false)
                            {
                                console.Write("Delete " +
                                    tool.IgnoredFilesForGroom.Count.ToString() +
                                    " ignored files? ");

                                doGroomAll = console.CheckConfirm();
                            }

                            if (doGroomAll == true)
                            {
                                foreach (FileInfo delFile in
                                    tool.IgnoredFilesForGroom)
                                {
                                    delFile.Delete();
                                }
                            }
                        }

                        break;
                }

                if (recursive)
                {
                    console.WriteLine();
                }
            }

            if (time)
            {
                DateTime finishedTime = DateTime.Now;
                console.WriteLine("Finished: " + finishedTime.ToString());
                console.WriteLine("Duration: " + (finishedTime - startTime).ToString());
            }

            Environment.Exit(exitCode);
        }



        // Helper methods
        
        static void FindManifests(
            DirectoryInfo nextDirectory,
            bool cascade,
            List<String> filePaths)
        {
            String checkManifestPath =
                PathUtilities.NativeFromNativeAndStandard(
                    nextDirectory.FullName,
                    Manifest.DefaultManifestStandardFilePath);

            bool foundManifest = false;
            if (File.Exists(checkManifestPath))
            {
                foundManifest = true;
                filePaths.Add(checkManifestPath);
            }

            if (cascade == true || foundManifest == false)
            {
                try
                {
                    foreach (DirectoryInfo nextSubDirectory in
                        nextDirectory.GetDirectories())
                    {
                        FindManifests(
                            nextSubDirectory,
                            cascade,
                            filePaths);
                    }
                }
                catch (System.UnauthorizedAccessException)
                {
                    // Do nothing
                }
            }
        }
    }
}
