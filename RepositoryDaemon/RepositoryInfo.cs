using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RepositoryDaemon
{
    [Serializable]
    public class RepositoryInfo
    {
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
        /// The path to the repository directory
        /// </summary>
        public String RepositoryPath { private set; get; }

        /// <summary>
        /// The path to the repository manifest file
        /// </summary>
        public String ManifestPath { private set; get; }
    }
}
