using System;
using System.Collections.Generic;
using Godot;

namespace Dragon.Utilities.Extensions
{
    /// <summary> Helper methods for working with CSVs. </summary>
    public static class CsvExtensions
    {
        /// <summary> Load the given CSV file as a concrete data object. </summary>
        /// <typeparam name="T"> The type of data object to attempt to serialise. </typeparam>
        /// <param name="relativeDirectoryPath"> The Godot-relative file path to load. </param>
        /// <returns> An array of the objects serialised from the loaded CSV. </returns>
        public static T[] LoadData<T>(String relativeDirectoryPath) where T : IParseable<T>
        {
            List<T> result = new List<T>();

            using FileAccess? file = FileAccess.Open(relativeDirectoryPath, FileAccess.ModeFlags.Read);
            if (file != null)
            {
                String[] header = file.GetCsvLine();
                while (!file.EofReached())
                {
                    String[] currentLine = file.GetCsvLine();
                    if (currentLine.Length > 0 && !String.IsNullOrWhiteSpace(currentLine[0]))
                    {
                        result.Add(T.Parse(header, currentLine));
                    }
                }
            }
            else
            {
                GD.PrintErr(FileAccess.GetOpenError());
            }

            return result.ToArray();
        }


        /// <summary> Indicates that the object is parseable from a CSV. </summary>
        /// <typeparam name="T"> The type of the parseable model. </typeparam>
        public interface IParseable<T> where T : IParseable<T>
        {
            /// <summary> Attempt to parse the loaded CSV data into a concrete model. </summary>
            /// <param name="header"> An ordered list of the file's headers. </param>
            /// <param name="data"> The data ordered into the same format at the header. </param>
            /// <returns> A constructed object. </returns>
            public static abstract T Parse(String[] header, String[] data);
        }
    }
}
