using System.Collections;
using System.Runtime.InteropServices;

namespace ImageMounter.IO
{

    /// <summary>
    /// An extension to Dictionary(Of TKey, TValue) that returns a
    /// default item for non-existing keys
    /// </summary>
    [ComVisible(false)]
    public abstract class NullSafeDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> m_Dictionary;

        /// <summary>Gets a value that is returned as item for non-existing
        ///     keys in dictionary</summary>
        protected abstract TValue GetDefaultValue(TKey Key);

        public object SyncRoot
        {
            get
            {
                return (ICollection)m_Dictionary.SyncRoot;
            }
        }

        /// <summary>
        ///     Creates a new NullSafeDictionary object
        ///     </summary>
        public NullSafeDictionary()
        {
            m_Dictionary = new Dictionary<TKey, TValue>();
        }

        /// <summary>
        ///     Creates a new NullSafeDictionary object
        ///     </summary>
        public NullSafeDictionary(IEqualityComparer<TKey> Comparer)
        {
            m_Dictionary = new Dictionary<TKey, TValue>(Comparer);
        }

        /// <summary>
        ///     Gets or sets the item for a key in dictionary. If no item exists for key, the default
        ///     value for this SafeDictionary is returned
        ///     </summary>
        ///     <param name="key"></param>
        public TValue this[TKey key]
        {
            get
            {
                lock (SyncRoot)
                {
                    if (m_Dictionary.TryGetValue(key, out Item))
                        return Item;
                    else
                        return GetDefaultValue(key);
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    if (m_Dictionary.ContainsKey(key))
                        m_Dictionary[key] = Value;
                    else
                        m_Dictionary.Add(key, Value);
                }
            }
        }

        private void ICollection_Add(KeyValuePair<TKey, TValue> item)
        {
            (ICollection<KeyValuePair<TKey, TValue>>)m_Dictionary.Add(item);
        }

        public void Clear()
        {
            m_Dictionary.Clear();
        }

        /* TODO ERROR: Skipped WarningDirectiveTrivia */
        private bool ICollection_Contains(KeyValuePair<TKey, TValue> item)
        {
            return (ICollection<KeyValuePair<TKey, TValue>>)m_Dictionary.Contains(item);
        }

        private void ICollection_CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            (ICollection<KeyValuePair<TKey, TValue>>)m_Dictionary.CopyTo(array, arrayIndex);
        }
        /* TODO ERROR: Skipped WarningDirectiveTrivia */
        public int Count
        {
            get
            {
                return m_Dictionary.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        private bool ICollection_Remove(KeyValuePair<TKey, TValue> item)
        {
            return (ICollection<KeyValuePair<TKey, TValue>>)m_Dictionary.Remove(item);
        }

        public void Add(TKey key, TValue value)
        {
            m_Dictionary.Add(key, value);
        }

        public bool ContainsKey(TKey key)
        {
            return m_Dictionary.ContainsKey(key);
        }

        public ICollection<TKey> Keys
        {
            get
            {
                return m_Dictionary.Keys;
            }
        }

        public bool Remove(TKey key)
        {
            return m_Dictionary.Remove(key);
        }

        public bool TryGetValue(TKey key, ref TValue value)
        {
            return m_Dictionary.TryGetValue(key, out value);
        }

        public ICollection<TValue> Values
        {
            get
            {
                return m_Dictionary.Values;
            }
        }

        private IEnumerator<KeyValuePair<TKey, TValue>> ICollection_GetEnumerator()
        {
            return (ICollection<KeyValuePair<TKey, TValue>>)m_Dictionary.GetEnumerator();
        }

        public IEnumerator GetEnumerator()
        {
            return m_Dictionary.GetEnumerator();
        }
    }

    public class NullSafeStringDictionary : NullSafeDictionary<string, string>
    {
        public NullSafeStringDictionary() : base()
        {
        }

        public NullSafeStringDictionary(IEqualityComparer<string> Comparer) : base(Comparer)
        {
        }

        protected override string GetDefaultValue(string Key)
        {
            return string.Empty;
        }
    }
}
