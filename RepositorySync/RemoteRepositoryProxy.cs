using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

using RepositoryManifest;


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

        public ManifestFileInfo PutFile(
            IRepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile)
        {
            throw new NotImplementedException();
        }

        public void RemoveFile(ManifestFileInfo removeManifestFile)
        {
            throw new NotImplementedException();
        }

        public ManifestFileInfo CopyFile(
            ManifestFileInfo fileToBeCopied,
            IRepositoryProxy otherRepositoryWithNewLocation,
            RepositoryManifest.ManifestFileInfo otherFileWithNewLocation)
        {
            throw new NotImplementedException();
        }

        public ManifestFileInfo MoveFile(
            ManifestFileInfo fileToBeMoved,
            IRepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation)
        {
            throw new NotImplementedException();
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
                response.GetResponseStream().CopyTo(fileStream);
            }

            return new FileInfo(tempFilePath);
        }


        // Helper methods

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


        // Accessors

        protected Uri BaseUri { set; get; }
        protected DirectoryInfo TempDirectory { set; get; }


        // Static

        protected static int RequestTimeout { set; get; }

        public static String LastModifiedUtcHeaderName;
        

        static RemoteRepositoryProxy()
        {
            RequestTimeout = 10000;

            LastModifiedUtcHeaderName = "Rep-LastModifiedUtc";
        }
    }
}
