using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RepositoryManifest;
using Utilities;


namespace RepositorySync
{
    public delegate void WriteLogDelegate(String message);

    class RepositorySync
    {
        public RepositorySync(
            IRepositoryProxy source,
            IRepositoryProxy dest)
        {
            SourceRep = source;
            DestRep = dest;

            Preview = false;
            BothWays = false;
            Mirror = false;
            Retry = false;

            SourceOnlyFiles = new List<ManifestFileInfo>();
            DestOnlyFiles = new List<ManifestFileInfo>();
            ChangedFiles = new Dictionary<ManifestFileInfo, ManifestFileInfo>();
            LastModifiedDateFiles = new Dictionary<ManifestFileInfo, ManifestFileInfo>();
            RegisteredDateFiles = new Dictionary<ManifestFileInfo, ManifestFileInfo>();
            MovedFiles = new Dictionary<FileHash, MovedFileSet>();
            MovedFileOrder = new List<FileHash>();
            ErrorFiles = new List<ManifestFileInfo>();
        }

        public void Clear()
        {
            SourceOnlyFiles.Clear();
            DestOnlyFiles.Clear();
            ChangedFiles.Clear();
            LastModifiedDateFiles.Clear();
            RegisteredDateFiles.Clear();
            MovedFiles.Clear();
            MovedFileOrder.Clear();
            ErrorFiles.Clear();
        }

        public void CompareManifests()
        {
            Clear();

            HashSet<ManifestFileInfo> destFileMatch =
                new HashSet<ManifestFileInfo>();

            CompareManifestsRecursiveSource(
                SourceRep.Manifest.RootDirectory,
                DestRep.Manifest.RootDirectory,
                destFileMatch);

            CompareManifestsRecursiveDest(
                DestRep.Manifest.RootDirectory,
                destFileMatch);

            TrackMoves();
        }

        protected void CompareManifestsRecursiveSource(
            ManifestDirectoryInfo sourceDir,
            ManifestDirectoryInfo destDir,
            HashSet<ManifestFileInfo> destFileMatch)
        {
            foreach (ManifestFileInfo sourceFile in sourceDir.Files.Values)
            {
                if (destDir != null &&
                    destDir.Files.ContainsKey(sourceFile.Name))
                {
                    ManifestFileInfo destFile = destDir.Files[sourceFile.Name];
                    destFileMatch.Add(destFile);

                    if (sourceFile.FileHash.Equals(destFile.FileHash) == false)
                    {
                        ChangedFiles.Add(sourceFile, destFile);
                    }
                    else
                    {
                        if (sourceFile.LastModifiedUtc !=
                            destFile.LastModifiedUtc)
                        {
                            LastModifiedDateFiles.Add(sourceFile, destFile);
                        }

                        if (sourceFile.RegisteredUtc !=
                            destFile.RegisteredUtc)
                        {
                            RegisteredDateFiles.Add(sourceFile, destFile);
                        }
                    }
                }
                else
                {
                    SourceOnlyFiles.Add(sourceFile);
                }
            }

            foreach (ManifestDirectoryInfo nextSourceDir in
                sourceDir.Subdirectories.Values)
            {
                ManifestDirectoryInfo nextDestDir = null;
                if (destDir != null &&
                    destDir.Subdirectories.ContainsKey(nextSourceDir.Name))
                {
                    nextDestDir = destDir.Subdirectories[nextSourceDir.Name];
                }

                CompareManifestsRecursiveSource(
                    nextSourceDir,
                    nextDestDir,
                    destFileMatch);
            }
        }

        protected void CompareManifestsRecursiveDest(
            ManifestDirectoryInfo destDir,
            HashSet<ManifestFileInfo> destFileMatch)
        {
            foreach (ManifestFileInfo destFile in
                destDir.Files.Values)
            {
                if (destFileMatch.Contains(destFile) == false)
                {
                    DestOnlyFiles.Add(destFile);
                }
            }

            foreach (ManifestDirectoryInfo nextDestDir in
                destDir.Subdirectories.Values)
            {
                CompareManifestsRecursiveDest(
                    nextDestDir,
                    destFileMatch);
            }
        }

