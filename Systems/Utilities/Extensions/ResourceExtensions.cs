using Godot;
using System;
using System.Collections.Generic;

namespace Dragon.Utilities.Extensions
{
    /// <summary> Helpful extensions for working with resources. </summary>
    public static class ResourceExtensions
    {
        /// <summary> Search the given directory, recursively, and load all the resources of the given type. </summary>
        /// <typeparam name="T"> The type of resource to load. </typeparam>
        /// <param name="directoryPath"> The path of the root directory. </param>
        /// <returns> An array of loaded resources. </returns>
        public static T[] GetResources<T>(String directoryPath) where T : Resource
        {
            String[] resourcePaths = FileExtensions.GetFilepaths(directoryPath, [".tres"]);

            List<T> results = new List<T>();
            foreach (String path in resourcePaths)
            {
                // Check the resource is of the correct type.
                Resource resource = ResourceLoader.Load(path);
                if (resource is T castResource)
                {
                    results.Add(castResource);
                }
            }

            return results.ToArray();
        }
    }
}
