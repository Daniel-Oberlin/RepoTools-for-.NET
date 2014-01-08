using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

using RepositoryManifest;
using Utilities;


namespace RepositorySync
{
    public class RemoteRepositoryProxy : IRepositoryProxy
    {
        public RemoteRepositoryProxy(
            String uriString,
            Manifest otherManifest = null)
        {
            GetRemoteManifest(uriString, otherManifest);
            CreateTempDirectory();
        }

        ~RemoteRepositoryProxy()
        {
            CleanupBeforeExit();
        }

        public void CleanupBeforeExit()
        {
            if (TempDirectory != null)
            {
                TempDirectory.Delete(true);
                TempDirectory = null;
            }
        }

        public Manifest Manifest { protected set; get; }

        public void PutFile(
            IRepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile)
        {
            FileInfo sourceFile =
                sourceRepository.GetFile(sourceManifestFile);

            Uri requestUri = MakeRemoteUri(sourceManifestFile);

            int retries = 0;
            bool success = false;
            do
            {

                try
                {
                    HttpWebRequest request =
                        (HttpWebRequest)WebRequest.Create(requestUri);

                    request.Method = "PUT";
                    request.Timeout = RequestTimeout;
                    request.ReadWriteTimeout = RequestReadWriteTimeout;
                    request.AllowWriteStreamBuffering = false;

                    SetStandardFileHeaders(request, sourceManifestFile);

                    using (FileStream fileStream = sourceFile.OpenRead())
                    {
                        request.ContentLength = fileStream.Length;
                        StreamUtilities.CopyStream(fileStream, request.GetRequestStream());
                        request.GetRequestStream().Close();
                    }

                    HttpWebResponse response =
                        (HttpWebResponse)request.GetResponse();

                    response.Close();
                    success = true;
                }
                catch (System.Net.WebException ex)
                {
                    if (++retries > MaxNumberOfRequestRetries)
                    {
                        throw ex;
                    }

                    System.Threading.Thread.Sleep(RequestRetryWaitInterval);
                }

            } while (success == false);

        }

        public void RemoveFile(ManifestFileInfo removeManifestFile)
        {
            Uri requestUri = MakeRemoteUri(removeManifestFile);

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(requestUri);

            request.Method = "DELETE";
            request.Timeout = RequestTimeout;

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            response.Close();
        }

        public void CopyFile(
            ManifestFileInfo fileToBeCopied,
            ManifestFileInfo otherFileWithNewLocation)
        {
            CopyOrMoveFile(
                fileToBeCopied,
                otherFileWithNewLocation,
                "COPY",
                RequestTimeout);
        }

        public void CopyFileInformation(
            ManifestFileInfo fileToBeUpdated,
            ManifestFileInfo otherFileWithNewFileInfo)
        {
            Uri requestUri = MakeRemoteUri(fileToBeUpdated);

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(requestUri);

            request.Method = "SETFILEINFO";
            request.Timeout = RequestTimeout;
            request.AllowWriteStreamBuffering = false;

            SetStandardFileHeaders(request, otherFileWithNewFileInfo);
            request.ContentLength = 0;
            request.GetRequestStream().Close();

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            response.Close();
        }

        public void MoveFile(
            ManifestFileInfo fileToBeMoved,
            ManifestFileInfo otherFileWithNewLocation)
        {
            CopyOrMoveFile(
                fileToBeMoved,
                otherFileWithNewLocation,
                "MOVE",
                RequestTimeout);
        }

        public void CopyManifestInformation(IRepositoryProxy otherRepository)
        {
            // Clone the manifest and all of its information
            Manifest dummyManifest = new Manifest(otherRepository.Manifest);

            // Remove the files before we send it over because all we care about is the info
            dummyManifest.RootDirectory.Files.Clear();
            dummyManifest.RootDirectory.Subdirectories.Clear();

            Uri requestUri = new Uri(BaseUri.ToString());

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(requestUri);

            request.Method = "SETMANIFESTINFO";
            request.Timeout = RequestTimeout;
            request.AllowWriteStreamBuffering = false;

            MemoryStream memStream = new MemoryStream();
            dummyManifest.WriteManifestStream(memStream);

            memStream.Seek(0, SeekOrigin.Begin);
            request.ContentLength = memStream.Length;

            StreamUtilities.CopyStream(memStream, request.GetRequestStream());
            request.GetRequestStream().Close();

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            response.Close();
        }


        // Support methods called by destination repository proxy

        public FileInfo GetFile(ManifestFileInfo readFile)
        {
            // We don't have a local copy, so we must make a temp clone
            // and supply that instead.  The clone will be removed eventually
            // during cleanup when the temp directory is deleted.
            return CloneFile(readFile, TempDirectory);
        }

