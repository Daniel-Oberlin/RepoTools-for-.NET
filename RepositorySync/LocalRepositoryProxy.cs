using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using RepositoryManifest;
using Utilities;


namespace RepositorySync
{
    /// <summary>
    /// Implementation of RepositoryProxy for a local filesystem.
    /// </summary>
    public class LocalRepositoryProxy :
        LocalRepositoryState,
        IRepositoryProxy
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="repositoryDirectory">
        /// The root directory where the manifest can be found
        /// </param>
        /// <param name="readOnly">
        /// Specify if this repository is to be used in read-only mode.
        /// </param>
        public LocalRepositoryProxy(
            DirectoryInfo rootDirectory,
            bool readOnly) :
            base(rootDirectory, readOnly)
        {
        }


        // Implement IRepositoryProxy
        
        public void PutFile(
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

            SetManifestChanged();
        }

        public void RemoveFile(ManifestFileInfo removeManifestFile)
        {
            String removeFilePath = MakeNativePath(removeManifestFile);

            File.Delete(removeFilePath);

            RemoveEmptyParentDirectories(removeFilePath);

            removeManifestFile.ParentDirectory.Files.Remove(
                removeManifestFile.Name);

            SetManifestChanged();
        }

        public void MoveFile(
            ManifestFileInfo fileToBeMoved,
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

            RemoveEmptyParentDirectories(oldFilePath);

            fileToBeMoved.ParentDirectory.Files.Remove(
                fileToBeMoved.Name);

            ManifestFileInfo newFileInfo =
                Manifest.PutFileFromOtherManifest(
                    otherFileWithNewLocation);

            SetFileDates(newFileInfo);

            SetManifestChanged();
        }

        public void CopyFile(
            ManifestFileInfo fileToBeCopied,
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

            SetManifestChanged();
        }

        public void CopyFileInformation(
            ManifestFileInfo fileToBeUpdated,
            ManifestFileInfo otherFileWithNewFileInfo)
        {
            fileToBeUpdated.LastModifiedUtc =
                otherFileWithNewFileInfo.LastModifiedUtc;

            fileToBeUpdated.RegisteredUtc =
                otherFileWithNewFileInfo.RegisteredUtc;

            SetFileDates(fileToBeUpdated);

            SetManifestChanged();
        }

        public void CopyManifestInformation(IRepositoryProxy otherRepository)
        {
            Manifest.CopyManifestInfoFrom(
                otherRepository.Manifest);

            SetManifestChanged();
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

        protected void RemoveEmptyParentDirectories(String removeFilePath)
        {
            String directoryPath = Path.GetDirectoryName(removeFilePath);
            DirectoryInfo directory = new DirectoryInfo(directoryPath);

            while (directory.GetDirectories().Count() == 0 &&
                directory.GetFiles().Count() == 0)
            {
                DirectoryInfo deleteDir = directory;
                directory = directory.Parent;

                deleteDir.Delete();
            }
        }


        // Static
        
        static public void SeedLocalRepository(
            Manifest sourceManifest,
            String seedDirectoryPath,
            // TODO: Use delegate for console like in other classes?
            Utilities.Console console)
        {
            String newManifestFilePath =
                PathUtilities.NativeFromNativeAndStandard(
                    seedDirectoryPath,
                    Manifest.DefaultManifestStandardFilePath);

            if (System.IO.File.Exists(newManifestFilePath))
            {
                console.WriteLine("Destination manifest already exists.");
                Environment.Exit(1);
            }

            if (Directory.Exists(seedDirectoryPath) == false)
            {
                try
                {
                    Directory.CreateDirectory(seedDirectoryPath);
                }
                catch (Exception e)
                {
                    console.WriteLine("Exception: " + e.Message);
                    console.WriteLine("Could not create destination directory.");
                    Environment.Exit(1);
                }
            }

            Manifest seedManifest = new Manifest(sourceManifest);

            seedManifest.RootDirectory.Files.Clear();
            seedManifest.RootDirectory.Subdirectories.Clear();
            seedManifest.LastUpdateDateUtc = new DateTime();

            try
            {
                seedManifest.WriteManifestFile(newManifestFilePath);
            }
            catch (Exception e)
            {
                console.WriteLine("Exception: " + e.Message);
                console.WriteLine("Could not write destination manifest.");
                Environment.Exit(1);
            }
        }
    }
}