        protected void TrackMoves()
        {
            // For large number of moved files it's probably faster to
            // rebuild these lists from scratch than to remove many
            // individual items from them.
            List<ManifestFileInfo> sourceOnlyFilesUpdated =
                new List<ManifestFileInfo>();

            List<ManifestFileInfo> destOnlyFilesUpdated =
                new List<ManifestFileInfo>();

            // Make files easy to find by their hashcodes
            HashFileDict sourceFileDict = new HashFileDict();
            foreach (ManifestFileInfo sourceFile in SourceOnlyFiles)
            {
                sourceFileDict.Add(sourceFile);
            }

            HashFileDict destFileDict = new HashFileDict();
            foreach (ManifestFileInfo destFile in DestOnlyFiles)
            {
                destFileDict.Add(destFile);
            }

            // Note which new files are really moved files for later when
            // we rebuild the new files list.
            HashSet<ManifestFileInfo> movedFiles =
                new HashSet<ManifestFileInfo>();

            foreach (ManifestFileInfo checkSourceFile in SourceOnlyFiles)
            {
                if (destFileDict.Dict.ContainsKey(checkSourceFile.FileHash))
                {
                    if (MovedFiles.ContainsKey(checkSourceFile.FileHash) == false)
                    {
                        MovedFiles.Add(
                            checkSourceFile.FileHash,
                            new MovedFileSet());

                        MovedFileOrder.Add(checkSourceFile.FileHash);
                    }

                    MovedFiles[checkSourceFile.FileHash].SourceFiles.Add(checkSourceFile);

                    if (MovedFiles[checkSourceFile.FileHash].DestFiles.Count == 0)
                    {
                        // First time only
                        foreach (ManifestFileInfo nextNewFile in
                            destFileDict.Dict[checkSourceFile.FileHash])
                        {
                            MovedFiles[checkSourceFile.FileHash].DestFiles.Add(nextNewFile);

                            // Remember for later rebuild
                            movedFiles.Add(nextNewFile);
                        }
                    }
                }
                else
                {
                    sourceOnlyFilesUpdated.Add(checkSourceFile);
                }
            }

            // Rebuild new file list
            foreach (ManifestFileInfo checkDestFile in DestOnlyFiles)
            {
                if (movedFiles.Contains(checkDestFile) == false)
                {
                    destOnlyFilesUpdated.Add(checkDestFile);
                }
            }

            // Replace with updated lists
            SourceOnlyFiles = sourceOnlyFilesUpdated;
            DestOnlyFiles = destOnlyFilesUpdated;
        }

