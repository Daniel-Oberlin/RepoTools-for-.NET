using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RepositoryDaemon
{
    /// <summary>
    /// Information about a repository that is registered with the daemon
    /// </summary>
    [Serializable]
    public class RepositoryInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="guid">
        /// Repository GUID
        /// </param>
        /// <param name="name">
        /// Repository Name
        /// </param>
        /// <param name="repositoryPath">
        /// Full path to the repository directory
        /// </param>
        /// <param name="manifestPath">
        /// Full path to the repository manifest
        /// </param>
        public RepositoryInfo(
            Guid guid,
            String name,
            String repositoryPath,
            String manifestPath)
        {
            Guid = guid;
            Name = name;
            RepositoryPath = repositoryPath;
            ManifestPath = manifestPath;
        }

        /// <summary>
        /// Unique identifier for this repository
        /// </summary>
        public Guid Guid { private set; get; }

        /// <summary>
        /// The name of the repository
        /// </summary>
        public String Name { private set; get; }

        /// <summary>
        /// The full path to the repository directory
        /// </summary>
        public String RepositoryPath { private set; get; }

        /// <summary>
        /// The full path to the repository manifest file
        /// </summary>
        public String ManifestPath { private set; get; }
    }
}
