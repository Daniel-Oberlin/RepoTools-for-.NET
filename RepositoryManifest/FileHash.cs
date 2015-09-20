using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

using Utilities;


namespace RepositoryManifest
{
    [Serializable]
    public class FileHash
    {
        public FileHash(byte[] hash, String hashType)
        {
            HashData = hash;
            HashType = hashType;
        }

        public FileHash()
        {
        }

        public FileHash(String hashString, String hashType)
        {
            int byteLength = hashString.Length / 2;
            HashData = new byte[byteLength];

            for (int nextByte = 0, nextChar = 0;
                nextByte < byteLength;
                nextByte++, nextChar += 2)
            {
                String nextByteString =
                    hashString.Substring(nextChar, 2);

                HashData[nextByte] =
                    Byte.Parse(
                        nextByteString,
                        System.Globalization.NumberStyles.HexNumber);
            }

            HashType = hashType;
        }

        public FileHash(FileHash orig)
        {
            HashData = orig.HashData;
            HashType = orig.HashType;

            myObjectHashIsSet = orig.myObjectHashIsSet;
            myObjectHash = orig.myObjectHash;
        }

        public override int GetHashCode()
        {
            // Not serialized so we regenerate on the fly
            if (myObjectHashIsSet == false)
            {
                myObjectHash = 0;
                for (int i = 0; i < 4; i++)
                {
                    myObjectHash <<= 8;
                    myObjectHash |= HashData[i];
                }
                myObjectHashIsSet = true;
            }

            return myObjectHash;
        } 

        public override bool Equals(object obj)
        {
            if (obj is FileHash)
            {
                FileHash other = (FileHash)obj;

                if (other.HashType != HashType ||
                    other.HashData.Length != HashData.Length)
                {
                    return false;
                }

                for (int i = 0; i < HashData.Length; i++)
                {
                    if (other.HashData[i] != HashData[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// The name of the hash algorithm used
        /// </summary>
        [DataMember]
        public String HashType { set; get; }

        /// <summary>
        /// Represent as a hex string
        /// </summary>
        /// <returns>
        /// The hex string
        /// </returns>
        public override string ToString()
        {
            string stringRep = "";
            foreach (byte nextByte in HashData)
            {
                stringRep += String.Format("{0,2:X2}", nextByte);
            }

            return stringRep;
        }

        /// <summary>
        /// The data of the file hash
        /// </summary>
        [DataMember]
        public byte[] HashData
        {
            set
            {
                myHashData = value;
            }

            get
            {
                // TODO: Use a readonly wrapper or something like that
                return (byte[]) myHashData.Clone();
            }
        }

        private byte[] myHashData;

        /// <summary>
        /// A hash code to be used by this object - not serialized
        /// </summary>
        [NonSerialized()]
        private int myObjectHash;

        /// <summary>
        /// A flag indicating if we've set myObjectHash
        /// </summary>
        [NonSerialized()]
        private bool myObjectHashIsSet;


        // Static

        static public FileHash ComputeHash(
            FileInfo file,
            string hashType = CryptUtilities.DefaultHashType)
        {
            return new FileHash(
                CryptUtilities.ComputeHash(file, hashType),
                hashType);
        }

        static public FileHash ComputeHash(
            byte[] data,
            string hashType = CryptUtilities.DefaultHashType)
        {
            return new FileHash(
                CryptUtilities.ComputeHash(data, hashType),
                hashType);
        }

        static public FileHash ComputeHash(
            Stream stream,
            string hashType = CryptUtilities.DefaultHashType)
        {
            return new FileHash(
                CryptUtilities.ComputeHash(stream, hashType),
                hashType);
        }
    }
}
