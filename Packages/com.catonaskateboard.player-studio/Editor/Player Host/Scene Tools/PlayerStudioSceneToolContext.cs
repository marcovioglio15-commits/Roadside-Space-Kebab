using UnityEditor;
using UnityEditor.EditorTools;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Routes the workspace's scene tool callbacks through public Editor APIs.</summary>
    [EditorToolContext("Player Transform", targetToolOwner = typeof(PlayerStudioWindow))]
    internal sealed class PlayerStudioSceneToolContext : EditorToolContext
    {
        #region Methods

        #region Scene Tools

        /// <summary>Leaves manipulation to the workspace handle instead of creating a second transform tool.</summary>
        /// <param name="tool">Native tool requested for this window's separate tool context.</param>
        /// <returns>Null because this context draws the player handle directly.</returns>
        protected override System.Type GetEditorToolType(Tool tool)
        {
            // The standard Scene window keeps its own tools and context.
            return null;
        }

        /// <summary>Draws the active draft handle only in the owning Player Studio scene view.</summary>
        /// <param name="window">Editor window currently drawing its scene tools.</param>
        public override void OnToolGUI(EditorWindow window)
        {
            // The context receives native scene events, including picking and dragging.
            if (window is PlayerStudioWindow studio)
                studio.DrawTransformHandles();
        }

        #endregion

        #endregion
    }
}
