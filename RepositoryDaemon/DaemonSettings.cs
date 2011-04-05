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
        }

        public static DaemonSettings ReadDaemonSettings(string daemonSettingsFilePath)
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

        public void WriteDaemonSettings(string daemonSettingsFilePath)
        {
            Exception exception = null;
            FileStream fileStream = null;

            try
            {
                fileStream = new FileStream(
                    daemonSettingsFilePath,
                    FileMode.Create);

                BinaryFormatter formatter =
                    new BinaryFormatter();

                formatter.Serialize(fileStream, this);
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
                        File.Delete(daemonSettingsFilePath);
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

        public Dictionary<Guid, RepositoryInfo> GuidToRepository { private set; get; }
        public Dictionary<String, RepositoryInfo> NameToRepository { private set; get; }
    }
}
