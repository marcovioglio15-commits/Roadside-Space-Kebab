using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits interaction snapshots with the same conditional controls used by the object workspace.</summary>
    [CustomEditor(typeof(SingleInteractionPreset), true)]
    internal sealed class SingleInteractionPresetEditor : UnityEditor.Editor
    {
        #region State

        private readonly ObjectStudioSections sections = new ObjectStudioSections();

        #endregion

        #region Methods

        #region Drawing

        /// <summary>Exposes only the settings supported by this dedicated asset type.</summary>
        public override void OnInspectorGUI()
        {
            // Native serialized editing supplies asset Undo; importing remains a separate item operation.
            serializedObject.Update();
            if (target is GrabPreset)
                SingleInteractionControls.DrawGrab(serializedObject.FindProperty("Settings"), sections);
            else
            {
                SingleInteractionControls.DrawRelease(serializedObject.FindProperty("Settings"), sections);
                if (target is ThrowPreset)
                    SingleInteractionControls.DrawTrajectory(serializedObject.FindProperty("Trajectory"), sections);
            }
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
