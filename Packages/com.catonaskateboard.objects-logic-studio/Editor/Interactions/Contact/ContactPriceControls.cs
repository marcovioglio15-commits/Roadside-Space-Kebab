using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows collection dependencies only when a consuming rule can use them.</summary>
    internal static class ContactPriceControls
    {
        #region Methods

        #region Drawing

        /// <summary>Offers portable same-object completion choices with independent array foldouts.</summary>
        /// <param name="settings">Detached collection settings shared by presets and object drafts.</param>
        internal static void Draw(SerializedProperty settings)
        {
            HoverControls.Field(settings, "CollectPrice");
            if (!settings.FindPropertyRelative("CollectPrice").boolValue)
                return;
            HoverControls.Field(settings, "Sound", "Collection Sound");
            HoverControls.Field(settings, "DeferDisplay");
            if (!settings.FindPropertyRelative("DeferDisplay").boolValue)
                return;
            HoverControls.Field(settings, "OnDespawn", "Pending At Despawn");
            SerializedProperty sources = settings.FindPropertyRelative("CompletionSources");
            if (sources.arraySize > 1)
                HoverControls.Field(settings, "CompletionMode");
            // Reuse the local descriptor menu so presets never retain prefab-stage component references.
            for (int index = 0; index < sources.arraySize; index++)
            {
                if (StudioArrayGUI.Header(sources, index, new GUIContent("Wait For " + (index + 1), sources.tooltip)))
                    break;
                if (sources.GetArrayElementAtIndex(index).isExpanded)
                    using (new StudioIndentScope())
                        LocalInteractionMenu.Draw(sources.GetArrayElementAtIndex(index), true);
            }
            StudioArrayGUI.Add(sources, "Add Completion", () => new LocalInteractionReference());
        }

        #endregion

        #endregion
    }
}
