using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            RootDirectory = new ManifestDirectoryInfo(
                original.RootDirectory, null);

            Name = original.Name;
            Description = original.Description;
            InceptionDateUtc = original.InceptionDateUtc;
            LastUpdateDateUtc = original.LastUpdateDateUtc;
            IgnoreList = new List<string>(original.IgnoreList);
            DefaultHashMethod = original.DefaultHashMethod;
        }

        /// <summary>
        /// Make a new manifest, cloning some default properties in the
        /// prototype
        /// </summary>
        /// <returns>
        /// The new manifest.
        /// </returns>
        public Manifest CloneFromPrototype()
        {
            // Make a copy
            Manifest clone = new Manifest(this);

            // Except for...
            clone.Guid = Guid.NewGuid();
            clone.InceptionDateUtc = DateTime.UtcNow;
            clone.LastUpdateDateUtc = new DateTime();

            return clone;
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

        /// <summary>
        /// Put an entry into this manifest corresponding to an entry in
        /// another manifest.  Will replace an existing entry.
        /// </summary>
        /// <param name="otherManifestFile">
        /// The other manifest file to be put into this ]manifest
        /// </param>
        /// <returns>
        /// The new file in this manifest
        /// </returns>
        public ManifestFileInfo PutFileFromOtherManifest(
            ManifestFileInfo otherManifestFile)
        {
            Stack<ManifestDirectoryInfo> stack =
                new Stack<ManifestDirectoryInfo>();

            ManifestDirectoryInfo nextParentOther =
                otherManifestFile.ParentDirectory;

            while (nextParentOther != null)
            {
                stack.Push(nextParentOther);
                nextParentOther = nextParentOther.ParentDirectory;
            }

            // Start in root directory
            ManifestDirectoryInfo currentParentThis = RootDirectory;

            // Pop the root directory since we're starting there
            stack.Pop();

            while (stack.Count > 0)
            {
                nextParentOther = stack.Pop();

                if (currentParentThis.Subdirectories.Keys.Contains(
                    nextParentOther.Name))
                {
                    currentParentThis = currentParentThis.Subdirectories[
                        nextParentOther.Name];
                }
                else
                {
                    ManifestDirectoryInfo newParent =
                        new ManifestDirectoryInfo(
                            nextParentOther.Name,
                            currentParentThis);

                    currentParentThis.Subdirectories[nextParentOther.Name] =
                        newParent;

                    currentParentThis = newParent;
                }
            }

            ManifestFileInfo newManifestFile =
                new ManifestFileInfo(
                    otherManifestFile,
                    currentParentThis);

            currentParentThis.Files[newManifestFile.Name] =
                newManifestFile;

            return newManifestFile;
        }

        /// <summary>
        /// Count the number of files in the manifest
        /// </summary>
        /// <returns>
        /// The number of files in the manifest
        /// </returns>
        public Int64 CountFiles()
        {
            return CountFilesRecursive(RootDirectory);
        }

        /// <summary>
        /// Recursive helper function to count the files
        /// </summary>
        /// <param name="currentDir">
        /// Current directory in the recursion
        /// </param>
        /// <returns>
        /// Number of files below the current directory
        /// </returns>
        protected Int64 CountFilesRecursive(ManifestDirectoryInfo currentDir)
        {
            Int64 fileCount = currentDir.Files.Count;

            foreach (ManifestDirectoryInfo nextDirInfo in
                currentDir.Subdirectories.Values)
            {
                fileCount += CountFilesRecursive(nextDirInfo);
            }

            return fileCount;
        }

        /// <summary>
        /// Count the number of bytes stored in the repository
        /// </summary>
        /// <returns>
        /// The number of bytes stored in the repository
        /// </returns>
        public Int64 CountBytes()
        {
            return CountBytesRecursive(RootDirectory);
        }

        /// <summary>
        /// Recursive helper to count the number of bytes
        /// </summary>
        /// <param name="currentDir">
        /// Current directory in the recursion
        /// </param>
        /// <returns>
        /// The number of bytes stored in the current directory
        /// </returns>
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


        // Static

        /// <summary>
        /// The default filename for a manifest
        /// </summary>
        public static String DefaultManifestFileName;

        /// <summary>
        /// The path delimeter string for standard pathnames
        /// </summary>
        public static String StandardPathDelimeterString;

        /// <summary>
        /// Static constructor
        /// </summary>
        static Manifest()
        {
            DefaultManifestFileName = ".repositoryManifest";
            StandardPathDelimeterString = "/";
        }

        /// <summary>
        /// Make a standard UNIX-style relative path, which will not vary
        /// across platforms.
        /// </summary>
        /// <param name="fileInfo">
        /// The file whose path will be generated
        /// </param>
        /// <returns>
        /// The path
        /// </returns>
        public static String MakeStandardPathString(ManifestFileInfo fileInfo)
        {
            return MakeStandardPathString(fileInfo.ParentDirectory) + fileInfo.Name;
        }

        /// <summary>
        /// Make a standard UNIX-style relative path, which will not vary
        /// across platforms.
        /// </summary>
        /// <param name="directoryInfo">
        /// The directory whose path will be generated
        /// </param>
        /// <returns>
        /// The path
        /// </returns>
        public static String MakeStandardPathString(ManifestDirectoryInfo directoryInfo)
        {
            String pathString = directoryInfo.Name + StandardPathDelimeterString;

            if (directoryInfo.ParentDirectory != null)
            {
                pathString = MakeStandardPathString(directoryInfo.ParentDirectory) + pathString;
            }

            return pathString;
        }

        /// <summary>
        /// Make a platform-specific relative path
        /// </summary>
        /// <param name="fileInfo">
        /// The file whose path will be generated
        /// </param>
        /// <returns>
        /// The path
        /// </returns>
        public static String MakeNativePathString(ManifestFileInfo fileInfo)
        {
            return Path.Combine(
                MakeNativePathString(fileInfo.ParentDirectory),
                fileInfo.Name);
        }

        /// <summary>
        /// Make a platform-specific relative path
        /// </summary>
        /// <param name="directoryInfo">
        /// The directory whose path will be generated
        /// </param>
        /// <returns>
        /// The path
        /// </returns>
        public static String MakeNativePathString(ManifestDirectoryInfo directoryInfo)
        {
            String pathString = directoryInfo.Name;

            if (directoryInfo.ParentDirectory != null)
            {
                pathString = Path.Combine(
                    MakeNativePathString(directoryInfo.ParentDirectory),
                    pathString);
            }

            return pathString;
        }

        /// <summary>
        /// Make a hexadecimal string representing a hashcode
        /// </summary>
        /// <param name="hash">
        /// The hashcode
        /// </param>
        /// <returns>
        /// The string
        /// </returns>
        public static String MakeHashString(byte[] hash)
        {
            String hashString = "";
            foreach (Byte nextByte in hash)
            {
                hashString += String.Format("{0,2:X2}", nextByte);
            }

            return hashString;
        }
    }
}
