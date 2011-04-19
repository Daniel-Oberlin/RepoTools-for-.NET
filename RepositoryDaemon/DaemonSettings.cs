using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System.Text;

namespace RepositoryDaemon
{
    [Serializable]
    public class DaemonSettings
    {
        public DaemonSettings()
        {
            GuidToRepository = new Dictionary<Guid, RepositoryInfo>();
            NameToRepository = new Dictionary<string, RepositoryInfo>();

            FieldLockObject = new object();

            ManifestFlushIntervalSeconds =
                DefaultManifestFlushIntervalSeconds;
        }

        /// <summary>
        /// Finalizer writes settings
        /// </summary>
        ~DaemonSettings()
        {
            WriteDaemonSettings();
        }

        public static DaemonSettings ReadDaemonSettings(
            string daemonSettingsFilePath)
        {
            FileStream fileStream =
                new FileStream(daemonSettingsFilePath, FileMode.Open);

            BinaryFormatter formatter =
                new BinaryFormatter();

            DaemonSettings settings = null;

            Exception exception = null;
            try
            {
                settings = (DaemonSettings)
                    formatter.Deserialize(fileStream);

                settings.DaemonSettingsFilePath =
                    daemonSettingsFilePath;
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                fileStream.Close();
            }

            if (exception != null)
            {
                throw exception;
            }

            return settings;
        }

        public void AddRepository(RepositoryInfo repoInfo)
        {
            lock (this)
            {
                if (GetRepositoryFromGuid(repoInfo.Guid) != null)
                {
                    throw new Exception("Repository GUID is already registered.");
                }

                if (repoInfo.Name != null &&
                    GetRepositoryFromName(repoInfo.Name) != null)
                {
                    throw new Exception("Repository name is already registered.");
                }

                GuidToRepository.Add(repoInfo.Guid, repoInfo);
                if (repoInfo.Name != null)
                {
                    NameToRepository[repoInfo.Name] = repoInfo;
                }
            }
        }

        public void RemoveRepository(Guid guid)
        {
            lock (this)
            {
                if (GuidToRepository.ContainsKey(guid) == false)
                {
                    throw new Exception("Repository GUID is not registered.");
                }

                RepositoryInfo repoInfo = GuidToRepository[guid];
                GuidToRepository.Remove(guid);

                if (repoInfo.Name != null)
                {
                    NameToRepository.Remove(repoInfo.Name);
                }
            }
        }

        public RepositoryInfo GetRepositoryFromGuid(Guid guid)
        {
            lock (this)
            {
                if (GuidToRepository.ContainsKey(guid))
                {
                    return GuidToRepository[guid];
                }
            }

            return null;
        }

        public RepositoryInfo GetRepositoryFromName(String name)
        {
            lock (this)
            {
                if (NameToRepository.ContainsKey(name))
                {
                    return NameToRepository[name];
                }
            }

            return null;
        }

        public List<RepositoryInfo> GetRepositories()
        {
            lock (this)
            {
                return new List<RepositoryInfo>(GuidToRepository.Values);
            }
        }

        public int ManifestFlushIntervalSeconds
        {
            set
            {
                lock (FieldLockObject)
                {
                    myManifestFlushIntervalSeconds = value;
                }
            }

            get
            {
                lock (FieldLockObject)
                {
                    return myManifestFlushIntervalSeconds;
                }
            }
        }

        protected int myManifestFlushIntervalSeconds;

        protected void WriteDaemonSettings()
        {
            Exception exception = null;
            FileStream fileStream = null;

            try
            {
                fileStream = new FileStream(
                    DaemonSettingsFilePath,
                    FileMode.Create);

                BinaryFormatter formatter =
                    new BinaryFormatter();

                lock (this)
                {
                    formatter.Serialize(fileStream, this);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Close();
                }

                if (exception != null)
                {
                    try
                    {
                        File.Delete(DaemonSettingsFilePath);
                    }
                    catch (Exception)
                    {
                        // Ignore - the file may not exist, and anyways
                        // the previous exception is more informative.
                    }

                    throw exception;
                }
            }
        }

        protected Dictionary<Guid, RepositoryInfo> GuidToRepository { private set; get; }
        protected Dictionary<String, RepositoryInfo> NameToRepository { private set; get; }

        internal String DaemonSettingsFilePath { set; get; }

        private object FieldLockObject { set; get; }


        // Static

        public static int DefaultManifestFlushIntervalSeconds
        {
            private set;
            get;
        }

        static DaemonSettings()
        {
            DefaultManifestFlushIntervalSeconds = 20;
        }

    }
}