        public void DoUpdate()
        {
            // Update manifest information
            if (Mirror)
            {
                if (SourceRep.Manifest.ManifestInfoLastModifiedUtc !=
                    DestRep.Manifest.ManifestInfoLastModifiedUtc)
                {
                    WriteLine("Updating destination manifest information.");
                    if (Preview == false)
                    {
                        DestRep.CopyManifestInformation(SourceRep);
                    }
                }
            }
            else if (SourceRep.Manifest.ManifestInfoLastModifiedUtc >
                DestRep.Manifest.ManifestInfoLastModifiedUtc)
            {
                WriteLine("Updating destination manifest information.");
                if (Preview == false)
                {
                    DestRep.CopyManifestInformation(SourceRep);
                }
            }
            else if (BothWays &&
                SourceRep.Manifest.ManifestInfoLastModifiedUtc <
                DestRep.Manifest.ManifestInfoLastModifiedUtc)
            {
                WriteLine("Updating source manifest information.");
                if (Preview == false)
                {
                    SourceRep.CopyManifestInformation(DestRep);
                }
            }

            // Add any new files that are only in source
            foreach (ManifestFileInfo nextSourceFile in SourceOnlyFiles)
            {
                Write(
                    "Adding to dest: " +
                    Manifest.MakeStandardPathString(nextSourceFile));

                PutFileHelper(
                    SourceRep,
                    DestRep,
                    nextSourceFile);

                WriteLine();
            }

            // Deal with files that are only in dest
            if (Mirror)
            {
                foreach (ManifestFileInfo nextDestFile in DestOnlyFiles)
                {
                    Write(
                        "Removing from dest: " +
                        Manifest.MakeStandardPathString(nextDestFile));

                    RemoveFileHelper(
                        DestRep,
                        nextDestFile);

                    WriteLine();
                }
            }
            else if (BothWays)
            {
                foreach (ManifestFileInfo nextDestFile in DestOnlyFiles)
                {
                    Write(
                        "Adding to source: " +
                        Manifest.MakeStandardPathString(nextDestFile));

                    PutFileHelper(
                        DestRep,
                        SourceRep,
                        nextDestFile);

                    WriteLine();
                }
            }

            // Update files as needed
            foreach (ManifestFileInfo nextSourceFile in ChangedFiles.Keys)
            {
                ManifestFileInfo nextDestFile = ChangedFiles[nextSourceFile];

                if (nextSourceFile.LastModifiedUtc >
                    nextDestFile.LastModifiedUtc)
                {
                    Write(
                        "Updating dest: " +
                        Manifest.MakeStandardPathString(nextSourceFile));

                    PutFileHelper(
                        SourceRep,
                        DestRep,
                        nextSourceFile);

                    WriteLine();
                }
                else if (
                    nextSourceFile.LastModifiedUtc <
                    nextDestFile.LastModifiedUtc)
                {
                    if (Mirror)
                    {
                        Write(
                            "Updating dest: " +
                            Manifest.MakeStandardPathString(nextSourceFile));

                        PutFileHelper(
                            SourceRep,
                            DestRep,
                            nextSourceFile);

                        WriteLine();
                    }
                    else if (BothWays)
                    {
                        Write(
                            "Updating source: " +
                            Manifest.MakeStandardPathString(nextSourceFile));

                        PutFileHelper(
                            DestRep,
                            SourceRep,
                            nextDestFile);

                        WriteLine();
                    }
                }
            }

            // Move any files in dest that were moved in source
            foreach (FileHash nextHash in MovedFileOrder)
            {
                MovedFileSet fileSet = MovedFiles[nextHash];

                // Determine if dest is newer than source.
                // Note that moved files can be a group of source files and a
                // group of dest files.  We check for the definitive case
                // where all source files are more recently modified than all
                // dest files.  So we compare the earliest source modification
                // against the latest dest modification.
                DateTime earliestSourceTime = DateTime.MaxValue;
                DateTime latestSourceTime = DateTime.MinValue;
                foreach (ManifestFileInfo nextSourceFile in fileSet.SourceFiles)
                {
                    if (nextSourceFile.RegisteredUtc < earliestSourceTime)
                    {
                        earliestSourceTime = nextSourceFile.RegisteredUtc;
                    }
                    if (nextSourceFile.RegisteredUtc > latestSourceTime)
                    {
                        latestSourceTime = nextSourceFile.RegisteredUtc;
                    }
                }

                DateTime earliestDestTime = DateTime.MaxValue;
                DateTime latestDestTime = DateTime.MinValue;
                foreach (ManifestFileInfo nextDestFile in fileSet.DestFiles)
                {
                    if (nextDestFile.RegisteredUtc < earliestDestTime)
                    {
                        earliestDestTime = nextDestFile.RegisteredUtc;
                    }
                    if (nextDestFile.RegisteredUtc > latestDestTime)
                    {
                        latestDestTime = nextDestFile.RegisteredUtc;
                    }
                }

                // Only handle the definitive cases
                bool ambiguousSet = true;
                if (Mirror || earliestSourceTime > latestDestTime)
                {
                    ambiguousSet = false;

                    MoveFileSet(
                        SourceRep,
                        fileSet.SourceFiles,
                        DestRep,
                        fileSet.DestFiles);
                }
                else if (earliestDestTime > latestSourceTime)
                {
                    ambiguousSet = false;

                    if (BothWays)
                    {
                        MoveFileSet(
                            DestRep,
                            fileSet.DestFiles,
                            SourceRep,
                            fileSet.SourceFiles);
                    }
                }
                
                if (ambiguousSet == true)
                {
                    WriteLine("Ambiguous - files were moved on both sides:");

                    foreach (ManifestFileInfo nextSourceFile in
                        fileSet.SourceFiles)
                    {
                        Write(
                            "   Source: " +
                            Manifest.MakeStandardPathString(nextSourceFile));                          
                    }

                    foreach (ManifestFileInfo nextDestFile in
                        fileSet.DestFiles)
                    {
                        Write(
                            "   Dest:   " +
                            Manifest.MakeStandardPathString(nextDestFile));
                    }

                    WriteLine();
                }
            }

            HashSet<ManifestFileInfo> infoUpdatedFiles =
                new HashSet<ManifestFileInfo>();

            // Handle last modified dates
            foreach (ManifestFileInfo nextSourceFile in LastModifiedDateFiles.Keys)
            {
                if (infoUpdatedFiles.Contains(nextSourceFile) == false)
                {
                    ManifestFileInfo nextDestFile =
                        LastModifiedDateFiles[nextSourceFile];

                    WriteLine(
                        "Updating dest information: " +
                        Manifest.MakeStandardPathString(nextSourceFile));

                    DestRep.CopyFileInformation(nextDestFile, nextSourceFile);

                    infoUpdatedFiles.Add(nextSourceFile);
                }
            }

            // Handle registered dates
            foreach (ManifestFileInfo nextSourceFile in RegisteredDateFiles.Keys)
            {
                if (infoUpdatedFiles.Contains(nextSourceFile) == false)
                {
                    ManifestFileInfo nextDestFile =
                        RegisteredDateFiles[nextSourceFile];

                    WriteLine(
                        "Updating dest information: " +
                        Manifest.MakeStandardPathString(nextSourceFile));

                    DestRep.CopyFileInformation(nextDestFile, nextSourceFile);

                    infoUpdatedFiles.Add(nextSourceFile);
                }
            }
        }

