using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Text;

using HttpServer;
using HttpServer.HttpModules;
using RepositoryManifest;
using RepositorySync;
using Utilities;


namespace RepositoryServer
{
    public class RepositoryServer
    {
        public RepositoryServer()
        {
            LoadSettings();

            GuidToRepository = new Dictionary<Guid, LocalRepositoryState>();
        }

        // DMO:
        // Duplicate checking logic is inconsistent between users and
        // repositories.

        public RepositoryInfo AddRepository(
            String repositoryPath,
            String manifestPath = null)
        {
            String fullRepositoryPath =
                new DirectoryInfo(repositoryPath).FullName;

            String fullManifestPath = null;

            if (manifestPath == null)
            {
                // Here we are using a trick that a standard file path can be
                // interpreted correctly as the latter part of a native path in
                // MS-DOS.
                fullManifestPath = Path.Combine(
                    fullRepositoryPath,
                    Manifest.DefaultManifestStandardFilePath);
            }
            else
            {
                fullManifestPath =
                    new FileInfo(manifestPath).FullName;
            }
               
            Manifest manifest =
                Manifest.ReadManifestFile(fullManifestPath);

            RepositoryInfo repoInfo =
                new RepositoryInfo(
                    manifest.Guid,
                    manifest.Name,
                    fullRepositoryPath,
                    fullManifestPath);

            Settings.AddRepository(repoInfo);

            return repoInfo;
        }

        public void RemoveRepository(String manifestPath)
        {
            Manifest manifest =
                Manifest.ReadManifestFile(manifestPath);

            Settings.RemoveRepository(manifest.Guid);
        }

        public User AddUser(string userName, bool isAdministrator = false)
        {
            return Settings.AddUser(userName);
        }

        public User RemoveUser(string userName)
        {
            if (Settings.Users.ContainsKey(userName) == false)
            {
                return null;
            }

            User remUser = Settings.Users[userName];
            Settings.Users.Remove(userName);

            if (Settings.UserHomePaths.ContainsKey(remUser))
            {
                Settings.UserHomePaths.Remove(remUser);
            }

            Dictionary<String, User> HostToUserClone =
                new Dictionary<string, User>(Settings.HostToUser);

            foreach (string nextHost in HostToUserClone.Keys)
            {
                if (HostToUserClone[nextHost] == remUser)
                {
                    Settings.HostToUser.Remove(nextHost);
                }
            }

            foreach (RepositoryInfo nextRepo in Settings.GetRepositories())
            {
                if (nextRepo.UserPriviligeList.ContainsKey(remUser))
                {
                    nextRepo.UserPriviligeList.Remove(remUser);
                }
            }

            return remUser;
        }

        public void Start()
        {
            LoadRepositories();

            HttpRequest.StreamFactory = HttpRequestStreamFactory;

            // TODO: Specify our address
            HttpServer.HttpListener listener =
                HttpServer.HttpListener.Create(
                    IPAddress.Any,
                    PortNumber);

            listener.RequestReceived += OnRequest;

            listener.Start(5);

            int milliSeconds =
                1000 * Settings.ManifestFlushIntervalSeconds;

            System.Threading.TimerCallback cb =
                new System.Threading.TimerCallback(
                    FlushManifestsCallback);

            System.Threading.Timer flushTimer = new
                System.Threading.Timer(
                    cb,
                    null,
                    milliSeconds,
                    milliSeconds);

            // TODO: Wait for signal
            System.Threading.Thread.Sleep(
                System.Threading.Timeout.Infinite);

            flushTimer.Dispose();
        }

        public ServerSettings Settings { private set; get; }


        // Protected

        protected void OnRequest(
            object source,
            RequestEventArgs args)
        {
            HttpClientContext context =
                (HttpServer.HttpClientContext) source;

            HttpRequest request =
                (HttpRequest) args.Request;


            // DEGBUG OUTPUT
            System.Console.WriteLine(
                "Connection acccepted from " +
                context.RemoteAddress);

            System.Console.WriteLine(request.Method);
            System.Console.WriteLine(request.Uri);

            foreach (String nextKey in request.Headers.Keys)
            {
                System.Console.WriteLine(
                    nextKey + "=" + request.Headers[nextKey]);
            }
            System.Console.WriteLine();


            // Handle methods
            switch (request.Method)
            {
                case "GET":
                    HandleGetRequest(context, request);
                    break;

                case "PUT":
                    HandlePutRequest(context, request);
                    break;

                case "DELETE":
                    HandleDeleteRequest(context, request);
                    break;

                case "COPY":
                case "MOVE":
                    HandleCopyOrMoveRequest(context, request);
                    break;

                case "SETMANIFESTINFO":
                    HandleSetManifestInfoRequest(context, request);
                    break;

                case "SETFILEINFO":
                    HandleSetFileInfoRequest(context, request);
                    break;
            }
        }

