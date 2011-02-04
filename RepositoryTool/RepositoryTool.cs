using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using System.IO;
using System.Security.Cryptography;

using RepositoryManifest;


namespace RepositoryTool
{
    public delegate void WriteLogDelegate(String message);

    public enum Mode
    {
        Create,
        Validate,
        Status,
        Update
    }

    public class RepositoryTool
    {
        public RepositoryTool()
        {
            ShowProgress = false;
          
            NewFiles = new List<FileInfo>();
            ModifiedFiles = new List<ManifestFileInfo>();
            MissingFiles = new List<ManifestFileInfo>();
            DateModifiedFiles = new List<ManifestFileInfo>();

            Clear();
        }

        public void Clear()
        {
            FileAddedCount = 0;
            FileCheckedCount = 0;

            NewFiles.Clear();
            ModifiedFiles.Clear();
            MissingFiles.Clear();
            DateModifiedFiles.Clear();
        }

        public void Execute(
            Manifest manifest,
            Mode mode)
        {
            Clear();

            ExecuteRecursive(
                RootDirectory,
                manifest.RootDirectory,
                mode);
        }

        protected void ExecuteRecursive(
            DirectoryInfo currentDirectoryInfo,
            ManifestDirectoryInfo currentManfestDirInfo,
            Mode mode)
        {
            // Setup data for current directory
            Dictionary<String, FileInfo> fileDict =
                new Dictionary<string,FileInfo>();

            Dictionary<String, DirectoryInfo> dirDict =
                new Dictionary<string, DirectoryInfo>();

            if (currentDirectoryInfo != null)
            {
                FileInfo[] fileList =
                    currentDirectoryInfo.GetFiles();

                foreach (FileInfo nextFileInfo in fileList)
                {
                    fileDict.Add(nextFileInfo.Name, nextFileInfo);
                }

                DirectoryInfo[] dirList =
                    currentDirectoryInfo.GetDirectories();

                foreach (DirectoryInfo nextDirInfo in dirList)
                {
                    dirDict.Add(nextDirInfo.Name, nextDirInfo);
                }
            }


            // Iterate through existing manifest entries
            if (currentManfestDirInfo != null)
            {
                foreach (ManifestFileInfo nextManFileInfo in
                    currentManfestDirInfo.Files.Values)
                {
                    if (ShowProgress)
                    {
                        Write(MakePathString(nextManFileInfo));
                    }

                    if (fileDict.ContainsKey(nextManFileInfo.Name))
                    {
                        FileCheckedCount++;

                        FileInfo nextFileInfo = fileDict[nextManFileInfo.Name];

                        if (nextFileInfo.Length != nextManFileInfo.FileLength)
                        {
                            Write(" [DIFFERENT]");
                            ModifiedFiles.Add(nextManFileInfo);
                        }
                        else if (mode == Mode.Validate ||
                            nextFileInfo.LastWriteTimeUtc != nextManFileInfo.LastModifiedTime)
                        {
                            // TODO: Catch exceptions
                            byte[] hash = ComputeHash(nextFileInfo);

                            if (CompareHash(hash, nextManFileInfo.Hash) == false)
                            {
                                Write(" [DIFFERENT]");
                                ModifiedFiles.Add(nextManFileInfo);
                            }
                            else if (nextFileInfo.LastWriteTimeUtc != nextManFileInfo.LastModifiedTime)
                            {
                                Write(" [DATE]");
                                DateModifiedFiles.Add(nextManFileInfo);
                            }
                        }
                        else
                        {
                            Write(" [SKIPPED]");
                        }
                    }
                    else
                    {
                        Write(" [MISSING]");
                        MissingFiles.Add(nextManFileInfo);
                    }

                    WriteLine("");
                }

                foreach (ManifestDirectoryInfo nextManDirInfo in
                    currentManfestDirInfo.Subdirectories.Values)
                {
                    DirectoryInfo nextDirInfo = null;
                    if (dirDict.ContainsKey(nextManDirInfo.Name))
                    {
                        nextDirInfo = dirDict[nextManDirInfo.Name];
                    }

                    ExecuteRecursive(
                        nextDirInfo,
                        nextManDirInfo,
                        mode);
                }
            }


            // Look for new files
            foreach (FileInfo nextFileInfo in fileDict.Values)
            {
                if (currentManfestDirInfo == null ||
                    currentManfestDirInfo.Files.ContainsKey(nextFileInfo.Name) == false)
                {
                    Write(MakePathString(nextFileInfo));

                    if (IgnoreFile(MakePathString(nextFileInfo)))
                    {
                        Write(" [IGNORED]");
                    }
                    else
                    {
                        NewFiles.Add(nextFileInfo);
                        Write(" [NEW]");

                        if (mode == Mode.Create)
                        {
                            ManifestFileInfo newManFileInfo =
                                new ManifestFileInfo(
                                    nextFileInfo.Name,
                                    currentManfestDirInfo);

                            newManFileInfo.FileLength =
                                nextFileInfo.Length;

                            newManFileInfo.LastModifiedTime =
                                nextFileInfo.LastWriteTimeUtc;

                            // TODO: Catch exceptions
                            newManFileInfo.Hash = ComputeHash(nextFileInfo);

                            currentManfestDirInfo.Files.Add(
                                nextFileInfo.Name,
                                newManFileInfo);
                        }
                    }

                    WriteLine("");
                }
            }


            // Recurse looking for new directories
            foreach (DirectoryInfo nextDirInfo in dirDict.Values)
            {
                ManifestDirectoryInfo nextManDirInfo = null;

                bool newDirectory =
                    currentManfestDirInfo.Subdirectories.ContainsKey(nextDirInfo.Name) == false;

                if (mode == Mode.Create)
                {
                    nextManDirInfo = new ManifestDirectoryInfo(
                        nextDirInfo.Name,
                        currentManfestDirInfo);

                    currentManfestDirInfo.Subdirectories.Add(
                        nextManDirInfo.Name,
                        nextManDirInfo);
                }

                if (currentManfestDirInfo == null || newDirectory)
                {
                    ExecuteRecursive(
                        nextDirInfo,
                        nextManDirInfo,
                        mode);
                }
            }
        }



