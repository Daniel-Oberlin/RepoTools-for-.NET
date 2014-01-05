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
    /// Implementation of encrypted repository.
    /// </summary>
    class CryptRepositoryProxy : IRepositoryProxy
    {
        // TODO: doc
        public CryptRepositoryProxy(
            IRepositoryProxy innerProxy,
            byte[] outerKey,
            bool readOnly) :
            this(innerProxy, null, outerKey, readOnly)
        {
        }

        // TODO: doc
        protected CryptRepositoryProxy(
            IRepositoryProxy innerProxy,
            Manifest outerManifest,
            byte[] outerKey,
            bool readOnly)
        {
            InnerProxy = innerProxy;
            OuterKey = outerKey;

            ProxyToInner = new ProxyToInnerProxy(this);
            myManifestChanged = false;
            myNeedToRegenerateFileMap = true;
            myReadOnly = readOnly;

            // Lazy generation
            myHashToInnerFileMap = null;

            if (outerManifest != null)
            {
                // Used during "seed" operation
                OuterManifest = outerManifest;
            }
            else
            {
                LoadOuterManifest();
            }

            // Generate map of outer hashes to inner files, find unresolved
            // outer files, and find orphaned inner files.
            ResolveInnerOuter();

            // Remove unresolved outer files
            foreach (ManifestFileInfo nextFile in UnresolvedOuterFiles)
            {
                nextFile.ParentDirectory.Files.Remove(nextFile.Name);
            }

            if (myReadOnly == false)
            {
                // CryptProxy does not own any space - the inner proxy could be
                // remote.  So we put the temp directory in the system temp.
                DirectoryInfo tempRootDirectory =
                    TempDirUtilities.GetSystemTempDirectory();

                TempDirUtilities.RemoveExtraTempDirectoriesFrom(
                    tempRootDirectory);

                TempDirectory = TempDirUtilities.CreateTempDirectoryIn(
                    tempRootDirectory);
            }
        }

        /// <summary>
        /// Finalizer removes temp directory and writes manifest
        /// </summary>
        ~CryptRepositoryProxy()
        {
            CleanupBeforeExit();
        }

        public void CleanupBeforeExit()
        {
            if (myReadOnly == false)
            {
                SaveOuterManifest();

                // Removed orphaned data files
                ResolveInnerOuter();
                foreach (ManifestFileInfo nextFile in OrphanedInnerFiles)
                {
                    InnerProxy.RemoveFile(nextFile);
                }

                TempDirectory.Delete(true);
            }
        }

        public Manifest Manifest
        {
            get { return OuterManifest; }
        }

        public void PutFile(
            IRepositoryProxy sourceRepository,
            ManifestFileInfo sourceManifestFile)
        {
            // Only add the file data if we don't have it already.
            if (HashToInnerFileMap.ContainsKey(
                sourceManifestFile.FileHash) == false)
            {
                FileInfo sourceFileInfo =
                    sourceRepository.GetFile(sourceManifestFile);

                byte[] keyData = sourceManifestFile.FileHash.HashData;

                String destFilePath =
                    Path.Combine(
                        TempDirectory.FullName,
                        DefaultEncryptedTempFileName);

                FileInfo cryptFileInfo =
                    CryptFile(
                        sourceFileInfo,
                        keyData,
                        destFilePath);

                // Name the inner file with the hash of the hash.  We protect
                // the hash in this way because it is used as the key to
                // encrypt the data in the file.
                FileHash hashedHash = CryptUtilities.ComputeHash(
                    sourceManifestFile.FileHash.HashData);

                String hashedHashString = hashedHash.ToString();

                // Make a dummy parent manifest directory to give to the inner
                // proxy.  This is actually rooted in the inner manifest, but
                // that is ok - although it is kind of a hack.  The fact is
                // that we don't maintain an actual manifest to mirror the
                // inner manifest - and we know that the implementation of
                // PutFile won't be affected by doing this.
                ManifestDirectoryInfo parentDirectory =
                    MakeInnerParentDirectory(
                        hashedHashString);

                ManifestFileInfo destManifestFile =
                    new ManifestFileInfo(
                        hashedHashString,
                        parentDirectory);

                destManifestFile.RegisteredUtc = DateTime.Now;

                destManifestFile.LastModifiedUtc =
                    cryptFileInfo.LastWriteTimeUtc;

                destManifestFile.FileLength =
                    cryptFileInfo.Length;

                destManifestFile.FileHash =
                    CryptUtilities.ComputeHash(cryptFileInfo);

                InnerProxy.PutFile(ProxyToInner, destManifestFile);

                myNeedToRegenerateFileMap = true;
            }

            ManifestFileInfo outerManifestFileInfo =
                Manifest.PutFileFromOtherManifest(sourceManifestFile);

            myManifestChanged = true;
        }

        public void RemoveFile(
            ManifestFileInfo removeManifestFile)
        {
            // Just remove the file from the outer manifest for now.
            // When we clean up, we'll remove the actual file if there
            // are no longer any references to it.
            removeManifestFile.ParentDirectory.Files.Remove(
                removeManifestFile.Name);

            myManifestChanged = true;
        }

        public void CopyFile(
            ManifestFileInfo fileToBeCopied,
            ManifestFileInfo otherFileWithNewLocation)
        {
            // No need to actually copy a file, just copy it in the
            // outer manifest.
            ManifestFileInfo newFileInfo =
                Manifest.PutFileFromOtherManifest(
                    otherFileWithNewLocation);

            myManifestChanged = true;
        }

        public void CopyFileInformation(
            ManifestFileInfo fileToBeUpdated,
            ManifestFileInfo otherFileWithNewFileInfo)
        {
            // No need to actually change the file information, just change
            // it in the outer manifest.
            fileToBeUpdated.LastModifiedUtc =
                otherFileWithNewFileInfo.LastModifiedUtc;

            fileToBeUpdated.RegisteredUtc =
                otherFileWithNewFileInfo.RegisteredUtc;

            myManifestChanged = true;
        }

        public void MoveFile(
            ManifestFileInfo fileToBeMoved,
            ManifestFileInfo otherFileWithNewLocation)
        {
            // No need to actually move a file, just move it in the
            // outer manifest.

            fileToBeMoved.ParentDirectory.Files.Remove(
                fileToBeMoved.Name);

            ManifestFileInfo newFileInfo =
                Manifest.PutFileFromOtherManifest(
                    otherFileWithNewLocation);

            myManifestChanged = true;
        }

        public void CopyManifestInformation(
            IRepositoryProxy otherRepository)
        {
            Manifest.CopyManifestInfoFrom(
                otherRepository.Manifest);

            myManifestChanged = true;
        }

        public FileInfo GetFile(ManifestFileInfo readFile)
        {
            // We don't have an unencrypted copy, so we must make a temp clone
            // and supply that instead.  The clone will be removed eventually
            // during cleanup when the temp directory is deleted.
            return CloneFile(readFile, TempDirectory);
        }

        public FileInfo CloneFile(
            ManifestFileInfo copyFile,
            DirectoryInfo copyToDirectory)
        {
            ManifestFileInfo innerManifestFileInfo =
                HashToInnerFileMap[copyFile.FileHash];

            FileInfo innerFileInfo =
                InnerProxy.GetFile(innerManifestFileInfo);

            byte[] keyData = copyFile.FileHash.HashData;

            String destFilePath =
                Path.Combine(
                    copyToDirectory.FullName,
                    DefaultDecryptedTempFileName);

            FileInfo fileInfo = CryptFile(
                innerFileInfo,
                keyData,
                destFilePath);

            // Make sure that the last-modified date matches that of the
            // expected outer file.  This is necessary because one inner file
            // may correspond to several outer files - each of which might
            // have separate dates.
            fileInfo.LastWriteTimeUtc = copyFile.LastModifiedUtc;

            return fileInfo;
        }

        public FileInfo GetInnerFile(ManifestFileInfo readFile)
        {
            // We don't have an encrypted copy, so we must make a temp clone
            // and supply that instead.  The clone will be removed eventually
            // during cleanup when the temp directory is deleted.
            return CloneFile(readFile, TempDirectory);
        }

        public FileInfo CloneInnerFile(
            ManifestFileInfo copyFile,
            DirectoryInfo copyToDirectory)
        {
            // This method is a callback from the InnerProxy, and we expect
            // that the encrypted inner file has already been created - so
            // all that remains is to move the file to the destination
            // directory.  This isn't as efficient as it could be, but the
            // calling structure requires that the ManifestFileInfo be
            // set up before this callback occurs, and the ManifestFileInfo
            // must have the hash of the encrypted file.  So in effect, the
            // encrypted file must already be generated before this method
            // is called.  Otherwise we could be more efficient and generate
            // the file directly in the destination directory.
            String sourceFilePath =
                Path.Combine(
                    TempDirectory.FullName,
                    DefaultEncryptedTempFileName);

            String destFilePath =
                Path.Combine(
                    copyToDirectory.FullName,
                    DefaultEncryptedTempFileName);

            File.Move(sourceFilePath, destFilePath);

            return new FileInfo(destFilePath);
        }


        // Helper methods

        protected void LoadOuterManifest()
        {
            if (InnerProxy.Manifest.RootDirectory.Files.ContainsKey(
                DefaultOuterManifestFileName) == false)
            {
                throw new Exception("Encrypted manifest is not present.");
            }

            ManifestFileInfo outerManifestManifestFileInfo =
                InnerProxy.Manifest.RootDirectory.Files[
                    DefaultOuterManifestFileName];

            FileInfo outerManifestFileInfo =
                InnerProxy.GetFile(outerManifestManifestFileInfo);

            Stream outerManifestFileStream =
                outerManifestFileInfo.OpenRead();

            Stream outerManifestCryptoStream =
                CryptUtilities.MakeCryptoReadStreamFrom(
                    outerManifestFileStream,
                    OuterKey);

            OuterManifest =
                Manifest.ReadManifestStream(outerManifestCryptoStream);

            outerManifestCryptoStream.Close();
        }

        protected void SaveOuterManifest()
        {
            // Write the encrypted outer manifest
            String tempFilePath =
                Path.Combine(
                    TempDirectory.FullName,
                    DefaultEncryptedTempFileName);

            Stream outerManifestFileStream =
                File.OpenWrite(tempFilePath);

            Stream outerManifestCryptoStream =
                CryptUtilities.MakeCryptoWriteStreamFrom(
                    outerManifestFileStream,
                    OuterKey);

            OuterManifest.WriteManifestStream(outerManifestCryptoStream);
            outerManifestCryptoStream.Close();

            // The new ManifestFileInfo is actually rooted in the inner
            // manifest, but that is ok - although it is kind of a hack.
            // The fact is that we don't maintain an actual manifest to
            // mirror the inner manifest - and we know that the
            // implementation of PutFile won't be affected by doing this.
            ManifestDirectoryInfo parentDirectory =
                InnerProxy.Manifest.RootDirectory;

            ManifestFileInfo destManifestFile =
                new ManifestFileInfo(
                    DefaultOuterManifestFileName,
                    parentDirectory);

            destManifestFile.RegisteredUtc = DateTime.Now;

            FileInfo outerManifestFileInfo = new FileInfo(tempFilePath);

            destManifestFile.LastModifiedUtc =
                outerManifestFileInfo.LastWriteTimeUtc;

            destManifestFile.FileLength =
                outerManifestFileInfo.Length;

            destManifestFile.FileHash =
                CryptUtilities.ComputeHash(outerManifestFileInfo);

            InnerProxy.PutFile(ProxyToInner, destManifestFile);
        }

        // Generate map of outer hashes to inner files, find unresolved
        // outer files, and find orphaned inner files.
        protected void ResolveInnerOuter()
        {
            Dictionary<String, ManifestFileInfo> hashedStringMap =
                new Dictionary<string, ManifestFileInfo>();

            BuildHashedStringMap(
                InnerProxy.Manifest.RootDirectory,
                hashedStringMap);

            myHashToInnerFileMap =
                new Dictionary<FileHash, ManifestFileInfo>();

            UnresolvedOuterFiles =
                new List<ManifestFileInfo>();

            BuildHashToInnerFileMap(
                OuterManifest.RootDirectory,
                hashedStringMap);

            OrphanedInnerFiles =
                new List<ManifestFileInfo>();

            BuildInnerFilesOrphanedList(hashedStringMap);
        }

        protected void BuildHashedStringMap(
            ManifestDirectoryInfo dir,
            Dictionary<String, ManifestFileInfo> hashedStringMap)
        {
            foreach (String nextFileName
                in dir.Files.Keys)
            {
                if (nextFileName != DefaultOuterManifestFileName)
                {
                    hashedStringMap.Add(
                        nextFileName,
                        dir.Files[nextFileName]);
                }
            }

            foreach (ManifestDirectoryInfo nextDir in
                dir.Subdirectories.Values)
            {
                BuildHashedStringMap(nextDir, hashedStringMap);
            }
        }

        protected void BuildHashToInnerFileMap(
            ManifestDirectoryInfo dir,
            Dictionary<String, ManifestFileInfo> hashedStringMap)
        {
            foreach (ManifestFileInfo nextFile in dir.Files.Values)
            {
                FileHash fileHash = nextFile.FileHash;
                if (myHashToInnerFileMap.ContainsKey(fileHash))
                {
                    continue;
                }

                // Using FileHash class for convenience
                FileHash hashedHash = CryptUtilities.ComputeHash(
                    fileHash.HashData);

                String hashedHashString = hashedHash.ToString();

                if (hashedStringMap.Keys.Contains(
                    hashedHashString))
                {
                    myHashToInnerFileMap[fileHash] =
                        hashedStringMap[hashedHashString];
                }
                else
                {
                    UnresolvedOuterFiles.Add(nextFile);
                }
            }

            foreach (ManifestDirectoryInfo nextDir in
                dir.Subdirectories.Values)
            {
                BuildHashToInnerFileMap(
                    nextDir,
                    hashedStringMap);
            }
        }

        protected void BuildInnerFilesOrphanedList(
            Dictionary<String, ManifestFileInfo> hashedStringMap)
        {
            HashSet<ManifestFileInfo> resolvedInnerFiles =
                new HashSet<ManifestFileInfo>();

            foreach (ManifestFileInfo nextFile in
                myHashToInnerFileMap.Values)
            {
                resolvedInnerFiles.Add(nextFile);
            }

            foreach (ManifestFileInfo nextFile in
                hashedStringMap.Values)
            {
                if (resolvedInnerFiles.Contains(nextFile) == false)
                {
                    OrphanedInnerFiles.Add(nextFile);
                }
            }
        }

        protected ManifestDirectoryInfo MakeInnerParentDirectory(
            String hashedHashString)
        {
            // TODO: Make real implementation
            //
            //
            //

            return InnerProxy.Manifest.RootDirectory;
        }

        protected FileInfo CryptFile(
            FileInfo sourceFileInfo,
            byte[] keyData,
            String destFilePath)
        {
            Stream sourceFileStream =
                sourceFileInfo.OpenRead();

            Stream cryptoStream =
                CryptUtilities.MakeCryptoReadStreamFrom(
                    sourceFileStream,
                    keyData);

            FileStream destFileStream =
                File.OpenWrite(destFilePath);

            StreamUtilities.CopyStream(
                cryptoStream, destFileStream);

            destFileStream.Close();

            return new FileInfo(destFilePath);
        }


        // Accessors

        public DirectoryInfo TempDirectory { protected set; get; }

        public List<ManifestFileInfo> UnresolvedOuterFiles
            { protected set; get; }

        public List<ManifestFileInfo> OrphanedInnerFiles
            { protected set; get; }

        protected IRepositoryProxy InnerProxy { set; get; }
        protected ProxyToInnerProxy ProxyToInner { set; get; }

        protected Dictionary<FileHash, ManifestFileInfo> HashToInnerFileMap
        {
            get
            {
                if (myNeedToRegenerateFileMap == true)
                {
                    ResolveInnerOuter();
                    myNeedToRegenerateFileMap = false;
                }

                return myHashToInnerFileMap;
            }
        }

        protected Dictionary<FileHash, ManifestFileInfo> myHashToInnerFileMap;

        protected byte[] OuterKey { set; get; }
        protected Manifest OuterManifest { set; get; }

        protected bool myNeedToRegenerateFileMap;
        protected bool myManifestChanged;
        protected bool myReadOnly;


        // Static

        /// <summary>
        /// Static constructor
        /// </summary>
        static CryptRepositoryProxy()
        {
            DefaultOuterManifestFileName =
                Manifest.DefaultManifestFileName +
                ".outer";
        }

        static public void SeedLocalRepository(
            Manifest sourceManifest,
            byte[] outerKey,
            String seedDirectoryPath,
            // TODO: Use delegate for console like in other classes?
            Utilities.Console console)
        {
            Manifest innerManifestPrototype =
                Manifest.MakeCleanManifest();

            LocalRepositoryProxy.SeedLocalRepository(
                innerManifestPrototype,
                seedDirectoryPath,
                console);

            LocalRepositoryProxy innerProxy =
                new LocalRepositoryProxy(
                    new DirectoryInfo(seedDirectoryPath),
                    false);

            Manifest outerManifest = new Manifest(sourceManifest);

            outerManifest.RootDirectory.Files.Clear();
            outerManifest.RootDirectory.Subdirectories.Clear();
            outerManifest.LastUpdateDateUtc = new DateTime();

            CryptRepositoryProxy cryptProxy =
                new CryptRepositoryProxy(
                    innerProxy,
                    outerManifest,
                    outerKey,
                    false);

            try
            {
                cryptProxy.SaveOuterManifest();
            }
            catch (Exception e)
            {
                console.WriteLine("Exception: " + e.Message);
                console.WriteLine("Could not write destination encrypted manifest.");
                Environment.Exit(1);
            }

            // Note, manifest will be written again when the CryptProxy is
            // finalized.
        }


        /// <summary>
        /// The default file path for the outer manifest
        /// </summary>
        public static String DefaultOuterManifestFileName;

        public static String DefaultEncryptedTempFileName =
            "temp-encrypted-data";

        public static String DefaultDecryptedTempFileName =
            "temp-decrypted-data";

        // Helper class supplied to InnerProxy
        protected class ProxyToInnerProxy : IRepositoryProxy
        {
            public ProxyToInnerProxy(CryptRepositoryProxy proxy)
            {
                myProxy = proxy;
            }

            public Manifest Manifest
            {
                get { throw new NotImplementedException(); }
            }

            public void PutFile(
                IRepositoryProxy sourceRepository,
                ManifestFileInfo sourceManifestFile)
            {
                throw new NotImplementedException();
            }

            public void RemoveFile(
                ManifestFileInfo removeManifestFile)
            {
                throw new NotImplementedException();
            }

            public void CopyFile(
                ManifestFileInfo fileToBeCopied,
                ManifestFileInfo otherFileWithNewLocation)
            {
                throw new NotImplementedException();
            }

            public void CopyFileInformation(
                ManifestFileInfo fileToBeUpdated,
                ManifestFileInfo otherFileWithNewFileInfo)
            {
                throw new NotImplementedException();
            }

            public void MoveFile(
                ManifestFileInfo fileToBeMoved,
                ManifestFileInfo otherFileWithNewLocation)
            {
                throw new NotImplementedException();
            }

            public void CopyManifestInformation(
                IRepositoryProxy otherRepository)
            {
                throw new NotImplementedException();
            }

            public FileInfo GetFile(
                ManifestFileInfo readFile)
            {
                return myProxy.GetInnerFile(readFile);
            }

            public FileInfo CloneFile(
                ManifestFileInfo copyFile,
                DirectoryInfo copyToDirectory)
            {
                return myProxy.CloneInnerFile(copyFile, copyToDirectory);
            }

            protected CryptRepositoryProxy myProxy;
        }
    }
}
