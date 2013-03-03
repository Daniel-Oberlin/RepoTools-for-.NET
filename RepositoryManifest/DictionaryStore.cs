using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RepositoryManifest
{
    /// <summary>
    /// This class is used to assist with serialization of Dictionary objects.
    /// Mono has a bug that causes serialized Dictionary objects to fail
    /// deserialization from .NET:
    /// 
    /// https://bugzilla.xamarin.com/show_bug.cgi?id=3050
    /// 
    /// My solution is to use an object to store the Dictionary as a pair of
    /// Lists and to serialize that instead.
    /// </summary>
    /// <typeparam name="KeyType">The type of the Key of the Dictionary</typeparam>
    /// <typeparam name="ValueType">The type of the Value of the Dictionary</typeparam>
    [Serializable]
    class DictionaryStore<KeyType, ValueType>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dict">The dictionary to store</param>
        public DictionaryStore(Dictionary<KeyType, ValueType> dict)
        {
            keyList = new List<KeyType>();
            valueList = new List<ValueType>();

            foreach (KeyType nextKey in dict.Keys)
            {
                ValueType nextValue = dict[nextKey];

                keyList.Add(nextKey);
                valueList.Add(nextValue);
            }
        }

        /// <summary>
        /// Restore a dictionary object
        /// </summary>
        /// <returns>
        /// The dictionary object
        /// </returns>
        public Dictionary<KeyType, ValueType> getDictionary()
        {
            Dictionary<KeyType, ValueType> dict =
                new Dictionary<KeyType, ValueType>();

            for (int i = 0; i < keyList.Count; i++)
            {
                dict.Add(keyList[i], valueList[i]);
            }

            return dict;
        }

        private List<KeyType> keyList;
        private List<ValueType> valueList;
    }
}
