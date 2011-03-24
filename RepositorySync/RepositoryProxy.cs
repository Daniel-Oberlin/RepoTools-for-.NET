using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using RepositoryManifest;


namespace RepositorySync
{
    /// <summary>
    /// Abstraction for a repository proxy
    /// </summary>
    public abstract class RepositoryProxy
    {
        /// <summary>
        /// The manifest for this repository
        /// </summary>
        public abstract Manifest Manifest { protected set; get; }


        // Primary methods called by sync

        /// <summary>
        /// Put a file into the repository - possibly replacing an existing file
        /// </summary>
        /// <param name="sourceRepository">
        /// The source repository containing the file to be added
        /// </param>
        /// <param name="sourceManifestFile">
        /// The source file to be added
        /// </param>
        /// <returns>
        /// The file that was added
        /// </returns>
        public abstract ManifestFileInfo PutFile(
            RepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile);

        /// <summary>
        /// Remove a file from this repository
        /// </summary>
        /// <param name="removeManifestFile">
        /// The file to be removed
        /// </param>
        public abstract void RemoveFile(
            ManifestFileInfo removeManifestFile);

        /// <summary>
        /// Copy a file in this repository
        /// </summary>
        /// <param name="fileToBeCopied">
        /// File in this repository to be copied
        /// </param>
        /// <param name="otherRepositoryWithNewLocation">
        /// Other repository where the file is in the new location
        /// </param>
        /// <param name="otherFileWithNewLocation">
        /// File in the other repository which is residing in the new location
        /// </param>
        /// <returns>
        /// The copied file
        /// </returns>
        public abstract ManifestFileInfo CopyFile(
            ManifestFileInfo fileToBeCopied,
            RepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation);

        /// <summary>
        /// Move a file in this repository
        /// </summary>
        /// <param name="fileToBeMoved">
        /// File in this repository to be moved
        /// </param>
        /// <param name="otherRepositoryWithNewLocation">
        /// Other repository where the file is in the new location
        /// </param>
        /// <param name="otherFileWithNewLocation">
        /// File in the other repository which is residing in the new location
        /// </param>
        /// <returns>
        /// The moved file
        /// </returns>
        public abstract ManifestFileInfo MoveFile(
            ManifestFileInfo fileToBeMoved,
            RepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation);

        /// <summary>
        /// Copy manifest information such as name, description, etc.
        /// </summary>
        /// <param name="otherRepository">
        /// The repository from which to copy the infomration
        /// </param>
        public abstract void CopyManifestInformation(
            RepositoryProxy otherRepository);

        // Secondary methods called by destination repository proxy

        /// <summary>
        /// Get a file (or a copy of the file) for purposes of reading
        /// </summary>
        /// <param name="readFile">
        /// The manifest information for the file
        /// </param>
        /// <returns>
        /// The file
        /// </returns>
        public abstract FileInfo GetFile(
            ManifestFileInfo readFile);

        /// <summary>
        /// Clone a file for purposes of adding to another repository
        /// </summary>
        /// <param name="copyFile">
        /// The manifest information for the file to be cloned
        /// </param>
        /// <param name="copyToDirectory">
        /// The directory which will contain the clone
        /// </param>
        /// <returns>
        /// The cloned file
        /// </returns>
        public abstract FileInfo CloneFile(
            ManifestFileInfo copyFile,
            DirectoryInfo copyToDirectory);
    }
}
