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
    /// Implementation of an encrypted repository.  The "inner" repository is
    /// just a normal repository that holds encrypted data which is managed
    /// by this - the outer repository.  The outer repository represents the
    /// unencrypted repository data to the program and wraps the inner
    /// repository.  The outer repository manifest is written as an encrypted
    /// file in the inner repository as well.  Filenames are replaced with the
    /// hash information in the inner repository.
    /// </summary>
    class CryptRepositoryProxy : IRepositoryProxy
    {
        /// <summary>
        /// CryptRepositoryProxy constructor
        /// </summary>
        /// <param name="innerProxy">
        /// An existing repository to use as the inner repository.
        /// </param>
        /// <param name="outerKey">
        /// A string which will be used as the encryption key.
        /// </param>
        /// <param name="readOnly">
        /// Specify if this repository will be used in read-only mode.
        /// </param>
        public CryptRepositoryProxy(
            IRepositoryProxy innerProxy,
            String outerKey,
            bool readOnly) :
            this(innerProxy, null, outerKey, readOnly)
        {
        }

        /// <summary>
        /// CryptRepositoryProxy constructor
        /// </summary>
        /// <param name="innerProxy">
        /// An existing repository to use as the inner repository.
        /// </param>
        /// <param name="outerManifest">
        /// Provide an outer manifest - used during seed.  Null otherwise.
        /// </param>
        /// <param name="outerKey">
        /// A string which will be used as the encryption key.
        /// </param>
        /// <param name="readOnly">
        /// Specify if this repository will be used in read-only mode.
        /// </param>
        protected CryptRepositoryProxy(
            IRepositoryProxy innerProxy,
            Manifest outerManifest,
            String outerKeyString,
            bool readOnly)
        {
            InnerProxy = innerProxy;
            OuterKeyString = outerKeyString;

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
        /// Get rid of temp directory and cleanup orphaned files.
        /// </summary>
        public void CleanupBeforeExit()
        {
            if (myReadOnly == false &&
                TempDirectory != null)
            {
                SaveOuterManifest();

                // Removed orphaned data files
                ResolveInnerOuter();
                foreach (ManifestFileInfo nextFile in OrphanedInnerFiles)
                {
                    InnerProxy.RemoveFile(nextFile);
                }

                TempDirectory.Delete(true);
                TempDirectory = null;

                InnerProxy.CleanupBeforeExit();
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
            // Name the inner file with the hash of the hash.  We protect
            // the hash in this way because it is used as the salt to
            // encrypt the data in the file, and it might provide some
            // benefit to a cryptographic attack.
            FileHash hashedHash = FileHash.ComputeHash(
                sourceManifestFile.FileHash.HashData);

            String hashedHashString = hashedHash.ToString();

            // Only add the file data if we don't have it already.
            if (myHashedStringMap.ContainsKey(hashedHashString) == false)
            {
                FileInfo sourceFileInfo =
                    sourceRepository.GetFile(sourceManifestFile);

                byte[] keyData = CryptUtilities.MakeKeyBytesFromString(
                    OuterKeyString,
                    sourceManifestFile.FileHash.HashData);

                // Use the inner proxy temp directory because that is likely
                // the ultimate destination of the file and we don't want to
                // copy the data if we can avoid it.  This is a minor break in
                // encapsulation but has a significant impact on performance.
                String destFilePath =
                    Path.Combine(
                        InnerProxy.TempDirectory.FullName,
                        DefaultEncryptedTempFileName);

                Stream sourceFileStream = sourceFileInfo.OpenRead();

                byte[] cryptHash = WriteCryptFileAndHash(
                    sourceFileStream,
                    keyData,
                    destFilePath);

                FileInfo cryptFileInfo =
                    new FileInfo(destFilePath);

                // Make a dummy parent manifest directory to give to the inner
                // proxy.  This is actually rooted in the inner manifest, but
                // that is ok - although it is kind of a hack.  The fact is
                // that we don't maintain an actual manifest to mirror the
                // inner manifest - and we know that the implementation of
                // PutFile won't be affected by doing this.
                ManifestDirectoryInfo parentDirectory =
                    MakeInnerParentDirectory(
                        hashedHashString,
                        InnerProxy.Manifest.RootDirectory);

                ManifestFileInfo destManifestFile =
                    new ManifestFileInfo(
                        hashedHashString,
                        parentDirectory);

                destManifestFile.RegisteredUtc =
                    DateTime.UtcNow;

                destManifestFile.LastModifiedUtc =
                    cryptFileInfo.LastWriteTimeUtc;

                destManifestFile.FileLength =
                    cryptFileInfo.Length;

                destManifestFile.FileHash =
                    new FileHash(cryptHash, CryptUtilities.DefaultHashType);

                InnerProxy.PutFile(ProxyToInner, destManifestFile);

                myHashedStringMap.Add(hashedHashString, destManifestFile);

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

            byte[] keyData = CryptUtilities.MakeKeyBytesFromString(
                OuterKeyString,
                copyFile.FileHash.HashData);

            String destFilePath =
                Path.Combine(
                    copyToDirectory.FullName,
                    DefaultDecryptedTempFileName);

            ReadCryptFile(
                innerFileInfo,
                keyData,
                destFilePath);

            FileInfo fileInfo =
                new FileInfo(destFilePath);

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
            // that the encrypted inner file has already been created.  So
            // all that remains is to return the file so that it can be moved
            // to its final destination.  Since the file was generated in the
            // temp directory of the inner repository, this should be very
            // efficient.
            String filePath =
                Path.Combine(
                    InnerProxy.TempDirectory.FullName,
                    DefaultEncryptedTempFileName);

            return new FileInfo(filePath);
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

            byte[] outerKeyBytes = CryptUtilities.MakeKeyBytesFromString(
                OuterKeyString,
                InnerProxy.Manifest.Guid.ToByteArray());

            Stream outerManifestCryptoStream =
                CryptUtilities.MakeDecryptionReadStreamFrom(
                    outerManifestFileStream,
                    outerKeyBytes);

            OuterManifest =
                Manifest.ReadManifestStream(outerManifestCryptoStream);

            outerManifestCryptoStream.Close();
        }

        protected void SaveOuterManifest()
        {
            // Serialize the manifest to memory
            MemoryStream serializedManifestStream =
                new MemoryStream();

            OuterManifest.WriteManifestStream(serializedManifestStream);
            serializedManifestStream.Position = 0;

            String tempFilePath =
                Path.Combine(
                    InnerProxy.TempDirectory.FullName,
                    DefaultEncryptedTempFileName);

            // We use the inner GUID as salt for the outer manifest, so update
            // it each time we write the outer manifest.  The inner GUID is
            // really useless anyways.
            InnerProxy.Manifest.ChangeGUID();

            byte[] outerKeyData = CryptUtilities.MakeKeyBytesFromString(
                OuterKeyString,
                InnerProxy.Manifest.Guid.ToByteArray());

            byte[] cryptHash = WriteCryptFileAndHash(
                serializedManifestStream,
                outerKeyData,
                tempFilePath);

            // The new ManifestFileInfo is actually rooted in the inner
            // Manifest object, but that is ok - although it is kind of a
            // hack.  The fact is that we don't maintain an actual Manifest
            // object to mirror the inner manifest - and we know that the
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
                new FileHash(cryptHash, CryptUtilities.DefaultHashType);

            InnerProxy.PutFile(ProxyToInner, destManifestFile);
        }

        // Generate map of outer hashes to inner files, find unresolved
        // outer files, and find orphaned inner files.
        protected void ResolveInnerOuter()
        {
            myHashedStringMap =
                new Dictionary<string, ManifestFileInfo>();

            BuildHashedStringMap(InnerProxy.Manifest.RootDirectory);

            myHashToInnerFileMap =
                new Dictionary<FileHash, ManifestFileInfo>();

            UnresolvedOuterFiles =
                new List<ManifestFileInfo>();

            BuildHashToInnerFileMap(OuterManifest.RootDirectory);

            OrphanedInnerFiles =
                new List<ManifestFileInfo>();

            BuildInnerFilesOrphanedList();
        }

        protected void BuildHashedStringMap(
            ManifestDirectoryInfo dir)
        {
            foreach (String nextFileName
                in dir.Files.Keys)
            {
                if (nextFileName != DefaultOuterManifestFileName)
                {
                    myHashedStringMap.Add(
                        nextFileName,
                        dir.Files[nextFileName]);
                }
            }

            foreach (ManifestDirectoryInfo nextDir in
                dir.Subdirectories.Values)
            {
                BuildHashedStringMap(nextDir);
            }
        }

        protected void BuildHashToInnerFileMap(
            ManifestDirectoryInfo dir)
        {
            foreach (ManifestFileInfo nextFile in dir.Files.Values)
            {
                FileHash fileHash = nextFile.FileHash;
                if (myHashToInnerFileMap.ContainsKey(fileHash))
                {
                    continue;
                }

                // Using FileHash class for convenience
                FileHash hashedHash = FileHash.ComputeHash(
                    fileHash.HashData);

                String hashedHashString = hashedHash.ToString();

                if (myHashedStringMap.Keys.Contains(
                    hashedHashString))
                {
                    myHashToInnerFileMap[fileHash] =
                        myHashedStringMap[hashedHashString];
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
                    nextDir);
            }
        }

        protected void BuildInnerFilesOrphanedList()
        {
            HashSet<ManifestFileInfo> resolvedInnerFiles =
                new HashSet<ManifestFileInfo>();

            foreach (ManifestFileInfo nextFile in
                myHashToInnerFileMap.Values)
            {
                resolvedInnerFiles.Add(nextFile);
            }

            foreach (ManifestFileInfo nextFile in
                myHashedStringMap.Values)
            {
                if (resolvedInnerFiles.Contains(nextFile) == false)
                {
                    OrphanedInnerFiles.Add(nextFile);
                }
            }
        }

        protected ManifestDirectoryInfo MakeInnerParentDirectory(
            String hashedHashString,
            ManifestDirectoryInfo dir)
        {
            if (dir.Files.Count < 256)
            {
                return dir;
            }

            int nextDirLength = dir.Name.Length;

            // Root dir is named ".", but pretend it is ""
            if (nextDirLength == 1)
            {
                nextDirLength = 0;
            }

            // Increase specificity of next subdirectory name by 2 letters:
            // For example a497 -> a4973e
            nextDirLength += 2;

            String nextDirName =
                hashedHashString.Substring(0, nextDirLength);

            if (dir.Subdirectories.ContainsKey(nextDirName))
            {
                return MakeInnerParentDirectory(
                    hashedHashString,
                    dir.Subdirectories[nextDirName]);
            }

            return new ManifestDirectoryInfo(nextDirName, dir);
        }

        protected void ReadCryptFile(
            FileInfo sourceFileInfo,
            byte[] keyData,
            String destFilePath)
        {
            Stream sourceFileStream =
                sourceFileInfo.OpenRead();

            Stream cryptoStream =
                CryptUtilities.MakeDecryptionReadStreamFrom(
                    sourceFileStream,
                    keyData);

            FileStream destFileStream =
                File.OpenWrite(destFilePath);

            StreamUtilities.CopyStream(
                cryptoStream, destFileStream);

            destFileStream.Close();
        }

        protected byte[] WriteCryptFileAndHash(
            Stream sourceFileStream,
            byte[] keyData,
            String destFilePath,
            String hashType = CryptUtilities.DefaultHashType)
        {
            FileStream destFileStream = null;
            System.Security.Cryptography.CryptoStream cryptoStream = null;

            try
            {
                // Set up the dest file streams
                destFileStream =
                    File.OpenWrite(destFilePath);

                // Set up a temporary stream to hold encrypted data chunks so
                // that we can send the encrypted data to the file and to the
                // hash algorithm.
                MemoryStream encryptedMemoryStream =
                    new MemoryStream();

                // Set up a CryptoStream and attach it to the temporary stream
                cryptoStream =
                    CryptUtilities.MakeEncryptionWriteStreamFrom(
                        encryptedMemoryStream,
                        keyData);

                // Set up the hash algorithm
                System.Security.Cryptography.HashAlgorithm hashAlgorithm =
                    CryptUtilities.GetHashAlgorithm(hashType);

                // Buffers and chunks
                int chunkSize = 1024;
                byte[] fileReadBuffer = new byte[chunkSize];
                byte[] encryptedBuffer = null;

                // Read the first chunk to set up the loop
                int bytesRead = sourceFileStream.Read(
                    fileReadBuffer,
                    0,
                    chunkSize);

                // Read until the end
                while (bytesRead > 0)
                {
                    // Encrypt to the MemoryStream 
                    cryptoStream.Write(fileReadBuffer, 0, bytesRead);
                    encryptedBuffer = encryptedMemoryStream.GetBuffer();

                    // Write encrypted data to file
                    destFileStream.Write(
                        encryptedBuffer,
                        0,
                        (int)encryptedMemoryStream.Length);

                    // Hash encrypted data
                    hashAlgorithm.TransformBlock(
                        encryptedBuffer,
                        0,
                        (int)encryptedMemoryStream.Length,
                        encryptedBuffer,
                        0);

                    // Read next chunk
                    bytesRead = sourceFileStream.Read(
                        fileReadBuffer,
                        0,
                        chunkSize);

                    // Reset position so we don't use a lot of memory
                    encryptedMemoryStream.SetLength(0);
                }

                // Need to do this - I think it writes special padding
                // information to the end of the data.
                cryptoStream.FlushFinalBlock();
                encryptedBuffer = encryptedMemoryStream.GetBuffer();

                // Write final data to file
                destFileStream.Write(
                    encryptedBuffer,
                    0,
                    (int)encryptedMemoryStream.Length);

                // Hash final data
                hashAlgorithm.TransformFinalBlock(
                    encryptedBuffer,
                    0,
                    (int)encryptedMemoryStream.Length);

                sourceFileStream.Close();
                cryptoStream.Close();
                destFileStream.Close();

                return hashAlgorithm.Hash;
            }
            catch (Exception e)
            {
                sourceFileStream.Close();

                if (cryptoStream != null)
                {
                    cryptoStream.Close();
                }

                if (destFileStream != null)
                {
                    destFileStream.Close();
                    File.Delete(destFilePath);
                }

                throw e;
            }
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
        protected Dictionary<String, ManifestFileInfo> myHashedStringMap;

        protected String OuterKeyString { set; get; }
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
            String outerKeyString,
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
                    outerKeyString,
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

            cryptProxy.CleanupBeforeExit();
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

            public void CleanupBeforeExit()
            {
                throw new NotImplementedException();
            }

            public DirectoryInfo TempDirectory
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            protected CryptRepositoryProxy myProxy;
        }
    }
}
