using Godot;

namespace Dragon.Utilities.Singletons
{
    /// <summary> Implementation of a singleton as a Node. </summary>
    /// <typeparam name="T"> Type of node. </typeparam>
    public partial class SingletonNode<T> : Node where T : Node
    {
        /// <summary> The singleton node's instance. </summary>
        public static T Instance => SingletonHelper<T>.Instance;


        /// <summary> Singleton node's constructor. </summary>
        protected SingletonNode()
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
