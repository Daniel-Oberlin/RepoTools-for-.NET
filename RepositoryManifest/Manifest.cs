using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Runtime.InteropServices;


namespace RepositoryManifest
{
    /// <summary>
    /// The contents of a repository
    /// </summary>
    [Serializable]
    public class Manifest
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public Manifest() :
            this(Guid.NewGuid(), new ManifestDirectoryInfo(".", null))
        {
        }

        /// <summary>
        /// Constructory
        /// </summary>
        /// <param name="guid">
        /// Unique identifier for repository listed by this manifest
        /// </param>
        /// <param name="rootDirectory">
        /// Top level directory in the manifest
        /// </param>
        public Manifest(
            Guid guid,
            ManifestDirectoryInfo rootDirectory)
        {
            Guid = guid;
            RootDirectory = rootDirectory;
        }

        /// <summary>
        /// Unique identifier for this repository
        /// </summary>
        public Guid Guid { private set; get; }

        /// <summary>
        /// The containing directory for the repository
        /// </summary>
        public ManifestDirectoryInfo RootDirectory { private set; get; }

        /// <summary>
        /// Read a manifest from a disk file
        /// </summary>
        /// <param name="manifestFilePath">
        /// The path to the manifest file
        /// </param>
        /// <returns>
        /// The manifest
        /// </returns>
        public static Manifest ReadManifestFile(string manifestFilePath)
        {
            FileStream fileStream =
                new FileStream(manifestFilePath, FileMode.Open);

            BinaryFormatter formatter =
                new BinaryFormatter();

            Manifest manifest = null;

            try
            {
                manifest = (Manifest)
                    formatter.Deserialize(fileStream);
            }
            finally
            {
                fileStream.Close();
            }

            return manifest;
        }

        public void WriteManifestFile(string manifestFilePath)
        {
            FileStream fileStream =
                new FileStream(manifestFilePath, FileMode.Create);

            BinaryFormatter formatter =
                new BinaryFormatter();

            try
            {
                formatter.Serialize(fileStream, this);
            }
            catch (Exception ex)
            {
                File.Delete(manifestFilePath);
                throw ex;
            }
            finally
            {
                fileStream.Close();
            }
        }
    }
}
