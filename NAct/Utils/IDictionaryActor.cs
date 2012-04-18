using System;
using System.Collections.Generic;

namespace NAct.Utils
{
    /// <summary>
    /// A dictionary, which is also an actor. This is as similar as possible to System.Collections.Generic.IDictionary, 
    /// but I've removed some boring methods and of course all the reader methods have become asynchronous.
    /// </summary>
    public interface IDictionaryActor<K, V> : IActor
    {
        void Clear();

        void Count(Action<int> callback);

        void ContainsKey(K key, Action<bool> callback);

        void Add(K key, V value);

        void Remove(K key);

        void TryGetValue(K key, Action<bool, V> callback);

        void GetValue(K key, Action<V> callback);

        V this[K key] { set; }

        void Keys(Action<IEnumerable<K>> callback);

        void Values(Action<IEnumerable<V>> callback);

        void Entries(Action<IEnumerable<KeyValuePair<K, V>>> callback);

        /// <summary>
        /// Allows multiple actions on the dictionary to be performed all at once - a transaction.
        /// 
        /// The code you provide here must not access your state (an exception will be thrown if it does).
        /// The code will run in the actor of the dictionary.
        /// </summary>
        /// <param name="transaction">
        /// An operation on a non-actor dictionary. The return value doesn't matter, it's just there to
        /// make sure you don't access your state.
        /// </param>
        void Atomically(Func<IDictionary<K, V>, bool> transaction);
    }
}
