using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RepositoryManifest
{
    /// <summary>
    /// Information about a directory in the repository
    /// </summary>
    [Serializable]
    public class ManifestDirectoryInfo : ManifestObjectInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">
        /// Object name
        /// </param>
        public ManifestDirectoryInfo(
            String name,
            ManifestDirectoryInfo parentDirectory) :
            base(name, parentDirectory)
        {
            Files = new Dictionary<string, ManifestFileInfo>();
            Subdirectories = new Dictionary<string, ManifestDirectoryInfo>();
        }

        public Dictionary<String, ManifestFileInfo> Files { private set; get; }

        public Dictionary<String, ManifestDirectoryInfo> Subdirectories { private set; get; }
    }
}
