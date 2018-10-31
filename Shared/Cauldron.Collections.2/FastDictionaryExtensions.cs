using System;
using System.Collections.Generic;
using System.Linq;

namespace Cauldron.Collections
{
    /// <exclude/>
#if PUBLIC
    public
#else
    internal
#endif
    static class FastDictionaryExtensions
    {
        /// <summary>
        /// Creates a <see cref="FastDictionary{TKey, TValue}"/> from an <see cref="IEnumerable{T}"/> according to specified key selector and element selector functions.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <typeparam name="TKey">The type of the key returned by keySelector.</typeparam>
        /// <typeparam name="TElement">The type of the value returned by elementSelector.</typeparam>
        /// <param name="source">An <see cref="IEnumerable{T}"/> to create a <see cref="FastDictionary{TKey, TValue}"/> from.</param>
        /// <param name="keySelector">A function to extract a key from each element.</param>
        /// <param name="elementSelector">A transform function to produce a result element value from each element.</param>
        /// <returns>
        /// A <see cref="FastDictionary{TKey, TValue}"/> that contains values of type TElement selected from the input sequence.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/> or <paramref name="keySelector"/> or <paramref name="elementSelector"/> is null.-or-paramref name="keySelector"/> produces a key that is null.
        /// </exception>
        public static FastDictionary<TKey, TElement> ToFastDictionary<TSource, TKey, TElement>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector)
            where TKey : class
            where TElement : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (elementSelector == null) throw new ArgumentNullException(nameof(elementSelector));

            // We have to this to avoid resizing of fast dictionary backing array while we are adding items.
            var items = source.ToArray();
            var result = new FastDictionary<TKey, TElement>(items.Length);

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var key = keySelector(item);

                if (key == null)
                    throw new ArgumentNullException(nameof(keySelector), nameof(keySelector) + " returns a null.");

                result.Add(key, elementSelector(item));
            }

            return result;
        }
    }
}