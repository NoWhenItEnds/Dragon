using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dragon.Utilities.Extensions
{
    /// <summary> A helper class for working with files and filepaths. </summary>
    public static class FileExtensions
    {
        /// <summary> Search the given directory, recursively, for files. </summary>
        /// <param name="directoryPath"> The Godot filepath to search. </param>
        /// <param name="extensions"> A list of allowed extensions. A null means to accept everything. </param>
        /// <returns> The filepaths of all the found extensions. </returns>
        /// <exception cref="DirectoryNotFoundException"/>
        public static String[] GetFilepaths(String directoryPath, String[]? extensions = null)
        {
            DirAccess dataDirectory = DirAccess.Open(directoryPath);
            if (dataDirectory == null)
            {
                throw new DirectoryNotFoundException($"The '{directoryPath}' directory does not exist!");
            }

            List<String> resources = new List<String>();
            dataDirectory.ListDirBegin();
            String current = dataDirectory.GetNext();
            while (!String.IsNullOrEmpty(current))
            {
                String currentPath = directoryPath + '/' + current;
                if (dataDirectory.CurrentIsDir())
                {
                    resources.AddRange(GetFilepaths(currentPath, extensions));
                }
                else
                {
                    // Only add the file if it's part of the acceptable extensions.
                    if (extensions == null || extensions.Contains(Path.GetExtension(currentPath)))
                    {
                        resources.Add(currentPath);
                    }
                }
                current = dataDirectory.GetNext();
            }
            dataDirectory.ListDirEnd();

            return resources.ToArray();
        }
    }
}
