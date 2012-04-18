using System;
using System.Collections.Generic;

namespace NAct.Utils
{
    /// <summary>
    /// A basic in-memory implementation of an actor dictionary, using a System.Collections.Generic.Dictionary.
    /// </summary>
    public class DictionaryActor<K, V> : IDictionaryActor<K, V>
    {
        private readonly Dictionary<K, V> m_Dictionary = new Dictionary<K, V>();
        
        public void Clear()
        {
            m_Dictionary.Clear();
        }

        public void Count(Action<int> callback)
        {
            callback(m_Dictionary.Count);
        }

        public void ContainsKey(K key, Action<bool> callback)
        {
            callback(m_Dictionary.ContainsKey(key));
        }

        public void Add(K key, V value)
        {
            m_Dictionary.Add(key, value);
        }

        public void Remove(K key)
        {
            m_Dictionary.Remove(key);
        }

        public void TryGetValue(K key, Action<bool, V> callback)
        {
            V value;
            bool wasThere = m_Dictionary.TryGetValue(key, out value);
            callback(wasThere, value);
        }

        public void GetValue(K key, Action<V> callback)
        {
            callback(m_Dictionary[key]);
        }

        public V this[K key]
        {
            set { m_Dictionary[key] = value; }
        }

        public void Keys(Action<IEnumerable<K>> callback)
        {
            // We have to copy these before passing to another actor to maintain thread-safety
            callback(new List<K>(m_Dictionary.Keys));
        }

        public void Values(Action<IEnumerable<V>> callback)
        {
            // We have to copy these before passing to another actor to maintain thread-safety
            callback(new List<V>(m_Dictionary.Values));
        }

        public void Entries(Action<IEnumerable<KeyValuePair<K, V>>> callback)
        {
            // We have to copy these before passing to another actor to maintain thread-safety
            callback(new List<KeyValuePair<K, V>>(m_Dictionary));
        }

        public void Atomically(Func<IDictionary<K, V>, bool> transaction)
        {
            transaction(m_Dictionary);
        }
    }
}
