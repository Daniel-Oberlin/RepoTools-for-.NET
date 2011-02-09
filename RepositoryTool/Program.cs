using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using RepositoryManifest;


namespace RepositoryTool
{
    class Program
    {
        static void Main(string[] args)
        {
            int exitCode = 0;
            DateTime startTime = DateTime.Now;

            int argIndex = 0;
            string commandArg = args[argIndex++];

            RepositoryTool tool = new RepositoryTool();

            tool.WriteLogDelegate =
                delegate(String message)
                {
                    Write(message);
                };

            tool.RootDirectory =
                new DirectoryInfo(Directory.GetCurrentDirectory());

            bool ignoreDate = false;
            bool ignoreNew = false;
            bool time = false;

            string repositoryName = null;
            string repositoryDescription = null;
            string hashMethod = null;

            while (argIndex < args.Count())
            {
                string nextArg = args[argIndex++];

                switch (nextArg)
                {
                    case "-silent":
                        silent = true;
                        tool.WriteLogDelegate = null;
                        break;

                    case "-detailReport":
                        detailReport = true;
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

                    case "-force":
                        tool.Force = true;
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

                    //case "-ignore":
                    //    break;

                    //case "-dontIgnore":
                    //    break;

                    default:
                        WriteLine("Unrecognized parameter \" " + nextArg + "\"");
                        commandArg = "";
                        exitCode = 1;
                        break;
                }
            }

            String manifestFilePath =
                tool.RootDirectory.FullName +
                RepositoryTool.PathDelimeterString +
                RepositoryTool.ManifestFileName;

            switch (commandArg)
            {
                case "create":
                    {
                        bool doCreate = false;

                        if (File.Exists(manifestFilePath))
                        {
                            Write("Replace existing manifest file? ");
                            String confirmString = Console.ReadLine();

                            if (confirmString.StartsWith("y") ||
                                confirmString.StartsWith("Y"))
                            {
                                doCreate = true;
                            }
                        }

                        if (doCreate == true)
                        {
                        tool.Manifest = new Manifest();

                        tool.Manifest.DefaultHashMethod =
                            RepositoryTool.NewHashType;
                        }

                        break;
                    }

                case "validate":
                case "status":
                case "update":
                case "edit":
                case "clean":
                    {
                        if (commandArg == "validate")
                        {
                            tool.Force = true;
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

                        WriteLine("Date of creation:      " + tool.Manifest.InceptionDateUtc.ToString());

                        if (tool.Manifest.LastUpdateDateUtc != null)
                        {
                            WriteLine("Date of last update:   " + tool.Manifest.LastUpdateDateUtc.ToString());
                        }
                        else
                        {
                            WriteLine("Date of last update:   Never.");
                        }

                        NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;
                        nfi.NumberDecimalDigits = 0;


                        WriteLine("Total number of files: " + tool.Manifest.CountFiles().ToString("N", nfi));
                        WriteLine("Total number of bytes: " + tool.Manifest.CountBytes().ToString("N", nfi));
                    }

                    if (tool.Manifest.Description != null)
                    {
                        WriteLine();
                        WriteLine("Description: ");
                        WriteLine(tool.Manifest.Description);
                        WriteLine();
                    }

                    break;

                case "":
                    break;

                default:
                    WriteLine("Unrecognized command \" " + commandArg + "\"");
                    exitCode = 1;
                    break;
            }

            switch (commandArg)
            {
                case "create":
                case "update":
                case "edit":

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

                case "clean":
                    
                    if (tool.NewFilesForClean.Count > 0)
                    {
                        Write("Delete " +
                            tool.NewFilesForClean.Count.ToString() +
                            " new files? ");

                        String confirmString = Console.ReadLine();

                        if (confirmString.StartsWith("y") ||
                            confirmString.StartsWith("Y"))
                        {
                            foreach (FileInfo delFile in tool.NewFilesForClean)
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
        static bool detailReport = false;

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

        static void DetailFiles(List<ManifestFileInfo> files)
        {
            if (detailReport)
            {
                foreach (ManifestFileInfo nextManFileInfo in files)
                {
                    WriteLine("   " + RepositoryTool.MakePathString(nextManFileInfo));
                }

                WriteLine();
            }
        }
    }
}
