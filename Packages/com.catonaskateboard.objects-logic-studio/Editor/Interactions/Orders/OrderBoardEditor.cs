using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits board slots individually in both the inspector and the scene-board window.</summary>
    [CustomEditor(typeof(OrderBoard))]
    internal sealed class OrderBoardEditor : UnityEditor.Editor
    {
        #region Methods
        #region Drawing

        /// <summary>Uses safe array controls for existing authored text references.</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            // The shared field renderer keeps array resizing behind explicit add/remove commands.
            if (property.NextVisible(true))
                while (property.NextVisible(false))
                    StudioFieldGUI.PropertyField(property, true);
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
        #endregion
    }
}
