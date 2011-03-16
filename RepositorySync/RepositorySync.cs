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

            SourceOnlyFiles = new List<ManifestFileInfo>();
            DestOnlyFiles = new List<ManifestFileInfo>();
            ChangedFiles = new List<ManifestFileInfo>();
            DateChangedFiles = new List<ManifestFileInfo>();
            MovedFiles = new Dictionary<FileHash, MovedFileSet>();
            MovedFileOrder = new List<FileHash>();
        }

        public void Clear()
        {
            SourceOnlyFiles.Clear();
            DestOnlyFiles.Clear();
            ChangedFiles.Clear();
            DateChangedFiles.Clear();
            MovedFiles.Clear();
            MovedFileOrder.Clear();
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
                        ChangedFiles.Add(sourceFile);
                    }
                    else if (
                        sourceFile.LastModifiedUtc !=
                        destFile.LastModifiedUtc)
                    {
                        DateChangedFiles.Add(sourceFile);
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

                    MovedFiles[checkSourceFile.FileHash].OldFiles.Add(checkSourceFile);

                    if (MovedFiles[checkSourceFile.FileHash].NewFiles.Count == 0)
                    {
                        // First time only
                        foreach (ManifestFileInfo nextNewFile in destFileDict.Dict[checkSourceFile.FileHash])
                        {
                            MovedFiles[checkSourceFile.FileHash].NewFiles.Add(nextNewFile);

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


        // Data members and accessors

        public WriteLogDelegate WriteLogDelegate { set; get; } 

        public RepositoryProxy SourceRep { private set; get; }
        public RepositoryProxy DestRep { private set; get; }

        public List<ManifestFileInfo> SourceOnlyFiles { private set; get; }
        public List<ManifestFileInfo> DestOnlyFiles { private set; get; }
        public List<ManifestFileInfo> ChangedFiles { private set; get; }
        public List<ManifestFileInfo> DateChangedFiles { private set; get; }
        public Dictionary<FileHash, MovedFileSet> MovedFiles { private set; get; }
        public List<FileHash> MovedFileOrder { private set; get; }
    }
}