using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.StudioInput.Editor
{
    /// <summary>Caches compatible action choices while preserving the stable references already stored in presets.</summary>
    [InitializeOnLoad]
    public static class StudioInputActionMenu
    {
        #region Cache

        private static readonly Dictionary<string, Choices> menus = new Dictionary<string, Choices>();

        /// <summary>Stores labels and imported action subassets together, without creating reference assets.</summary>
        private sealed class Choices
        {
            public readonly List<InputActionReference> References = new List<InputActionReference> { null };
            public GUIContent[] Labels;
        }

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Invalidates menus after action imports, renames or deletions.</summary>
        static StudioInputActionMenu()
        {
            // Asset discovery belongs to cache rebuilds, never ordinary Inspector repaints.
            EditorApplication.projectChanged += menus.Clear;
        }

        #endregion

        #region Controls

        /// <summary>Displays compatible actions in native map submenus while retaining the current reference.</summary>
        /// <param name="serialized">Input preset or isolated draft being edited.</param>
        /// <param name="name">Serialized field containing an action reference.</param>
        /// <param name="key">Stable cache key identifying the compatibility filter.</param>
        /// <param name="compatible">Filter accepting actions supported by this field.</param>
        public static void Draw(SerializedObject serialized, string name, string key, Func<InputAction, bool> compatible)
        {
            // Existing standalone references match imported subassets by action ID and asset identity.
            using SerializedProperty property = serialized.FindProperty(name);
            InputActionReference current = property.objectReferenceValue as InputActionReference;
            if (!menus.TryGetValue(key, out Choices choices))
                menus.Add(key, choices = Build(compatible));
            int selected = current == null ? 0 : -1;
            for (int index = 1; index < choices.References.Count; index++)
                if (SameAction(current, choices.References[index]))
                {
                    selected = index;
                    break;
                }

            // An incompatible stored value remains visible and unchanged until explicitly replaced.
            GUIContent label = new GUIContent(property.displayName, property.tooltip);
            EditorGUI.BeginChangeCheck();
            int requested = EditorGUILayout.Popup(label, selected, choices.Labels);
            if (EditorGUI.EndChangeCheck() && requested >= 0)
                property.objectReferenceValue = choices.References[requested];
            if (selected < 0)
                EditorGUILayout.LabelField("Current action", current != null && current.action != null
                    ? current.action.actionMap.name + "_" + current.action.name + " (incompatible)" : "Missing action");
        }

        /// <summary>Compares action identity without replacing a saved reference merely because the menu was drawn.</summary>
        /// <param name="left">Current preset reference.</param>
        /// <param name="right">Imported action subasset.</param>
        /// <returns>True when both references resolve to the same action in the same asset.</returns>
        private static bool SameAction(InputActionReference left, InputActionReference right)
        {
            // Separate reference objects can describe the same stable action.
            return left != null && right != null && left.asset == right.asset
                && left.action != null && right.action != null && left.action.id == right.action.id;
        }

        /// <summary>Enumerates imported action subassets once for a role after a project change.</summary>
        /// <param name="compatible">Predicate selecting supported action shapes.</param>
        /// <returns>Compatible persistent references and their unambiguous labels.</returns>
        private static Choices Build(Func<InputAction, bool> compatible)
        {
            // Imported references retain asset identity even when multiple maps share the same action names.
            Choices choices = new Choices();
            foreach (string guid in AssetDatabase.FindAssets("t:InputActionAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is InputActionReference reference && (reference.hideFlags & HideFlags.HideInHierarchy) == 0
                        && reference.action != null && compatible(reference.action))
                        choices.References.Add(reference);
            }

            // Keep None first, followed by contiguous alphabetical map and action groups.
            choices.References.Sort(1, choices.References.Count - 1, Comparer<InputActionReference>.Create(Compare));
            choices.Labels = CreateLabels(choices.References);
            return choices;
        }

        /// <summary>Orders action entries consistently without displaying their storage paths.</summary>
        /// <param name="left">First imported action reference.</param>
        /// <param name="right">Second imported action reference.</param>
        /// <returns>The map, action or source ordering used by the native popup.</returns>
        private static int Compare(InputActionReference left, InputActionReference right)
        {
            // A source path is only a stable tie-breaker for identically named asset copies.
            int order = StringComparer.Ordinal.Compare(MenuPath(left), MenuPath(right));
            if (order == 0)
                order = StringComparer.Ordinal.Compare(left.asset.name, right.asset.name);
            return order != 0 ? order : StringComparer.Ordinal.Compare(
                AssetDatabase.GetAssetPath(left.asset), AssetDatabase.GetAssetPath(right.asset));
        }

        /// <summary>Builds map submenus and adds a short source qualifier only for ambiguous action names.</summary>
        /// <param name="references">Sorted references with the unassigned entry at index zero.</param>
        /// <returns>Native popup labels aligned with the original reference indices.</returns>
        private static GUIContent[] CreateLabels(List<InputActionReference> references)
        {
            // Count shared map/action names before choosing compact labels for each leaf.
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 1; index < references.Count; index++)
            {
                string path = MenuPath(references[index]);
                counts.TryGetValue(path, out int count);
                counts[path] = count + 1;
            }

            GUIContent[] labels = new GUIContent[references.Count];
            labels[0] = new GUIContent("None", "Clear this role. Required actions must be assigned before Apply.");
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 1; index < references.Count; index++)
            {
                InputActionReference reference = references[index];
                string path = MenuPath(reference);
                if (counts[path] > 1)
                    path += " (" + reference.asset.name.Replace('/', '∕') + ")";
                string label = path;
                int copy = 1;
                while (!used.Add(label))
                    label = path + " " + ++copy;
                labels[index] = new GUIContent(label, reference.action.actionMap.name + "_" + reference.action.name
                    + " — " + reference.asset.name);
            }
            return labels;
        }

        /// <summary>Uses Unity's menu separator only between the map and its action.</summary>
        /// <param name="reference">Imported action with a validated map.</param>
        /// <returns>A native menu path containing exactly one submenu level.</returns>
        private static string MenuPath(InputActionReference reference)
        {
            // Literal slashes in authored names must not create unintended additional submenu levels.
            return reference.action.actionMap.name.Replace('/', '∕') + "/" + reference.action.name.Replace('/', '∕');
        }

        #endregion

        #endregion
    }
}
