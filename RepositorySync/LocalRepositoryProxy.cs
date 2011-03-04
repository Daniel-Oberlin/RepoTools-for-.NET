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

            string manifestFilePath =
                Path.Combine(
                    RootDirectory.FullName,
                    Manifest.DefaultManifestFileName);
            try
            {
                Manifest = Manifest.ReadManifestFile(manifestFilePath);
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

        // TODO: DISPOSE of temp directory!

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

            if (File.Exists(newFilePath))
            {
                File.Delete(newFilePath);
            }

            fileCopy.MoveTo(newFilePath);

            // TODO: Check hashcode?

            ManifestFileInfo newFileInfo =
                Manifest.PutFileFromOtherManifest(
                    sourceManifestFile);

            return newFileInfo;
        }

        public override void RemoveFile(
            ManifestFileInfo removeManifestFile)
        {
            String removeFilePath = MakeNativePath(removeManifestFile);

            File.Delete(removeFilePath);

            removeManifestFile.ParentDirectory.Files.Remove(
                removeManifestFile.Name);
        }

        public override ManifestFileInfo MoveFile(
            ManifestFileInfo fileToBeMoved,
            RepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation)
        {
            String oldFilePath = MakeNativePath(fileToBeMoved);
            String newFilePath = MakeNativePath(otherFileWithNewLocation);

            File.Move(oldFilePath, newFilePath);

            fileToBeMoved.ParentDirectory.Files.Remove(
                fileToBeMoved.Name);

            ManifestFileInfo newFileInfo =
                Manifest.PutFileFromOtherManifest(
                    otherFileWithNewLocation);

            return newFileInfo;
        }

        public override ManifestFileInfo CopyFile(
            ManifestFileInfo fileToBeCopied,
            RepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation)
        {
            String oldFilePath = MakeNativePath(fileToBeCopied);
            String newFilePath = MakeNativePath(otherFileWithNewLocation);

            File.Copy(oldFilePath, newFilePath, true);

            ManifestFileInfo newFileInfo =
                Manifest.PutFileFromOtherManifest(
                    otherFileWithNewLocation);

            return newFileInfo;
        }

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
                    Manifest.MakeHashString(
                        copyFile.Hash));

            File.Copy(originalFilePath, copyFilePath);

            return new FileInfo(copyFilePath);
        }

        protected String MakeNativePath(ManifestFileInfo file)
        {
            return Path.Combine(
                RootDirectory.FullName,
                Manifest.MakeNativePathString(file));
        }

        protected DirectoryInfo RootDirectory { private set; get; }
        protected DirectoryInfo TempDirectory { private set; get; }
    }
}
