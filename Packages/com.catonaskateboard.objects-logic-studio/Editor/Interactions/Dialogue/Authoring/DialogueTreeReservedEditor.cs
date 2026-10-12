#if !OBJECTS_LOGIC_STUDIO_DIALOGUE_GRAPH
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps unfinished graph assets intact without exposing their authoring controls.</summary>
    [CustomEditor(typeof(DialogueTree))]
    internal sealed class DialogueTreeReservedEditor : UnityEditor.Editor
    {
        #region Methods
        #region Drawing
        /// <summary>Shows retained graph assets as unavailable while entry-list authoring remains active.</summary>
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Dialogue tree editing is not available in this release.", EditorStyles.wordWrappedLabel);
        }
        #endregion
        #endregion
    }
}
#endif
