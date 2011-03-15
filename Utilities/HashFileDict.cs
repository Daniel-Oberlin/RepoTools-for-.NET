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
            FileHash hash = new FileHash(manFileInfo.Hash);

            if (Dict.ContainsKey(hash) == false)
            {
                Dict.Add(hash, new List<ManifestFileInfo>());
            }

            Dict[hash].Add(manFileInfo);
        }

        public Dictionary<FileHash, List<ManifestFileInfo>> Dict { private set; get; }
    }
}
