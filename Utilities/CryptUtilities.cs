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
        static CryptUtilities()
        {
            // Make an initialization vector
            String initString =
                "DoNotChangeThisStringUnderAnyCircumstances";

            byte[] initStringBytes =
                System.Text.Encoding.UTF8.GetBytes(initString);

            // Conveniently, MD5 is 16 bytes which is the same lentgh we need
            // for the AES initialization vector.
            AESInitVector = ComputeHash(initStringBytes, "MD5");
        }

        static public byte[] MakeKeyBytesFromString(
            string keyString,
            byte[] salt)
        {
            var deriveBytes = new Rfc2898DeriveBytes(
                keyString,
                salt,
                10000);

            return deriveBytes.GetBytes(32);
        }

        static public CryptoStream MakeDecryptionReadStreamFrom(
            Stream readEncryptedDataFrom,
            byte[] key)
        {
            var transform = new RijndaelManaged();

            return new CryptoStream(
                readEncryptedDataFrom,
                transform.CreateDecryptor(key, AESInitVector),
                CryptoStreamMode.Read);
        }

        static public CryptoStream MakeEncryptionWriteStreamFrom(
            Stream writeEncryptedDataToStream,
            byte[] key)
        {
            var transform = new RijndaelManaged();

            return new CryptoStream(
                writeEncryptedDataToStream,
                transform.CreateEncryptor(key, AESInitVector),
                CryptoStreamMode.Write);
        }

        static public byte[] ComputeHash(
            FileInfo file,
            string hashType = DefaultHashType)
        {
            Stream fileStream =
                file.Open(FileMode.Open, FileAccess.Read);

            return ComputeHash(fileStream, hashType);
        }

        static public byte[] ComputeHash(
            byte[] data,
            string hashType = DefaultHashType)
        {
            Stream dataStream =
                new MemoryStream(data);

            return ComputeHash(dataStream, hashType);
        }

        static public byte[] ComputeHash(
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

            return hash;
        }

        // I think I chose this because SHA256 was less available on Mono.
        //
        // Efficiency should be a major consideration here, since it drives
        // a large part of the performance.
        public const string DefaultHashType = "MD5";

        protected static byte[] AESInitVector;
    }
}
