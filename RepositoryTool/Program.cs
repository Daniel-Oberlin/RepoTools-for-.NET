using System;
using System.Collections.Generic;
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
            //bool backDate = false;

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

                    //case "-backDate":
                    //    backDate = true;
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
                        tool.Manifest = new Manifest();
                        break;
                    }

                case "validate":
                case "status":
                case "update":
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
                        else
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

                case "clean":
                    break;

                case "info":
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
                    break;
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

                WriteLine("");
            }
        }
    }
}
