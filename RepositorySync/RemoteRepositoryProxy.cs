using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

using RepositoryManifest;


namespace RepositorySync
{
    public class RemoteRepositoryProxy : RepositoryProxy
    {
        public RemoteRepositoryProxy(
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
                (HttpWebRequest) WebRequest.Create(BaseUri);

            request.Timeout = 10000;
            request.Method = "POST";

            HttpWebResponse response =
                (HttpWebResponse)request.GetResponse();

            Manifest = Manifest.ReadManifestStream(
                response.GetResponseStream());
        }

        public override Manifest Manifest { protected set; get; }

        public override RepositoryManifest.ManifestFileInfo PutFile(RepositoryProxy sourceRepository, RepositoryManifest.ManifestFileInfo sourceManifestFile)
        {
            throw new NotImplementedException();
        }

        public override void RemoveFile(RepositoryManifest.ManifestFileInfo removeManifestFile)
        {
            throw new NotImplementedException();
        }

        public override RepositoryManifest.ManifestFileInfo CopyFile(RepositoryManifest.ManifestFileInfo fileToBeCopied, RepositoryProxy otherRepositoryWithNewLocation, RepositoryManifest.ManifestFileInfo otherFileWithNewLocation)
        {
            throw new NotImplementedException();
        }

        public override RepositoryManifest.ManifestFileInfo MoveFile(RepositoryManifest.ManifestFileInfo fileToBeMoved, RepositoryProxy otherRepositoryWithNewLocation, RepositoryManifest.ManifestFileInfo otherFileWithNewLocation)
        {
            throw new NotImplementedException();
        }

        public override void CopyManifestInformation(RepositoryProxy otherRepository)
        {
            throw new NotImplementedException();
        }

        public override System.IO.FileInfo GetFile(RepositoryManifest.ManifestFileInfo readFile)
        {
            throw new NotImplementedException();
        }

        public override System.IO.FileInfo CloneFile(RepositoryManifest.ManifestFileInfo copyFile, System.IO.DirectoryInfo copyToDirectory)
        {
            throw new NotImplementedException();
        }


        // Protected

        protected Uri BaseUri { set; get; }
    }
}