        protected void HandleGetRequest(
            HttpClientContext context,
            HttpRequest request)
        {
            try
            {
                // TODO: Authenticate based on request address

                FileInfo fileInfo =
                    new FileInfo(GetLocalFilePathFromRequest(request));

                HttpResponse response = (HttpResponse)request.CreateResponse(context);
                response.ContentType = "application/octet-stream";

                if (request.UriParts.Length == 1)
                {
                    // Requesting manifest
                    Guid repoGuid = GetRepositoryGuidFromRequest(request);

                    Manifest manifestClone =
                        GetRepositoryFromGuid(repoGuid).CloneManifest();

                    MemoryStream memStream = new MemoryStream();
                    manifestClone.WriteManifestStream(memStream);

                    memStream.Seek(0, SeekOrigin.Begin);
                    response.ContentLength = memStream.Length;
                    response.SendHeaders();
                    StreamUtilities.CopyStream(memStream, context.Stream);
                    //memStream.CopyTo(context.Stream);
                    
                    context.Stream.Close();                    
                }
                else
                {
                    // Requesting file
                    using (FileStream stream =
                        new FileStream(
                            fileInfo.FullName,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite))
                    {
                        response.ContentLength = stream.Length;
                        response.SendHeaders();
                        StreamUtilities.CopyStream(stream, context.Stream);
                        //stream.CopyTo(context.Stream);

                        context.Stream.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.InternalServerError,
                    "Internal server error",
                    ex.ToString(),
                    "text/plain");
            }
        }

        protected void HandlePutRequest(
            HttpClientContext context,
            HttpRequest request)
        {
            try
            {
                // Close the file
                request.Body.Close();

                LocalRepositoryState repoState = GetRepositoryFromRequest(request);

                // TODO: Authenticate based on request address
                // ...delete temp file if not authenticated...
                // Better to authenticate when the headers are received...

                string tempFilePath = GetTempFilePathFromRequest(request);
                FileInfo tempFile = new FileInfo(tempFilePath);

                String newFilePath = GetLocalFilePathFromRequest(request);
                String directoryPath = Path.GetDirectoryName(newFilePath);

                try
                {
                    if (Directory.Exists(directoryPath) == false)
                    {
                        Directory.CreateDirectory(directoryPath);
                    }

                    if (File.Exists(newFilePath))
                    {
                        File.Delete(newFilePath);
                    }

                    SetFileInfoFromRequest(tempFile, request);

                    tempFile.MoveTo(newFilePath);
                }
                catch (Exception ex)
                {
                    tempFile.Delete();
                    throw ex;
                }

                FileInfo newFile = new FileInfo(newFilePath);

                lock (repoState.Manifest)
                {
                    ManifestFileInfo manFileInfo =
                        GetOrMakeManifestFileInfoFromRequest(
                            repoState.Manifest,
                            request);

                    SetManifestFileInfo(manFileInfo, request, newFile);

                    repoState.SetManifestChanged();
                }

                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.OK,
                    "File accepted",
                    "",
                    "text/plain");
            }
            catch (Exception ex)
            {
                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.InternalServerError,
                    "Internal server error",
                    ex.ToString(),
                    "text/plain");

                System.Console.WriteLine(ex.ToString());
            }
        }

