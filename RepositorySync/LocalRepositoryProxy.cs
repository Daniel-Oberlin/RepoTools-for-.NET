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
    public class LocalRepositoryProxy : LocalRepositoryState, IRepositoryProxy
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="repositoryDirectory">
        /// The root directory where the manifest can be found
        /// </param>
        public LocalRepositoryProxy(
            DirectoryInfo rootDirectory) :
            base(rootDirectory)
        {
        }


        // Implement IRepositoryProxy
        
        public ManifestFileInfo PutFile(
            IRepositoryProxy sourceRepository,
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

        public void RemoveFile(ManifestFileInfo removeManifestFile)
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

        public ManifestFileInfo MoveFile(
            ManifestFileInfo fileToBeMoved,
            IRepositoryProxy otherRepositoryWithNewLocation,
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

        public ManifestFileInfo CopyFile(
            ManifestFileInfo fileToBeCopied,
            IRepositoryProxy otherRepositoryWithNewLocation,
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

        public void CopyManifestInformation(IRepositoryProxy otherRepository)
        {
            Manifest.CopyManifestInfoFrom(
                otherRepository.Manifest);

            ManifestChanged = true;
        }


        // Support methods called by destination repository proxy

        public FileInfo GetFile(ManifestFileInfo readFile)
        {
            String filePath = MakeNativePath(readFile);
            return new FileInfo(filePath);
        }

        public FileInfo CloneFile(
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

        protected void SetFileDates(ManifestFileInfo file)
        {
            FileInfo fileInfo =
                new FileInfo(MakeNativePath(file));

            fileInfo.LastWriteTimeUtc = file.LastModifiedUtc;
        }
    }
}
