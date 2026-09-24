using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Routes contact and dialogue authoring through the persistent prefab workspace.</summary>
    [CustomEditor(typeof(ObjectExtendedInteraction), true)]
    internal sealed class ExtendedInteractionEditor : UnityEditor.Editor
    {
        #region Methods

        #region Inspector

        /// <summary>Identifies the component and opens its exact feature card.</summary>
        public override void OnInspectorGUI()
        {
            // Scene instances expose status while editing remains restricted to their source prefab.
            ObjectExtendedInteraction feature = (ObjectExtendedInteraction)target;
            EditorGUILayout.LabelField(feature.InteractionName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(feature.Kind == ExtendedInteractionKind.Dialogue ? "Multiple Interaction · Dialogue"
                : "Passive Interaction · Modify by contact", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode
                || !ObjectAuthoringSave.TryValidate(feature.gameObject, out _)))
                if (GUILayout.Button(new GUIContent("Open Objects Logic Studio", "Select this exact component in the tool; use Open to enter its prefab workspace.")))
                    ObjectsLogicStudioWindow.Open(feature);
            if (Application.isPlaying)
                EditorGUILayout.LabelField(feature switch
                {
                    ObjectDialogue dialogue => dialogue.IsSpeaking ? "Speaking" : "Waiting",
                    ObjectContactModifier contact => contact.IsModifying ? "Modifying" : "Waiting for contact",
                    _ => string.Empty
                });
        }

        #endregion

        #endregion
    }
}
