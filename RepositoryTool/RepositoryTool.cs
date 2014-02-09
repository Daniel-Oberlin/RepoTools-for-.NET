using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using RepositoryManifest;
using Utilities;


namespace RepositoryTool
{
    public delegate void WriteLogDelegate(String message);

    public class RepositoryTool
    {
        public RepositoryTool()
        {
            FileCheckedCount = 0;

            ShowProgress = false;
            Update = false;
            AlwaysCheckHash = false;
            MakeNewHash = false;
            BackDate = false;
            TrackMoves = false;
            TrackDuplicates = false;

            NewFiles = new List<ManifestFileInfo>();
            NewFilesForGroom = new List<FileInfo>();
            ChangedFiles = new List<ManifestFileInfo>();
            MissingFiles = new List<ManifestFileInfo>();
            LastModifiedDateFiles = new List<ManifestFileInfo>();
            ErrorFiles = new List<ManifestFileInfo>();
            IgnoredFiles = new List<ManifestFileInfo>();
            NewlyIgnoredFiles = new List<ManifestFileInfo>();
            IgnoredFilesForGroom = new List<FileInfo>();
            MovedFiles = new Dictionary<FileHash,MovedFileSet>();
            MovedFileOrder = new List<FileHash>();
            DuplicateFiles = new Dictionary<FileHash, List<ManifestFileInfo>>();
        }

        public void Clear()
        {
            FileCheckedCount = 0;

            NewFiles.Clear();
            NewFilesForGroom.Clear();
            ChangedFiles.Clear();
            MissingFiles.Clear();
            LastModifiedDateFiles.Clear();
            ErrorFiles.Clear();
            IgnoredFiles.Clear();
            NewlyIgnoredFiles.Clear();
            IgnoredFilesForGroom.Clear();
            MovedFiles.Clear();
            MovedFileOrder.Clear();
            DuplicateFiles.Clear();
        }

