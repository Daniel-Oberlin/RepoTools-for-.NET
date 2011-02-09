using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RepositoryManifest
{
    /// <summary>
    /// Information about a file in the repository
    /// </summary>
    [Serializable]
    public class ManifestFileInfo : ManifestObjectInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">
        /// The file name
        /// </param>
        /// <param name="parentDirectory">
        /// The parent directory
        /// </param>
        public ManifestFileInfo(
            String name,
            ManifestDirectoryInfo parentDirectory) :
            base(name, parentDirectory)
        {
            Hash = null;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="original">
        /// The original object
        /// </param>
        /// <param name="parentDirectory">
        /// The parent directory
        /// </param>
        public ManifestFileInfo(
            ManifestFileInfo original,
            ManifestDirectoryInfo parentDirectory) :
            base(original.Name, parentDirectory)
        {
            FileLength = original.FileLength;
            LastModifiedUtc = original.LastModifiedUtc;
            Hash = (byte[]) original.Hash.Clone();
        }

        /// <summary>
        /// The length of this file in bytes
        /// </summary>
        public Int64 FileLength { set; get; }

        /// <summary>
        /// The time that this file was last mofified according to the filesystem
        /// </summary>
        public DateTime LastModifiedUtc { set; get; }

        /// <summary>
        /// The SHA256 hash of the file data
        /// </summary>
        public byte[] Hash { set; get; }

        /// <summary>
        /// The name of the hash algorithm used
        /// </summary>
        public String HashType { set; get; }
    }
}
