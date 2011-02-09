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

    public class RepositoryTool
    {
        public RepositoryTool()
        {
            ShowProgress = false;
            Update = false;
            Force = false;
            NewHash = false;
            BackDate = false;

            NewFiles = new List<ManifestFileInfo>();
            NewFilesForClean = new List<FileInfo>();
            ModifiedFiles = new List<ManifestFileInfo>();
            MissingFiles = new List<ManifestFileInfo>();
            DateModifiedFiles = new List<ManifestFileInfo>();
            ErrorFiles = new List<ManifestFileInfo>();

            Clear();
        }

        public void Clear()
        {
            FileCheckedCount = 0;

            NewFiles.Clear();
            NewFilesForClean.Clear();
            ModifiedFiles.Clear();
            MissingFiles.Clear();
            DateModifiedFiles.Clear();
            ErrorFiles.Clear();
        }

        public void DoUpdate()
        {
            Clear();

            UpdateRecursive(
                RootDirectory,
                Manifest.RootDirectory);

            Manifest.LastUpdateDateUtc =
                DateTime.UtcNow;
        }

        protected void UpdateRecursive(
            DirectoryInfo currentDirectoryInfo,
            ManifestDirectoryInfo currentManfestDirInfo)
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
            List<ManifestFileInfo> listClone =
                new List<ManifestFileInfo>(currentManfestDirInfo.Files.Values);

            foreach (ManifestFileInfo nextManFileInfo in listClone)
            {
                if (ShowProgress)
                {
                    Write(MakePathString(nextManFileInfo));
                }

                if (fileDict.ContainsKey(nextManFileInfo.Name))
                {
                    FileCheckedCount++;

                    FileInfo nextFileInfo = fileDict[nextManFileInfo.Name];

                    if (nextFileInfo.Length != nextManFileInfo.FileLength &&
                        Update == false &&
                        Force == false)
                    {
                        Write(" [DIFFERENT]");
                        ModifiedFiles.Add(nextManFileInfo);
                    }
                    else if (Force == true ||
                        nextFileInfo.LastWriteTimeUtc != nextManFileInfo.LastModifiedUtc ||
                        nextFileInfo.Length != nextManFileInfo.FileLength)
                    {
                        byte[] checkHash = null;

                        try
                        {
                            checkHash = ComputeHash(
                                nextFileInfo,
                                nextManFileInfo.HashType);
                        }
                        catch (Exception)
                        {
                            // TODO: More detail?
                        }

                        if (checkHash == null)
                        {
                            Write(" [ERROR]");
                            ErrorFiles.Add(nextManFileInfo);
                        }
                        else if (CompareHash(checkHash, nextManFileInfo.Hash) == false)
                        {
                            Write(" [DIFFERENT]");
                            ModifiedFiles.Add(nextManFileInfo);
                        }
                        else if (nextFileInfo.LastWriteTimeUtc != nextManFileInfo.LastModifiedUtc)
                        {
                            Write(" [DATE]");
                            DateModifiedFiles.Add(nextManFileInfo);

                            if (BackDate == true)
                            {
                                nextFileInfo.LastWriteTimeUtc =
                                    nextManFileInfo.LastModifiedUtc;
                            }
                        }

                        byte[] newHash = checkHash;
                        string newHashType = nextManFileInfo.HashType;

                        if (NewHash)
                        {
                            newHashType = GetNewHashType(Manifest);

                            checkHash = ComputeHash(
                                nextFileInfo,
                                newHashType);
                        }                        

                        nextManFileInfo.Hash = newHash;
                        nextManFileInfo.HashType = newHashType;

                        nextManFileInfo.LastModifiedUtc = nextFileInfo.LastWriteTimeUtc;
                        nextManFileInfo.FileLength = nextFileInfo.Length;
                    }
                    else
                    {
                        Write(" [SKIPPED]");
                    }
                }
                else
                {
                    Write(" [MISSING]");
                    currentManfestDirInfo.Files.Remove(nextManFileInfo.Name);
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

                UpdateRecursive(
                    nextDirInfo,
                    nextManDirInfo);
            }


            // Look for new files
            foreach (FileInfo nextFileInfo in fileDict.Values)
            {
                if (currentManfestDirInfo.Files.ContainsKey(nextFileInfo.Name) == false)
                {
                    ManifestFileInfo newManFileInfo =
                       new ManifestFileInfo(
                           nextFileInfo.Name,
                           currentManfestDirInfo);

                    Write(MakePathString(newManFileInfo));

                    if (IgnoreFile(MakePathString(newManFileInfo)))
                    {
                        Write(" [IGNORED]");
                    }
                    else
                    {
                        FileCheckedCount++;

                        bool checkHash = Update == true || Force == true;

                        if (checkHash)
                        {
                            try
                            {
                                newManFileInfo.Hash =
                                    ComputeHash(nextFileInfo, NewHashType);

                                newManFileInfo.HashType =
                                    GetNewHashType(Manifest);
                            }
                            catch (Exception)
                            {
                                // TODO: More detail?
                            }
                        }

                        if (checkHash && newManFileInfo.Hash == null)
                        {
                            ErrorFiles.Add(newManFileInfo);
                            Write(" [ERROR]");
                        }
                        else
                        {
                            NewFiles.Add(newManFileInfo);
                            NewFilesForClean.Add(nextFileInfo);
                            Write(" [NEW]");
                        }

                        newManFileInfo.FileLength =
                            nextFileInfo.Length;

                        newManFileInfo.LastModifiedUtc =
                            nextFileInfo.LastWriteTimeUtc;

                        currentManfestDirInfo.Files.Add(
                            nextFileInfo.Name,
                            newManFileInfo);
                    }

                    WriteLine("");
                }
            }


            // Recurse looking for new directories
            foreach (DirectoryInfo nextDirInfo in dirDict.Values)
            {
                if (currentManfestDirInfo.Subdirectories.ContainsKey(nextDirInfo.Name) == false)
                {
                    ManifestDirectoryInfo nextManDirInfo = new ManifestDirectoryInfo(
                        nextDirInfo.Name,
                        currentManfestDirInfo);

                    currentManfestDirInfo.Subdirectories.Add(
                        nextManDirInfo.Name,
                        nextManDirInfo);

                    UpdateRecursive(
                        nextDirInfo,
                        nextManDirInfo);
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

        protected String GetNewHashType(Manifest man)
        {
            if (man.DefaultHashMethod != null)
            {
                switch (man.DefaultHashMethod)
                {
                    case "MD5":
                    case "SHA256":
                        return man.DefaultHashMethod;
                }
            }

            return NewHashType;
        }

        protected byte[] ComputeHash(
            FileInfo file,
            string hashType)
        {
            byte[] hash = null;

            Stream fileStream =
                file.Open(FileMode.Open, FileAccess.Read);

            switch (hashType)
            {
                case "MD5":
                    hash = new MD5CryptoServiceProvider().ComputeHash(fileStream);
                    break;

                case "SHA256":
                    hash = new SHA256Managed().ComputeHash(fileStream);
                    break;

                default:
                    throw new Exception("Unrecognized hash method: " + hashType);
            }

            fileStream.Close();

            return hash;
        }

        protected bool CompareHash(byte[] hash1, byte[] hash2)
        {
            if (hash1 == null || hash2 == null)
            {
                return false;
            }

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
        public bool Update { set; get; }
        public bool Force { set; get; }
        public bool NewHash { set; get; }
        public bool BackDate { set; get; }

        public int FileCheckedCount { private set; get; }

        public Manifest Manifest { set; get; }
        public List<ManifestFileInfo> NewFiles { private set; get; }
        public List<FileInfo> NewFilesForClean { private set; get; }
        public List<ManifestFileInfo> ModifiedFiles { private set; get; }
        public List<ManifestFileInfo> MissingFiles { private set; get; }
        public List<ManifestFileInfo> DateModifiedFiles { private set; get; }
        public List<ManifestFileInfo> ErrorFiles { private set; get; }



        // Static

        public static String ManifestFileName;
        public static String PathDelimeterString;
        public static String ManifestFilePath;
        public static String NewHashType;

        static RepositoryTool()
        {
            // TODO: Fix later for different platforms
            ManifestFileName = ".repositoryManifest";
            PathDelimeterString = "\\";
            ManifestFilePath = "." + PathDelimeterString + ManifestFileName;
            NewHashType = "MD5";
        }
    }
}
