using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RepositoryManifest
{
    public class FileHash
    {
        public FileHash(byte[] hash)
        {
            Hash = hash;

            for (int i = 0; i < 4; i++)
            {
                myHash <<= 8;
                myHash |= hash[i];
            }
        }

        public FileHash(FileHash orig)
        {
            Hash = orig.Hash;
            myHash = orig.myHash;
        }

        public override int GetHashCode()
        {
            return myHash;
        }

        public override bool Equals(object obj)
        {
            if (obj is FileHash)
            {
                FileHash other = (FileHash)obj;

                if (other.Hash.Length != Hash.Length)
                {
                    return false;
                }

                for (int i = 0; i < Hash.Length; i++)
                {
                    if (other.Hash[i] != Hash[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public byte[] Hash { private set; get; }

        public override string ToString()
        {
            string stringRep = "";
            foreach (byte nextByte in Hash)
            {
                stringRep += nextByte.ToString("X");
            }

            return stringRep;
        }

        private int myHash;
    }
}
