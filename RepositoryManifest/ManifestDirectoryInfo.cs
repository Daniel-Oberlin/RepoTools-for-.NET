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
        /// The name of this directory
        /// </param>
        /// <param name="parentDirectory">
        /// The parent directory
        /// </param>
        public ManifestDirectoryInfo(
            String name,
            ManifestDirectoryInfo parentDirectory) :
            base(name, parentDirectory)
        {
            Files = new Dictionary<string, ManifestFileInfo>();
            Subdirectories = new Dictionary<string, ManifestDirectoryInfo>();
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
        public ManifestDirectoryInfo(
            ManifestDirectoryInfo original,
            ManifestDirectoryInfo parentDirectory) :
            base(original.Name, parentDirectory)
        {
            Files = new Dictionary<string, ManifestFileInfo>();
            Subdirectories = new Dictionary<string, ManifestDirectoryInfo>();

            foreach (String nextDirName in original.Subdirectories.Keys)
            {
                ManifestDirectoryInfo dirClone =
                    new ManifestDirectoryInfo(
                        original.Subdirectories[nextDirName],
                        this);

                Subdirectories.Add(nextDirName, dirClone);
            }

            foreach (String nextFileName in original.Files.Keys)
            {
                ManifestFileInfo fileClone =
                    new ManifestFileInfo(
                        original.Files[nextFileName],
                        this);

                Files.Add(nextFileName, fileClone);
            }
        }

        /// <summary>
        /// The files contained by this directory
        /// </summary>
        public Dictionary<String, ManifestFileInfo> Files { private set; get; }

        /// <summary>
        /// The directories contained by this directory
        /// </summary>
        public Dictionary<String, ManifestDirectoryInfo> Subdirectories { private set; get; }

        /// <summary>
        /// Is the subdirectory empty?
        /// </summary>
        public bool Empty
        {
            get
            {
                return
                    Files.Count == 0 &&
                    Subdirectories.Count == 0;
            }
        }
    }
}
