using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits the preparation sequence inside each reusable contact definition.</summary>
    internal static class ContactPreparationControls
    {
        #region Methods
        #region Drawing

        /// <summary>Shows only enabled snap, confirmation and transform animation branches.</summary>
        /// <param name="settings">Serialized preparation payload.</param>
        /// <param name="sections">Window-local foldout state.</param>
        internal static void Draw(SerializedProperty settings, ObjectStudioSections sections)
        {
            if (!sections.Draw("Preparation", "Snap the contact item, optionally confirm, then animate before applying effects.", settings))
                return;
            using EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope();
            HoverControls.Field(settings, "Snap");
            if (settings.FindPropertyRelative("Snap").boolValue)
            {
                HoverControls.Field(settings, "SnapSpace");
                HoverControls.Field(settings, "Position");
                HoverControls.Field(settings, "Rotation");
                HoverControls.Field(settings, "Instant");
                if (!settings.FindPropertyRelative("Instant").boolValue)
                    HoverControls.Field(settings, "Duration");
                HoverControls.Field(settings, "RequireInput");
            }
            HoverControls.Field(settings, "Animate");
            if (!settings.FindPropertyRelative("Animate").boolValue)
                return;
            HoverControls.Field(settings, "AnimateOther");
            HoverControls.Field(settings, "AnimationSpace");
            // Counterpart paths use an explicit sample instead of borrowing the modifying object's hierarchy.
            HierarchyPathMenu.Draw(settings.FindPropertyRelative("Path"),
                HierarchyPathMenu.Source(settings, settings.FindPropertyRelative("AnimateOther").boolValue), false, transforms: true);
            HoverControls.Field(settings, "StateA");
            HoverControls.Field(settings, "StateB");
            HoverControls.Field(settings, "AnimationDuration");
            HoverControls.Field(settings, "ForwardRotation");
            HoverControls.Field(settings, "Return");
            if (!settings.FindPropertyRelative("Return").boolValue)
                return;
            HoverControls.Field(settings, "ReturnDuration");
            HoverControls.Field(settings, "ReturnRotation");
        }

        /// <summary>Checks shared definitions before offering the component's confirmation targeting.</summary>
        /// <param name="settings">Serialized modification list.</param>
        /// <returns>True when at least one assigned definition requires confirmation.</returns>
        internal static bool RequiresInput(SerializedProperty settings)
        {
            SerializedProperty definitions = settings.FindPropertyRelative("Modifications");
            for (int index = 0; index < definitions.arraySize; index++)
                if (definitions.GetArrayElementAtIndex(index).objectReferenceValue is ContactModificationDefinition definition
                    && definition.Settings?.Preparation is { Snap: true, RequireInput: true })
                    return true;
            return false;
        }

        #endregion
        #endregion
    }
}
