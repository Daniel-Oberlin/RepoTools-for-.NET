using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RepositoryManifest
{
    /// <summary>
    /// Base clase for manifest object information
    /// </summary>
    [Serializable]
    public class ManifestObjectInfo
    {
        /// <summary>
        /// Constructory
        /// </summary>
        /// <param name="name">
        /// Object name
        /// </param>
        /// <param name="parentDirectory">
        /// Parent directory
        /// </param>
        public ManifestObjectInfo(
            String name,
            ManifestDirectoryInfo parentDirectory)
        {
            Name = name;
            ParentDirectory = parentDirectory;
        }

        public String Name { private set; get; }

        public ManifestDirectoryInfo ParentDirectory { private set; get; }
    }
}