        protected void HandleDeleteRequest(
            HttpClientContext context,
            HttpRequest request)
        {
            try
            {
                LocalRepositoryState repoState = GetRepositoryFromRequest(request);

                // TODO: Authenticate based on request address

                String filePath = GetLocalFilePathFromRequest(request);
                File.Delete(filePath);

                HttpResponse response = (HttpResponse)request.CreateResponse(context);
                response.ContentType = "application/octet-stream";

                lock (repoState.Manifest)
                {
                    ManifestFileInfo manFileInfo =
                        GetOrMakeManifestFileInfoFromRequest(
                            repoState.Manifest,
                            request,
                            false);

                    if (manFileInfo == null)
                    {
                        throw new Exception(
                            "File not registered:  " +
                            filePath);
                    }

                    manFileInfo.ParentDirectory.Files.Remove(
                        manFileInfo.Name);

                    repoState.SetManifestChanged();
                }

                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.OK,
                    "File accepted",
                    "",
                    "text/plain");
            }
            catch (Exception ex)
            {
                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.InternalServerError,
                    "Internal server error",
                    ex.ToString(),
                    "text/plain");
            }
        }

        protected void HandleCopyOrMoveRequest(
            HttpClientContext context,
            HttpRequest request)
        {
            try
            {
                LocalRepositoryState repoState = GetRepositoryFromRequest(request);

                // TODO: Authenticate based on request address

                FileInfo sourceFileInfo =
                    new FileInfo(GetLocalFilePathFromRequest(request));

                String destFilePath =
                    GetLocalDestinationFilePathFromRequest(request, repoState);

                if (request.Method == "MOVE")
                {
                    sourceFileInfo.MoveTo(destFilePath);
                }
                else
                {
                    // Must be copy
                    sourceFileInfo.CopyTo(destFilePath);
                }

                FileInfo destFile = new FileInfo(destFilePath);
                SetFileInfoFromRequest(destFile, request);

                lock (repoState.Manifest)
                {
                    if (request.Method == "MOVE")
                    {
                        ManifestFileInfo removeFileInfo =
                            GetOrMakeManifestFileInfoFromRequest(
                                repoState.Manifest,
                                request,
                                false);

                        if (removeFileInfo != null)
                        {
                            // If this is ever null, something weird is happening
                            // but we already copied the file so might as well
                            // proceed.
                            removeFileInfo.ParentDirectory.Files.Remove(
                                removeFileInfo.Name);
                        }
                    }

                    ManifestFileInfo manFileInfo =
                        GetOrMakeDestinationManifestFileInfoFromRequest(
                            repoState.Manifest,
                            request);

                    SetManifestFileInfo(manFileInfo, request, destFile);

                    repoState.SetManifestChanged();
                }

                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.OK,
                    "File accepted",
                    "",
                    "text/plain");
            }
            catch (Exception ex)
            {
                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.InternalServerError,
                    "Internal server error",
                    ex.ToString(),
                    "text/plain");

                System.Console.WriteLine(ex.ToString());
            }
        }

        protected void HandleSetManifestInfoRequest(
            HttpClientContext context,
            HttpRequest request)
        {
            try
            {
                LocalRepositoryState repoState = GetRepositoryFromRequest(request);

                // TODO: Authenticate based on request address
                // ...delete temp file if not authenticated...
                // Better to authenticate when the headers are received...

                Stream infoStream = request.Body;
                infoStream.Seek(0, SeekOrigin.Begin);

                Manifest dummyManifest =
                    Manifest.ReadManifestStream(infoStream); 

                lock (repoState.Manifest)
                {
                    repoState.Manifest.CopyManifestInfoFrom(dummyManifest);
                    repoState.SetManifestChanged();
                }

                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.OK,
                    "Info accepted",
                    "",
                    "text/plain");
            }
            catch (Exception ex)
            {
                // TODO: Make this a method
                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.InternalServerError,
                    "Internal server error",
                    ex.ToString(),
                    "text/plain");

                System.Console.WriteLine(ex.ToString());
            }
        }

        protected void HandleSetFileInfoRequest(
            HttpClientContext context,
            HttpRequest request)
        {
            try
            {
                LocalRepositoryState repoState = GetRepositoryFromRequest(request);

                // TODO: Authenticate based on request address
                // ...delete temp file if not authenticated...
                // Better to authenticate when the headers are received...

                String newFilePath = GetLocalFilePathFromRequest(request);
                FileInfo newFile = new FileInfo(newFilePath);

                lock (repoState.Manifest)
                {
                    ManifestFileInfo manFileInfo =
                        GetOrMakeManifestFileInfoFromRequest(
                            repoState.Manifest,
                            request,
                            false);

                    SetManifestFileInfo(manFileInfo, request, newFile);

                    newFile.LastWriteTimeUtc =
                        manFileInfo.LastModifiedUtc;

                    repoState.SetManifestChanged();
                }

                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.OK,
                    "Info accepted",
                    "",
                    "text/plain");
            }
            catch (Exception ex)
            {
                context.Respond(
                    "HTTP/1.0",
                    HttpStatusCode.InternalServerError,
                    "Internal server error",
                    ex.ToString(),
                    "text/plain");

                System.Console.WriteLine(ex.ToString());
            }
        }


        // Helper methods

        protected String GetSettingsFilePath()
        {
            String computerName =
                System.Windows.Forms.SystemInformation.ComputerName;

            String appDirectoryPathName = Path.GetDirectoryName(
                new Uri(Assembly.GetExecutingAssembly().GetName().CodeBase).LocalPath);

            return Path.Combine(
                appDirectoryPathName,
                ServerSettingsFileName + "-" + computerName);
        }

        protected void LoadSettings()
        {
            Settings = null;

            String serverSettingsFilePath = GetSettingsFilePath();

            if (File.Exists(serverSettingsFilePath))
            {
                try
                {
                    Settings = ServerSettings.ReadServerSettings(serverSettingsFilePath);
                }
                catch (Exception)
                {
                    // TODO: Handle exceptions
                }
            }

            if (Settings == null)
            {
                Settings = new ServerSettings();
                Settings.ServerSettingsFilePath = serverSettingsFilePath;
            }
        }

        protected void LoadRepositories()
        {
            lock (GuidToRepository)
            {
                GuidToRepository.Clear();

                foreach (RepositoryInfo nextRepo in Settings.GetRepositories())
                {
                    DirectoryInfo rootDirectory =
                        new DirectoryInfo(nextRepo.RepositoryPath);

                    GuidToRepository[nextRepo.Guid] =
                        new LocalRepositoryState(rootDirectory);
                }
            }
        }

        protected Guid GetRepositoryGuidFromRequest(HttpRequest request)
        {
            // Parse GUID
            Guid repoGuid;
            try
            {
                repoGuid = new Guid(request.UriParts[0]);
            }
            catch (Exception)
            {
                throw new Exception("Could not parse repository GUID.");
            }

            // Find manifest
            if (Settings.GetRepositoryFromGuid((Guid) repoGuid) != null)
            {
                return repoGuid;
            }

            throw new Exception("Repository GUID not registered.");
        }

        protected LocalRepositoryState GetRepositoryFromGuid(Guid guid)
        {
            lock (GuidToRepository)
            {
                return GuidToRepository[guid];
            }
        }

        protected LocalRepositoryState GetRepositoryFromRequest(HttpRequest request)
        {
            Guid guid = GetRepositoryGuidFromRequest(request);
            return GetRepositoryFromGuid(guid);
        }

        protected String GetLocalFilePathFromRequest(HttpRequest request)
        {
            Guid repoGuid = GetRepositoryGuidFromRequest(request);
            String filePath = Settings.GetRepositoryFromGuid(repoGuid).RepositoryPath;

            // DMO: Probably a bad design choice to encode the filenames
            // directly into the URI.  Filenames can have characters like
            // '#' for example...

            String[] splitParts = request._uriRawPath.Split(new char[] {'/'} );
            for (int i = 2; i < splitParts.Length; i++)
            {
                filePath = Path.Combine(filePath, Uri.UnescapeDataString(splitParts[i]));
            }

            return filePath;
        }

        protected String GetTempFilePathFromRequest(HttpRequest request)
        {
            Guid manifestGuid = GetRepositoryGuidFromRequest(request);
            LocalRepositoryState repoState = GetRepositoryFromGuid(manifestGuid);

            // TODO: Authenticate based on request address

            string[] fileHashParts =
                request.Headers[RemoteRepositoryProxy.FileHashHeaderName].Split(
                    new char[] { ':' });

            string tempFilePath = Path.Combine(
                repoState.TempDirectory.FullName,
                fileHashParts[1]);

            return tempFilePath;
        }

        protected Stream HttpRequestStreamFactory(HttpRequest request)
        {
            if (request.Method == "PUT")
            {
                String tempFilePath = GetTempFilePathFromRequest(request);
                return new FileStream(tempFilePath, FileMode.Create);
            }

            return new MemoryStream();
        }

        protected String GetLocalDestinationFilePathFromRequest(
            HttpRequest request,
            LocalRepositoryState repoState)
        {
            String destRepoPath =
                request.Headers[RemoteRepositoryProxy.DestinationPathHeaderName];

            // Remove the leading './' from the relative path
            destRepoPath =
                destRepoPath.Substring(2, destRepoPath.Length - 2);

            string[] pathParts = destRepoPath.Split(new char[] { '/' });

            String filePath = repoState.RootDirectory.FullName;
            for (int i = 0; i < pathParts.Length; i++)
            {
                filePath = Path.Combine(filePath, pathParts[i]);
            }

            return filePath;
        }

        protected void SetFileInfoFromRequest(
            FileInfo file,
            HttpRequest request)
        {
            long lastModifiedUtcTicks = long.Parse(
                request.Headers[RemoteRepositoryProxy.LastModifiedUtcHeaderName]);

            file.LastWriteTimeUtc =
                new DateTime(lastModifiedUtcTicks, DateTimeKind.Utc);
        }

        protected ManifestFileInfo GetOrMakeManifestFileInfoFromParts(
            Manifest manifest,
            List<String> parts,
            bool makeNewEntryIfNeeded = true)
        {
            lock (manifest)
            {
                ManifestDirectoryInfo currentParentThis =
                    manifest.RootDirectory;

                int partIndex = 0;
                for (; partIndex < parts.Count - 1; partIndex++)
                {
                    String uriPart = parts[partIndex];

                    if (currentParentThis.Subdirectories.Keys.Contains(uriPart))
                    {
                        currentParentThis = currentParentThis.Subdirectories[uriPart];
                    }
                    else
                    {
                        if (makeNewEntryIfNeeded)
                        {
                            ManifestDirectoryInfo newParent =
                                new ManifestDirectoryInfo(
                                    uriPart,
                                    currentParentThis);

                            currentParentThis.Subdirectories[uriPart] =
                                newParent;

                            currentParentThis = newParent;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }

                String fileName = parts[partIndex];

                if (currentParentThis.Files.Keys.Contains(fileName))
                {
                    return currentParentThis.Files[fileName];
                }

                if (makeNewEntryIfNeeded)
                {
                    ManifestFileInfo newManifestFile =
                        new ManifestFileInfo(
                            fileName,
                            currentParentThis);

                    currentParentThis.Files[newManifestFile.Name] =
                        newManifestFile;

                    return newManifestFile;
                }

                return null;
            }
        }

        protected ManifestFileInfo GetOrMakeManifestFileInfoFromRequest(
            Manifest manifest,
            HttpRequest request,
            bool makeNewEntryIfNeeded = true)
        {
            List<String> parts = new List<string>();

            for (int uriPartIndex = 1;
                uriPartIndex < request.UriParts.Length;
                uriPartIndex++)
            {
                parts.Add(request.UriParts[uriPartIndex]);
            }

            return GetOrMakeManifestFileInfoFromParts(
                manifest,
                parts,
                makeNewEntryIfNeeded);
        }

        protected ManifestFileInfo GetOrMakeDestinationManifestFileInfoFromRequest(
            Manifest manifest,
            HttpRequest request,
            bool makeNewEntryIfNeeded = true)
        {
            List<String> parts = new List<string>();

            String destRepoPath =
                request.Headers[RemoteRepositoryProxy.DestinationPathHeaderName];

            // Remove the leading './' from the relative path
            destRepoPath =
                destRepoPath.Substring(2, destRepoPath.Length - 2);

            string[] pathParts = destRepoPath.Split(new char[] { '/' });

            for (int i = 0; i < pathParts.Length; i++)
            {
                parts.Add(pathParts[i]);
            }

            return GetOrMakeManifestFileInfoFromParts(
                manifest,
                parts,
                makeNewEntryIfNeeded);
        }

        protected void SetManifestFileInfo(
            ManifestFileInfo manFileInfo,
            HttpRequest request,
            FileInfo file)
        {

            manFileInfo.FileLength = file.Length;

            long lastModifiedUtcTicks = long.Parse(
                request.Headers[RemoteRepositoryProxy.LastModifiedUtcHeaderName]);

            manFileInfo.LastModifiedUtc =
                new DateTime(lastModifiedUtcTicks, DateTimeKind.Utc);

            long registeredUtcTicks = long.Parse(
                request.Headers[RemoteRepositoryProxy.RegisteredUtcHeaderName]);

            manFileInfo.RegisteredUtc =
                new DateTime(registeredUtcTicks, DateTimeKind.Utc);

            string[] fileHashParts =
                request.Headers[RemoteRepositoryProxy.FileHashHeaderName].Split(
                    new char[] { ':' });

            manFileInfo.FileHash =
                new FileHash(
                    fileHashParts[1],
                    fileHashParts[0]);
        }

        protected void FlushManifestsCallback(object state)
        {
            List<LocalRepositoryState> repositoryList;
            lock (GuidToRepository)
            {
                repositoryList = new List<LocalRepositoryState>(
                    GuidToRepository.Values);
            }

            foreach (LocalRepositoryState nextRepo in repositoryList)
            {
                nextRepo.FlushManifest();
            }
        }


        // Accessors

        protected Dictionary<Guid, LocalRepositoryState> GuidToRepository { set; get; }


        // Static

        public static String ServerSettingsFileName;
        public static int PortNumber { private set; get; }

        static RepositoryServer()
        {
            ServerSettingsFileName = ".repositoryServerSettings";
            PortNumber = 7555;
        }
    }
}
