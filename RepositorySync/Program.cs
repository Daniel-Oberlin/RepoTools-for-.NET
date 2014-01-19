using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RepositoryManifest;
using System.Resources;
using Utilities;


namespace RepositorySync
{
    class Program
    {
        static void Main(string[] args)
        {
            int exitCode = 0;
            IRepositoryProxy sourceRep = null;
            IRepositoryProxy destRep = null;

            do
            {
                DateTime startTime = DateTime.Now;

                ManifestConsole console = new ManifestConsole();

                int argIndex = 0;
                string commandArg = "help";

                if (argIndex < args.Count())
                {
                    commandArg = args[argIndex++];
                }

                switch (commandArg)
                {
                    case "help":
                    case "diff":
                    case "update":
                    case "sync":
                    case "mirror":
                    case "seed":
                        break;

                    default:
                        console.WriteLine("Unrecognized command \"" + commandArg + "\"");
                        Environment.Exit(1);
                        break;
                }

                if (commandArg == "help")
                {
                    console.Write(Properties.Resources.RepositorySyncHelp);
                    Environment.Exit(1);
                    break;
                }

                bool repositoriesOnCommandLine = args.Length >= 3;

                string sourceRepString = "";
                string destRepString = "";
                if (repositoriesOnCommandLine == true)
                {
                    // Skip repositories for now and process options
                    sourceRepString = args[argIndex++];
                    destRepString = args[argIndex++];
                }
                else
                {
                    console.WriteLine("Source or destination repositories were not supplied.");
                    exitCode = 1;
                    break;
                }


                // Process options

                bool time = false;

                String sourceKeyString = null;
                String destKeyString = null;

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

                        case "-noTimeout":
                            RemoteRepositoryProxy.RequestReadWriteTimeout =
                                System.Threading.Timeout.Infinite;

                            RemoteRepositoryProxy.RequestTimeout =
                                System.Threading.Timeout.Infinite;
                            break;

                        case "-time":
                            time = true;
                            break;

                        case "-sourceKey":
                            if (ArgUtilities.HasAnotherArgument(
                                args, argIndex, console) == true)
                            {
                                sourceKeyString = args[argIndex++];
                            }
                            break;

                        case "-destKey":
                            if (ArgUtilities.HasAnotherArgument(
                                args, argIndex, console) == true)
                            {
                                destKeyString = args[argIndex++];
                            }
                            break;

                        case "-cryptSource":
                            console.Write("Enter source key: ");
                            sourceKeyString = console.EnterPassword();
                            break;

                        case "-cryptDest":
                            console.Write("Enter dest key: ");
                            destKeyString = console.EnterPassword();
                            break;

                        default:
                            console.WriteLine("Unrecognized parameter \" " + nextArg + "\"");
                            Environment.Exit(1);
                            break;
                    }
                }


                // Resolve repositories

                bool sourceReadOnly = true;
                bool destReadOnly = false;
                if (commandArg == "diff")
                {
                    destReadOnly = true;
                }
                else if (commandArg == "sync")
                {
                    sourceReadOnly = false;
                }

                bool seedCommand = commandArg == "seed";

                bool remoteSource =
                    RemoteRepositoryProxy.IsRemoteRepositoryString(
                        sourceRepString);

                bool remoteDest =
                    RemoteRepositoryProxy.IsRemoteRepositoryString(
                        destRepString);

                if (remoteSource == false)
                {
                    try
                    {
                        sourceRep = new LocalRepositoryProxy(
                            new System.IO.DirectoryInfo(sourceRepString),
                            sourceReadOnly);

                        if (sourceKeyString != null)
                        {
                            CryptRepositoryProxy cryptProxy =
                                new CryptRepositoryProxy(
                                    sourceRep,
                                    sourceKeyString,
                                    sourceReadOnly);

                            ReportCrypt(cryptProxy, "Source", console);
                            sourceRep = cryptProxy;
                        }
                    }
                    catch (Exception e)
                    {
                        console.WriteLine("Exception: " + e.Message);
                        exitCode = 1;
                        break;
                    }
                }

                if (remoteDest == false && seedCommand == false)
                {
                    try
                    {
                        destRep = new LocalRepositoryProxy(
                            new System.IO.DirectoryInfo(destRepString),
                            destReadOnly);

                        if (destKeyString != null)
                        {
                            CryptRepositoryProxy cryptProxy =
                                new CryptRepositoryProxy(
                                    destRep,
                                    destKeyString,
                                    destReadOnly);

                            ReportCrypt(cryptProxy, "Dest", console);
                            destRep = cryptProxy;
                        }
                    }
                    catch (Exception e)
                    {
                        console.WriteLine("Exception: " + e.Message);
                        exitCode = 1;
                        break;
                    }
                }

