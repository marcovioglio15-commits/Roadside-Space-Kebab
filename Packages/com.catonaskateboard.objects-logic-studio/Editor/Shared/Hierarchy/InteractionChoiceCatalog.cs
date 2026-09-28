using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Shares named existing-interaction selection without storing names as identity.</summary>
    internal sealed class InteractionChoiceCatalog
    {
        #region State

        private readonly Dictionary<(long Excluded, long SameKindAs), (long[] Identities, GUIContent[] Labels)> filtered =
            new Dictionary<(long Excluded, long SameKindAs), (long[] Identities, GUIContent[] Labels)>();
        private ObjectInteraction[] features = Array.Empty<ObjectInteraction>();
        private long[] identities = Array.Empty<long>();
        private GUIContent[] labels = Array.Empty<GUIContent>();

        #endregion

        #region Methods

        #region Catalog

        /// <summary>Caches names and stable prefab IDs at hierarchy refresh boundaries.</summary>
        /// <param name="root">Prefab branch containing selectable interactions.</param>
        /// <param name="product">Exclude product configuration itself when selecting ingredient-gated features.</param>
        internal void Refresh(GameObject root, bool product)
        {
            // Duplicate names include a path, type and component index so every choice remains identifiable.
            List<ObjectInteraction> found = new List<ObjectInteraction>();
            if (root != null)
                foreach (ObjectInteraction interaction in root.GetComponentsInChildren<ObjectInteraction>(true))
                    if (interaction is not ObjectInteractionUnlock && (!product || interaction is not ObjectAssemblyProduct))
                        found.Add(interaction);
            filtered.Clear();
            features = found.ToArray();
            identities = new long[found.Count];
            labels = new GUIContent[found.Count + 1];
            labels[0] = new GUIContent("Select an existing interaction", "Configure the interaction in its own category first.");
            for (int index = 0; index < found.Count; index++)
            {
                identities[index] = ObjectWorkspaceTarget.FileId(found[index]);
                string path = AnimationUtility.CalculateTransformPath(found[index].transform, root.transform);
                labels[index + 1] = new GUIContent(found[index].InteractionName + "  ("
                    + found[index].GetType().Name.Replace("Object", string.Empty) + ", "
                    + (path.Length > 0 ? path : "Root") + ", " + (index + 1) + ")", "Stable reference to this existing component.");
            }
        }

        /// <summary>Offers configured features by name while excluding already authored overrides.</summary>
        /// <param name="menu">Menu receiving available feature entries.</param>
        /// <param name="excluded">Existing ingredient-rule target identities.</param>
        /// <param name="selected">Called with the exact selected component identity.</param>
        internal void AddChoices(GenericMenu menu, long[] excluded, Action<long> selected)
        {
            // A rule is never created with an empty target that can be confused with its ingredient conditions.
            if (identities.Length == 0)
                menu.AddDisabledItem(new GUIContent("Add product interactions in the other categories first"));
            for (int index = 0; index < identities.Length; index++)
            {
                long identity = identities[index];
                if (identity == 0 || Array.IndexOf(excluded, identity) >= 0)
                    menu.AddDisabledItem(labels[index + 1]);
                else
                    menu.AddItem(labels[index + 1], false, () => selected(identity));
            }
        }

        /// <summary>Caches a filtered selector so unrelated interaction types are never offered as replacements.</summary>
        /// <param name="excluded">Component identity omitted from this selector, or zero.</param>
        /// <param name="sameKindAs">Component whose type must match, zero for all types, or an unresolved identity.</param>
        /// <returns>Parallel saved identities and labels including an explicit empty selection.</returns>
        internal (long[] Identities, GUIContent[] Labels) Options(long excluded, long sameKindAs)
        {
            // Lists are built only after a catalog refresh or when the selected target changes.
            if (filtered.TryGetValue((excluded, sameKindAs), out (long[] Identities, GUIContent[] Labels) cached))
                return cached;
            List<long> values = new List<long> { 0 };
            List<GUIContent> names = new List<GUIContent> { labels.Length > 0 ? labels[0] : new GUIContent("Select an existing interaction") };
            int matching = Array.IndexOf(identities, sameKindAs);
            for (int index = 0; index < identities.Length; index++)
                if (identities[index] != 0 && identities[index] != excluded
                    && (sameKindAs == 0 || matching >= 0 && InteractionUnlockSettings.IsSameKind(features[matching], features[index])))
                {
                    values.Add(identities[index]);
                    names.Add(labels[index + 1]);
                }
            cached = (values.ToArray(), names.ToArray());
            filtered.Add((excluded, sameKindAs), cached);
            return cached;
        }

        /// <summary>Edits an existing component reference through its cached and filtered display name.</summary>
        /// <param name="property">Serialized stable component identity.</param>
        /// <param name="label">Role of the selected feature.</param>
        /// <param name="excluded">Identity forbidden in this selector, or zero.</param>
        /// <param name="sameKindAs">Identity whose type must match, or zero for any type.</param>
        internal void Draw(SerializedProperty property, string label, long excluded = 0, long sameKindAs = 0)
        {
            // Invalid selections remain unresolved until explicitly replaced; the field never rewrites them on repaint.
            (long[] Identities, GUIContent[] Labels) options = Options(excluded, sameKindAs);
            int current = Array.IndexOf(options.Identities, property.longValue);
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(new GUIContent(label, property.tooltip), current >= 0 ? current : 0, options.Labels);
            if (EditorGUI.EndChangeCheck())
                property.longValue = options.Identities[selected];
            if (current < 0)
                EditorGUILayout.LabelField("The selected interaction is unavailable for this field. Choose an existing compatible interaction.", EditorStyles.miniLabel);
        }

        #endregion

        #endregion
    }
}
