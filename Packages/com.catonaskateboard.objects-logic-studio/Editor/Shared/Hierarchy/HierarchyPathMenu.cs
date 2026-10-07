using StudioGUI = CatOnASkateboard.StudioColors.Editor.StudioFieldGUI;
using CatOnASkateboard.StudioColors.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Maps actual prefab branches to reusable paths through component-filtered menus.</summary>
    internal static class HierarchyPathMenu
    {
        #region State

        private static readonly Dictionary<string, GameObject> sources = new Dictionary<string, GameObject>();

        #endregion

        #region Methods

        #region Source

        /// <summary>Uses the selected interaction hierarchy or an explicit sample prefab for standalone presets and counterparts.</summary>
        /// <param name="property">Settings whose paths share one hierarchy source.</param>
        /// <param name="explicitSource">Whether these settings affect a different item instead of the current interaction.</param>
        /// <param name="suggested">Optional ingredient preview prefab supplying the hierarchy.</param>
        /// <returns>The existing object used to build path choices, or null until selected.</returns>
        internal static GameObject Source(SerializedProperty property, bool explicitSource = false, GameObject suggested = null)
        {
            // Actual prefab bindings need no extra source picker; sample sources never enter runtime settings.
            if (suggested != null)
                return suggested;
            if (!explicitSource)
                switch (property.serializedObject.targetObject)
                {
                    case ObjectWorkspace workspace:
                        return workspace.Target.Resolve();
                    case Component component:
                        return component.GetComponentInParent<ObjectItem>(true) is ObjectItem item ? item.gameObject : component.gameObject;
                }
            string key = EntityId.ToULong(property.serializedObject.targetObject.GetEntityId()) + ":" + property.propertyPath;
            sources.TryGetValue(key, out GameObject source);
            source = (GameObject)StudioGUI.ObjectField(new GUIContent("Hierarchy Source",
                "Existing prefab used to map renderer and mesh selections. The reusable settings retain only relative routes."), source, typeof(GameObject), false);
            sources[key] = source;
            return source;
        }

        #endregion

        #region Menu

        /// <summary>Shows a hierarchy selector without exposing a manually entered path.</summary>
        /// <param name="property">Serialized relative route receiving the selection.</param>
        /// <param name="source">Existing hierarchy supplying component choices.</param>
        /// <param name="mesh">Whether to list mesh components instead of all renderers.</param>
        /// <param name="all">Whether an empty route means all renderers.</param>
        /// <param name="transforms">Whether to offer all transforms instead of filtering by render components.</param>
        internal static void Draw(SerializedProperty property, GameObject source, bool mesh, bool all = false, bool transforms = false)
        {
            // Missing imported routes remain visible until explicitly rebound on the destination hierarchy.
            string label = string.IsNullOrEmpty(property.stringValue) ? all ? "All Renderers" : "Root" : property.stringValue;
            if (property.stringValue == ".")
                label = "Root";
            // An empty route must not appear valid when the prefab root has no matching component.
            if (source != null && !all && !transforms)
            {
                Transform target = string.IsNullOrEmpty(property.stringValue) || property.stringValue == "."
                    ? source.transform : source.transform.Find(property.stringValue);
                if (target == null)
                    label += " (missing)";
                else if (mesh ? target.GetComponent<MeshFilter>() == null && target.GetComponent<SkinnedMeshRenderer>() == null
                    : target.GetComponent<Renderer>() == null)
                    label += mesh ? " (no mesh component)" : " (no renderer)";
            }
            Rect rect = EditorGUILayout.GetControlRect();
            StudioFieldMenu.Context(rect, property);
            using StudioFieldColors colors = new StudioFieldColors(rect, StudioFieldColors.Key(property));
            rect = EditorGUI.PrefixLabel(rect, new GUIContent(transforms ? "Transform" : mesh ? "Mesh Target" : "Renderer Target", property.tooltip));
            using (new EditorGUI.DisabledScope(source == null))
                if (GUI.Button(rect, new GUIContent(label, source == null ? "Choose a hierarchy source first." : property.tooltip), EditorStyles.popup))
                    Show(property, source, mesh, all, transforms);
        }

        /// <summary>Builds component choices only when the user opens the menu.</summary>
        /// <param name="property">Destination route.</param>
        /// <param name="source">Existing prefab hierarchy.</param>
        /// <param name="mesh">Whether to require a mesh component.</param>
        /// <param name="all">Whether to offer all renderers independently of the root renderer.</param>
        /// <param name="transforms">Whether transforms without renderers are also selectable.</param>
        private static void Show(SerializedProperty property, GameObject source, bool mesh, bool all, bool transforms)
        {
            // Duplicate sibling names are rejected rather than silently resolving another branch.
            GenericMenu menu = new GenericMenu();
            UnityEngine.Object owner = property.serializedObject.targetObject;
            string field = property.propertyPath;
            string selected = property.stringValue;
            Transform[] branches = source.GetComponentsInChildren<Transform>(true);
            Dictionary<string, int> occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Transform branch in branches)
            {
                string path = AnimationUtility.CalculateTransformPath(branch, source.transform);
                occurrences.TryGetValue(path, out int count);
                occurrences[path] = count + 1;
            }
            if (all)
                menu.AddItem(new GUIContent("All Renderers"), selected.Length == 0, () => Assign(owner, field, string.Empty));
            foreach (Transform branch in branches)
            {
                if (!transforms && (mesh ? branch.GetComponent<MeshFilter>() == null && branch.GetComponent<SkinnedMeshRenderer>() == null
                    : branch.GetComponent<Renderer>() == null))
                    continue;
                string path = AnimationUtility.CalculateTransformPath(branch, source.transform);
                GUIContent label = new GUIContent(path.Length == 0 ? "Root" : "Hierarchy/" + path);
                if (occurrences[path] > 1 || path.Length > 0 && source.transform.Find(path) != branch)
                {
                    menu.AddDisabledItem(new GUIContent(label.text + " (ambiguous name)"));
                    continue;
                }
                string route = all && path.Length == 0 ? "." : path;
                menu.AddItem(label, selected == route, () => Assign(owner, field, route));
            }
            if (menu.GetItemCount() == 0)
                menu.AddDisabledItem(new GUIContent("No compatible components"));
            menu.ShowAsContext();
        }

        /// <summary>Commits a delayed menu selection through a fresh serialized wrapper and native Undo.</summary>
        /// <param name="owner">Object retaining the current draft or preset.</param>
        /// <param name="path">Serialized property path captured at menu creation.</param>
        /// <param name="route">Chosen hierarchy route.</param>
        private static void Assign(UnityEngine.Object owner, string path, string route)
        {
            // A closed inspector or removed array row must not leave an invalid serialized handle in a callback.
            if (owner == null)
                return;
            using SerializedObject data = new SerializedObject(owner);
            SerializedProperty property = data.FindProperty(path);
            if (property == null)
                return;
            property.stringValue = route;
            data.ApplyModifiedProperties();
            if (owner is ObjectWorkspace workspace)
                workspace.Persist();
        }

        #endregion

        #endregion
    }
}