                if (remoteSource == true)
                {
                    try
                    {
                        sourceRep = new RemoteRepositoryProxy(
                            sourceRepString,
                            destRep.Manifest);

                        if (sourceKeyString != null)
                        {
                            CryptRepositoryProxy cryptProxy =
                                new CryptRepositoryProxy(
                                    sourceRep,
                                    sourceKeyString,
                                    sourceReadOnly);

                            ReportCrypt(cryptProxy, "Source", console);
                            sourceRep = cryptProxy;
                        }
                    }
                    catch (Exception e)
                    {
                        console.WriteLine("Exception: " + e.Message);
                        exitCode = 1;
                        break;
                    }
                }

                if (remoteDest == true && seedCommand == false)
                {
                    try
                    {
                        destRep = new RemoteRepositoryProxy(
                            destRepString,
                            sourceRep.Manifest);

                        if (destKeyString != null)
                        {
                            CryptRepositoryProxy cryptProxy =
                                new CryptRepositoryProxy(
                                    destRep,
                                    destKeyString,
                                    destReadOnly);

                            ReportCrypt(cryptProxy, "Dest", console);
                            destRep = cryptProxy;
                        }
                    }
                    catch (Exception e)
                    {
                        console.WriteLine("Exception: " + e.Message);
                        exitCode = 1;
                        break;
                    }
                }

                if (sourceRep == null && destRep == null)
                {
                    console.WriteLine("Could not resolve a source or destination repository.");
                    exitCode = 1;
                    break;
                }
                else if (sourceRep == null)
                {
                    console.WriteLine("Could not resolve a source repository.");
                    exitCode = 1;
                    break;
                }
                else if (destRep == null && seedCommand == false)
                {
                    console.WriteLine("Could not resolve a destination repository.");
                    exitCode = 1;
                    break;
                }


                switch (commandArg)
                {
                    case "update":
                    case "sync":
                    case "mirror":
                        if (sourceRep != null &&
                            destRep != null &&
                            sourceRep.Manifest.Guid.Equals(
                            destRep.Manifest.Guid) == false)
                        {
                            console.WriteLine("Source and destination GUIDs are different.");
                            exitCode = 1;
                        }
                        break;
                }

                if (exitCode == 1)
                {
                    break;
                }

                RepositorySync syncTool = null;
                if (seedCommand == false)
                {
                    syncTool = new RepositorySync(sourceRep, destRep);

                    syncTool.WriteLogDelegate =
                        delegate(String message)
                        {
                            console.Write(message);
                        };

                    syncTool.CompareManifests();
                }


                // Process command

                if (time)
                {
                    console.WriteLine("Started: " + startTime.ToString());
                }

                switch (commandArg)
                {
                    case "diff":
                        ShowDiff(syncTool, console);
                        break;

                    case "update":
                        syncTool.DoUpdate();
                        break;

                    case "sync":
                        syncTool.BothWays = true;
                        syncTool.DoUpdate();
                        break;

                    case "mirror":
                        syncTool.Mirror = true;
                        syncTool.DoUpdate();
                        break;

                    case "seed":
                        if (destKeyString == null)
                        {
                            LocalRepositoryProxy.SeedLocalRepository(
                                sourceRep.Manifest,
                                destRepString,
                                console);
                        }
                        else
                        {
                            CryptRepositoryProxy.SeedLocalRepository(
                                sourceRep.Manifest,
                                destKeyString,
                                destRepString,
                                console);
                        }
                        break;
                }

                if (time)
                {
                    DateTime finishedTime = DateTime.Now;
                    console.WriteLine("Finished: " + finishedTime.ToString());
                    console.WriteLine("Duration: " + (finishedTime - startTime).ToString());
                }


            } while (false);


            if (sourceRep != null)
            {
                sourceRep.CleanupBeforeExit();
                sourceRep = null;
            }

            if (destRep != null)
            {
                destRep.CleanupBeforeExit();
                destRep = null;
            }