        // Helper methods

        public void WriteLine(String message)
        {
            Write(message + "\r\n");
        }

        public void Write(String message)
        {
            if (ShowProgress && WriteLogDelegate != null)
            {
                WriteLogDelegate.Invoke(message);
            }
        }

        public static String MakePathString(ManifestFileInfo fileInfo)
        {
            return MakePathString(fileInfo.ParentDirectory) + fileInfo.Name;
        }

        public static String MakePathString(ManifestDirectoryInfo directoryInfo)
        {
            String pathString = directoryInfo.Name + PathDelimeterString;

            if (directoryInfo.ParentDirectory != null)
            {
                pathString = MakePathString(directoryInfo.ParentDirectory) + pathString;
            }

            return pathString;
        }

        public String MakePathString(FileInfo fileInfo)
        {
            return MakePathString(fileInfo.Directory) + fileInfo.Name;
        }

        public String MakePathString(DirectoryInfo directoryInfo)
        {
            String pathString;
            if (directoryInfo.FullName != RootDirectory.FullName)
            {
                pathString = directoryInfo.Name + PathDelimeterString;

                pathString =
                    MakePathString(directoryInfo.Parent) +
                    pathString;
            }
            else
            {
                pathString = "." + PathDelimeterString;
            }

            return pathString;
        }

        protected byte[] ComputeHash(FileInfo file)
        {
            Stream fileStream =
                file.Open(FileMode.Open, FileAccess.Read);

            SHA256 hashFunction = new SHA256Managed();

            byte[] hash = hashFunction.ComputeHash(fileStream);

            fileStream.Close();

            return hash;
        }

        protected bool CompareHash(byte[] hash1, byte[] hash2)
        {
            if (hash1 == null || hash2 == null)
            {
                return false;
            }

            Debug.Assert(hash1.Count() == hash2.Count());

            if (hash1.Count() != hash2.Count())
            {
                return false;
            }

            for (int i = 0; i < hash1.Count(); i++ )
            {
                if (hash1[i] != hash2[i])
                {
                    return false;
                }
            }

            return true;
        }

        protected String MakeHashString(byte[] hash)
        {
            String hashString = "";
            foreach (Byte nextByte in hash)
            {
                hashString += String.Format("{0,2:X2}", nextByte);
            }

            return hashString;
        }

        protected bool IgnoreFile(String fileName)
        {
            if (fileName == ManifestFilePath)
            {
                return true;
            }

            // TODO: Allow for other filters

            return false;
        }



        // Data members and accessors

        public WriteLogDelegate WriteLogDelegate { set; get; } 
        public DirectoryInfo RootDirectory { set; get; }

        public bool ShowProgress { set; get; }

        public int FileAddedCount { private set; get; }
        public int FileCheckedCount { private set; get; }

        public List<FileInfo> NewFiles { private set; get; }
        public List<ManifestFileInfo> ModifiedFiles { private set; get; }
        public List<ManifestFileInfo> MissingFiles { private set; get; }
        public List<ManifestFileInfo> DateModifiedFiles { private set; get; }



        // Static

        public static String ManifestFileName;
        public static String PathDelimeterString;
        public static String ManifestFilePath;

        static RepositoryTool()
        {
            // TODO: Fix later for different platforms
            ManifestFileName = ".repositoryManifest";
            PathDelimeterString = "\\";
            ManifestFilePath = "." + PathDelimeterString + ManifestFileName;
        }
    }
}
