#nullable disable warnings
using Godot;

namespace Halcyon.Utilities.Singletons
{
    /// <summary> Implementation of a singleton as a Control. </summary>
    /// <typeparam name="T"> Type of control. </typeparam>
    public partial class SingletonControl<T> : Control where T : Control
    {
        /// <summary> The singleton control's instance. </summary>
        private static T? _instance = null;

        /// <summary> The singleton control's instance. </summary>
        public static T Instance => _instance;


        /// <summary> Singleton control's constructor. </summary>
        protected SingletonControl()
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
        ~SingletonControl()
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