        protected void MoveFileSet(
            IRepositoryProxy sourceRep,
            List<ManifestFileInfo> sourceFiles,
            IRepositoryProxy destRep,
            List<ManifestFileInfo> destFiles)
        {
            // Move the existing dest files when possible to avoid
            // a copy which could be much slower.

            int nextSourceFileIndex = 0;
            int nextDestFileIndex = 0;

            int countDifference = sourceFiles.Count - destFiles.Count;

            // If some files must be copied, do that now
            if (countDifference > 0)
            {
                for (; nextSourceFileIndex < countDifference;
                    nextSourceFileIndex++)
                {
                    ManifestFileInfo nextSourceFile =
                        sourceFiles[nextSourceFileIndex];

                    Write(
                        "Copying: " +
                        Manifest.MakeStandardPathString(destFiles[0]) +
                        " -> " +
                        Manifest.MakeStandardPathString(nextSourceFile));

                    CopyFileHelper(
                        sourceRep,
                        destRep,
                        nextSourceFile,
                        destFiles[0]);

                    WriteLine();
                }
            }

            // Move as many files as already exist in dest
            for (; nextSourceFileIndex < sourceFiles.Count;
                    nextSourceFileIndex++, nextDestFileIndex++)
            {
                ManifestFileInfo nextSourceFile =
                    sourceFiles[nextSourceFileIndex];

                ManifestFileInfo nextDestFile =
                    destFiles[nextDestFileIndex];

                Write(
                    "Moving: " +
                    Manifest.MakeStandardPathString(nextDestFile) +
                    " -> " +
                    Manifest.MakeStandardPathString(nextSourceFile));

                MoveFileHelper(
                    sourceRep,
                    destRep,
                    nextSourceFile,
                    destFiles[nextDestFileIndex]);

                WriteLine();
            }

            // Remove any excess destination files
            for (; nextDestFileIndex < destFiles.Count;
                nextDestFileIndex++)
            {
                ManifestFileInfo nextDestFile =
                    destFiles[nextDestFileIndex];

                Write(
                    "Removing from dest: " +
                    Manifest.MakeStandardPathString(nextDestFile));

                RemoveFileHelper(
                    DestRep,
                    nextDestFile);

                WriteLine();
            }
        }

        // Helper methods
        
        protected void WriteLine(String message = "")
        {
            Write(message + "\r\n");
        }

        protected void Write(String message)
        {
            if (WriteLogDelegate != null)
            {
                WriteLogDelegate.Invoke(message);
            }
        }

