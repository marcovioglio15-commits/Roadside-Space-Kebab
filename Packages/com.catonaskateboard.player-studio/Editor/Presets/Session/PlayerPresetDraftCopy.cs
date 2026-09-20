using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Transfers pending serialized values to a disposable candidate without applying them to their source.</summary>
    internal static class PlayerPresetDraftCopy
    {
        #region Methods

        /// <summary>Copies user fields from an unapplied serialized view to an isolated object of the same type.</summary>
        /// <param name="source">Pending fields; null means no draft replacement is needed.</param>
        /// <param name="destination">Candidate owned by validation or Quick Play.</param>
        internal static void Apply(SerializedObject source, ScriptableObject destination)
        {
            // Iterating top-level fields also transfers nested serialized settings as a whole.
            if (source == null)
                return;
            using SerializedObject copy = new SerializedObject(destination);
            using SerializedProperty property = source.GetIterator();
            if (property.NextVisible(true))
                do
                    if (property.name != "m_Script")
                        copy.CopyFromSerializedProperty(property);
                while (property.NextVisible(false));
            copy.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
