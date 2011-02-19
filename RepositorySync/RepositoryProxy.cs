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
        public abstract Manifest Manifest { protected set; public get; }


        // Primary methods called by sync

        /// <summary>
        /// Add a file to the repository
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
        public abstract ManifestFileInfo AddFile(
            RepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile);

        /// <summary>
        /// Replace an existing file in the repository
        /// </summary>
        /// <param name="sourceRepository">
        /// The source repository containing the replacing file
        /// </param>
        /// <param name="sourceManifestFile">
        /// The source file which will replace the existing file
        /// </param>
        public abstract void ReplaceFile(
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
        public abstract void MoveFile(
            ManifestFileInfo fileToBeMoved,
            RepositoryProxy otherRepositoryWithNewLocation,
            ManifestFileInfo otherFileWithNewLocation);


        // Secondary methods called by destination repository proxy

        /// <summary>
        /// Get a file for purposes of opening and reading
        /// </summary>
        /// <param name="readFile">
        /// The manifest information for the file to be opened and read
        /// </param>
        /// <returns>
        /// The file to be opened and read
        /// </returns>
        public abstract FileInfo GetFileForRead(
            ManifestFileInfo readFile);

        /// <summary>
        /// Make a copy of a file
        /// </summary>
        /// <param name="copyFile">
        /// The manifest information for the file to be copied
        /// </param>
        /// <param name="copyToDirectory">
        /// The directory to which the file will be copied
        /// </param>
        /// <returns>
        /// The copied file
        /// </returns>
        public abstract FileInfo MakeFileCopy(
            ManifestFileInfo copyFile,
            DirectoryInfo copyToDirectory);
    }
}
