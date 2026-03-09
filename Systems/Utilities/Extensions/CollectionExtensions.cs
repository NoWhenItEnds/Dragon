using System;
using System.Collections.Generic;
using System.Linq;

namespace Dragon.Utilities.Extensions
{
    /// <summary> Additional helper methods for working with collections. </summary>
    public static class CollectionExtensions
    {
        /// <summary> A single, reusable instance of Random. </summary>
        private static readonly Random RANDOM = new Random();


        /// <summary> Gets a random element from an IEnumerable collection. </summary>
        /// <typeparam name="T"> The type of the elements in the collection. </typeparam>
        /// <param name="source"> The source collection. </param>
        /// <returns> A random element, or the default value of T (probably null) if the collection is empty. </returns>
        public static T? GetRandomElement<T>(this IEnumerable<T> source)
        {
            // Ensure that collection isn't null. Not that is should be, but better save than sorry.
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // Materialise once so we don't enumerate separately.
            IList<T> list = source as IList<T> ?? source.ToList();
            return list.Count > 0 ? list[RANDOM.Next(list.Count)] : default(T);
        }
    }
}