        public void DoUpdate()
        {
            Clear();

            UpdateRecursive(
                RootDirectory,
                Manifest.RootDirectory);

            if (TrackMoves == true)
            {
                DoTrackMoves();
            }

            if (TrackDuplicates == true)
            {
                DoTrackDuplicates();
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
                FileInfo[] fileList = null; ;
                try
                {
                    fileList = currentDirectoryInfo.GetFiles();
                }
                catch (Exception)
                {
                    WriteLine(Manifest.MakeStandardPathString(
                        currentManfestDirInfo));

                    if (IgnoreFile(Manifest.MakeStandardPathString(
                        currentManfestDirInfo)) == true)
                    {
                        // This was implemented primarily to allow the user to
                        // silence the process of skipping over inaccessible
                        // system directories by ignoring them.  For example,
                        // in some cases the "$RECYCLE BIN" under Windows
                        // is not accessible and will generate an error.  The
                        // user can now add such directories to the ignore list
                        // and they will be silently ignored.  The special
                        // message for showProgress alerts the user that the
                        // directory is actually being skipped altogether
                        // since it can't be accessed.  The only significant
                        // implication of this is that the ignored files won't
                        // be enumerated and counted as being ignored.
                        if (ShowProgress)
                        {
                            WriteLine(
                                Manifest.MakeStandardPathString(currentManfestDirInfo) +
                                " [IGNORED DIRECTORY AND CANNOT ACCESS]");
                        }
                    }
                    else
                    {
                        ForceWriteLine("Could not access contents of: " +
                            currentDirectoryInfo.FullName);
                    }

                    return;
                }

                foreach (FileInfo nextFileInfo in fileList)
                {
                    fileDict.Add(nextFileInfo.Name.Normalize(), nextFileInfo);
                }

                DirectoryInfo[] dirList =
                    currentDirectoryInfo.GetDirectories();

                foreach (DirectoryInfo nextDirInfo in dirList)
                {
                    dirDict.Add(nextDirInfo.Name.Normalize(), nextDirInfo);
                }
            }

            // Clone in case we modify during iteration
            List<ManifestFileInfo> fileListClone =
                new List<ManifestFileInfo>(currentManfestDirInfo.Files.Values);

            // Iterate through existing manifest entries
            foreach (ManifestFileInfo nextManFileInfo in fileListClone)
            {
                if (ShowProgress)
                {
                    Write(Manifest.MakeStandardPathString(nextManFileInfo));
                }

                if (fileDict.ContainsKey(nextManFileInfo.Name))
                {
                    FileCheckedCount++;

                    FileInfo nextFileInfo = fileDict[nextManFileInfo.Name];

                    if (IgnoreFile(Manifest.MakeStandardPathString(nextManFileInfo)))
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
                        ChangedFiles.Add(nextManFileInfo);
                    }
                    else if (AlwaysCheckHash == true ||
                        nextManFileInfo.FileHash == null ||
                        CompareLastModifiedDates(nextFileInfo.LastWriteTimeUtc, nextManFileInfo.LastModifiedUtc) == false ||
                        nextFileInfo.Length != nextManFileInfo.FileLength)
                    {
                        FileHash checkHash = null;

                        Exception exception = null;
                        try
                        {
                            string hashType = Manifest.DefaultHashMethod;
                            if (nextManFileInfo.FileHash != null)
                            {
                                hashType = nextManFileInfo.FileHash.HashType;
                            }

                            checkHash = FileHash.ComputeHash(
                                nextFileInfo,
                                hashType);
                        }
                        catch (Exception ex)
                        {
                            exception = ex;
                        }

                        if (exception != null)
                        {
                            WriteLine(" [ERROR]");
                            ErrorFiles.Add(nextManFileInfo);

                            Write(exception.ToString());
                        }
                        else
                        {
                            if (nextManFileInfo.FileHash == null)
                            {
                                Write(" [NULL HASH IN MANIFEST]");
                                ChangedFiles.Add(nextManFileInfo);
                            }
                            else if (checkHash.Equals(nextManFileInfo.FileHash) == false)
                            {
                                Write(" [DIFFERENT]");
                                ChangedFiles.Add(nextManFileInfo);
                            }
                            else
                            {
                                if (CompareLastModifiedDates(
                                    nextFileInfo.LastWriteTimeUtc,
                                    nextManFileInfo.LastModifiedUtc) == false)
                                {
                                    Write(" [LAST MODIFIED DATE]");
                                    LastModifiedDateFiles.Add(nextManFileInfo);

                                    if (BackDate == true)
                                    {
                                        nextFileInfo.LastWriteTimeUtc =
                                            nextManFileInfo.LastModifiedUtc;
                                    }
                                }
                            }
                        }

                        FileHash newHash = checkHash;
                        if (MakeNewHash)
                        {
                            checkHash = FileHash.ComputeHash(
                                nextFileInfo,
                                GetNewHashType(Manifest));
                        }

                        // Update hash and last modified date accordingly
                        nextManFileInfo.FileHash = newHash;

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

            // Clone in case we modify during iteration
            List<ManifestDirectoryInfo> directoryListClone =
                new List<ManifestDirectoryInfo>(
                    currentManfestDirInfo.Subdirectories.Values);

            foreach (ManifestDirectoryInfo nextManDirInfo in
                directoryListClone)
            {
                DirectoryInfo nextDirInfo = null;
                if (dirDict.ContainsKey(nextManDirInfo.Name))
                {
                    nextDirInfo = dirDict[nextManDirInfo.Name];
                }

                UpdateRecursive(
                    nextDirInfo,
                    nextManDirInfo);

                if (nextManDirInfo.Empty)
                {
                    currentManfestDirInfo.Subdirectories.Remove(
                        nextManDirInfo.Name);
                }
            }


            // Look for new files
            foreach (FileInfo nextFileInfo in fileDict.Values)
            {
                if (currentManfestDirInfo.Files.ContainsKey(
                    nextFileInfo.Name.Normalize()) == false)
                {
                    ManifestFileInfo newManFileInfo =
                       new ManifestFileInfo(
                           nextFileInfo.Name.Normalize(),
                           currentManfestDirInfo);

                    Write(Manifest.MakeStandardPathString(newManFileInfo));

                    if (IgnoreFile(Manifest.MakeStandardPathString(newManFileInfo)))
                    {
                        IgnoredFiles.Add(newManFileInfo);

                        // Don't groom the manifest file!
                        if (Manifest.MakeNativePathString(newManFileInfo) !=
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
                        if (Update == true ||
                            AlwaysCheckHash == true ||
                            TrackMoves == true)
                        {
                            checkHash = true;
                        }

                        
                        Exception exception = null;
                        if (checkHash)
                        {
                            try
                            {
                                newManFileInfo.FileHash =
                                    FileHash.ComputeHash(
                                        nextFileInfo,
                                        NewHashType);
                            }
                            catch (Exception ex)
                            {
                                exception = ex;
                            }
                        }

                        if (checkHash && newManFileInfo.FileHash == null)
                        {
                            ErrorFiles.Add(newManFileInfo);

                            WriteLine(" [ERROR]");
                            Write(exception.ToString());
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

                        newManFileInfo.RegisteredUtc =
                            DateTime.Now.ToUniversalTime();

                        currentManfestDirInfo.Files.Add(
                            nextFileInfo.Name.Normalize(),
                            newManFileInfo);
                    }

                    WriteLine("");
                }
            }


            // Recurse looking for new directories
            foreach (DirectoryInfo nextDirInfo in dirDict.Values)
            {
                if (currentManfestDirInfo.Subdirectories.ContainsKey(
                    nextDirInfo.Name.Normalize()) == false)
                {
                    ManifestDirectoryInfo nextManDirInfo =
                            new ManifestDirectoryInfo(
                                nextDirInfo.Name.Normalize(),
                                currentManfestDirInfo);

                    currentManfestDirInfo.Subdirectories.Add(
                        nextManDirInfo.Name,
                        nextManDirInfo);

                    UpdateRecursive(
                        nextDirInfo,
                        nextManDirInfo);

                    if (nextManDirInfo.Empty)
                    {
                        currentManfestDirInfo.Subdirectories.Remove(
                            nextManDirInfo.Name);
                    }
                }
            }
        }

        protected void DoTrackMoves()
        {
            // For large number of moved files it's probably faster to
            // rebuild these lists from scratch than to remove many
            // individual items from them.
            List<ManifestFileInfo> missingFilesUpdated =
                new List<ManifestFileInfo>();

            List<ManifestFileInfo> newFilesUpdated =
                new List<ManifestFileInfo>();

            // Make files easy to find by their hashcodes
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
                if (newFileDict.Dict.ContainsKey(checkMissingFile.FileHash))
                {
                    if (MovedFiles.ContainsKey(checkMissingFile.FileHash) == false)
                    {
                        MovedFiles.Add(
                            checkMissingFile.FileHash,
                            new MovedFileSet());

                        MovedFileOrder.Add(checkMissingFile.FileHash);
                    }

                    MovedFiles[checkMissingFile.FileHash].OldFiles.Add(checkMissingFile);

                    if (MovedFiles[checkMissingFile.FileHash].NewFiles.Count == 0)
                    {
                        // First time only
                        foreach (ManifestFileInfo nextNewFile in
                            newFileDict.Dict[checkMissingFile.FileHash])
                        {
                            MovedFiles[checkMissingFile.FileHash].NewFiles.Add(nextNewFile);

                            // Remember for later rebuild
                            movedFiles.Add(nextNewFile);
                        }
                    }
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

                    if (Manifest.MakeNativePathString(checkNewFile) !=
                        ManifestNativeFilePath)
                    {
                        NewFilesForGroom.Add(
                            new FileInfo(
                                Manifest.MakeNativePathString(checkNewFile)));
                    }
                }
            }

            // Replace with updated lists
            MissingFiles = missingFilesUpdated;
            NewFiles = newFilesUpdated;
        }

        protected void DoTrackDuplicates()
        {
            DuplicateFiles.Clear();

            Dictionary<FileHash, List<ManifestFileInfo>> fileDict =
                new Dictionary<FileHash, List<ManifestFileInfo>>();

            CheckDuplicatesRecursive(
                Manifest.RootDirectory,
                fileDict);

            foreach (FileHash nextHash in fileDict.Keys)
            {
                List<ManifestFileInfo> nextList =
                    fileDict[nextHash];

                if (nextList.Count > 1)
                {
                    DuplicateFiles.Add(nextHash, nextList);
                }
            }
        }

        protected void CheckDuplicatesRecursive(
            ManifestDirectoryInfo currentDirectory,
            Dictionary<FileHash, List<ManifestFileInfo>> fileDict)
        {
            foreach (ManifestFileInfo nextFileInfo in
                currentDirectory.Files.Values)
            {
                if (fileDict.ContainsKey(nextFileInfo.FileHash) == false)
                {
                    fileDict.Add(
                        nextFileInfo.FileHash,
                        new List<ManifestFileInfo>());
                }

                fileDict[nextFileInfo.FileHash].Add(nextFileInfo);
            }

            foreach (ManifestDirectoryInfo nextDirInfo in
                currentDirectory.Subdirectories.Values)
            {
                CheckDuplicatesRecursive(
                    nextDirInfo,
                    fileDict);
            }
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
                return Manifest.MakeCleanManifest();
            }

            return manifest;
        }

        protected void WriteLine(String message)
        {
            Write(message + "\r\n");
        }

        protected void Write(String message)
        {
            if (ShowProgress && WriteLogDelegate != null)
            {
                WriteLogDelegate.Invoke(message);
            }
        }

        protected void ForceWriteLine(String message)
        {
            ForceWrite(message + "\r\n");
        }

        protected void ForceWrite(String message)
        {
            if (WriteLogDelegate != null)
            {
                WriteLogDelegate.Invoke(message);
            }
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

        protected bool IgnoreFile(String fileName)
        {
            foreach (String nextExpression in Manifest.IgnoreList)
            {
                System.Text.RegularExpressions.Regex regex =
                    new System.Text.RegularExpressions.Regex(nextExpression);

                if (regex.IsMatch(fileName))
                {
                    return true;
                }
            }

            return false;
        }

        // When copying NTFS files over to OSX, "last modified" dates can
        // be slightly different up to almost 1 second.  It seems like many
        // smaller files get the date copied exactly.  For the other files,
        // it almost seems like any precision higher than 1 second is ignored
        // because the time differences are uniformly and randomly distributed
        // between 0s and 1s.  So we choose a small tolerance and allow for
        // the dates to vary slightly from those recorded in the manifest.
        //
        // Further note, I had to increase the tolerance to 2s because of
        // difficulties maintaining consistency with "last modified dates" of
        // encrypted files.  It seems that we get a higher precision time when
        // we get the FileInfo object immediately after we write the file -
        // with precision at 1ms.  Then later when we query the file again, we
        // see a precision of 1s.  So we use a 2s tolerance to account for +/-
        // 1s.  These observations were while using the exFAT format, so not
        // sure to what extent that makes a difference.
        protected bool CompareLastModifiedDates(DateTime date1, DateTime date2)
        {
            if (Math.Abs(date1.Subtract(date2).Ticks) >
                Math.Abs(LastModifiedDateTolerance.Ticks))
            {
                return false;
            }

            return true;
        }


        // Data members and accessors

        public WriteLogDelegate WriteLogDelegate { set; get; } 
        public DirectoryInfo RootDirectory { set; get; }

        public bool ShowProgress { set; get; }
        public bool Update { set; get; }
        public bool AlwaysCheckHash { set; get; }
        public bool MakeNewHash { set; get; }
        public bool BackDate { set; get; }
        public bool TrackMoves { set; get; }
        public bool TrackDuplicates { set; get; }

        public int FileCheckedCount { private set; get; }

        public Manifest Manifest { set; get; }
        public List<ManifestFileInfo> NewFiles { private set; get; }
        public List<FileInfo> NewFilesForGroom { private set; get; }
        public List<ManifestFileInfo> ChangedFiles { private set; get; }
        public List<ManifestFileInfo> MissingFiles { private set; get; }
        public List<ManifestFileInfo> LastModifiedDateFiles { private set; get; }
        public List<ManifestFileInfo> ErrorFiles { private set; get; }
        public List<ManifestFileInfo> IgnoredFiles { private set; get; }
        public List<ManifestFileInfo> NewlyIgnoredFiles { private set; get; }
        public List<FileInfo> IgnoredFilesForGroom { private set; get; }
        public Dictionary<FileHash, MovedFileSet> MovedFiles { private set; get; }
        public List<FileHash> MovedFileOrder { private set; get; }
        public Dictionary<FileHash, List<ManifestFileInfo>> DuplicateFiles { private set; get; }


        // Static

        public static String ManifestFileName;
        public static String ManifestNativeFilePath;
        public static String PrototypeManifestFileName;
        public static String NewHashType;
        public static TimeSpan LastModifiedDateTolerance;

        static RepositoryTool()
        {
            ManifestNativeFilePath =
                Path.Combine(
                    ".",
                    Manifest.DefaultManifestFileName);

            PrototypeManifestFileName = ".manifestPrototype";
            NewHashType = Utilities.CryptUtilities.DefaultHashType;

            // Tolerate up to one second of difference
            LastModifiedDateTolerance = new TimeSpan(0, 0, 2);
        }
    }
}
