using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RepositoryManifest
{
    [Serializable]
    public class FileHash
    {
        public FileHash(byte[] hash, String hashType)
        {
            myHashData = hash;
            HashType = hashType;
        }

        public FileHash(FileHash orig)
        {
            myHashData = orig.myHashData;
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
                    myObjectHash |= myHashData[i];
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
                    other.myHashData.Length != myHashData.Length)
                {
                    return false;
                }

                for (int i = 0; i < myHashData.Length; i++)
                {
                    if (other.myHashData[i] != myHashData[i])
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
        public String HashType { private set; get; }

        /// <summary>
        /// Represent as a hex string
        /// </summary>
        /// <returns>
        /// The hex string
        /// </returns>
        public override string ToString()
        {
            string stringRep = "";
            foreach (byte nextByte in myHashData)
            {
                stringRep += String.Format("{0,2:X2}", nextByte);
            }

            return stringRep;
        }

        /// <summary>
        /// The data of the file hash
        /// </summary>
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
    }
}
