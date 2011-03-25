using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using RepositoryManifest;


namespace RepositorySync
{
    /// <summary>
    /// Implementation of RepositoryProxy for a local filesystem.
    /// </summary>
    public class LocalRepositoryProxy : RepositoryProxy
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="repositoryDirectory">
        /// The root directory where the manifest can be found
        /// </param>
        public LocalRepositoryProxy(
            DirectoryInfo rootDirectory)
        {
            RootDirectory = rootDirectory;
            ManifestChanged = false;

            ManifestFilePath =
                Path.Combine(
                    RootDirectory.FullName,
                    Manifest.DefaultManifestFileName);
            try
            {
                Manifest = Manifest.ReadManifestFile(ManifestFilePath);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Could not read manifest.",
                    ex);
            }

            try
            {
                TempDirectory = RootDirectory.CreateSubdirectory(
                    Path.GetRandomFileName());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Could not create temporary directory in repository.",
                    ex);
            }
        }

        /// <summary>
        /// Finalizer removes temp directory and writes manifest
        /// </summary>
        ~LocalRepositoryProxy()
        {
            Exception exception = null;

            try
            {
                TempDirectory.Delete(true);
            }
            catch (Exception ex)
            {
                if (exception == null)
                {
                    exception = ex;
                }
            }

            if (ManifestChanged)
            {
                Manifest.LastUpdateDateUtc = DateTime.UtcNow;
                try
                {
                    Manifest.WriteManifestFile(ManifestFilePath);
                }
                catch (Exception ex)
                {
                    // This exception overrides previous
                    exception = new Exception(
                     "Could not write manifest.",
                     ex);
                }
            }

            if (exception != null)
            {
                throw exception;
            }
        }
        
        public override Manifest Manifest { protected set; get; }

        public override ManifestFileInfo PutFile(
            RepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile)
        {
            FileInfo fileCopy =
                sourceRepository.CloneFile(
                    sourceManifestFile,
                    TempDirectory);

            String newFilePath = MakeNativePath(sourceManifestFile);
            String directoryPath = Path.GetDirectoryName(newFilePath);

            if (Directory.Exists(directoryPath) == false)
            {
                Directory.CreateDirectory(directoryPath);
            }

            if (File.Exists(newFilePath))
            {
                File.Delete(newFilePath);
            }

            fileCopy.MoveTo(newFilePath);

            ManifestFileInfo newFileInfo =
                Manifest.PutFileFromOtherManifest(
                    sourceManifestFile);

            SetFileDates(newFileInfo);

            ManifestChanged = true;

            return newFileInfo;
        }

        public override void RemoveFile(
            ManifestFileInfo removeManifestFile)
        {
            String removeFilePath = MakeNativePath(removeManifestFile);

            File.Delete(removeFilePath);

            removeManifestFile.ParentDirectory.Files.Remove(
                removeManifestFile.Name);

            String directoryPath = Path.GetDirectoryName(removeFilePath);
            DirectoryInfo directory = new DirectoryInfo(directoryPath);

            while (directory.GetDirectories().Count() == 0 &&
                directory.GetFiles().Count() == 0)
            {
                DirectoryInfo deleteDir = directory;
                directory = directory.Parent;

                deleteDir.Delete();
            }

            ManifestChanged = true;
        }

        public override ManifestFileInfo MoveFile(
            ManifestFileInfo fileToBeMoved,
            RepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation)
        {
            String oldFilePath = MakeNativePath(fileToBeMoved);
            String newFilePath = MakeNativePath(otherFileWithNewLocation);
            String directoryPath = Path.GetDirectoryName(newFilePath);

            if (Directory.Exists(directoryPath) == false)
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.Move(oldFilePath, newFilePath);

            fileToBeMoved.ParentDirectory.Files.Remove(
                fileToBeMoved.Name);

            ManifestFileInfo newFileInfo =
                Manifest.PutFileFromOtherManifest(
                    otherFileWithNewLocation);

            SetFileDates(newFileInfo);

            ManifestChanged = true;

            return newFileInfo;
        }

        public override ManifestFileInfo CopyFile(
            ManifestFileInfo fileToBeCopied,
            RepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation)
        {
            String oldFilePath = MakeNativePath(fileToBeCopied);
            String newFilePath = MakeNativePath(otherFileWithNewLocation);
            String directoryPath = Path.GetDirectoryName(newFilePath);

            if (Directory.Exists(directoryPath) == false)
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.Copy(oldFilePath, newFilePath, true);

            ManifestFileInfo newFileInfo =
                Manifest.PutFileFromOtherManifest(
                    otherFileWithNewLocation);

            SetFileDates(newFileInfo);

            ManifestChanged = true;

            return newFileInfo;
        }

        public override void CopyManifestInformation(
            RepositoryProxy otherRepository)
        {
            Manifest.CopyManifestInfoFrom(
                otherRepository.Manifest);

            ManifestChanged = true;
        }


        // Secondary methods called by destination repository proxy

        public override FileInfo GetFile(
            ManifestFileInfo readFile)
        {
            String filePath = MakeNativePath(readFile);
            return new FileInfo(filePath);
        }

        public override FileInfo CloneFile(
            ManifestFileInfo copyFile,
            DirectoryInfo copyToDirectory)
        {
            String originalFilePath = MakeNativePath(copyFile);

            // Name the file according to its unique hash code
            String copyFilePath =
                Path.Combine(
                    copyToDirectory.FullName,
                    copyFile.FileHash.ToString());

            File.Copy(originalFilePath, copyFilePath);

            return new FileInfo(copyFilePath);
        }


        // Helper methods

        protected String MakeNativePath(ManifestFileInfo file)
        {
            return Path.Combine(
                RootDirectory.FullName,
                Manifest.MakeNativePathString(file));
        }

        protected void SetFileDates(ManifestFileInfo file)
        {
            FileInfo fileInfo =
                new FileInfo(MakeNativePath(file));

            fileInfo.CreationTimeUtc = file.CreationUtc;
            fileInfo.LastWriteTimeUtc = file.LastModifiedUtc;
        }

        protected string ManifestFilePath { set; get; }
        protected DirectoryInfo RootDirectory { set; get; }
        protected DirectoryInfo TempDirectory { set; get; }

        protected bool ManifestChanged { set; get; }
    }
}
