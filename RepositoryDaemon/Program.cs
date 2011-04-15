using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Resources;


namespace RepositoryDaemon
{
    class Program
    {
        public delegate void TaskDelegate();

        static void Main(string[] args)
        {
            RepositoryDaemon daemon = new RepositoryDaemon();

            int exitCode = 0;
            Utilities.Console console = new Utilities.Console();

            int argIndex = 0;
            string commandArg = "help";
            string commandTarget = "";

            if (argIndex < args.Count())
            {
                commandArg = args[argIndex++];
            }

            // Initial screen for valid command
            switch (commandArg)
            {
                case "help":
                case "status":
                case "start":
                    break;

                case "add":
                case "remove":
                    commandTarget = args[argIndex++];
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
                }
            }

            switch (commandArg)
            {
                case "help":
                    console.Write(Properties.Resources.RepositoryDaemonHelp);
                    break;

                case "add":
                    // TODO: Allow for separate manifest and repository paths
                    daemon.AddRepository(commandTarget);
                    daemon.SaveSettings();
                    break;

                case "remove":
                    // TODO: Allow for separate manifest and repository paths
                    String manifestPath =
                        System.IO.Path.Combine(
                            commandTarget,
                            RepositoryManifest.Manifest.DefaultManifestFileName);

                    daemon.RemoveRepository(manifestPath);
                    daemon.SaveSettings();
                    break;

                case "status":
                    foreach (RepositoryInfo nextInfo in daemon.Settings.GuidToRepository.Values)
                    {
                        if (nextInfo.Name != null)
                        {
                            console.WriteLine("Name:            " + nextInfo.Name);
                        }
                        console.WriteLine("GUID:            " + nextInfo.Guid);
                        console.WriteLine("Repository Path: " + nextInfo.RepositoryPath);
                        console.WriteLine("Manifest Path:   " + nextInfo.ManifestPath);
                        console.WriteLine();
                    }
                    break;

                case "start":

                    
                    TaskDelegate startDelegate = daemon.Start;
                    startDelegate.BeginInvoke(null, null);

                    console.WriteLine("Press enter to terminate...");
                    Console.ReadLine();
                    
                    /*
                    daemon.Start();
                    */

                    break;
            }

            Environment.Exit(exitCode);
        }
    }
}
