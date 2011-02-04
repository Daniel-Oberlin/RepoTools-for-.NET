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

            Dictionary<String, Mode> modes = new Dictionary<string, Mode>();
            modes.Add("create", Mode.Create);
            modes.Add("validate", Mode.Validate);
            modes.Add("status", Mode.Status);
            modes.Add("update", Mode.Update);

            int argIndex = 0;
            string commandArg = args[argIndex++];

            RepositoryTool tool = new RepositoryTool();

            tool.WriteLogDelegate =
                delegate(String message)
                {
                    Write(message);
                };

            tool.RootDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

            bool ignoreDate = false;
            bool ignoreNew = false;
            //bool backDate = false;
            //bool verbose = false;

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

                    //case "-backDate":
                    //    backDate = true;
                    //    break;

                    //case "-verbose":
                    //    verbose = true;
                    //    break;

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
                case "validate":
                case "status":
                    {
                        Mode mode = modes[commandArg];

                        String manifestFilePath =
                            tool.RootDirectory.FullName +
                            RepositoryTool.PathDelimeterString +
                            RepositoryTool.ManifestFileName;

                        Manifest manifest = null;

                        if (mode == Mode.Create)
                        {
                            manifest = new Manifest();
                        }
                        else
                        {
                            try
                            {
                                manifest = Manifest.ReadManifestFile(manifestFilePath);
                            }
                            catch (Exception ex)
                            {
                                ReportException(ex);
                                WriteLine("Could not read manifest.");
                            }
                        }

                        if (manifest == null)
                        {
                            exitCode = 1;
                        }
                        else
                        {
                            tool.Execute(manifest, mode);

                            if (tool.MissingFiles.Count > 0)
                            {
                                WriteLine(tool.MissingFiles.Count.ToString() + " files are missing.");
                                DetailFiles(tool.MissingFiles);
                                exitCode = 1;
                            }

                            if (tool.ModifiedFiles.Count > 0)
                            {
                                WriteLine(tool.ModifiedFiles.Count.ToString() + " files are different.");
                                DetailFiles(tool.ModifiedFiles);
                                exitCode = 1;
                            }

                            if (tool.NewFiles.Count > 0)
                            {
                                if (mode == Mode.Create || mode == Mode.Update)
                                {
                                    WriteLine(tool.NewFiles.Count.ToString() + " files were added.");
                                }
                                else
                                {
                                    WriteLine(tool.NewFiles.Count.ToString() + " files are new.");
                                }
                                
                                DetailFiles(tool.NewFiles, tool);

                                if (ignoreNew == false)
                                {
                                    exitCode = 1;
                                }
                            }

                            if (tool.DateModifiedFiles.Count > 0)
                            {
                                WriteLine(tool.DateModifiedFiles.Count.ToString() + " file dates are modified.");
                                DetailFiles(tool.DateModifiedFiles);

                                if (ignoreDate == false)
                                {
                                    exitCode = 1;
                                }
                            }

                            if (tool.ErrorFiles.Count > 0)
                            {
                                WriteLine(tool.ErrorFiles.Count.ToString() + " files have errors.");
                                DetailFiles(tool.ErrorFiles);

                                if (ignoreDate == false)
                                {
                                    exitCode = 1;
                                }
                            }

                            if (mode != Mode.Create)
                            {
                                WriteLine(tool.FileCheckedCount.ToString() + " files were checked.");
                            }

                            if (mode == Mode.Validate)
                            {
                                if (exitCode == 0)
                                {
                                    WriteLine("No problems.");
                                }
                                else
                                {
                                    WriteLine("Problems found.");
                                }
                            }

                            if (mode == Mode.Create || mode == Mode.Update)
                            {
                                try
                                {
                                    manifest.WriteManifestFile(manifestFilePath);
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
                    }


                case "update":
                    break;

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

        static void DetailFiles(List<FileInfo> files, RepositoryTool tool)
        {
            if (detailReport)
            {
                foreach (FileInfo nextFileInfo in files)
                {
                    WriteLine("   " + tool.MakePathString(nextFileInfo));
                }

                WriteLine("");
            }
        }
    }
}
