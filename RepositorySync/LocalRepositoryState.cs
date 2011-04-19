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

            ManifestFileLock = new object();

            myManifestChangedLock = new object();
            myManifestChanged = false;
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

            FlushManifest();

            if (exception != null)
            {
                throw exception;
            }
        }

        public void FlushManifest()
        {
            if (ManifestChanged)
            {
                Manifest manifestClone;
                lock (Manifest)
                {
                    Manifest.LastUpdateDateUtc = DateTime.UtcNow;
                    manifestClone = CloneManifest();

                    // Assume success - back out later if we need to
                    ClearManifestChanged();
                }

                try
                {
                    lock (ManifestFileLock)
                    {
                        Manifest.WriteManifestFile(ManifestFilePath);
                    }
                }
                catch (Exception ex)
                {
                    // Not successful
                    SetManifestChanged();
                    throw new Exception(
                        "Could not write manifest.",
                        ex);
                }
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

        public Manifest CloneManifest()
        {
            lock (Manifest)
            {
                return new Manifest(Manifest);
            }
        }

        public Object ManifestFileLock { protected set; get; }

        public DirectoryInfo RootDirectory { protected set; get; }
        public DirectoryInfo TempDirectory { protected set; get; }

        public string ManifestFilePath { protected set; get; }

        public void SetManifestChanged()
        {
            lock (myManifestChangedLock)
            {
                myManifestChanged = true;
            }
        }

        protected void ClearManifestChanged()
        {
            lock (myManifestChangedLock)
            {
                myManifestChanged = false;
            }
        }

        public bool ManifestChanged
        {
            get
            {
                lock (myManifestChangedLock)
                {
                    return myManifestChanged;
                }
            }
        }

        protected bool myManifestChanged;
        protected object myManifestChangedLock;
    }
}
