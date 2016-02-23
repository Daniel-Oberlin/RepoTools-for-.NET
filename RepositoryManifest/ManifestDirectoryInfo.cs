using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;

namespace RepositoryManifest
{
    /// <summary>
    /// Information about a directory in the repository
    /// </summary>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
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
            filesStore = null;

            Subdirectories = new Dictionary<string, ManifestDirectoryInfo>();
            subdirectoriesStore = null;
        }

        /// <summary>
        /// Default constructor needed for json.NET
        /// </summary>
        public ManifestDirectoryInfo() :
            base("", null)
        {
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
            filesStore = null;

            Subdirectories = new Dictionary<string, ManifestDirectoryInfo>();
            subdirectoriesStore = null;

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
        [JsonProperty]
        public Dictionary<String, ManifestFileInfo> Files { private set; get; }

        /// <summary>
        /// This is used for serialization - see the DictionaryStore class
        /// </summary>
        private DictionaryStore<String, ManifestFileInfo> filesStore;

        /// <summary>
        /// The directories contained by this directory
        /// </summary>
        [JsonProperty]
        public Dictionary<String, ManifestDirectoryInfo> Subdirectories { private set; get; }

        /// <summary>
        /// This is used for serialization - see the DictionaryStore class
        /// </summary>
        private DictionaryStore<String, ManifestDirectoryInfo> subdirectoriesStore;

        /// <summary>
        /// Move the contents of the Dictionary objects into DictionaryStore
        /// objects - recursive method.
        /// </summary>
        public void SaveToStore()
        {
            foreach (ManifestDirectoryInfo nextDir in Subdirectories.Values)
            {
                nextDir.SaveToStore();
            }

            filesStore =
                new DictionaryStore<string, ManifestFileInfo>(Files);

            Files = null;

            subdirectoriesStore =
                new DictionaryStore<string, ManifestDirectoryInfo>(Subdirectories);

            Subdirectories = null;
        }

        /// <summary>
        /// Move the contents of the DictionaryStore objects back into
        /// Dictionary objects - recursive method.
        /// </summary>
        public void RestoreFromStore()
        {
            if (subdirectoriesStore != null)
            {
                Subdirectories = subdirectoriesStore.getDictionary();
                subdirectoriesStore = null;

                Files = filesStore.getDictionary();
                filesStore = null;

                foreach (ManifestDirectoryInfo nextDir in Subdirectories.Values)
                {
                    nextDir.RestoreFromStore();
                }
            }
        }

        /// <summary>
        /// Clear references to parent directories so that there are no loops
        /// during JSON serialization.
        /// </summary>
        public void ClearParentReferences()
        {
            foreach (ManifestFileInfo nextFile in Files.Values)
            {
                nextFile.ParentDirectory = null;
            }

            foreach (ManifestDirectoryInfo nextDir in Subdirectories.Values)
            {
                nextDir.ParentDirectory = null;
                nextDir.ClearParentReferences();
            }
        }

        /// <summary>
        /// Restore references to parent directories during JSON
        /// deserialization.
        /// </summary>
        public void RestoreParentReferences()
        {
            foreach (ManifestFileInfo nextFile in Files.Values)
            {
                nextFile.ParentDirectory = this;
            }

            foreach (ManifestDirectoryInfo nextDir in Subdirectories.Values)
            {
                nextDir.ParentDirectory = this;
                nextDir.RestoreParentReferences();
            }
        }

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
