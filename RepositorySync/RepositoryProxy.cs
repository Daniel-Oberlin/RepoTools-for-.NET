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
    public interface IRepositoryProxy
    {
        /// <summary>
        /// The manifest for this repository
        /// </summary>
        Manifest Manifest { get; }


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
        void PutFile(
            IRepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile);

        /// <summary>
        /// Remove a file from this repository
        /// </summary>
        /// <param name="removeManifestFile">
        /// The file to be removed
        /// </param>
        void RemoveFile(ManifestFileInfo removeManifestFile);

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
        void CopyFile(
            ManifestFileInfo fileToBeCopied,
            IRepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation);

        /// <summary>
        /// Copy file information - anything but content - from another
        /// manifest entry
        /// </summary>
        /// <param name="fileToBeUpdated">
        /// The file in this repository whose information is being updated
        /// </param>
        /// <param name="otherFileWithNewFileInfo">
        /// The file in the other repository with the new information
        /// </param>
        void CopyFileInformation(
            ManifestFileInfo fileToBeUpdated,
            ManifestFileInfo otherFileWithNewFileInfo);

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
        void MoveFile(
            ManifestFileInfo fileToBeMoved,
            IRepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation);

        /// <summary>
        /// Copy manifest information such as name, description, etc.
        /// </summary>
        /// <param name="otherRepository">
        /// The repository from which to copy the infomration
        /// </param>
        void CopyManifestInformation(IRepositoryProxy otherRepository);


        // Support methods called by destination repository proxy

        /// <summary>
        /// Get a file (or a copy of the file) for purposes of reading
        /// </summary>
        /// <param name="readFile">
        /// The manifest information for the file
        /// </param>
        /// <returns>
        /// The file
        /// </returns>
        FileInfo GetFile(ManifestFileInfo readFile);

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
        FileInfo CloneFile(
            ManifestFileInfo copyFile,
            DirectoryInfo copyToDirectory);
    }
}
