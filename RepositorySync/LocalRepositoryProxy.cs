using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using RepositoryManifest;


namespace RepositorySync
{
    public class LocalRepositoryProxy : RepositoryProxy
    {

        public LocalRepositoryProxy(
            DirectoryInfo repositoryDirectory)
        {
            // Load manifest
            // Make temporary directory
        }

        public override Manifest Manifest
        {
            get
            {
                throw new NotImplementedException();
            }
            protected set
            {
                throw new NotImplementedException();
            }
        }

        public override ManifestFileInfo AddFile(
            RepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile)
        {
            throw new NotImplementedException();
        }

        public override void ReplaceFile(
            RepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile)
        {
            throw new NotImplementedException();
        }

        public override void RemoveFile(
            ManifestFileInfo removeManifestFile)
        {
            throw new NotImplementedException();
        }

        public override void MoveFile(
            ManifestFileInfo fileToBeMoved,
            RepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation)
        {
            throw new NotImplementedException();
        }

        public override FileInfo GetFileForRead(
            ManifestFileInfo readFile)
        {
            throw new NotImplementedException();
        }

        public override FileInfo MakeFileCopy(
            ManifestFileInfo copyFile,
            DirectoryInfo copyToDirectory)
        {
            throw new NotImplementedException();
        }

        protected DirectoryInfo TempDirectory { set; get; }
    }
}
