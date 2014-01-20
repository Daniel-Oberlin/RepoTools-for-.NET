using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Utilities
{
    public class StreamUtilities
    {
        /// <summary>
        /// Copy from one stream to another, may work better than native
        /// stream copying - especially with network streams which have
        /// sometimes gotten stuck.
        /// </summary>
        /// <param name="fromStream">
        /// Source stream
        /// </param>
        /// <param name="toStream">
        /// Destination stream
        /// </param>
        /// <param name="chunkSize">
        /// Size of chunks to copy
        /// </param>
        public static void CopyStream(
            Stream fromStream,
            Stream toStream,
            int chunkSize = 1024)
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
