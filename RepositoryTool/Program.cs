using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using RepositoryManifest;
using System.Resources;


namespace RepositoryTool
{
    class Program
    {
        static void Main(string[] args)
        {
            int exitCode = 0;
            DateTime startTime = DateTime.Now;

            int argIndex = 0;

            string commandArg = "help";
            if (args.Length > 0)
            {
                commandArg = args[argIndex++];
            }

            RepositoryTool tool = new RepositoryTool();

            tool.WriteLogDelegate =
                delegate(String message)
                {
                    Write(message);
                };

            // Default manifest file name located in current directory
            tool.RootDirectory =
                new DirectoryInfo(Directory.GetCurrentDirectory());

            String manifestFilePath =
                Path.Combine(
                    tool.RootDirectory.FullName,
                    RepositoryTool.ManifestFileName);

            bool ignoreDate = false;
            bool ignoreNew = false;
            bool time = false;
            bool all = false;
            bool force = false;

            string repositoryName = null;
            string repositoryDescription = null;
            string hashMethod = null;

            List<String> ignoreList = new List<string>();
            List<String> dontIgnoreList = new List<string>();

            while (argIndex < args.Length)
            {
                string nextArg = args[argIndex++];

                switch (nextArg)
                {
                    case "-all":
                        all = true;
                        break;

                    case "-silent":
                        silent = true;
                        tool.WriteLogDelegate = null;
                        break;

                    case "-detail":
                        detail = true;
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
                        tool.NewHash = true;
                        break;

                    case "-time":
                        time = true;
                        break;
                        
                    case "-name":
                        repositoryName = args[argIndex++];
                        break;

                    case "-description":
                        repositoryDescription = args[argIndex++];
                        break;

                    case "-hashMethod":
                        hashMethod = args[argIndex++];
                        break;

                    case "-backDate":
                        tool.BackDate = true;
                        break;
                        
                    case "-checkMoves":
                        tool.CheckMoves = true;
                        break;

                    case "-ignore":
                        ignoreList.Add(args[argIndex++]);
                        break;

                    case "-dontIgnore":
                        dontIgnoreList.Add(args[argIndex++]);
                        break;

                    case "-manifestFile":
                        manifestFilePath = args[argIndex++];
                        FileInfo fileInfo = new FileInfo(manifestFilePath);
                        tool.RootDirectory = fileInfo.Directory;
                        break;

                    case "-force":
                        force = true;
                        break;

                    default:
                        WriteLine("Unrecognized parameter \" " + nextArg + "\"");
                        commandArg = "";
                        exitCode = 1;
                        break;
                }
            }

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
                                Write("Replace existing manifest file? ");
                                String confirmString = Console.ReadLine();

                                doCreate = CheckConfirmString(confirmString);
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
                        }
                        catch (Exception ex)
                        {
                            ReportException(ex);
                            WriteLine("Could not read manifest.");
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
                                WriteLine(tool.MissingFiles.Count.ToString() + " files are missing.");
                                DetailFiles(tool.MissingFiles);
                                different = true;
                            }

                            if (tool.ModifiedFiles.Count > 0)
                            {
                                WriteLine(tool.ModifiedFiles.Count.ToString() + " files are different.");
                                DetailFiles(tool.ModifiedFiles);
                                different = true;
                            }

                            if (tool.NewFiles.Count > 0)
                            {
                                WriteLine(tool.NewFiles.Count.ToString() + " files are new.");                                
                                DetailFiles(tool.NewFiles);

                                if (ignoreNew == false)
                                {
                                    different = true;
                                }
                            }

                            if (tool.DateModifiedFiles.Count > 0)
                            {
                                WriteLine(tool.DateModifiedFiles.Count.ToString() + " file dates are modified.");
                                DetailFiles(tool.DateModifiedFiles);

                                if (ignoreDate == false)
                                {
                                    different = true;
                                }
                            }

                            if (tool.ErrorFiles.Count > 0)
                            {
                                WriteLine(tool.ErrorFiles.Count.ToString() + " files have errors.");
                                DetailFiles(tool.ErrorFiles);
                                different = true;
                            }

                            if (tool.MovedFiles.Count > 0)
                            {
                                WriteLine(tool.MovedFiles.Count.ToString() + " files were moved.");
                                DetailFiles(tool.MovedFiles);
                                different = true;
                            }

                            if (tool.NewlyIgnoredFiles.Count > 0)
                            {
                                WriteLine(tool.NewlyIgnoredFiles.Count.ToString() + " files are newly ignored.");
                                DetailFiles(tool.NewlyIgnoredFiles);
                            }

                            if (tool.IgnoredFiles.Count > 1)
                            {
                                WriteLine(tool.IgnoredFiles.Count.ToString() + " files were ignored.");

                                if (all == true)
                                {
                                    DetailFiles(tool.IgnoredFiles);
                                }
                            }


                            WriteLine(tool.FileCheckedCount.ToString() + " files were checked.");

