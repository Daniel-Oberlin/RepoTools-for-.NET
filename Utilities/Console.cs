﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RepositoryManifest;


namespace Utilities
{
    public class Console
    {
        public Console()
        {
            Silent = false;
            Detail = false;
        }

        public void Write(String message)
        {
            if (Silent == false)
            {
                System.Console.Write(message);
            }
        }

        public void WriteLine(String message)
        {
            Write(message + "\r\n");
        }

        public void WriteLine()
        {
            WriteLine("");
        }

        public void ReportException(Exception ex)
        {
            WriteLine(ex.GetType().ToString() + ": " + ex.Message);
        }

        public bool CheckConfirm()
        {
            String confirmString = System.Console.ReadLine();

            if (confirmString.StartsWith("y") ||
                confirmString.StartsWith("Y"))
            {
                return true;
            }

            return false;
        }

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

        public bool Silent { set; get; }
        public bool Detail { set; get; }
    }
}
