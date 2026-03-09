#nullable disable warnings
using Godot;

namespace Halcyon.Utilities.Singletons
{
    /// <summary> Implementation of a singleton as a Node3D. </summary>
    /// <typeparam name="T"> Type of node3D. </typeparam>
    public partial class SingletonNode3D<T> : Node3D where T : Node3D
    {
        /// <summary> The singleton node3D's instance. </summary>
        private static T? _instance = null;

        /// <summary> The singleton node3D's instance. </summary>
        public static T Instance => _instance;


        /// <summary> Singleton node3D's constructor. </summary>
        protected SingletonNode3D()
        {
            if (!Engine.IsEditorHint())
            {
                if (_instance == null)
                {
                    _instance = this as T;
                }
                else    // There can only be one! (Destroy this one.)
                {
                    QueueFree();
                }
            }
        }


        /// <summary> De-constructor for singleton. Removes reference and allows GC to collect. </summary>
        ~SingletonNode3D()
        {
            if (_instance == this)
            {
                _instance = null;
                QueueFree();
            }
        }


        /// <summary> Make sure to clean up the singleton when a close request is issued. </summary>
        public override void _Notification(int what)
        {
            if (what == NotificationWMCloseRequest)
            {
                if (_instance != null && _instance == this)
                {
                    _instance = null;
                }

                QueueFree();
            }
        }


        /// <summary> Make sure to clean up when the object exits the tree and is de-spawned. </summary>
        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