            Environment.Exit(exitCode);
        }



        static void ShowDiff(
            RepositorySync syncTool,
            ManifestConsole console)
        {
            bool different = false;

            Manifest sourceMan = syncTool.SourceRep.Manifest;
            Manifest destMan = syncTool.DestRep.Manifest;

            if (sourceMan.Name != destMan.Name)
            {
                console.WriteLine("Manifest names are different.");
                if (console.Detail)
                {
                    console.WriteLine(
                        "   Source name: " +
                        sourceMan.Name);

                    console.WriteLine(
                        "     Dest name: " +
                        destMan.Name);

                    console.WriteLine();
                }
                different = true;
            }

            if (sourceMan.Guid.Equals(destMan.Guid) == false)
            {
                console.WriteLine("Manifest GUIDs are different.");
                if (console.Detail)
                {
                    console.WriteLine(
                        "   Source GUID: " +
                        sourceMan.Guid.ToString());

                    console.WriteLine(
                        "     Dest GUID: " +
                        destMan.Guid.ToString());

                    console.WriteLine();
                }
                different = true;
            }

            if (sourceMan.DefaultHashMethod != destMan.DefaultHashMethod)
            {
                console.WriteLine("Manifest default hash methods are different.");
                if (console.Detail)
                {
                    console.WriteLine(
                        "   Source method: " +
                        sourceMan.DefaultHashMethod);

                    console.WriteLine(
                        "     Dest method: " +
                        destMan.DefaultHashMethod);

                    console.WriteLine();
                }
                different = true;
            }

            if (sourceMan.Description != destMan.Description)
            {
                console.WriteLine("Manifest descriptions are different.");
                if (console.Detail)
                {
                    console.WriteLine("Source description:");
                    console.WriteLine(sourceMan.Description);
                    console.WriteLine();

                    console.WriteLine("Dest description:");
                    console.WriteLine(destMan.Description);
                    console.WriteLine();
                }
                different = true;
            }

            if (sourceMan.InceptionDateUtc != destMan.InceptionDateUtc)
            {
                console.WriteLine("Manifest creation dates are different.");
                if (console.Detail)
                {
                    console.WriteLine(
                        "   Source date: " +
                        sourceMan.InceptionDateUtc.ToLocalTime().ToString());

                    console.WriteLine(
                        "     Dest date: " +
                        destMan.InceptionDateUtc.ToLocalTime().ToString());

                    console.WriteLine();
                }
                different = true;
            }

            if (sourceMan.ManifestInfoLastModifiedUtc !=
                destMan.ManifestInfoLastModifiedUtc)
            {
                console.WriteLine("Last change to manifest information dates are different.");
                if (console.Detail)
                {
                    console.WriteLine(
                        "   Source date: " +
                        sourceMan.ManifestInfoLastModifiedUtc.ToLocalTime().ToString());

                    console.WriteLine(
                        "     Dest date: " +
                        destMan.ManifestInfoLastModifiedUtc.ToLocalTime().ToString());

                    console.WriteLine();
                }
                different = true;
            }

            bool ignoreListDifferent = false;
            if (sourceMan.IgnoreList.Count != destMan.IgnoreList.Count)
            {
                ignoreListDifferent = true;
            }
            else
            {
                for (int i = 0; i < sourceMan.IgnoreList.Count; i++)
                {
                    if (sourceMan.IgnoreList[i] != destMan.IgnoreList[i])
                    {
                        ignoreListDifferent = true;
                        break;
                    }
                }
            }

            if (ignoreListDifferent)
            {
                console.WriteLine("Manifest ignore lists are different.");
                if (console.Detail)
                {
                    console.WriteLine("Source list:");
                    foreach (string ignoreString in sourceMan.IgnoreList)
                    {
                        console.WriteLine("   " + ignoreString);
                    }
                    console.WriteLine();

                    console.WriteLine("Dest list: ");
                    foreach (string ignoreString in destMan.IgnoreList)
                    {
                        console.WriteLine("   " + ignoreString);
                    }
                    console.WriteLine();
                }
                different = true;
            }

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
                    " files have changed content.");

                console.DetailFiles(syncTool.ChangedFiles.Keys);
                different = true;
            }

            if (syncTool.LastModifiedDateFiles.Count != 0)
            {
                console.WriteLine(
                    syncTool.LastModifiedDateFiles.Count.ToString() +
                    " files have last-modified dates which are different.");

                console.DetailFiles(syncTool.LastModifiedDateFiles.Keys);
                different = true;
            }

            if (syncTool.RegisteredDateFiles.Count != 0)
            {
                console.WriteLine(
                    syncTool.RegisteredDateFiles.Count.ToString() +
                    " files have manifest creation dates which are different.");

                console.DetailFiles(syncTool.RegisteredDateFiles.Keys);
                different = true;
            }

            if (syncTool.MovedFiles.Count > 0)
            {
                console.WriteLine(syncTool.MovedFiles.Count.ToString() + " files were moved.");
                console.DetailFiles(syncTool.MovedFileOrder, syncTool.MovedFiles, true);
                different = true;
            }

            if (different == false)
            {
                console.WriteLine("No differences.");
            }
        }

        static void ReportCrypt(
            CryptRepositoryProxy rep,
            String name,
            Utilities.Console console)
        {
            if (rep.UnresolvedOuterFiles.Count > 0)
            {
                console.WriteLine(name +
                    " crypt has these unresolved files (ignoring):");

                foreach (ManifestFileInfo nextFile in
                    rep.UnresolvedOuterFiles)
                {
                    console.WriteLine("     " +
                        Manifest.MakeStandardPathString(nextFile));
                }

                console.WriteLine();
            }

            if (rep.OrphanedInnerFiles.Count > 0)
            {
                console.WriteLine(
                    name +
                    " crypt has " +
                    rep.OrphanedInnerFiles.Count +
                    " orphaned data files.");

                console.WriteLine();
            }
        }
    }
}
