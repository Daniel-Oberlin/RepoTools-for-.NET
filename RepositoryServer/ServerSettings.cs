using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System.Text;

namespace RepositoryServer
{
    [Serializable]
    public class ServerSettings
    {
        public ServerSettings()
        {
            Users = new Dictionary<string, User>();
            UserHomePaths = new Dictionary<User, List<string>>();
            HostToUser = new Dictionary<string, User>();

            GuidToRepository = new Dictionary<Guid, RepositoryInfo>();
            NameToRepository = new Dictionary<string, RepositoryInfo>();

            ManifestFlushIntervalSeconds =
                DefaultManifestFlushIntervalSeconds;
        }

        /// <summary>
        /// Finalizer writes settings
        /// </summary>
        ~ServerSettings()
        {
            WriteServerSettings();
        }

        public static ServerSettings ReadServerSettings(
            string serverSettingsFilePath)
        {
            FileStream fileStream =
                new FileStream(serverSettingsFilePath, FileMode.Open);

            BinaryFormatter formatter =
                new BinaryFormatter();

            ServerSettings settings = null;

            Exception exception = null;
            try
            {
                settings = (ServerSettings)
                    formatter.Deserialize(fileStream);

                settings.ServerSettingsFilePath =
                    serverSettingsFilePath;
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
                lock (this)
                {
                    myManifestFlushIntervalSeconds = value;
                }
            }

            get
            {
                lock (this)
                {
                    return myManifestFlushIntervalSeconds;
                }
            }
        }

        public User AddUser(string userName)
        {
            lock (this)
            {
                if (Users.ContainsKey(userName))
                {
                    return null;
                }

                User newUser = new User(userName, false);
                Users.Add(userName, newUser);
                return newUser;
            }
        }

        protected int myManifestFlushIntervalSeconds;

        protected void WriteServerSettings()
        {
            Exception exception = null;
            FileStream fileStream = null;

            try
            {
                fileStream = new FileStream(
                    ServerSettingsFilePath,
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
                        File.Delete(ServerSettingsFilePath);
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

        public Dictionary<string, User> Users { private set; get; }
        public Dictionary<User, List<String>> UserHomePaths { private set; get; }
        public Dictionary<String, User> HostToUser { private set; get; }

        protected Dictionary<Guid, RepositoryInfo> GuidToRepository { private set; get; }
        protected Dictionary<String, RepositoryInfo> NameToRepository { private set; get; }

        internal String ServerSettingsFilePath { set; get; }


        // Static

        public static int DefaultManifestFlushIntervalSeconds
        {
            private set;
            get;
        }

        static ServerSettings()
        {
            DefaultManifestFlushIntervalSeconds = 300;
        }

    }
}
