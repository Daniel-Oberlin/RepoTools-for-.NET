using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

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
            AlwaysCheckHash = false;
            NewHash = false;
            BackDate = false;

            NewFiles = new List<ManifestFileInfo>();
            NewFilesForGroom = new List<FileInfo>();
            ModifiedFiles = new List<ManifestFileInfo>();
            MissingFiles = new List<ManifestFileInfo>();
            DateModifiedFiles = new List<ManifestFileInfo>();
            ErrorFiles = new List<ManifestFileInfo>();
            IgnoredFiles = new List<ManifestFileInfo>();
            NewlyIgnoredFiles = new List<ManifestFileInfo>();
            IgnoredFilesForGroom = new List<FileInfo>();
            MovedFiles = new Dictionary<ManifestFileInfo, ManifestFileInfo>();

            Clear();
        }

        public void Clear()
        {
            FileCheckedCount = 0;

            NewFiles.Clear();
            NewFilesForGroom.Clear();
            ModifiedFiles.Clear();
            MissingFiles.Clear();
            DateModifiedFiles.Clear();
            ErrorFiles.Clear();
            IgnoredFiles.Clear();
            NewlyIgnoredFiles.Clear();
            IgnoredFilesForGroom.Clear();
            MovedFiles.Clear();
        }

        public void DoUpdate()
        {
            Clear();

            UpdateRecursive(
                RootDirectory,
                Manifest.RootDirectory);

            if (CheckMoves == true)
            {
                DoCheckMoves();
            }

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
                    Write(MakeStandardPathString(nextManFileInfo));
                }

                if (fileDict.ContainsKey(nextManFileInfo.Name))
                {
                    FileCheckedCount++;

                    FileInfo nextFileInfo = fileDict[nextManFileInfo.Name];

                    if (IgnoreFile(MakeStandardPathString(nextManFileInfo)))
                    {
                        Write(" [NEWLY IGNORED]");

                        currentManfestDirInfo.Files.Remove(
                            nextManFileInfo.Name);

                        NewlyIgnoredFiles.Add(nextManFileInfo);
                    }
                    else if (nextFileInfo.Length != nextManFileInfo.FileLength &&
                        Update == false &&
                        AlwaysCheckHash == false)
                    {
                        Write(" [DIFFERENT]");
                        ModifiedFiles.Add(nextManFileInfo);
                    }
                    else if (AlwaysCheckHash == true ||
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

                    Write(MakeStandardPathString(newManFileInfo));

                    if (IgnoreFile(MakeStandardPathString(newManFileInfo)))
                    {
                        IgnoredFiles.Add(newManFileInfo);

                        // Don't groom the manifest file!
                        if (MakeNativePathString(newManFileInfo) !=
                            ManifestNativeFilePath)
                        {
                            IgnoredFilesForGroom.Add(nextFileInfo);
                        }

                        Write(" [IGNORED]");
                    }
                    else
                    {
                        FileCheckedCount++;

                        bool checkHash = false;
                        if (Update == true || AlwaysCheckHash == true || CheckMoves == true)
                        {
                            checkHash = true;
                        }

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
                            NewFilesForGroom.Add(nextFileInfo);
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

        protected void DoCheckMoves()
        {
            // For large number of moved files it's probably faster to
            // rebuild these lists from scratch than to remove many
            // individual items from them.
            List<ManifestFileInfo> missingFilesUpdated =
                new List<ManifestFileInfo>();

            List<ManifestFileInfo> newFilesUpdated =
                new List<ManifestFileInfo>();

            // Make files easy to find by their hashcodes, and count the
            // number of files with a given hashcode.
            HashFileDict missingFileDict = new HashFileDict();
            foreach (ManifestFileInfo missingFile in MissingFiles)
            {
                missingFileDict.Add(missingFile);
            }

            HashFileDict newFileDict = new HashFileDict();
            foreach (ManifestFileInfo newFile in NewFiles)
            {
                newFileDict.Add(newFile);
            }

            // Note which new files are really moved files for later when
            // we rebuild the new files list.
            HashSet<ManifestFileInfo> movedFiles =
                new HashSet<ManifestFileInfo>();

            foreach (ManifestFileInfo checkMissingFile in MissingFiles)
            {
                HashWrapper wrapper =
                    new HashWrapper(checkMissingFile.Hash);

                if (missingFileDict.Dict[wrapper].Count == 1 &&
                    newFileDict.Dict.ContainsKey(wrapper) &&
                    newFileDict.Dict[wrapper].Count == 1)
                {
                    ManifestFileInfo newFile =
                        newFileDict.Dict[wrapper][0];

                    MovedFiles.Add(checkMissingFile, newFile);

                    // Remember for later rebuild
                    movedFiles.Add(newFile);
                }
                else
                {
                    missingFilesUpdated.Add(checkMissingFile);
                }
            }

            // Rebuild new file list
            NewFilesForGroom.Clear();
            foreach (ManifestFileInfo checkNewFile in NewFiles)
            {
                if (movedFiles.Contains(checkNewFile) == false)
                {
                    newFilesUpdated.Add(checkNewFile);

                    if (MakeNativePathString(checkNewFile) !=
                        ManifestNativeFilePath)
                    {
                        NewFilesForGroom.Add(
                            new FileInfo(
                                MakeNativePathString(checkNewFile)));
                    }
                }
            }

            // Replace with updated lists
            MissingFiles = missingFilesUpdated;
            NewFiles = newFilesUpdated;
        }


        // Helper methods

        public Manifest MakeManifest()
        {
            Manifest manifest = null;

            String appDirectoryPathName = Path.GetDirectoryName(
                new Uri(Assembly.GetExecutingAssembly().GetName().CodeBase).LocalPath);

            String manifestPrototypeFilePath = Path.Combine(
                appDirectoryPathName, PrototypeManifestFileName);

            if (File.Exists(manifestPrototypeFilePath))
            {
                Manifest prototype =
                    Manifest.ReadManifestFile(manifestPrototypeFilePath);

                manifest = prototype.CloneFromPrototype();
            }
            else
            {
                // Default implementation when there is no prototype
                manifest = new Manifest();

                manifest.DefaultHashMethod = NewHashType;

                manifest.IgnoreList.Add(
                    "^" +
                    System.Text.RegularExpressions.Regex.Escape(ManifestStandardFilePath) +
                    "$");
            }

            return manifest;
        }

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

        public static String MakeStandardPathString(ManifestFileInfo fileInfo)
        {
            return MakeStandardPathString(fileInfo.ParentDirectory) + fileInfo.Name;
        }

        public static String MakeStandardPathString(ManifestDirectoryInfo directoryInfo)
        {
            String pathString = directoryInfo.Name + StandardPathDelimeterString;

            if (directoryInfo.ParentDirectory != null)
            {
                pathString = MakeStandardPathString(directoryInfo.ParentDirectory) + pathString;
            }

            return pathString;
        }

        public static String MakeNativePathString(ManifestFileInfo fileInfo)
        {
            return Path.Combine(
                MakeNativePathString(fileInfo.ParentDirectory),
                fileInfo.Name);
        }

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

            if (hash1.Length != hash2.Length)
            {
                return false;
            }

            for (int i = 0; i < hash1.Length; i++ )
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
            foreach (String nextExpression in Manifest.IgnoreList)
            {
                System.Text.RegularExpressions.Regex regex =
                    new System.Text.RegularExpressions.Regex(nextExpression);

                if (regex.Matches(fileName).Count > 0)
                {
                    return true;
                }
            }

            return false;
        }



        // Data members and accessors

        public WriteLogDelegate WriteLogDelegate { set; get; } 
        public DirectoryInfo RootDirectory { set; get; }

        public bool ShowProgress { set; get; }
        public bool Update { set; get; }
        public bool AlwaysCheckHash { set; get; }
        public bool NewHash { set; get; }
        public bool BackDate { set; get; }
        public bool CheckMoves { set; get; }

        public int FileCheckedCount { private set; get; }

        public Manifest Manifest { set; get; }
        public List<ManifestFileInfo> NewFiles { private set; get; }
        public List<FileInfo> NewFilesForGroom { private set; get; }
        public List<ManifestFileInfo> ModifiedFiles { private set; get; }
        public List<ManifestFileInfo> MissingFiles { private set; get; }
        public List<ManifestFileInfo> DateModifiedFiles { private set; get; }
        public List<ManifestFileInfo> ErrorFiles { private set; get; }
        public List<ManifestFileInfo> IgnoredFiles { private set; get; }
        public List<ManifestFileInfo> NewlyIgnoredFiles { private set; get; }
        public List<FileInfo> IgnoredFilesForGroom { private set; get; }
        public Dictionary<ManifestFileInfo, ManifestFileInfo> MovedFiles { private set; get; }


        // Static

        public static String StandardPathDelimeterString;
        public static String ManifestFileName;
        public static String ManifestNativeFilePath;
        public static String ManifestStandardFilePath;
        public static String PrototypeManifestFileName;
        public static String NewHashType;

        static RepositoryTool()
        {
            // TODO: Fix later for different platforms
            StandardPathDelimeterString = "/";

            ManifestFileName = ".repositoryManifest";

            ManifestNativeFilePath =
                Path.Combine(
                    ".",
                    ManifestFileName);

            ManifestStandardFilePath =
                "." +
                StandardPathDelimeterString +
                ManifestFileName;

            PrototypeManifestFileName = ".manifestPrototype";
            NewHashType = "MD5";
        }
    }



    public class HashWrapper
    {
        public HashWrapper(byte[] hash)
        {
            Hash = hash;

            for (int i = 0; i < 4; i++)
            {
                myHash <<= 8;
                myHash |= hash[i];
            }
        }

        public override int GetHashCode()
        {
            return myHash;
        }

        public override bool Equals(object obj)
        {
            if (obj is HashWrapper)
            {
                HashWrapper other = (HashWrapper)obj;

                if (other.Hash.Length != Hash.Length)
                {
                    return false;
                }

                for (int i = 0; i < Hash.Length; i++)
                {
                    if (other.Hash[i] != Hash[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public byte[] Hash { private set; get; }

        private int myHash;
    }

    public class HashFileDict
    {
        public HashFileDict()
        {
            Dict = new Dictionary<HashWrapper,List<ManifestFileInfo>>();
        }

        public void Add(ManifestFileInfo manFileInfo)
        {
            HashWrapper hash = new HashWrapper(manFileInfo.Hash);

            if (Dict.ContainsKey(hash) == false)
            {
                Dict.Add(hash, new List<ManifestFileInfo>());
            }

            Dict[hash].Add(manFileInfo);
        }

        public Dictionary<HashWrapper, List<ManifestFileInfo>> Dict { private set; get; }
    }
}
