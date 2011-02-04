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
        public ManifestFileInfo(
            String name,
            ManifestDirectoryInfo parentDirectory) :
            base(name, parentDirectory)
        {
            Hash = null;
        }

        /// <summary>
        /// The length of this file in bytes
        /// </summary>
        public Int64 FileLength { set; get; }

        /// <summary>
        /// The time that this file was last mofified according to the filesystem
        /// </summary>
        public DateTime LastModifiedTime { set; get; }

        /// <summary>
        /// The SHA256 hash of the file data
        /// </summary>
        public byte[] Hash { set; get; }
    }
}
