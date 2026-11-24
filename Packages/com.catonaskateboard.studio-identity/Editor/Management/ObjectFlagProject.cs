using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Provides explicit project scans and editor integration points without runtime dependencies.</summary>
    public static class ObjectFlagProject
    {
        #region Integration

        /// <summary>Builds a portable preset element for a selected flag and its zero-based slot.</summary>
        public static Func<ObjectFlag, int, object> PortableValueFactory { internal get; set; }
        /// <summary>Resolves empty portable arrays against their original editor field schema.</summary>
        public static Func<SerializedProperty, bool> PortableArrayPredicate { internal get; set; }
        /// <summary>Lets package editors open the exact owner and property from a Ping action.</summary>
        public static event Func<UnityEngine.Object, string, bool> OpenEditor;
        /// <summary>Whether the latest native Undo event was handled by flag snapshots instead of a prefab editor.</summary>
        public static bool UndoRestored { get; internal set; }

        #endregion
        #region Methods
        #region Portable Schema

        /// <summary>Distinguishes a preset value from Unity collection containers before reading typed metadata.</summary>
        /// <param name="property">Serialized property encountered during traversal.</param>
        /// <returns>True for a populated portable value with the expected metadata types.</returns>
        public static bool IsPortableValue(SerializedProperty property)
        {
            // Unity also names its collection child Array; it is never the portable Boolean field.
            if (property == null || property.isArray
                || property.propertyType is not (SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference))
                return false;
            if (property.type != "ObjectPresetValue" && (property.propertyType != SerializedPropertyType.ManagedReference
                || !property.managedReferenceFullTypename.EndsWith(".ObjectPresetValue", StringComparison.Ordinal)))
                return false;
            return property.FindPropertyRelative("Name")?.propertyType == SerializedPropertyType.String
                && property.FindPropertyRelative("TypeName")?.propertyType == SerializedPropertyType.String
                && property.FindPropertyRelative("Array")?.propertyType == SerializedPropertyType.Boolean
                && property.FindPropertyRelative("Children") is { isArray: true, propertyType: SerializedPropertyType.Generic };
        }

        #endregion
        #region Scanning

        /// <summary>Scans serialized fields directly; dependency lists never count a flag as its own use.</summary>
        /// <param name="scenes">Whether scene files are included.</param>
        /// <param name="packages">Whether package assets are included.</param>
        /// <param name="emptySlots">Whether unassigned flag-array slots are invalid.</param>
        /// <param name="asset">Optional root asset limiting the scan.</param>
        /// <param name="follow">Whether a scoped scan follows shared asset references.</param>
        /// <returns>A complete result or explicit cancellation and failure diagnostics.</returns>
        internal static ObjectFlagScan Scan(bool scenes, bool packages, bool emptySlots, UnityEngine.Object asset = null, bool follow = true)
        {
            ObjectFlagScan result = new ObjectFlagScan();
            List<string> paths = asset != null ? new List<string> { AssetDatabase.GetAssetPath(asset) } : new List<string>(AssetDatabase.GetAllAssetPaths());
            HashSet<string> visited = new HashSet<string>(paths);
            try
            {
                // Scan runs only on an explicit command, never during repaint or per frame.
                for (int index = 0; index < paths.Count; index++)
                {
                    string path = paths[index];
                    if ((!packages && path.StartsWith("Packages/", StringComparison.Ordinal) && (asset == null || index > 0))
                        || !Candidate(path, scenes || asset != null && index == 0))
                        continue;
                    if (EditorUtility.DisplayCancelableProgressBar("Identity Flags", path, (float)index / paths.Count))
                    {
                        result.Cancelled = true;
                        break;
                    }
                    try
                    {
                        int firstLink = result.Links.Count;
                        ObjectFlagAssetAccess.Visit(path, owner =>
                        {
                            Capture(result, path, owner, emptySlots);
                            return false;
                        });
                        if (asset != null && follow)
                            for (int link = firstLink; link < result.Links.Count; link++)
                            {
                                string target = result.Links[link].TargetPath;
                                if (visited.Add(target))
                                    paths.Add(target);
                            }
                    }
                    catch (Exception exception)
                    {
                        result.Errors.Add(path + ": " + exception.Message);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return result;
        }

        /// <summary>Includes serialized Unity assets and custom ScriptableObjects while skipping imported media.</summary>
        /// <param name="path">Asset database path.</param>
        /// <param name="scenes">Whether scene files are enabled.</param>
        /// <returns>True for assets that can contain serialized selections.</returns>
        private static bool Candidate(string path, bool scenes)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".unity":
                    return scenes;
                case ".prefab":
                case ".asset":
                case ".mat":
                case ".anim":
                case ".controller":
                case ".overridecontroller":
                case ".playable":
                case ".guiskin":
                    return true;
                default:
                    Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                    return type != null && typeof(ScriptableObject).IsAssignableFrom(type) && type != typeof(MonoScript);
            }
        }

        /// <summary>Records whole flag arrays once and follows shared assets through actual references.</summary>
        /// <param name="result">Scan receiving fields and navigation edges.</param>
        /// <param name="path">Containing project asset.</param>
        /// <param name="owner">Currently loaded serialized owner.</param>
        /// <param name="emptySlots">Whether empty flag slots are invalid.</param>
        private static void Capture(ObjectFlagScan result, string path, UnityEngine.Object owner, bool emptySlots)
        {
            if (owner is ObjectFlag or ObjectFlagGroupsPreset or ObjectFlagValidationPreset)
                return;
            using SerializedObject data = new SerializedObject(owner);
            using SerializedProperty property = data.GetIterator();
            bool enter = true;
            while (property.Next(enter))
            {
                enter = true;
                bool flag = ObjectFlagStorage.IsFlag(property, out bool portable);
                if (flag)
                {
                    result.Fields.Add(new ObjectFlagField(path, owner, property, portable, flag, emptySlots));
                    enter = false;
                }
                else if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue is ScriptableObject target && target is not ObjectFlag)
                    result.Links.Add(new ObjectFlagLink(path + ":" + ObjectFlagAssetAccess.Id(owner),
                        AssetDatabase.GetAssetPath(target) + ":" + ObjectFlagAssetAccess.Id(target),
                        ObjectFlagLabels.Root(owner, path), ObjectFlagLabels.Path(property)));
            }
        }

        #endregion
        #region Navigation

        /// <summary>Opens a package-specific editor or a focused selection window for other owners.</summary>
        /// <param name="owner">Resolved owner to inspect.</param>
        /// <param name="path">Exact serialized selection path.</param>
        internal static void Open(UnityEngine.Object owner, string path)
        {
            EditorGUIUtility.PingObject(owner);
            if (OpenEditor != null)
                foreach (Func<UnityEngine.Object, string, bool> open in OpenEditor.GetInvocationList())
                    if (open(owner, path))
                        return;
            Selection.activeObject = owner;
            ObjectFlagOwnerWindow.Open(owner, path);
        }

        #endregion
        #endregion
    }

    /// <summary>Retains scan results without holding scenes open.</summary>
    internal sealed class ObjectFlagScan
    {
        internal readonly List<ObjectFlagField> Fields = new List<ObjectFlagField>();
        internal readonly List<ObjectFlagLink> Links = new List<ObjectFlagLink>();
        internal readonly List<string> Errors = new List<string>();
        internal bool Cancelled;
        internal bool Complete => !Cancelled && Errors.Count == 0;
    }

    /// <summary>Projects a shared asset's fields beneath the prefab or preset that references it.</summary>
    internal sealed class ObjectFlagLink
    {
        internal readonly string Owner;
        internal readonly string Target;
        internal readonly string Root;
        internal readonly string Label;
        internal string TargetPath => Target.Substring(0, Target.IndexOf(':'));

        #region Methods
        #region Construction

        /// <summary>Captures a shared asset edge while both owners are loaded.</summary>
        /// <param name="owner">Source asset and component identity.</param>
        /// <param name="target">Referenced shared asset identity.</param>
        /// <param name="root">Source navigation root.</param>
        /// <param name="label">Reference field's named path.</param>
        internal ObjectFlagLink(string owner, string target, string root, string label)
        {
            Owner = owner;
            Target = target;
            Root = root;
            Label = label;
        }

        #endregion
        #endregion
    }
}
