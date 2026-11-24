using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Builds named navigation paths from serialized fields and collection element labels.</summary>
    internal static class ObjectFlagLabels
    {
        #region Methods
        #region Paths

        /// <summary>Names the asset, hierarchy branch and exact component owning a field.</summary>
        /// <param name="owner">Loaded serialized owner.</param>
        /// <param name="assetPath">Containing project asset.</param>
        /// <returns>The navigation root for this owner.</returns>
        internal static string Root(UnityEngine.Object owner, string assetPath)
        {
            // Asset folders and subasset names disambiguate otherwise identical selection paths.
            string root = System.IO.Path.GetFileNameWithoutExtension(assetPath) + " [" + System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/') + "]";
            if (owner is not Component && owner.name != System.IO.Path.GetFileNameWithoutExtension(assetPath))
                root += "/" + owner.name.Replace('/', '∕');
            string type = owner.GetType().Name;
            string studio = owner.GetType().Namespace?.Contains("ObjectsLogicStudio", StringComparison.Ordinal) ?? false
                ? "Object Logic Studio" : owner.GetType().Namespace?.Contains("PlayerStudio", StringComparison.Ordinal) ?? false
                    ? "Player Studio" : "Identity";
            if (owner is Component component)
            {
                string branch = Hierarchy(component.transform, assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase));
                if (branch.Length > 0)
                    root += "/" + branch;
                Component[] siblings = component.GetComponents(component.GetType());
                type = type.StartsWith("Object", StringComparison.Ordinal) && type != "ObjectIdentity" ? type.Substring(6) + "Interaction" : type;
                type = ObjectNames.NicifyVariableName(type) + (siblings.Length > 1 ? " #" + (Array.IndexOf(siblings, component) + 1) : string.Empty);
            }
            else
                type = ObjectNames.NicifyVariableName(type);
            return root + "/" + studio + "/" + type;
        }

        /// <summary>Distinguishes same-named hierarchy branches without exposing internal object IDs.</summary>
        /// <param name="target">Transform owning the selection.</param>
        /// <param name="includeRoot">Whether scene root objects must be named too.</param>
        /// <returns>The named branch with sibling positions only where needed.</returns>
        private static string Hierarchy(Transform target, bool includeRoot)
        {
            List<string> labels = new List<string>();
            for (Transform current = target; current != null && (includeRoot || current.parent != null); current = current.parent)
            {
                int matches = 0;
                if (current.parent != null)
                {
                    foreach (Transform sibling in current.parent)
                        if (sibling.name == current.name)
                            matches++;
                }
                else
                    foreach (GameObject sibling in current.gameObject.scene.GetRootGameObjects())
                        if (sibling.name == current.name)
                            matches++;
                labels.Add(current.name.Replace('/', '∕') + (matches > 1 ? " [" + current.GetSiblingIndex() + "]" : string.Empty));
            }
            labels.Reverse();
            return string.Join("/", labels);
        }

        /// <summary>Replaces numeric array paths with named order, variant and preset elements.</summary>
        /// <param name="property">Field whose full navigation path is required.</param>
        /// <param name="portable">Whether portable storage metadata must be hidden.</param>
        /// <returns>A slash-separated nested navigation path.</returns>
        internal static string Path(SerializedProperty property, bool portable = false)
        {
            List<string> labels = new List<string>();
            string prefix = string.Empty;
            bool portableContext = false;
            foreach (string segment in property.propertyPath.Split('.'))
            {
                prefix = prefix.Length == 0 ? segment : prefix + "." + segment;
                if (segment == "Array")
                    continue;
                SerializedProperty current = property.serializedObject.FindProperty(prefix);
                if (segment.StartsWith("data[", StringComparison.Ordinal))
                {
                    if (ObjectFlagProject.IsPortableValue(current))
                    {
                        portableContext = true;
                        if (current.FindPropertyRelative("Name").stringValue is "settings" or "Settings")
                            continue;
                    }
                    labels.Add(Element(current, segment));
                    continue;
                }
                if (segment is "settings" or "Settings" or "Children" or "Values" or "Fields" or "Components")
                    continue;
                if (portableContext && segment is "Link" or "Asset"
                    || segment == "Configuration" && property.serializedObject.targetObject.GetType().Name == "ObjectInteractionPreset")
                    continue;
                labels.Add(segment switch
                {
                    "Catalog" => "Orders Catalog",
                    "Variants" => "Variant Ingredients",
                    _ => current != null ? current.displayName : ObjectNames.NicifyVariableName(segment)
                });
            }
            // Portable root values carry their original field name in metadata.
            if (portable && labels.Count > 0)
            {
                string name = property.FindPropertyRelative("Name").stringValue;
                if (!name.StartsWith("data[", StringComparison.Ordinal))
                    labels[labels.Count - 1] = ObjectNames.NicifyVariableName(name);
            }
            return string.Join("/", labels);
        }

        /// <summary>Finds a useful label for a named element or portable component record.</summary>
        /// <param name="property">Array element being named.</param>
        /// <param name="fallback">Serialized array position.</param>
        /// <returns>A stable label including the element position.</returns>
        private static string Element(SerializedProperty property, string fallback)
        {
            if (property == null)
                return fallback;
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                return property.objectReferenceValue.name.Replace('/', '∕') + " [" + fallback.Substring(5).TrimEnd(']') + "]";
            foreach (string field in new[] { "Name", "DisplayName", "Label", "TypeName" })
            {
                SerializedProperty name = property.FindPropertyRelative(field);
                if (name == null || name.propertyType != SerializedPropertyType.String || name.stringValue.Length == 0
                    || name.stringValue.StartsWith("data[", StringComparison.Ordinal))
                    continue;
                string label = name.stringValue;
                if (field == "Name" && ObjectFlagProject.IsPortableValue(property))
                    label = ObjectNames.NicifyVariableName(label);
                if (field == "TypeName")
                {
                    label = label.Split(',')[0];
                    label = ObjectNames.NicifyVariableName(label.Substring(label.LastIndexOf('.') + 1));
                }
                return label.Replace('/', '∕') + " [" + fallback.Substring(5).TrimEnd(']') + "]";
            }
            // Named values inside portable collection elements use a Children list.
            SerializedProperty children = property.FindPropertyRelative("Children");
            if (children != null && children.isArray)
                for (int index = 0; index < children.arraySize; index++)
                {
                    SerializedProperty child = children.GetArrayElementAtIndex(index);
                    if (ObjectFlagProject.IsPortableValue(child) && child.FindPropertyRelative("Name").stringValue == "Name"
                        && child.FindPropertyRelative("Text")?.propertyType == SerializedPropertyType.String)
                        return child.FindPropertyRelative("Text").stringValue.Replace('/', '∕') + " [" + fallback.Substring(5).TrimEnd(']') + "]";
                }
            return "Element " + fallback.Substring(5).TrimEnd(']');
        }

        #endregion
        #endregion
    }
}
