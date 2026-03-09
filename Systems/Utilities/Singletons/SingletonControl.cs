using System;
using Godot;

namespace Dragon.Utilities.Singletons
{
    /// <summary> Implementation of a singleton as a Control. </summary>
    /// <typeparam name="T"> Type of control. </typeparam>
    public partial class SingletonControl<T> : Control where T : Control
    {
        /// <summary> The singleton control's instance. </summary>
        public static T Instance => SingletonHelper<T>.Instance;


        /// <summary> Singleton control's constructor. </summary>
        protected SingletonControl()
        {
            if (SingletonHelper<T>.Register(this))
            {
                QueueFree();
            }
        }


        /// <summary> Make sure to clean up the singleton when a close request is issued. </summary>
        public override void _Notification(Int32 what)
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
