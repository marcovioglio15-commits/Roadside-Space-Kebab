using System.Collections.Generic;
using UnityEditor;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Retains small mode dependencies so standalone group editors show meaningful conditional controls.</summary>
    internal static class ObjectPresetContext
    {
        #region Methods

        #region Dependencies

        /// <summary>Captures scalar mode choices from the active feature's settings.</summary>
        /// <param name="state">Workspace or group document.</param>
        /// <returns>Preview-only dependency values with absolute draft paths.</returns>
        internal static ObjectPresetValue[] Capture(ObjectWorkspace state)
        {
            string path = state.Category == ObjectInteractionCategory.SingleInteraction ? "Single.Draft." + (state.Single.Kind switch
            {
                SingleInteractionKind.Drop => "Release",
                _ => state.Single.Kind.ToString()
            }) : state.Category == ObjectInteractionCategory.Hover ? "Draft" : "Extended.Draft." + (state.Extended.Kind switch
            {
                ExtendedInteractionKind.ModifyByContact => "Contact",
                ExtendedInteractionKind.PlayAmbient => "Ambient",
                ExtendedInteractionKind.AvailableOrders => "Orders",
                ExtendedInteractionKind.Unlock => "Unlock.Settings",
                ExtendedInteractionKind.AssemblyProduct => "AssemblyProduct.Settings",
                _ => state.Extended.Kind.ToString()
            });
            using SerializedObject data = new SerializedObject(state);
            List<ObjectPresetValue> values = new List<ObjectPresetValue>();
            Capture(data.FindProperty(path), values);
            // Throw combines launch settings with the same release body controls used by Drop.
            if (state.Category == ObjectInteractionCategory.SingleInteraction && state.Single.Kind == SingleInteractionKind.Throw)
                Capture(data.FindProperty("Single.Draft.Release"), values);
            return values.ToArray();
        }

        /// <summary>Collects scalar dependencies without duplicating arrays or external object links.</summary>
        /// <param name="settings">One active feature settings block.</param>
        /// <param name="values">List receiving absolute field paths and values.</param>
        private static void Capture(SerializedProperty settings, List<ObjectPresetValue> values)
        {
            if (settings == null)
                return;
            using SerializedProperty field = settings.Copy();
            using SerializedProperty end = settings.GetEndProperty();
            bool enter = true;
            while (field.Next(enter) && field.depth > settings.depth && !SerializedProperty.EqualContents(field, end))
            {
                enter = !field.isArray || field.propertyType == SerializedPropertyType.String;
                if (field.propertyType is not (SerializedPropertyType.Boolean or SerializedPropertyType.Enum or SerializedPropertyType.Integer or SerializedPropertyType.Float))
                    continue;
                ObjectPresetValue value = ObjectPresetValue.Capture(field, null);
                value.Name = field.propertyPath;
                values.Add(value);
            }
        }

        /// <summary>Restores dependency choices only in an isolated preset preview.</summary>
        /// <param name="preset">Standalone group preset.</param>
        /// <param name="document">Temporary group document.</param>
        internal static void Apply(ObjectGroupPreset preset, ObjectWorkspace document)
        {
            if (preset.Context == null)
                return;
            Apply(preset.Context.Values, document);
        }

        /// <summary>Refreshes preview dependencies after another group changes the interaction's mode.</summary>
        /// <param name="values">Current scalar mode choices.</param>
        /// <param name="document">Detached group document receiving those choices.</param>
        internal static void Apply(ObjectPresetValue[] values, ObjectWorkspace document)
        {
            using SerializedObject data = new SerializedObject(document);
            foreach (ObjectPresetValue value in values)
            {
                SerializedProperty property = data.FindProperty(value.Name);
                if (property != null)
                    value.Apply(property, null);
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Creates preview dependencies as one subasset instead of repeating them in every group.</summary>
        /// <param name="state">Document supplying mode choices.</param>
        /// <param name="owner">Saved asset containing the new dependencies.</param>
        /// <returns>A shared preview context owned by the preset asset.</returns>
        internal static ObjectGroupContext Create(ObjectWorkspace state, UnityEngine.Object owner)
        {
            ObjectGroupContext context = UnityEngine.ScriptableObject.CreateInstance<ObjectGroupContext>();
            context.name = owner.name + " · Preview settings";
            context.hideFlags = UnityEngine.HideFlags.HideInHierarchy;
            context.Values = Capture(state);
            AssetDatabase.AddObjectToAsset(context, owner);
            EditorUtility.SetDirty(context);
            return context;
        }

        /// <summary>Updates a group's preview context without changing dependencies shared by other presets.</summary>
        /// <param name="preset">Group being explicitly updated.</param>
        /// <param name="state">Document supplying the latest mode choices.</param>
        internal static void Update(ObjectGroupPreset preset, ObjectWorkspace state)
        {
            string path = AssetDatabase.GetAssetPath(preset);
            bool shared = preset.Context == null || AssetDatabase.GetAssetPath(preset.Context) != path;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is ObjectGroupPreset other && other != preset && other.Context == preset.Context)
                    shared = true;
            if (shared)
                preset.Context = Create(state, preset);
            else
            {
                Undo.RecordObject(preset.Context, "Update group preview");
                preset.Context.Values = Capture(state);
                EditorUtility.SetDirty(preset.Context);
            }
            AssetDatabase.SaveAssets();
        }

        #endregion

        #endregion
    }
}
