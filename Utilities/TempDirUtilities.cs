using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Utilities
{
    public class TempDirUtilities
    {
        static public DirectoryInfo GetSystemTempDirectory()
        {
            string tempPath = Environment.GetFolderPath(
                Environment.SpecialFolder.InternetCache);

            // The special folder does not exist on mono
            if (string.IsNullOrEmpty(tempPath))
            {
                tempPath = "/var/tmp/";
            }

            if (!Directory.Exists(tempPath))
            {
                throw new Exception(
                    "Temp path does not exist: " + tempPath);
            }

            return new DirectoryInfo(tempPath);
        }

        static public DirectoryInfo CreateTempDirectoryIn(
            DirectoryInfo topLevelDirectory)
        {
            try
            {
                return topLevelDirectory.CreateSubdirectory(
                    theTempDirectoryPrefix +
                    Path.GetRandomFileName());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Could not create temporary directory inside: " +
                    topLevelDirectory,
                    ex);
            }
        }

        static public void RemoveExtraTempDirectoriesFrom(
            DirectoryInfo topLevelDirectory)
        {
            foreach (DirectoryInfo nextSubDirectory in
                topLevelDirectory.GetDirectories())
            {
                if (nextSubDirectory.Name.StartsWith(theTempDirectoryPrefix) &&
                    nextSubDirectory.GetFiles().Count() == 0 &&
                    nextSubDirectory.GetDirectories().Count() == 0)
                {
                    nextSubDirectory.Delete();
                }
            }
        }

        static protected String theTempDirectoryPrefix = "temp-";
    }
}
