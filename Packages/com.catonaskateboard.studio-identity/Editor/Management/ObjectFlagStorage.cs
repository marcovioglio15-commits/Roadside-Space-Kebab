using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Reads and updates native flag fields and portable Object Logic Studio preset values.</summary>
    internal static class ObjectFlagStorage
    {
        #region Methods
        #region Discovery

        /// <summary>Identifies serialized flag selections, including empty arrays and portable values.</summary>
        /// <param name="property">Field being traversed.</param>
        /// <param name="portable">Receives whether the field uses portable preset storage.</param>
        /// <returns>True for a complete flag field or flag array.</returns>
        internal static bool IsFlag(SerializedProperty property, out bool portable)
        {
            // Empty portable vectors need their original field schema; names alone are ambiguous.
            portable = ObjectFlagProject.IsPortableValue(property)
                && (property.FindPropertyRelative("Array").boolValue ? PortableArray(property)
                    : FlagType(property.FindPropertyRelative("TypeName").stringValue));
            return portable || property.propertyType == SerializedPropertyType.ObjectReference
                && (FlagType(property.type) || property.objectReferenceValue is ObjectFlag)
                || property.isArray && property.propertyType != SerializedPropertyType.String
                && FlagType(property.arrayElementType);
        }

        /// <summary>Matches the actual serialized flag type without misclassifying similarly named assets.</summary>
        /// <param name="type">Unity's serialized scalar or array-element type.</param>
        /// <returns>True only for an ObjectFlag reference.</returns>
        private static bool FlagType(string type)
        {
            return type is "ObjectFlag" or "PPtr<$ObjectFlag>" or "PPtr<ObjectFlag>";
        }

        /// <summary>Identifies vector-backed preset arrays through element types or their registered schema.</summary>
        /// <param name="property">Portable value to inspect.</param>
        /// <returns>True for an actual flag array, including an empty one.</returns>
        private static bool PortableArray(SerializedProperty property)
        {
            SerializedProperty children = property.FindPropertyRelative("Children");
            if (children.arraySize > 0)
            {
                SerializedProperty first = children.GetArrayElementAtIndex(0);
                if (ObjectFlagProject.IsPortableValue(first) && !first.FindPropertyRelative("Array").boolValue
                    && FlagType(first.FindPropertyRelative("TypeName").stringValue))
                    return true;
            }
            return ObjectFlagProject.PortableArrayPredicate?.Invoke(property) ?? false;
        }

        /// <summary>Checks a stored missing-reference identifier without treating optional nulls as errors.</summary>
        /// <param name="property">Object reference to inspect.</param>
        /// <returns>True when the stored reference no longer resolves.</returns>
        internal static bool Broken(SerializedProperty property)
        {
            // A deleted definition may remain loaded briefly while other assets still reference it.
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script")
                return false;
            return property.objectReferenceValue == null ? EntityId.ToULong(property.objectReferenceEntityIdValue) != 0UL
                : property.objectReferenceValue is ObjectFlag flag && !AssetDatabase.Contains(flag);
        }

        /// <summary>Finds the actual asset reference behind a portable value.</summary>
        /// <param name="value">Portable preset scalar.</param>
        /// <returns>The stored asset reference field.</returns>
        private static SerializedProperty Asset(SerializedProperty value)
        {
            return value?.FindPropertyRelative("Link.Asset");
        }

        /// <summary>Resolves one stored reference when restoring native Undo snapshots.</summary>
        /// <param name="property">Flag scalar or complete array.</param>
        /// <param name="portable">Whether portable preset storage is used.</param>
        /// <param name="index">Array slot, ignored for scalar fields.</param>
        /// <returns>The actual serialized asset reference.</returns>
        internal static SerializedProperty Reference(SerializedProperty property, bool portable, int index)
        {
            bool multiple = portable ? property.FindPropertyRelative("Array").boolValue : property.isArray;
            SerializedProperty value = multiple ? (portable ? property.FindPropertyRelative("Children") : property).GetArrayElementAtIndex(index) : property;
            return portable ? Asset(value) : value;
        }

        /// <summary>Recognizes a deleted link captured before a portable preset was saved.</summary>
        /// <param name="value">Portable scalar value.</param>
        /// <returns>True when its link explicitly records an unresolved reference.</returns>
        private static bool MissingLink(SerializedProperty value)
        {
            // A null managed element is an invalid slot even when no link record survives.
            SerializedProperty reference = Asset(value);
            return reference == null || Broken(reference) || value.FindPropertyRelative("Link.External").boolValue
                && value.FindPropertyRelative("Link.SourceGuid").stringValue.StartsWith("missing:", StringComparison.Ordinal);
        }

        /// <summary>Replaces portable link metadata together with its actual asset reference.</summary>
        /// <param name="value">Portable scalar receiving the selection.</param>
        /// <param name="flag">Replacement definition, or null to clear.</param>
        private static void SetLink(SerializedProperty value, ObjectFlag flag)
        {
            SerializedProperty link = value.FindPropertyRelative("Link");
            link.FindPropertyRelative("Asset").objectReferenceValue = flag;
            link.FindPropertyRelative("Local").boolValue = false;
            link.FindPropertyRelative("External").boolValue = false;
            string guid = string.Empty;
            long id = 0;
            if (flag != null)
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(flag, out guid, out id);
            link.FindPropertyRelative("SourceGuid").stringValue = guid;
            link.FindPropertyRelative("FileId").longValue = id;
            link.FindPropertyRelative("TypeName").stringValue = flag != null ? typeof(ObjectFlag).AssemblyQualifiedName : string.Empty;
        }

        #endregion
        #region Reading

        /// <summary>Reads selection order and preserves null slots for conflict detection.</summary>
        /// <param name="property">Native field or portable value.</param>
        /// <param name="portable">Whether the field stores portable values.</param>
        /// <returns>The current selections, including missing entries.</returns>
        internal static List<ObjectFlag> Read(SerializedProperty property, bool portable)
        {
            // Preserve slot order and nulls so delayed edits can detect outside changes.
            List<ObjectFlag> flags = new List<ObjectFlag>();
            bool multiple = portable ? property.FindPropertyRelative("Array").boolValue : property.isArray;
            if (!multiple)
                flags.Add(Value(portable ? Asset(property) : property));
            else
            {
                SerializedProperty array = portable ? property.FindPropertyRelative("Children") : property;
                for (int index = 0; index < array.arraySize; index++)
                    flags.Add(Value(portable ? Asset(array.GetArrayElementAtIndex(index)) : array.GetArrayElementAtIndex(index)));
            }
            return flags;
        }

        /// <summary>Reads one live definition while treating detached deleted assets as missing.</summary>
        /// <param name="reference">Serialized asset reference.</param>
        /// <returns>The persistent definition, or null for an invalid slot.</returns>
        private static ObjectFlag Value(SerializedProperty reference)
        {
            return reference == null || Broken(reference) ? null : reference.objectReferenceValue as ObjectFlag;
        }

        /// <summary>Counts missing references while preserving optional unassigned scalar fields.</summary>
        /// <param name="property">Selection to inspect.</param>
        /// <param name="portable">Whether it uses portable values.</param>
        /// <param name="emptySlots">Whether empty flag-array entries are included.</param>
        /// <returns>The number of invalid entries.</returns>
        internal static int Missing(SerializedProperty property, bool portable, bool emptySlots)
        {
            // Optional scalar nulls are valid; array slots follow the explicit cleanup policy.
            bool multiple = portable ? property.FindPropertyRelative("Array").boolValue : property.isArray;
            if (!multiple)
                return (portable ? MissingLink(property) : Broken(property)) ? 1 : 0;
            SerializedProperty array = portable ? property.FindPropertyRelative("Children") : property;
            int count = 0;
            for (int index = 0; index < array.arraySize; index++)
            {
                SerializedProperty reference = portable ? Asset(array.GetArrayElementAtIndex(index)) : array.GetArrayElementAtIndex(index);
                if ((portable ? MissingLink(array.GetArrayElementAtIndex(index)) : Broken(reference)) || emptySlots && reference?.objectReferenceValue == null)
                    count++;
            }
            return count;
        }

        #endregion
        #region Writing

        /// <summary>Replaces one selection from an explicit edit using the registered portable-value factory.</summary>
        /// <param name="property">Current destination field.</param>
        /// <param name="portable">Whether portable values must be built.</param>
        /// <param name="flags">New selection.</param>
        internal static void Write(SerializedProperty property, bool portable, IReadOnlyList<ObjectFlag> flags)
        {
            // Rebuild only the selection while preserving its parent settings and metadata.
            bool multiple = portable ? property.FindPropertyRelative("Array").boolValue : property.isArray;
            if (!multiple)
            {
                if (portable)
                    SetLink(property, flags.Count > 0 ? flags[0] : null);
                else
                    property.objectReferenceValue = flags.Count > 0 ? flags[0] : null;
                return;
            }
            SerializedProperty array = portable ? property.FindPropertyRelative("Children") : property;
            if (portable && ObjectFlagProject.PortableValueFactory == null)
                throw new InvalidOperationException("The portable preset editor is unavailable.");
            array.arraySize = flags.Count;
            for (int index = 0; index < flags.Count; index++)
                if (portable)
                    array.GetArrayElementAtIndex(index).managedReferenceValue = ObjectFlagProject.PortableValueFactory(flags[index], index);
                else
                    array.GetArrayElementAtIndex(index).objectReferenceValue = flags[index];
        }

        /// <summary>Removes only matching or missing entries without resetting other field values.</summary>
        /// <param name="property">Current selection or broken scalar reference.</param>
        /// <param name="portable">Whether portable values are stored.</param>
        /// <param name="remove">Definitions being deleted, or null for validation cleanup.</param>
        /// <param name="emptySlots">Whether validation removes empty array slots.</param>
        /// <returns>Number of references removed.</returns>
        internal static int Clean(SerializedProperty property, bool portable, HashSet<ObjectFlag> remove, bool emptySlots)
        {
            bool multiple = portable ? property.FindPropertyRelative("Array").boolValue : property.isArray;
            SerializedProperty array = portable && multiple ? property.FindPropertyRelative("Children") : property;
            int removed = 0;
            // Descending deletion keeps every unrelated element and its order stable.
            for (int index = multiple ? array.arraySize - 1 : 0; index >= 0; index--)
            {
                SerializedProperty value = multiple ? array.GetArrayElementAtIndex(index) : property;
                SerializedProperty reference = portable ? Asset(value) : value;
                bool matches = remove != null ? reference?.objectReferenceValue is ObjectFlag flag && remove.Contains(flag)
                    : (portable ? MissingLink(value) : Broken(reference)) || multiple && emptySlots && reference?.objectReferenceValue == null;
                if (!matches)
                    continue;
                if (reference != null)
                    reference.objectReferenceValue = null;
                if (portable && !multiple)
                    SetLink(value, null);
                if (multiple)
                {
                    if (portable)
                        value.managedReferenceValue = null;
                    array.DeleteArrayElementAtIndex(index);
                }
                removed++;
            }
            return removed;
        }

        #endregion
        #endregion
    }
}
