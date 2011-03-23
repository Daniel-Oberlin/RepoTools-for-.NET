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
            RepositoryProxy source,
            RepositoryProxy dest)
        {
            SourceRep = source;
            DestRep = dest;

            Preview = false;

            SourceOnlyFiles = new List<ManifestFileInfo>();
            DestOnlyFiles = new List<ManifestFileInfo>();
            ChangedFiles = new Dictionary<ManifestFileInfo, ManifestFileInfo>();
            LastModifiedDateFiles = new List<ManifestFileInfo>();
            CreationDateFiles = new List<ManifestFileInfo>();
            ManifestCreationDateFiles = new List<ManifestFileInfo>();
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
            CreationDateFiles.Clear();
            ManifestCreationDateFiles.Clear();
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
                            LastModifiedDateFiles.Add(sourceFile);
                        }

                        if (sourceFile.CreationUtc !=
                            destFile.CreationUtc)
                        {
                            CreationDateFiles.Add(sourceFile);
                        }

                        if (sourceFile.ManifestCreationUtc !=
                            destFile.ManifestCreationUtc)
                        {
                            ManifestCreationDateFiles.Add(sourceFile);
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
            if (SourceRep.Manifest.ManifestInfoLastModifiedUtc >
                DestRep.Manifest.ManifestInfoLastModifiedUtc)
            {
                WriteLine("Updating source manifest information.");
                SourceRep.Manifest.CopyManifestInfoFrom(DestRep.Manifest);
            }

            // Add any files that are not in dest
            foreach (ManifestFileInfo nextSourceFile in SourceOnlyFiles)
            {
                Write(
                    "Adding: " +
                    Manifest.MakeStandardPathString(nextSourceFile));

                PutFileHelper(nextSourceFile);

                WriteLine();
            }

            // Update any files in dest that are older than source
            foreach (ManifestFileInfo nextSourceFile in ChangedFiles.Keys)
            {
                ManifestFileInfo nextDestFile = ChangedFiles[nextSourceFile];

                if (nextSourceFile.LastModifiedUtc >
                    nextDestFile.LastModifiedUtc)
                {
                    Write(
                        "Updating: " +
                        Manifest.MakeStandardPathString(nextSourceFile));

                    PutFileHelper(nextSourceFile);

                    WriteLine();
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
                foreach (ManifestFileInfo nextSourceFile in fileSet.SourceFiles)
                {
                    if (nextSourceFile.LastModifiedUtc < earliestSourceTime)
                    {
                        earliestSourceTime = nextSourceFile.ManifestCreationUtc;
                    }
                }

                DateTime latestDestTime = DateTime.MinValue;
                foreach (ManifestFileInfo nextDestFile in fileSet.DestFiles)
                {
                    if (nextDestFile.LastModifiedUtc > latestDestTime)
                    {
                        latestDestTime = nextDestFile.ManifestCreationUtc;
                    }
                }

                // Only handle the definitive case
                if (earliestSourceTime > latestDestTime)
                {
                    // Move the existing dest files when possible to avoid
                    // a copy which could be much slower.
                    Stack<ManifestFileInfo> existingDestFiles =
                        new Stack<ManifestFileInfo>();

                    foreach (ManifestFileInfo destFile in fileSet.DestFiles)
                    {
                        existingDestFiles.Push(destFile);
                    }

                    foreach (ManifestFileInfo nextSourceFile in fileSet.SourceFiles)
                    {
                        ManifestFileInfo lastMovedFile = null;

                        if (existingDestFiles.Count > 0)
                        {
                            ManifestFileInfo moveDestFile =
                                existingDestFiles.Pop();

                            Write(
                                "Moving: " +
                                Manifest.MakeStandardPathString(moveDestFile) +
                                " -> " +
                                Manifest.MakeStandardPathString(nextSourceFile));

                            lastMovedFile = MoveFileHelper(nextSourceFile, moveDestFile);

                            WriteLine();
                        }
                        else
                        {
                            Write(
                                "Copying: " +
                                Manifest.MakeStandardPathString(lastMovedFile) +
                                " -> " +
                                Manifest.MakeStandardPathString(nextSourceFile));

                            CopyFileHelper(nextSourceFile, lastMovedFile);

                            WriteLine();
                        }
                    }
                }
                else
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
            ManifestFileInfo sourceFile)
        {
            if (Preview == false)
            {
                try
                {
                    DestRep.PutFile(
                        SourceRep,
                        sourceFile);
                }
                catch (Exception ex)
                {
                    ErrorFiles.Add(sourceFile);

                    WriteLine(" [ERROR]");
                    Write(ex.ToString());
                }
            }
        }

        protected ManifestFileInfo MoveFileHelper(
            ManifestFileInfo sourceFileWithNewLocation,
            ManifestFileInfo destFileToBeMoved)
        {
            ManifestFileInfo movedFile = null;

            if (Preview == false)
            {
                try
                {
                    movedFile = DestRep.MoveFile(
                        destFileToBeMoved,
                        SourceRep,
                        sourceFileWithNewLocation);
                }
                catch (Exception ex)
                {
                    ErrorFiles.Add(sourceFileWithNewLocation);

                    WriteLine(" [ERROR]");
                    Write(ex.ToString());
                }
            }

            return movedFile;
        }

        protected ManifestFileInfo CopyFileHelper(
            ManifestFileInfo sourceFileWithNewLocation,
            ManifestFileInfo destFileToBeCopied)
        {
            ManifestFileInfo copiedFile = null;

            if (Preview == false)
            {
                try
                {
                    copiedFile = DestRep.CopyFile(
                        destFileToBeCopied,
                        SourceRep,
                        sourceFileWithNewLocation);

                }
                catch (Exception ex)
                {
                    ErrorFiles.Add(sourceFileWithNewLocation);

                    WriteLine(" [ERROR]");
                    Write(ex.ToString());
                }
            }

            return copiedFile;
        }


        // Data members and accessors

        public WriteLogDelegate WriteLogDelegate { set; get; } 

        public RepositoryProxy SourceRep { private set; get; }
        public RepositoryProxy DestRep { private set; get; }

        public bool Preview { set; get; }

        public List<ManifestFileInfo> SourceOnlyFiles { private set; get; }
        public List<ManifestFileInfo> DestOnlyFiles { private set; get; }
        public Dictionary<ManifestFileInfo, ManifestFileInfo> ChangedFiles { private set; get; }
        public List<ManifestFileInfo> LastModifiedDateFiles { private set; get; }
        public List<ManifestFileInfo> CreationDateFiles { private set; get; }
        public List<ManifestFileInfo> ManifestCreationDateFiles { private set; get; }
        public Dictionary<FileHash, MovedFileSet> MovedFiles { private set; get; }
        public List<FileHash> MovedFileOrder { private set; get; }
        public List<ManifestFileInfo> ErrorFiles { private set; get; }
    }
}