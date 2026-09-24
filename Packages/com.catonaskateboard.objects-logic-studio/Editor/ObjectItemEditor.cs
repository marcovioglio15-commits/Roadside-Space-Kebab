using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shows runtime consumption receipts without exposing mutable inventory fields.</summary>
    [CustomEditor(typeof(ObjectItem))]
    internal sealed class ObjectItemEditor : UnityEditor.Editor
    {
        #region Methods

        #region Inspector

        /// <summary>Explains contact ownership and displays the surviving item's consumed tag counts in Play.</summary>
        public override void OnInspectorGUI()
        {
            // Receipts belong to the running instance and never modify the prefab asset.
            ObjectItem item = (ObjectItem)target;
            EditorGUILayout.LabelField("Contact participants use this object's tag and owned child colliders.", EditorStyles.wordWrappedMiniLabel);
            if (!Application.isPlaying)
                return;
            EditorGUILayout.LabelField(item.IsConsumed ? "Consumed" : "Available", EditorStyles.boldLabel);
            if (item.ConsumedTags.Count == 0)
                EditorGUILayout.LabelField("No consumed items.", EditorStyles.miniLabel);
            foreach (KeyValuePair<string, int> receipt in item.ConsumedTags)
                EditorGUILayout.LabelField(new GUIContent(receipt.Key, "Tag recorded at consumption; count belongs to this runtime item."),
                    new GUIContent(receipt.Value.ToString()));
        }

        #endregion

        #endregion
    }
}
