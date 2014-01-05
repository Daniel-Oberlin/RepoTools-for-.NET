using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;


namespace Utilities
{
    public class CryptUtilities
    {
        // Crypt Utilities

        static public byte[] MakeKeyBytesFromString(
            string keyString)
        {
            // TODO
            return new byte[0];
        }

        static public Stream MakeCryptoReadStreamFrom(
            Stream inputStream,
            byte[] key)
        {
            // TODO
            return inputStream;
        }

        static public Stream MakeCryptoWriteStreamFrom(
            Stream inputStream,
            byte[] key)
        {
            // TODO
            return inputStream;
        }

        // Hash Utilities

        static public FileHash ComputeHash(
            FileInfo file,
            string hashType = DefaultHashType)
        {
            Stream fileStream =
                file.Open(FileMode.Open, FileAccess.Read);

            return ComputeHash(fileStream, hashType);
        }

        static public FileHash ComputeHash(
            byte[] data,
            string hashType = DefaultHashType)
        {
            Stream dataStream =
                new MemoryStream(data);

            return ComputeHash(dataStream, hashType);
        }

        static public FileHash ComputeHash(
            Stream stream,
            string hashType = DefaultHashType)
        {
            byte[] hash = null;

            switch (hashType)
            {
                case "MD5":
                    hash = new MD5CryptoServiceProvider().ComputeHash(stream);
                    break;

                case "SHA256":
                    hash = new SHA256Managed().ComputeHash(stream);
                    break;

                default:
                    throw new Exception("Unrecognized hash method: " + hashType);
            }

            stream.Close();

            return new FileHash(hash, hashType);
        }

        // I think I chose this because SHA256 was less available on Mono.
        //
        // Efficiency should be a major consideration here, since it drives
        // a large part of the performance.  We do rely on it for security
        // with encrypted repositories, so in that case it may make sense
        // to use something stronger.
        public const string DefaultHashType = "MD5";
    }
}
