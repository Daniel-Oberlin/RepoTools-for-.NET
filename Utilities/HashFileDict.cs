using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RepositoryManifest;


namespace Utilities
{
    public class HashFileDict
    {
        public HashFileDict()
        {
            Dict = new Dictionary<FileHash, List<ManifestFileInfo>>();
        }

        public void Add(ManifestFileInfo manFileInfo)
        {
            if (Dict.ContainsKey(manFileInfo.FileHash) == false)
            {
                Dict.Add(manFileInfo.FileHash, new List<ManifestFileInfo>());
            }

            Dict[manFileInfo.FileHash].Add(manFileInfo);
        }

        public Dictionary<FileHash, List<ManifestFileInfo>> Dict { private set; get; }
    }
}
