using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Resources;


namespace RepositoryServer
{
    class Program
    {
        public delegate void TaskDelegate();

        static void Main(string[] args)
        {
            RepositoryServer server = new RepositoryServer();

            int exitCode = 0;
            Utilities.Console console = new Utilities.Console();

            int argIndex = 0;
            string commandArg = "help";
            string commandTarget = "";

            bool grantAdmin = false;
            bool revokeAdmin = false;
            
            List<string> addUserHosts = new List<string>();
            List<string> remUserHosts = new List<string>();

            Dictionary<string, UserPrivilige> userPrivs =
                new Dictionary<string, UserPrivilige>();

            if (argIndex < args.Count())
            {
                commandArg = args[argIndex++];
            }

            // Initial screen for valid command
            switch (commandArg)
            {
                case "help":
                case "info":
                case "start":
                    break;

                case "repo":
                case "addRepo":
                case "remRepo":
                case "user":
                case "addUser":
                case "remUser":
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

                    case "-grantAdmin":
                        grantAdmin = true;
                        break;

                    case "-revokeAdmin":
                        revokeAdmin = true;
                        break;

                    case "-addUserHost":
                        addUserHosts.Add(args[argIndex++]);
                        break;

                    case "-remUserHost":
                        remUserHosts.Add(args[argIndex++]);
                        break;

                    case "-userPriv":
                        string userName = args[argIndex++];
                        UserPrivilige userPriv = (UserPrivilige)
                            Enum.Parse(typeof(UserPrivilige), args[argIndex++], true);
                        userPrivs.Add(userName, userPriv);
                        break;
                }
            }

            User user = null;
            RepositoryInfo repo = null;

            switch (commandArg)
            {
                case "help":
                    console.Write(Properties.Resources.RepositoryServerHelp);
                    break;

                case "repo":
                    repo = server.Settings.GetRepositoryFromName(commandArg);
                    break;

                case "addRepo":
                    // TODO: Allow for separate manifest and repository paths
                    repo = server.AddRepository(commandTarget);
                    break;

                case "remRepo":
                    // TODO: Allow for separate manifest and repository paths
                    // Here we are using a trick that a standard file path can be
                    // interpreted correctly as the latter part of a native path in
                    // MS-DOS.
                    String manifestPath =
                        System.IO.Path.Combine(
                            commandTarget,
                            RepositoryManifest.Manifest.DefaultManifestStandardFilePath);
                    server.RemoveRepository(manifestPath);
                    break;

                case "user":
                    user = server.Settings.Users[commandArg];
                    break;

                case "addUser":
                    user = server.AddUser(commandArg);
                    if (user == null)
                    {
                        Console.WriteLine("User " + commandArg + " already exists.");
                        exitCode = 1;
                    }
                    break;

                case "remUser":
                    user = server.RemoveUser(commandArg);
                    if (user == null)
                    {
                        Console.WriteLine("User " + commandArg + " does not exist.");
                        exitCode = 1;
                    }
                    break;

                case "info":
                    foreach (RepositoryInfo nextInfo in server.Settings.GetRepositories())
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
                    TaskDelegate startDelegate = server.Start;
                    startDelegate.BeginInvoke(null, null);

                    console.WriteLine("Press enter to terminate...");
                    Console.ReadLine();
                    
                    /*
                    server.Start();
                    */

                    break;
            }

            // Handle options
            switch (commandArg)
            {
                case "user":
                case "addUser":

                    if (grantAdmin)
                    {
                        user.IsAdministrator = true;
                    }

                    if (revokeAdmin)
                    {
                        user.IsAdministrator = false;
                    }

                    foreach (string addressString in addUserHosts)
                    {
                        System.Net.IPAddress checkAddr;
                        if (System.Net.IPAddress.TryParse(addressString, out checkAddr))
                        {
                            if (server.Settings.HostToUser.ContainsKey(addressString))
                            {
                                console.WriteLine("IP address already assigned: " + addressString);
                            }
                            else
                            {
                                server.Settings.HostToUser.Add(addressString, user);
                            }
                        }
                        else
                        {
                            console.WriteLine("Invalid IP address: " + addressString);
                        }
                    }

                    foreach (string addressString in remUserHosts)
                    {
                        System.Net.IPAddress checkAddr;
                        if (System.Net.IPAddress.TryParse(addressString, out checkAddr))
                        {
                            if (server.Settings.HostToUser.ContainsKey(addressString) == false)
                            {
                                console.WriteLine("IP address not assigned: " + addressString);
                            }
                            else
                            {
                                server.Settings.HostToUser.Remove(addressString);
                            }
                        }
                        else
                        {
                            console.WriteLine("Invalid IP address: " + addressString);
                        }
                    }

                    break;

                case "repo":
                case "addRepo":

                    foreach (string nextUser in userPrivs.Keys)
                    {
                        // TODO
                    }

                    break;

                default:
                    break;
            }

            Environment.Exit(exitCode);
        }
    }
}
