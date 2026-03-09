using Godot;
using Dragon.Utilities.Singletons;

namespace Dragon.Managers
{
    /// <summary> The game world's central manager singleton. </summary>
    public partial class GameManager : SingletonNode<GameManager>
    {
        /// <inheritdoc/>
        public override void _Ready()
        {
            GD.Print("Hello, World.");
        }
    }
}
