using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Utilities
{
    public class StreamUtilities
    {
        public static void CopyStream(Stream fromStream, Stream toStream, int chunkSize = 8192)
        {
            byte[] buffer = new byte[chunkSize];
            int bytesRead = fromStream.Read(buffer, 0, chunkSize);
			while (bytesRead > 0)
			{
                toStream.Write(buffer, 0, bytesRead);
                bytesRead = fromStream.Read(buffer, 0, chunkSize);
			}
        }
    }
}
