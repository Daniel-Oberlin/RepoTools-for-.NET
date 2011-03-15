using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RepositoryManifest;


namespace Utilities
{
    public class MovedFileSet
    {
        public MovedFileSet()
        {
            OldFiles = new List<ManifestFileInfo>();
            NewFiles = new List<ManifestFileInfo>();
        }

        public List<ManifestFileInfo> OldFiles { private set; get; }
        public List<ManifestFileInfo> NewFiles { private set; get; }
    }
}