        protected void PutFileHelper(
            IRepositoryProxy sourceRepository,
            IRepositoryProxy destRepository,
            ManifestFileInfo sourceFile)
        {
            if (Preview == false)
            {
                Exception exception;

                do
                {
                    exception = null;

                    try
                    {
                        destRepository.PutFile(
                            sourceRepository,
                            sourceFile);
                    }
                    catch (Exception ex)
                    {
                        exception = ex;

                        ErrorFiles.Add(sourceFile);

                        WriteLine(" [ERROR]");
                        WriteLine(ex.ToString());

                        if (Retry == true)
                        {
                            // Wait a minute...
                            System.Threading.Thread.Sleep(
                                theRetryMilliseconds);
                        }
                    }
                } while (exception != null & Retry == true);
            }
        }

        protected void MoveFileHelper(
            IRepositoryProxy sourceRepository,
            IRepositoryProxy destRepository,
            ManifestFileInfo sourceFileWithNewLocation,
            ManifestFileInfo destFileToBeMoved)
        {
            if (Preview == false)
            {
                Exception exception;

                do
                {
                    exception = null;

                    try
                    {
                        destRepository.MoveFile(
                            destFileToBeMoved,
                            sourceFileWithNewLocation);
                    }
                    catch (Exception ex)
                    {
                        exception = ex;

                        ErrorFiles.Add(sourceFileWithNewLocation);

                        WriteLine(" [ERROR]");
                        WriteLine(ex.ToString());

                        if (Retry == true)
                        {
                            // Wait a minute...
                            System.Threading.Thread.Sleep(
                                theRetryMilliseconds);
                        }
                    }
                } while (exception != null & Retry == true);
            }
        }

        protected void CopyFileHelper(
            IRepositoryProxy sourceRepository,
            IRepositoryProxy destRepository,
            ManifestFileInfo sourceFileWithNewLocation,
            ManifestFileInfo destFileToBeCopied)
        {
            if (Preview == false)
            {
                Exception exception;

                do
                {
                    exception = null;

                    try
                    {
                        destRepository.CopyFile(
                            destFileToBeCopied,
                            sourceFileWithNewLocation);

                    }
                    catch (Exception ex)
                    {
                        exception = ex;

                        ErrorFiles.Add(sourceFileWithNewLocation);

                        WriteLine(" [ERROR]");
                        WriteLine(ex.ToString());

                        if (Retry == true)
                        {
                            // Wait a minute...
                            System.Threading.Thread.Sleep(
                                theRetryMilliseconds);
                        }
                    }
                } while (exception != null & Retry == true);
            }
        }

        protected void RemoveFileHelper(
            IRepositoryProxy destRepository,
            ManifestFileInfo destFile)
        {
            if (Preview == false)
            {
                Exception exception;

                do
                {
                    exception = null;

                    try
                    {
                        destRepository.RemoveFile(destFile);
                    }
                    catch (Exception ex)
                    {
                        exception = ex;

                        ErrorFiles.Add(destFile);

                        WriteLine(" [ERROR]");
                        WriteLine(ex.ToString());

                        if (Retry == true)
                        {
                            // Wait a minute...
                            System.Threading.Thread.Sleep(
                                theRetryMilliseconds);
                        }
                    }
                } while (exception != null & Retry == true);
            }
        }


        // Data members and accessors

        public WriteLogDelegate WriteLogDelegate { set; get; } 

        public IRepositoryProxy SourceRep { private set; get; }
        public IRepositoryProxy DestRep { private set; get; }

        public bool Preview { set; get; }
        public bool BothWays { set; get; }
        public bool Mirror { set; get; }
        public bool Retry { set; get; }

        public List<ManifestFileInfo> SourceOnlyFiles { private set; get; }
        public List<ManifestFileInfo> DestOnlyFiles { private set; get; }
        public Dictionary<ManifestFileInfo, ManifestFileInfo> ChangedFiles { private set; get; }
        public Dictionary<ManifestFileInfo, ManifestFileInfo> LastModifiedDateFiles { private set; get; }
        public Dictionary<ManifestFileInfo, ManifestFileInfo> RegisteredDateFiles { private set; get; }
        public Dictionary<FileHash, MovedFileSet> MovedFiles { private set; get; }
        public List<FileHash> MovedFileOrder { private set; get; }
        public List<ManifestFileInfo> ErrorFiles { private set; get; }


        // Static

        static int theRetryMilliseconds = 60000;
    }
}