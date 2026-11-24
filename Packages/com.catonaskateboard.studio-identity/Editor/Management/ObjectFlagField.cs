using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Describes one stable serialized selection without retaining scene objects or property handles.</summary>
    [Serializable]
    internal sealed class ObjectFlagField
    {
        #region State

        [Tooltip("Containing project asset.")]
        [UnityEngine.SerializeField]
        internal string AssetPath;
        [Tooltip("Stable owner identity inside the asset.")]
        [UnityEngine.SerializeField]
        internal string OwnerId;
        [Tooltip("Exact serialized selection path.")]
        [UnityEngine.SerializeField]
        internal string PropertyPath;
        [Tooltip("Named asset and component path.")]
        [UnityEngine.SerializeField]
        internal string Root;
        [Tooltip("Named path inside the owner.")]
        [UnityEngine.SerializeField]
        internal string Label;
        [Tooltip("Selection uses portable preset values.")]
        [UnityEngine.SerializeField]
        internal bool Portable;
        [Tooltip("Selection allows multiple flags.")]
        [UnityEngine.SerializeField]
        internal bool Multiple;
        [Tooltip("Field is a flag selection.")]
        [UnityEngine.SerializeField]
        internal bool IsFlag;
        [Tooltip("Membership captured by the scan.")]
        [UnityEngine.SerializeField]
        internal ObjectFlag[] Flags;
        [Tooltip("Invalid slots captured by the scan.")]
        [UnityEngine.SerializeField]
        internal int Missing;
        [Tooltip("Serialized field fingerprint captured by the scan.")]
        [SerializeField]
        internal string ContentHash;

        #endregion
        #region Properties

        internal string OwnerKey => AssetPath + ":" + OwnerId;
        internal string Key => OwnerKey + ":" + PropertyPath;
        internal string Location => AssetPath + " / " + Root + " / " + Label + " (" + PropertyPath + ")";

        #endregion
        #region Methods
        #region Capture

        /// <summary>Captures labels, selected flags and missing slots while the owner is loaded.</summary>
        /// <param name="assetPath">Containing asset path.</param>
        /// <param name="owner">Owner visited by the project scanner.</param>
        /// <param name="property">Native selection or portable preset value.</param>
        /// <param name="portable">Whether this is a portable preset selection.</param>
        /// <param name="isFlag">Whether this field stores flags rather than another broken reference.</param>
        /// <param name="emptySlots">Whether empty flag array slots count as missing.</param>
        internal ObjectFlagField(string assetPath, UnityEngine.Object owner, SerializedProperty property, bool portable, bool isFlag, bool emptySlots)
        {
            AssetPath = assetPath;
            OwnerId = ObjectFlagAssetAccess.Id(owner);
            PropertyPath = property.propertyPath;
            Root = ObjectFlagLabels.Root(owner, assetPath);
            Label = ObjectFlagLabels.Path(property, portable);
            Portable = portable;
            IsFlag = isFlag;
            Multiple = portable ? property.FindPropertyRelative("Array").boolValue : property.isArray;
            Flags = ObjectFlagStorage.Read(property, portable).ToArray();
            Missing = ObjectFlagStorage.Missing(property, portable, emptySlots);
            ContentHash = property.contentHash.ToString();
        }

        #endregion
        #region Resolution

        /// <summary>Resolves the original field without guessing after asset structure changes.</summary>
        /// <param name="action">Receives the current owner and property.</param>
        /// <param name="write">Whether the action saves modifications.</param>
        /// <returns>True when the original owner and property still exist.</returns>
        internal bool Visit(Func<UnityEngine.Object, SerializedProperty, bool> action, bool write = false)
        {
            bool found = false;
            ObjectFlagAssetAccess.Visit(AssetPath, owner =>
            {
                if (ObjectFlagAssetAccess.Id(owner) != OwnerId)
                    return false;
                using SerializedObject data = new SerializedObject(owner);
                SerializedProperty property = data.FindProperty(PropertyPath);
                if (property == null)
                    return false;
                found = true;
                return action(owner, property);
            }, write);
            return found;
        }

        /// <summary>Compares current membership with the scan before accepting a delayed choice.</summary>
        /// <param name="property">Current serialized field.</param>
        /// <param name="expected">Membership captured when the editor opened.</param>
        /// <returns>True when neither membership nor slot count changed.</returns>
        internal bool Matches(SerializedProperty property, ObjectFlag[] expected)
        {
            List<ObjectFlag> current = ObjectFlagStorage.Read(property, Portable);
            if (current.Count != expected.Length)
                return false;
            for (int index = 0; index < current.Count; index++)
                if (current[index] != expected[index])
                    return false;
            return true;
        }

        #endregion
        #endregion
    }
}
