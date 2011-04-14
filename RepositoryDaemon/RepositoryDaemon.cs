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


namespace RepositoryDaemon
{
    public class RepositoryDaemon
    {
        public RepositoryDaemon()
        {
            LoadSettings();

            GuidToRepository = new Dictionary<Guid, LocalRepositoryState>();

            CreateTempDirectory();
        }

        ~RepositoryDaemon()
        {
            TempDirectory.Delete(true);
        }

        public void AddRepository(
            String repositoryPath,
            String manifestPath = null)
        {
            String fullRepositoryPath =
                new DirectoryInfo(repositoryPath).FullName;

            String fullManifestPath = null;

            if (manifestPath == null)
            {
                fullManifestPath = Path.Combine(
                    fullRepositoryPath,
                    Manifest.DefaultManifestFileName);
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

            if (Settings.GuidToRepository.ContainsKey(repoInfo.Guid))
            {
                throw new Exception("Repository GUID is already registered.");
            }

            if (manifest.Name != null &&
                Settings.NameToRepository.ContainsKey(repoInfo.Name))
            {
                throw new Exception("Repository name is already registered.");
            }

            Settings.GuidToRepository[manifest.Guid] = repoInfo;

            if (manifest.Name != null)
            {
                Settings.NameToRepository[manifest.Name] = repoInfo;
            }
        }

        public void RemoveRepository(String manifestPath)
        {
            Manifest manifest =
                Manifest.ReadManifestFile(manifestPath);

            if (Settings.GuidToRepository.ContainsKey(manifest.Guid) == false)
            {
                throw new Exception("Repository GUID is not registered.");
            }

            if (manifest.Name != null &&
                Settings.NameToRepository.ContainsKey(manifest.Name) == false)
            {
                throw new Exception("Repository name is not registered.");
            }

            Settings.GuidToRepository.Remove(manifest.Guid);

            if (manifest.Name != null)
            {
                Settings.NameToRepository.Remove(manifest.Name);
            }
        }

        public void Start()
        {
            LoadManifests();

            // TODO: Specify our address
            HttpServer.HttpListener listener =
                HttpServer.HttpListener.Create(
                    IPAddress.Any,
                    PortNumber);

            listener.RequestReceived += OnRequest;

            listener.Start(5);
             
            // TODO: Wait for signal
            System.Threading.Thread.Sleep(
                System.Threading.Timeout.Infinite);
        }

        public void SaveSettings()
        {
            if (Settings != null)
            {
                String daemonSettingsFilePath = GetSettingsFilePath();
                Settings.WriteDaemonSettings(daemonSettingsFilePath);
            }
        }

        public DaemonSettings Settings { private set; get; }


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
                Console.WriteLine(
                    nextKey + "=" + request.Headers[nextKey]);
            }
            Console.WriteLine();


            // Handle methods
            switch (request.Method)
            {
                case "GET":
                    HandleGetRequest(context, request);
                    break;

                case "PUT":
                    HandlePutRequest(context, request);
                    break;
            }
        }

        protected void HandleGetRequest(
            HttpClientContext context,
            HttpRequest request)
        {
            // TODO: Authenticate based on request address

            try
            {
                FileInfo fileInfo =
                    new FileInfo(GetFilePathFromRequest(request));

                HttpResponse response = (HttpResponse)request.CreateResponse(context);
                response.ContentType = "application/octet-stream";

                using (FileStream stream =
                    new FileStream(
                        fileInfo.FullName,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite))
                {
                    response.ContentLength = stream.Length;
                    response.SendHeaders();
                    stream.CopyTo(context.Stream);
                    context.Stream.Close();
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
            Guid manifestGuid = GetManifestGuidFromRequest(request);
            LocalRepositoryState repoState = GuidToRepository[manifestGuid];
            Manifest manifest = repoState.Manifest;

            // TODO: Lock manifest

            // TODO: Authenticate based on request address

            try
            {
                string[] fileHashParts =
                    request.Headers[RemoteRepositoryProxy.FileHashHeaderName].Split(
                        new char[] { ':' });

                string tempFilePath = Path.Combine(
                    TempDirectory.FullName,
                    fileHashParts[1]);

                long lastModifiedUtcTicks = long.Parse(
                    request.Headers[RemoteRepositoryProxy.LastModifiedUtcHeaderName]);

                using (FileStream fileStream =
                    new FileStream(
                        tempFilePath,
                        FileMode.Create))
                {
                    request.Body.CopyTo(fileStream);
                    request.Body.Close();
                }

                String newFilePath = GetFilePathFromRequest(request);
                String directoryPath = Path.GetDirectoryName(newFilePath);

                FileInfo tempFile = new FileInfo(tempFilePath);

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

                    tempFile.LastWriteTimeUtc =
                        new DateTime(lastModifiedUtcTicks, DateTimeKind.Utc);

                    tempFile.MoveTo(newFilePath);
                }
                catch (Exception ex)
                {
                    tempFile.Delete();
                    throw ex;
                }

                ManifestFileInfo manFileInfo =
                    GetOrMakeManifestFileInfoFromRequest(
                        manifest,
                        request);

                manFileInfo.FileLength =
                    request.ContentLength;

                manFileInfo.LastModifiedUtc =
                    new DateTime(lastModifiedUtcTicks, DateTimeKind.Utc);

                long registeredUtcTicks = long.Parse(
                    request.Headers[RemoteRepositoryProxy.RegisteredUtcHeaderName]);

                manFileInfo.RegisteredUtc =
                    new DateTime(registeredUtcTicks, DateTimeKind.Utc);

                manFileInfo.FileHash =
                    new FileHash(
                        fileHashParts[1],
                        fileHashParts[0]);

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

                Console.WriteLine(ex.ToString());
            }
        }


        // Helper methods

        protected void CreateTempDirectory()
        {
            try
            {
                String systemTempPath = Path.GetTempPath();
                DirectoryInfo systemTempDir = new DirectoryInfo(systemTempPath);

                TempDirectory = systemTempDir.CreateSubdirectory(
                    "temp-" +
                    Path.GetRandomFileName());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Could not create temporary directory.",
                    ex);
            }
        }

