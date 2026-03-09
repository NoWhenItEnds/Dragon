using Godot;

namespace Dragon.Utilities.Singletons
{
    /// <summary> Implementation of a singleton as a Node2D. </summary>
    /// <typeparam name="T"> Type of node2D. </typeparam>
    public partial class SingletonNode2D<T> : Node2D where T : Node2D
    {
        /// <summary> The singleton node2D's instance. </summary>
        public static T Instance => SingletonHelper<T>.Instance;


        /// <summary> Singleton node2D's constructor. </summary>
        protected SingletonNode2D()
        {
            if (SingletonHelper<T>.Register(this))
            {
                QueueFree();
            }
        }


        /// <summary> Make sure to clean up the singleton when a close request is issued. </summary>
        public override void _Notification(int what)
        {
            if (what == NotificationWMCloseRequest)
            {
                SingletonHelper<T>.ClearIfMatch(this);
                QueueFree();
            }
        }


        /// <summary> Make sure to clean up when the object exits the tree. </summary>
        public override void _ExitTree()
        {
            SingletonHelper<T>.ClearIfMatch(this);
        }
    }
}
