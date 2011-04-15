using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using RepositoryManifest;


namespace RepositorySync
{
    /// <summary>
    /// Manage state of a locally stored repository
    /// </summary>
    public class LocalRepositoryState
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="repositoryDirectory">
        /// The root directory where the manifest can be found
        /// </param>
        public LocalRepositoryState(
            DirectoryInfo rootDirectory)
        {
            RootDirectory = rootDirectory;

            LoadManifest();
            CreateTempDirectory();

            ManifestChanged = false;
        }

        /// <summary>
        /// Finalizer removes temp directory and writes manifest
        /// </summary>
        ~LocalRepositoryState()
        {
            Exception exception = null;

            try
            {
                TempDirectory.Delete(true);
            }
            catch (Exception ex)
            {
                if (exception == null)
                {
                    exception = ex;
                }
            }

            if (ManifestChanged)
            {
                Manifest.LastUpdateDateUtc = DateTime.UtcNow;
                try
                {
                    Manifest.WriteManifestFile(ManifestFilePath);
                }
                catch (Exception ex)
                {
                    // This exception overrides previous
                    exception = new Exception(
                     "Could not write manifest.",
                     ex);
                }
            }

            if (exception != null)
            {
                throw exception;
            }
        }


        // Helper methods

        protected void LoadManifest()
        {
            ManifestFilePath =
                Path.Combine(
                    RootDirectory.FullName,
                    Manifest.DefaultManifestFileName);

            try
            {
                Manifest = Manifest.ReadManifestFile(ManifestFilePath);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Could not read manifest.",
                    ex);
            }
        }

        protected void CreateTempDirectory()
        {
            try
            {
                TempDirectory = RootDirectory.CreateSubdirectory(
                    "temp-" +
                    Path.GetRandomFileName());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Could not create temporary directory in repository.",
                    ex);
            }
        }

        public String MakeNativePath(ManifestFileInfo file)
        {
            return Path.Combine(
                RootDirectory.FullName,
                Manifest.MakeNativePathString(file));
        }


        // Accessors

        public Manifest Manifest { protected set; get; }

        protected string ManifestFilePath { set; get; }
        protected DirectoryInfo RootDirectory { set; get; }
        protected DirectoryInfo TempDirectory { set; get; }

        public bool ManifestChanged { set; get; }
    }
}
