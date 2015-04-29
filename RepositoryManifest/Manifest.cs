using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Runtime.InteropServices;

using Utilities;


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

        public static Manifest MakeCleanManifest()
        {
            // Default implementation when there is no prototype
            Manifest manifest = new Manifest();

            manifest.DefaultHashMethod =
                Utilities.CryptUtilities.DefaultHashType;

            manifest.IgnoreList.Add(
                "^" +
                System.Text.RegularExpressions.Regex.Escape(Manifest.DefaultManifestStandardFilePath) +
                "$");

            String tempDirectoryStandardPath = "." +
                StandardPathDelimeterString +
                Utilities.TempDirUtilities.TempDirectoryName +
                StandardPathDelimeterString;

            manifest.IgnoreList.Add(
                "^" +
                System.Text.RegularExpressions.Regex.Escape(tempDirectoryStandardPath));

            return manifest;
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
            InceptionDateUtc = original.InceptionDateUtc;
            LastUpdateDateUtc = original.LastUpdateDateUtc;

            RootDirectory = new ManifestDirectoryInfo(
                original.RootDirectory, null);

            CopyManifestInfoFrom(original);
        }

        /// <summary>
        /// Copy changeable parts of the manifest information
        /// </summary>
        /// <param name="other">
        /// The manifest to copy from
        /// </param>
        public void CopyManifestInfoFrom(Manifest other)
        {
            Name = other.Name;
            Description = other.Description;
            ManifestInfoLastModifiedUtc = other.ManifestInfoLastModifiedUtc;
            IgnoreList = new List<string>(other.IgnoreList);
            DefaultHashMethod = other.DefaultHashMethod;
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
            clone.LastUpdateDateUtc = clone.InceptionDateUtc;
            clone.ManifestInfoLastModifiedUtc = clone.InceptionDateUtc;

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
        /// The UTC DateTime when the repository was last validated
        /// </summary>
        public DateTime LastValidateDateUtc { set; get; }

        /// <summary>
        /// The last time that the manifest information - name, description,
        /// ignores, etc. - was modified.
        /// </summary>
        public DateTime ManifestInfoLastModifiedUtc { set; get; }

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
            FileStream fileStream = File.OpenRead(manifestFilePath);

            return ReadManifestStream(fileStream);
        }

        /// <summary>
        /// Read a manifest from a stream
        /// </summary>
        /// <param name="inputStream">
        /// The input stream
        /// </param>
        /// <returns>
        /// The manifest
        /// </returns>
        public static Manifest ReadManifestStream(Stream inputStream)
        {
            MemoryStream memStream = new MemoryStream();

            // Intermediate step because network streams can be flaky
            // sometimes...
            Utilities.StreamUtilities.CopyStream(inputStream, memStream);
            memStream.Seek(0, SeekOrigin.Begin);

            BinaryFormatter formatter =
                new BinaryFormatter();

            Manifest manifest = null;

            try
            {
                manifest = (Manifest)
                    formatter.Deserialize(memStream);

                manifest.RootDirectory.RestoreFromStore();
                manifest.DoAnyUpgradeMaintenance();
            }
            finally
            {
                inputStream.Close();
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
            String backupManifestFilePath = manifestFilePath + ".bak";

            try
            {
                // Make backup in case we fail midway through writing
                File.Move(manifestFilePath, backupManifestFilePath);
            }
            catch (FileNotFoundException ex)
            {
                // Ignore because the manifest file may not exist.
                // Let any other exceptions get thrown...
            }

            Exception exception = null;
            try
            {
                using (FileStream fileStream = File.Create(manifestFilePath))
                {
                    WriteManifestStream(fileStream);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                if (exception != null)
                {
                    try
                    {
                        // Try to restore the backup...
                        File.Move(backupManifestFilePath, manifestFilePath);
                    }
                    catch (Exception)
                    {
                        // File may not exist.
                        // Also ignore any other exception to throw original instead...
                    }

                    throw exception;
                }
            }

            try
            {
                File.Delete(backupManifestFilePath);
            }
            catch (FileNotFoundException)
            {
                // Backup may not exist.
            }
        }

        /// <summary>
        /// Write a manifest to a stream
        /// </summary>
        /// <param name="stream">
        /// The stream
        /// </param>
        public void WriteManifestStream(Stream stream)
        {
            BinaryFormatter formatter =
                new BinaryFormatter();

            RootDirectory.SaveToStore();
            formatter.Serialize(stream, this);
            RootDirectory.RestoreFromStore();
        }

        /// <summary>
        /// Put an entry into this manifest corresponding to an entry in
        /// another manifest.  Will replace an existing entry.
        /// </summary>
        /// <param name="otherManifestFile">
        /// The other manifest file to be put into this manifest
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
        /// Give this manifest a new GUID
        /// </summary>
        public void ChangeGUID()
        {
            Guid = Guid.NewGuid(); ;
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

        protected void DoAnyUpgradeMaintenance()
        {
            /*
            DateTime blankTime = new DateTime();

            // Move from using byte array to FileHash class
            Stack<ManifestDirectoryInfo> dirStack =
                new Stack<ManifestDirectoryInfo>();

            dirStack.Push(RootDirectory);

            bool upgraded = false;
            while (dirStack.Count > 0)
            {
                ManifestDirectoryInfo currentDir =
                    dirStack.Pop();

                foreach (ManifestFileInfo nextFileInfo in
                    currentDir.Files.Values)
                {
                    // DO SOMETHING
                }

                foreach (ManifestDirectoryInfo nextDir in
                    currentDir.Subdirectories.Values)
                {
                    dirStack.Push(nextDir);
                }
            }

            if (upgraded)
            {
                Console.WriteLine("Manifest upgraded.");
            }
             * */
        }


        // Static

        /// <summary>
        /// The default file path for a manifest
        /// </summary>
        public static String DefaultManifestFileName;

        /// <summary>
        /// The default file path for a manifest
        /// </summary>
        public static String DefaultManifestStandardFilePath;

        /// <summary>
        /// The path delimeter string for standard pathnames
        /// </summary>
        public static String StandardPathDelimeterString;

        /// <summary>
        /// Static constructor
        /// </summary>
        static Manifest()
        {
            StandardPathDelimeterString = "/";

            DefaultManifestFileName = ".repositoryManifest";

            DefaultManifestStandardFilePath = "." +
                StandardPathDelimeterString +
                DefaultManifestFileName;
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
                fileInfo.Name).Normalize();
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
                    pathString).Normalize();
            }

            return pathString;
        }
    }
}
