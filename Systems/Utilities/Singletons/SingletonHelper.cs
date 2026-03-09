using Godot;

namespace Dragon.Utilities.Singletons
{
    /// <summary> Shared singleton logic to avoid duplication across node types. </summary>
    internal static class SingletonHelper<T> where T : GodotObject
    {
        /// <summary> The singleton instance. </summary>
        private static T? _instance;

        /// <summary> The singleton instance. </summary>
        public static T Instance => _instance!;


        /// <summary> Attempts to register the given object as the singleton instance. </summary>
        /// <param name="self"> The object attempting to register. </param>
        /// <returns> True if registration should be rejected (duplicate instance). </returns>
        public static bool Register(GodotObject self)
        {
            if (Engine.IsEditorHint()) return false;

            if (_instance == null)
            {
                _instance = self as T;
                return false;
            }

            return true; // Duplicate — caller should QueueFree.
        }


        /// <summary> Clears the instance if it matches the given object. </summary>
        /// <param name="self"> The object to compare against. </param>
        public static void ClearIfMatch(GodotObject self)
        {
            if (_instance == (T)self)
            {
                _instance = null;
            }
        }
    }
}
