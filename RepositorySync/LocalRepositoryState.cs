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

            ManifestFileLock = new object();
            myManifestChangedLock = new object();
            myManifestChanged = false;

            RootDirectory = rootDirectory;

            LoadManifest();
            RemoveExtraTempDirectories();
            CreateTempDirectory();
        }

        /// <summary>
        /// Finalizer removes temp directory and writes manifest
        /// </summary>
        ~LocalRepositoryState()
        {
            Exception exception = null;

            if (TempDirectory != null)
            {
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
            }

            FlushManifest();

            if (exception != null)
            {
                throw exception;
            }
        }

        public void FlushManifest()
        {
            if (ManifestChanged && Manifest != null)
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
            // Here we are using a trick that a standard file path can be
            // interpreted correctly as the latter part of a native path in
            // MS-DOS.
            ManifestFilePath =
                Path.Combine(
                    RootDirectory.FullName,
                    Manifest.DefaultManifestStandardFilePath);

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
                    theTempDirectoryPrefix +
                    Path.GetRandomFileName());
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Could not create temporary directory in repository.",
                    ex);
            }
        }

        protected void RemoveExtraTempDirectories()
        {
            foreach (DirectoryInfo nextSubDirectory in
                RootDirectory.GetDirectories())
            {
                if (nextSubDirectory.Name.StartsWith(theTempDirectoryPrefix) &&
                    nextSubDirectory.GetFiles().Count() == 0 &&
                    nextSubDirectory.GetDirectories().Count() == 0)
                {
                    nextSubDirectory.Delete();
                }
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


        // Static

        static protected String theTempDirectoryPrefix = "temp-";
    }
}