                            if (commandArg == "validate")
                            {
                                if (different)
                                {
                                    WriteLine("Problems found.");
                                    exitCode = 1;
                                }
                                else
                                {
                                    WriteLine("No problems.");
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
                        ReportException(ex);
                        WriteLine("Could not read manifest.");
                    }

                    if (tool.Manifest != null)
                    {
                        bool doClear = true;

                        if (force == false)
                        {
                            Write("Clear " +
                                tool.Manifest.CountFiles().ToString() +
                                " files from the manifest? ");

                            String confirmString = Console.ReadLine();

                            doClear = CheckConfirmString(confirmString);
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
                        ReportException(ex);
                        WriteLine("Could not read manifest.");
                    }

                    if (tool.Manifest == null)
                    {
                        exitCode = 1;
                    }
                    else
                    {
                        if (tool.Manifest.Name != null)
                        {
                            WriteLine("Name:                  " + tool.Manifest.Name);
                        }


                        WriteLine("GUID:                  " + tool.Manifest.Guid.ToString());

                        if (tool.Manifest.DefaultHashMethod != null)
                        {
                            WriteLine("Default hash method:   " + tool.Manifest.DefaultHashMethod);
                        }

                        WriteLine("Date of creation:      " +
					        (tool.Manifest.InceptionDateUtc.ToLocalTime()).ToString());

                        WriteLine("Date of last update:   " +
                            (tool.Manifest.LastUpdateDateUtc.ToLocalTime()).ToString());
					
                        NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
                        nfi.NumberDecimalDigits = 0;


                        WriteLine("Total number of files: " + tool.Manifest.CountFiles().ToString("N", nfi));
                        WriteLine("Total number of bytes: " + tool.Manifest.CountBytes().ToString("N", nfi));
                        WriteLine("Ignoring these file patterns:");
                        if (tool.Manifest.IgnoreList.Count > 0)
                        {
                            foreach (String nextIgnore in tool.Manifest.IgnoreList)
                            {
                                WriteLine("   " + nextIgnore);
                            }
                        }
                    }

                    if (tool.Manifest.Description != null)
                    {
                        WriteLine();
                        WriteLine("Description: ");
                        WriteLine(tool.Manifest.Description);
                    }

                    break;

                case "help":
                    Write(Properties.Resources.RepositoryToolHelp);
                    break;

                case "":
                    break;
                    
                default:
                    WriteLine("Unrecognized command \"" + commandArg + "\"");
                    exitCode = 1;
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
                        }

                        if (repositoryDescription != null)
                        {
                            tool.Manifest.Description = repositoryDescription;
                        }

                        if (hashMethod != null)
                        {
                            tool.Manifest.DefaultHashMethod = hashMethod;
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
                        }

                        if (tool.Manifest != null)
                        {
                            try
                            {
                                tool.Manifest.WriteManifestFile(manifestFilePath);
                            }
                            catch (Exception ex)
                            {
                                ReportException(ex);
                                WriteLine("Could not write manifest.");
                                exitCode = 1;
                            }
                        }
                    }
                    break;

                case "groom":
                    
                    if (tool.NewFilesForGroom.Count > 0)
                    {
                        bool doGroom = true;

                        if (force == false)
                        {
                            Write("Delete " +
                                tool.NewFilesForGroom.Count.ToString() +
                                " new files? ");

                            String confirmString = Console.ReadLine();

                            doGroom = CheckConfirmString(confirmString);
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
                            Write("Delete " +
                                tool.IgnoredFilesForGroom.Count.ToString() +
                                " ignored files? ");

                            String confirmString = Console.ReadLine();

                            doGroomAll = CheckConfirmString(confirmString);
                        }

                        if (doGroomAll == true)
                        {
                            foreach (FileInfo delFile in tool.IgnoredFilesForGroom)
                            {
                                delFile.Delete();
                            }
                        }
                    }

                    break;
            }

            if (time)
            {
                WriteLine("Duration: " + (DateTime.Now - startTime).ToString());
            }

            Environment.Exit(exitCode);
        }



        // Helper methods
        static bool silent = false;
        static bool detail = false;

        static void Write(String message)
        {
            if (silent == false)
            {
                Console.Write(message);
            }
        }

        static void WriteLine(String message)
        {
            Write(message + "\r\n");
        }

        static void WriteLine()
        {
            WriteLine("");
        }

        static void ReportException(Exception ex)
        {
            WriteLine(ex.GetType().ToString() + ": " + ex.Message);
        }

        static bool CheckConfirmString(String confirmString)
        {
            if (confirmString.StartsWith("y") ||
                confirmString.StartsWith("Y"))
            {
                return true;
            }

            return false;
        }

        static void DetailFiles(List<ManifestFileInfo> files)
        {
            if (detail)
            {
                foreach (ManifestFileInfo nextManFileInfo in files)
                {
                    WriteLine("   " + RepositoryTool.MakeStandardPathString(nextManFileInfo));
                }

                WriteLine();
            }
        }

        static void DetailFiles(Dictionary<ManifestFileInfo, ManifestFileInfo> files)
        {
            if (detail)
            {
                foreach (ManifestFileInfo missingFile in files.Keys)
                {
                    ManifestFileInfo newFile = files[missingFile];

                    WriteLine(
                        "   " +
                        RepositoryTool.MakeStandardPathString(missingFile) +
                        " -> " +
                        RepositoryTool.MakeStandardPathString(newFile));
                }

                WriteLine();
            }
        }
    }
}
