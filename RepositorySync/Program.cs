using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RepositoryManifest;
using System.Resources;


namespace RepositorySync
{
    class Program
    {
        static void Main(string[] args)
        {
            int exitCode = 0;
            Utilities.Console console = new Utilities.Console();

            DateTime startTime = DateTime.Now;

            int argIndex = 0;

            // Give the user some help if they need it
            string commandArg = "help";

            LocalRepositoryProxy sourceRep = null;
            LocalRepositoryProxy destRep = null;

            if (argIndex < args.Count())
            {
                commandArg = args[argIndex++];
            }

            // TODO: Handle errors
            if (argIndex < args.Count())
            {
                sourceRep = new LocalRepositoryProxy(
                    new System.IO.DirectoryInfo(
                        args[argIndex++]));
            }

            if (argIndex < args.Count())
            {
                destRep = new LocalRepositoryProxy(
                    new System.IO.DirectoryInfo(
                        args[argIndex++]));
            }

            RepositorySync syncTool =
                new RepositorySync(sourceRep, destRep);

            if (sourceRep != null && destRep != null)
            {
                syncTool.CompareManifests();
            }

            // Initial screen for valid command
            switch (commandArg)
            {
                case "diff":
                case "update":
                case "sync":
                case "mirror":
                case "repair":
                case "help":
                    break;

                default:
                    console.WriteLine("Unrecognized command \"" + commandArg + "\"");
                    exitCode = 1;
                    Environment.Exit(exitCode);
                    break;
            }

            while (argIndex < args.Length)
            {
                string nextArg = args[argIndex++];

                switch (nextArg)
                {
                    case "-silent":
                        console.Silent = true;
                        break;

                    case "-detail":
                        console.Detail = true;
                        break;
                }
            }

            switch (commandArg)
            {
                case "diff":

                    bool different = false;

                    if (syncTool.SourceOnlyFiles.Count != 0)
                    {
                        console.WriteLine(
                            syncTool.SourceOnlyFiles.Count.ToString() +
                            " files are only in source.");

                        console.DetailFiles(syncTool.SourceOnlyFiles);
                        different = true;
                    }

                    if (syncTool.DestOnlyFiles.Count != 0)
                    {
                        console.WriteLine(
                            syncTool.DestOnlyFiles.Count.ToString() +
                            " files are only in destination.");

                        console.DetailFiles(syncTool.DestOnlyFiles);
                        different = true;
                    }

                    if (syncTool.ChangedFiles.Count != 0)
                    {
                        console.WriteLine(
                            syncTool.ChangedFiles.Count.ToString() +
                            " files are different.");

                        console.DetailFiles(syncTool.ChangedFiles);
                        different = true;
                    }

                    if (syncTool.DateChangedFiles.Count != 0)
                    {
                        console.WriteLine(
                            syncTool.DateChangedFiles.Count.ToString() +
                            " files have different dates.");

                        console.DetailFiles(syncTool.DateChangedFiles);
                        different = true;
                    }

                    if (syncTool.MovedFiles.Count > 0)
                    {
                        console.WriteLine(syncTool.MovedFiles.Count.ToString() + " files were moved.");
                        console.DetailFiles(syncTool.MovedFileOrder, syncTool.MovedFiles);
                        different = true;
                    }

                    if (different == false)
                    {
                        console.WriteLine("No differences.");
                    }

                    break;

                case "update":
                case "sync":
                case "mirror":
                case "repair":
                    break;

                case "help":
                    console.Write(Properties.Resources.RepositorySyncHelp);
                    break;
            }

            Environment.Exit(exitCode);
        }
    }
}
