using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioColors.Editor
{
    /// <summary>Transfers the fields represented by a section or complete interaction as one operation.</summary>
    public static class StudioFieldGroup
    {
        #region State

        private static StudioPropertyValue[] clipboard;
        private static string[] names;
        /// <summary>Adds tool-specific updates for a complete field selection.</summary>
        public static event Action<GenericMenu, SerializedProperty[]> Populate;

        #endregion
        #region Methods
        #region Menu

        /// <summary>Opens a menu over a section without retaining live serialized handles in its callbacks.</summary>
        /// <param name="rect">Section or interaction header.</param>
        /// <param name="properties">Exact fields represented by the header.</param>
        /// <param name="key">Persistent section color key.</param>
        public static void Context(Rect rect, SerializedProperty[] properties, string key)
        {
            if (Event.current.type != EventType.ContextClick || !rect.Contains(Event.current.mousePosition))
                return;
            GenericMenu menu = new GenericMenu();
            if (properties.Length > 0 && Array.TrueForAll(properties, property => property != null))
            {
                // Capture all fields before the menu callback outlives the drawing pass.
                StudioPropertyValue[] values = Array.ConvertAll(properties, property => new StudioPropertyValue(property));
                string[] fields = Array.ConvertAll(properties, property => property.name);
                string[] paths = Array.ConvertAll(properties, property => property.propertyPath);
                UnityEngine.Object owner = properties[0].serializedObject.targetObject;
                Func<bool>[] guards = Array.ConvertAll(paths, path => StudioFieldMenu.Guard(owner, path));
                bool handled = StudioFieldMenu.TryClipboard(menu, properties);
                if (!handled)
                    menu.AddItem(new GUIContent("Copy"), false, () => { clipboard = values; names = fields; });
                if (!handled && GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode && Accepts(properties))
                {
                    StudioPropertyValue[] pasted = clipboard;
                    menu.AddItem(new GUIContent("Paste"), false, () => Paste(owner, paths, pasted, guards));
                }
                else if (!handled)
                    menu.AddDisabledItem(new GUIContent("Paste"));
                int count = menu.GetItemCount();
                Populate?.Invoke(menu, properties);
                if (menu.GetItemCount() == count)
                    StudioFieldMenu.Unavailable(menu);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Copy"));
                menu.AddDisabledItem(new GUIContent("Paste"));
                StudioFieldMenu.Unavailable(menu);
            }
            StudioFieldColors.Menu(menu, key);
            menu.ShowAsContext();
            Event.current.Use();
        }

        /// <summary>Checks every copied field before enabling a group paste.</summary>
        /// <param name="properties">Proposed destination selection.</param>
        /// <returns>True when field names and serialized types match in order.</returns>
        private static bool Accepts(SerializedProperty[] properties)
        {
            if (clipboard == null || clipboard.Length != properties.Length)
                return false;
            for (int index = 0; index < properties.Length; index++)
                if (names[index] != properties[index].name || !clipboard[index].Accepts(properties[index]))
                    return false;
            return true;
        }

        /// <summary>Applies the complete group through one serialized transaction and ownership notification.</summary>
        /// <param name="owner">Original destination draft or asset.</param>
        /// <param name="paths">Original destination field routes.</param>
        /// <param name="values">Immutable copied field snapshots.</param>
        /// <param name="guards">Checks protecting each source selection.</param>
        private static void Paste(UnityEngine.Object owner, string[] paths, StudioPropertyValue[] values, Func<bool>[] guards)
        {
            // The serialized buffer is committed only after every destination has accepted its value.
            if (owner == null || EditorApplication.isPlayingOrWillChangePlaymode || Array.Exists(guards, guard => !guard()))
                return;
            using SerializedObject data = new SerializedObject(owner);
            for (int index = 0; index < paths.Length; index++)
                if (!values[index].Accepts(data.FindProperty(paths[index])))
                    return;
            StudioFieldMenu.RecordChange(owner);
            for (int index = 0; index < paths.Length; index++)
                values[index].Apply(data.FindProperty(paths[index]));
            data.ApplyModifiedProperties();
            StudioFieldMenu.Notify(owner);
        }

        #endregion
        #endregion
    }
}
