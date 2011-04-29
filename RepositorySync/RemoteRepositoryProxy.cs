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
            TempDirectory.Delete(true);
        }

        public Manifest Manifest { protected set; get; }

        public void PutFile(
            IRepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile)
        {
            FileInfo sourceFile =
                sourceRepository.GetFile(sourceManifestFile);

            String manifestPath =
                Manifest.MakeStandardPathString(sourceManifestFile);

            // Remove the leading '.' from the relative path
            String uriPath =
                manifestPath.Substring(1, manifestPath.Length - 1);

            Uri requestUri = new Uri(BaseUri.ToString() + uriPath);

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(requestUri);

            request.Method = "PUT";
            request.Timeout = System.Threading.Timeout.Infinite;

            request.AllowWriteStreamBuffering = false;

            SetStandardFileHeaders(request, sourceManifestFile);

            using (FileStream fileStream = sourceFile.OpenRead())
            {
                request.ContentLength = fileStream.Length;

                //StreamUtilities.CopyStream(fileStream, request.GetRequestStream());
                fileStream.CopyTo(request.GetRequestStream());
                
                request.GetRequestStream().Close();
            }

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            // TODO: Handle error?
        }

        public void RemoveFile(ManifestFileInfo removeManifestFile)
        {
            String manifestPath =
                Manifest.MakeStandardPathString(removeManifestFile);

            // Remove the leading '.' from the relative path
            String uriPath =
                manifestPath.Substring(1, manifestPath.Length - 1);

            Uri requestUri = new Uri(BaseUri.ToString() + uriPath);

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(requestUri);

            // TODO: change timeout...?
            request.Method = "DELETE";
            request.Timeout = RequestTimeout;

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            // TODO: Handle error?
        }

        public void CopyFile(
            ManifestFileInfo fileToBeCopied,
            ManifestFileInfo otherFileWithNewLocation)
        {
            CopyOrMoveFile(
                fileToBeCopied,
                otherFileWithNewLocation,
                "COPY",
                System.Threading.Timeout.Infinite);
        }

        public void CopyFileInformation(
            ManifestFileInfo fileToBeUpdated,
            ManifestFileInfo otherFileWithNewFileInfo)
        {
            throw new NotImplementedException();
        }

        public void MoveFile(
            ManifestFileInfo fileToBeMoved,
            ManifestFileInfo otherFileWithNewLocation)
        {
            CopyOrMoveFile(
                fileToBeMoved,
                otherFileWithNewLocation,
                "MOVE",
                System.Threading.Timeout.Infinite);
        }

        public void CopyManifestInformation(IRepositoryProxy otherRepository)
        {
            throw new NotImplementedException();
        }


        // Support methods called by destination repository proxy

        public FileInfo GetFile(ManifestFileInfo readFile)
        {
            // We don't have a local copy, so we must make a temp clone
            // and supply that instead.
            return CloneFile(readFile, TempDirectory);
        }

        public FileInfo CloneFile(
            ManifestFileInfo copyFile, 
            DirectoryInfo copyToDirectory)
        {
            String manifestPath =
                Manifest.MakeStandardPathString(copyFile);

            // Remove the leading '.' from the relative path
            String uriPath =
                manifestPath.Substring(1, manifestPath.Length - 1);

            Uri requestUri = new Uri(BaseUri.ToString() + uriPath);

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(requestUri);

            request.Timeout = RequestTimeout;
            request.Method = "GET";

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            string tempFilePath = Path.Combine(
                TempDirectory.FullName,
                copyFile.FileHash.ToString());

            using (FileStream fileStream =
                new FileStream(
                    tempFilePath,
                    FileMode.Create))
            {
                //StreamUtilities.CopyStream(response.GetResponseStream(), fileStream);
                response.GetResponseStream().CopyTo(fileStream);

                response.GetResponseStream().Close();
            }

            return new FileInfo(tempFilePath);
        }


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
            String manifestPath =
                Manifest.MakeStandardPathString(fileToBeCopied);

            // Remove the leading '.' from the relative path
            String uriPath =
                manifestPath.Substring(1, manifestPath.Length - 1);

            Uri requestUri = new Uri(BaseUri.ToString() + uriPath);

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(requestUri);

            request.Method = method;
            request.Timeout = timeout;

            SetStandardFileHeaders(request, otherFileWithNewLocation);

            request.Headers[RemoteRepositoryProxy.DestinationPathHeaderName] =
                Manifest.MakeStandardPathString(otherFileWithNewLocation);

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            // TODO: Handle error?
        }


        // Accessors

        protected Uri BaseUri { set; get; }
        protected DirectoryInfo TempDirectory { set; get; }


        // Static

        protected static int RequestTimeout { set; get; }

        public static String LastModifiedUtcHeaderName;
        public static String RegisteredUtcHeaderName;
        public static String FileHashHeaderName;
        public static String DestinationPathHeaderName;

        static RemoteRepositoryProxy()
        {
            RequestTimeout = 10000;

            LastModifiedUtcHeaderName = "Rep-LastModifiedUtc";
            RegisteredUtcHeaderName = "Rep-RegisteredUtc";
            FileHashHeaderName = "Rep-FileHash";
            DestinationPathHeaderName = "Rep-DestinationPath";
        }
    }
}
