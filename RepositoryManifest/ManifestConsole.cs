using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Utilities;


namespace RepositoryManifest
{
    public class ManifestConsole : Utilities.Console
    {
        public void DetailFiles(IEnumerable<ManifestFileInfo> files)
        {
            if (Detail)
            {
                foreach (ManifestFileInfo nextManFileInfo in files)
                {
                    WriteLine("   " + Manifest.MakeStandardPathString(nextManFileInfo));
                }

                WriteLine();
            }
        }

        public void DetailFiles(
            List<FileHash> movedFileOrder,
            Dictionary<FileHash, MovedFileSet> movedFileSets,
            bool reverseOrder = false)
        {
            if (Detail)
            {
                foreach (FileHash nextHash in movedFileOrder)
                {
                    Write("   ");
                    MovedFileSet nextFileSet = movedFileSets[nextHash];

                    List<ManifestFileInfo> leftSide =
                        nextFileSet.OldFiles;

                    List<ManifestFileInfo> rightSide =
                        nextFileSet.NewFiles;

                    if (reverseOrder)
                    {
                        List<ManifestFileInfo> temp = leftSide;
                        leftSide = rightSide;
                        rightSide = temp;
                    }

                    foreach (ManifestFileInfo nextOldFile in leftSide)
                    {
                        Write(Manifest.MakeStandardPathString(nextOldFile));
                        Write(" ");
                    }

                    Write("->");

                    foreach (ManifestFileInfo nextNewFile in rightSide)
                    {
                        Write(" ");
                        Write(Manifest.MakeStandardPathString(nextNewFile));
                    }
                    WriteLine();
                }

                WriteLine();
            }
        }

        public void DetailFiles(
            Dictionary<FileHash, List<ManifestFileInfo>> files)
        {
            if (Detail)
            {
                foreach (FileHash nextHash in files.Keys)
                {
                    WriteLine("   " + nextHash.ToString());

                    foreach (ManifestFileInfo nextFile in files[nextHash])
                    {
                        WriteLine("      " + Manifest.MakeStandardPathString(nextFile));
                    }
                    WriteLine();
                }
                WriteLine();
            }
        }
    }
}
