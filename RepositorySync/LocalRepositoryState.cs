using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using RepositoryManifest;
using Utilities;


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
            DirectoryInfo rootDirectory,
            bool readOnly)
        {
            ManifestFileLock = new object();
            myManifestChangedLock = new object();
            myManifestChanged = false;
            myReadOnly = readOnly;

            RootDirectory = rootDirectory;

            LoadManifest();

            if (readOnly == false)
            {
                TempDirUtilities.RemoveExtraTempDirectoriesFrom(
                    RootDirectory);
                
                TempDirectory = TempDirUtilities.CreateTempDirectoryIn(
                    RootDirectory);
            }

            // There might not be a console present, but I guess it doesn't
            // hurt to put this here.
            System.Console.CancelKeyPress += delegate
            {
                CleanupBeforeExit();
            };
        }

        /// <summary>
        /// Finalizer removes temp directory and writes manifest
        /// </summary>
        ~LocalRepositoryState()
        {
            CleanupBeforeExit();
        }

        public void CleanupBeforeExit()
        {
            Exception exception = null;

            if (TempDirectory != null)
            {
                try
                {
                    TempDirectory.Delete(true);
                    TempDirectory = null;
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
            ManifestFilePath =
                PathUtilities.NativeFromNativeAndStandard(
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

        protected bool myReadOnly;
        protected bool myManifestChanged;
        protected object myManifestChangedLock;
    }
}