        public FileInfo CloneFile(
            ManifestFileInfo copyFile, 
            DirectoryInfo copyToDirectory)
        {
            Uri requestUri = MakeRemoteUri(copyFile);

            int retries = 0;
            bool success = false;
            string tempFilePath = null;
            do
            {

                try
                {
                    HttpWebRequest request =
                        (HttpWebRequest)WebRequest.Create(requestUri);

                    request.Method = "GET";
                    request.Timeout = RequestTimeout;
                    request.ReadWriteTimeout = RequestReadWriteTimeout;

                    HttpWebResponse response =
                        (HttpWebResponse)request.GetResponse();

                    tempFilePath = Path.Combine(
                        TempDirectory.FullName,
                        copyFile.FileHash.ToString());

                    using (FileStream fileStream =
                        new FileStream(
                            tempFilePath,
                            FileMode.Create))
                    {
                        StreamUtilities.CopyStream(response.GetResponseStream(), fileStream);
                        response.GetResponseStream().Close();
                    }

                    success = true;
                }
                catch (System.Net.WebException ex)
                {
System.Console.WriteLine("RETRY: " + ++numRetries);
                    if (++retries > MaxNumberOfRequestRetries)
                    {
                        throw ex;
                    }

                    System.Threading.Thread.Sleep(RequestRetryWaitInterval);
                }

            } while (success == false);

            return new FileInfo(tempFilePath);
        }
static int numRetries = 0;


        // Helper methods

        protected void CreateTempDirectory()
        {
            try
            {
                String systemTempPath = Path.GetTempPath();
                DirectoryInfo systemTempDir = new DirectoryInfo(systemTempPath);

                TempDirectory = systemTempDir.CreateSubdirectory(
                    Path.GetRandomFileName());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Could not create temporary directory.",
                    ex);
            }
        }

        protected void GetRemoteManifest(
            String uriString,
            Manifest otherManifest = null)
        {
            Uri parsedUri = null;
            try
            {
                parsedUri = new Uri(uriString);
            }
            catch (Exception)
            {
                throw new Exception("Could not parse URI string.");
            }

            if (parsedUri.Segments.Length == 1)
            {
                if (otherManifest != null)
                {
                    BaseUri = new Uri(
                        parsedUri.ToString() +
                        otherManifest.Guid.ToString());
                }
                else
                {
                    throw new Exception("No repository GUID specified.");
                }
            }
            else if (parsedUri.Segments.Length == 2)
            {
                BaseUri = parsedUri;
            }
            else
            {
                throw new Exception("Malformed URI.");
            }

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(BaseUri);

            request.Timeout = RequestTimeout;
            request.Method = "GET";

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            Manifest = Manifest.ReadManifestStream(
                response.GetResponseStream());

            response.Close();
        }

        protected void SetStandardFileHeaders(
            HttpWebRequest request,
            ManifestFileInfo file)
        {
            request.Headers[RemoteRepositoryProxy.LastModifiedUtcHeaderName] =
                    file.LastModifiedUtc.Ticks.ToString();

            request.Headers[RemoteRepositoryProxy.RegisteredUtcHeaderName] =
                file.RegisteredUtc.Ticks.ToString();

            request.Headers[RemoteRepositoryProxy.FileHashHeaderName] =
                file.FileHash.HashType +
                ":" +
                file.FileHash.ToString();
        }

        public void CopyOrMoveFile(
            ManifestFileInfo fileToBeCopied,
            ManifestFileInfo otherFileWithNewLocation,
            String method,
            int timeout)
        {
            Uri requestUri = MakeRemoteUri(fileToBeCopied);

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(requestUri);

            request.Method = method;
            request.Timeout = timeout;

            SetStandardFileHeaders(request, otherFileWithNewLocation);

            String destPathString =
                Manifest.MakeStandardPathString(otherFileWithNewLocation);

            String escapedDestPathString =
                System.Uri.EscapeDataString(destPathString);

            request.Headers[RemoteRepositoryProxy.DestinationPathHeaderName] =
                escapedDestPathString;

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            response.Close();
        }

        protected Uri MakeRemoteUri(ManifestFileInfo remoteFile)
        {
            String manifestPath =
                Manifest.MakeStandardPathString(remoteFile);

            // Remove the leading '.' from the relative path
            String uriPathString =
                manifestPath.Substring(1, manifestPath.Length - 1);

            String escapedUriPathString = System.Uri.EscapeDataString(uriPathString);

            Uri uri = new Uri(BaseUri.ToString() + escapedUriPathString);

            return uri;
        }

        // Accessors

        protected Uri BaseUri { set; get; }
        protected DirectoryInfo TempDirectory { set; get; }


        // Static

        public static int RequestTimeout { set; get; }
        public static int RequestReadWriteTimeout { set; get; }
        public static int RequestRetryWaitInterval { set; get; }
        public static int MaxNumberOfRequestRetries { set; get; }

        public static String LastModifiedUtcHeaderName;
        public static String RegisteredUtcHeaderName;
        public static String FileHashHeaderName;
        public static String DestinationPathHeaderName;

        static RemoteRepositoryProxy()
        {
            RequestTimeout = 10000;
            RequestReadWriteTimeout = 10000;
            RequestRetryWaitInterval = 20000;
            MaxNumberOfRequestRetries = 3;

            LastModifiedUtcHeaderName = "Rep-LastModifiedUtc";
            RegisteredUtcHeaderName = "Rep-RegisteredUtc";
            FileHashHeaderName = "Rep-FileHash";
            DestinationPathHeaderName = "Rep-DestinationPath";
        }

        static public bool IsRemoteRepositoryString(string s)
        {
            return
                s.StartsWith("http://") ||
                s.StartsWith("https://");
        }
    }
}
