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
       public Manifest()
        {
            Guid = Guid.NewGuid();
            RootDirectory = new ManifestDirectoryInfo(".", null);
            IgnoreList = new List<string>();
            InceptionDateUtc = DateTime.UtcNow;
            DefaultHashMethod = null;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="original">
        /// The original object
        /// </param>
       public Manifest(Manifest original)
       {
           Guid = original.Guid;
           RootDirectory = new ManifestDirectoryInfo(RootDirectory, null);
           Name = original.Name;
           Description = original.Description;
           InceptionDateUtc = original.InceptionDateUtc;
           LastUpdateDateUtc = original.LastUpdateDateUtc;
           IgnoreList = new List<string>(original.IgnoreList);
           DefaultHashMethod = original.DefaultHashMethod;
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
        /// The name of the repository
        /// </summary>
        public String Name { set; get; }

        /// <summary>
        /// A description of the contents of the repository
        /// </summary>
        public String Description { set; get; }

        /// <summary>
        /// The UTC DateTime when the repository was first created
        /// </summary>
        public DateTime InceptionDateUtc { set; get; }

        /// <summary>
        /// The UTC DateTime when the repository was last updated
        /// </summary>
        public DateTime LastUpdateDateUtc { set; get; }

        /// <summary>
        /// A list of regular expressions for filenames to ignore
        /// </summary>
        public List<String> IgnoreList { private set; get; }

        /// <summary>
        /// The name of the default hash method being used by the repository
        /// </summary>
        public String DefaultHashMethod { set; get; }

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

        /// <summary>
        /// Write a manifest to a disk file
        /// </summary>
        /// <param name="manifestFilePath">
        /// The path to the manifest file
        /// </param>
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

        public Int64 CountFiles()
        {
            return CountFilesRecursive(RootDirectory);
        }

        protected Int64 CountFilesRecursive(ManifestDirectoryInfo currentDir)
        {
            Int64 fileCount = 0;

            foreach (ManifestFileInfo nextFileInfo in
                currentDir.Files.Values)
            {
                fileCount++;
            }

            foreach (ManifestDirectoryInfo nextDirInfo in
                currentDir.Subdirectories.Values)
            {
                fileCount += CountFilesRecursive(nextDirInfo);
            }

            return fileCount;
        }

        public Int64 CountBytes()
        {
            return CountBytesRecursive(RootDirectory);
        }

        protected Int64 CountBytesRecursive(ManifestDirectoryInfo currentDir)
        {
            Int64 byteCount = 0;

            foreach (ManifestFileInfo nextFileInfo in
                currentDir.Files.Values)
            {
                byteCount += nextFileInfo.FileLength;
            }

            foreach (ManifestDirectoryInfo nextDirInfo in
                currentDir.Subdirectories.Values)
            {
                byteCount += CountBytesRecursive(nextDirInfo);
            }

            return byteCount;
        }
    }
}