        protected Guid GetManifestGuidFromRequest(HttpRequest request)
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
            if (Settings.GuidToRepository.ContainsKey((Guid) repoGuid))
            {
                return repoGuid;
            }

            throw new Exception("Repository GUID not registered.");
        }

        protected String GetFilePathFromRequest(HttpRequest request)
        {
            Guid repoGuid = GetManifestGuidFromRequest(request);

            if (request.UriParts.Length == 1)
            {
                // If no path is specified, return the manifest file
                return Settings.GuidToRepository[repoGuid].ManifestPath;
            }

            // Otherwise, locate the file
            String filePath = Settings.GuidToRepository[repoGuid].RepositoryPath;
            for (int i = 1; i < request.UriParts.Length; i++)
            {
                filePath = Path.Combine(filePath, request.UriParts[i]);
            }

            return filePath;
        }

        protected ManifestFileInfo GetOrMakeManifestFileInfoFromRequest(
            Manifest manifest,
            HttpRequest request)
        {
            ManifestDirectoryInfo currentParentThis =
                manifest.RootDirectory;

            int uriPartIndex = 1;
            for (;
                uriPartIndex < request.UriParts.Length - 1;
                uriPartIndex++)
            {
                String uriPart = request.UriParts[uriPartIndex];

                if (currentParentThis.Subdirectories.Keys.Contains(uriPart))
                {
                    currentParentThis = currentParentThis.Subdirectories[uriPart];
                }
                else
                {
                    ManifestDirectoryInfo newParent =
                        new ManifestDirectoryInfo(
                            uriPart,
                            currentParentThis);

                    currentParentThis.Subdirectories[uriPart] =
                        newParent;

                    currentParentThis = newParent;
                }
            }

            ManifestFileInfo newManifestFile =
                new ManifestFileInfo(
                    request.UriParts[uriPartIndex],
                    currentParentThis);

            currentParentThis.Files[newManifestFile.Name] =
                newManifestFile;

            return newManifestFile;
        }

        protected String GetSettingsFilePath()
        {
            String computerName =
                System.Windows.Forms.SystemInformation.ComputerName;

            String appDirectoryPathName = Path.GetDirectoryName(
                new Uri(Assembly.GetExecutingAssembly().GetName().CodeBase).LocalPath);

            return Path.Combine(
                appDirectoryPathName,
                DaemonSettingsFileName + "-" + computerName);
        }

        protected void LoadSettings()
        {
            Settings = null;

            String daemonSettingsFilePath = GetSettingsFilePath();

            if (File.Exists(daemonSettingsFilePath))
            {
                try
                {
                    Settings = DaemonSettings.ReadDaemonSettings(daemonSettingsFilePath);
                }
                catch (Exception)
                {
                    // TODO: Handle exceptions
                }
            }

            if (Settings == null)
            {
                Settings = new DaemonSettings();
            }
        }

        protected void LoadManifests()
        {
            GuidToRepository.Clear();

            foreach (RepositoryInfo nextRepo in Settings.GuidToRepository.Values)
            {
                DirectoryInfo rootDirectory =
                    new DirectoryInfo(nextRepo.RepositoryPath);

                GuidToRepository[nextRepo.Guid] =
                    new LocalRepositoryState(rootDirectory);
            }
        }


        // Accessors

        protected DirectoryInfo TempDirectory { set; get; }
        protected Dictionary<Guid, LocalRepositoryState> GuidToRepository { set; get; }


        // Static

        public static String DaemonSettingsFileName;
        public static int PortNumber { private set; get; }
        protected static int SendChunkSize { set; get; }

        static RepositoryDaemon()
        {
            DaemonSettingsFileName = ".repositoryDaemonSettings";
            PortNumber = 7555;
            SendChunkSize = 8192;
        }
    }
}
