using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Captures complete component settings and creates temporary typed documents for group editing.</summary>
    internal static class ObjectPresetCapture
    {
        #region Methods

        #region Components

        /// <summary>Copies every configurable field while excluding Unity ownership and generated connection IDs.</summary>
        /// <param name="component">Configured component.</param>
        /// <param name="root">Root used to describe portable local links.</param>
        /// <returns>Complete settings with exact serialized types.</returns>
        internal static ObjectComponentPreset Component(Component component, Transform root)
        {
            List<ObjectPresetValue> fields = new List<ObjectPresetValue>();
            using SerializedObject data = new SerializedObject(component);
            using SerializedProperty property = data.GetIterator();
            if (property.Next(true))
                do
                {
                    if (property.name.StartsWith("m_", StringComparison.Ordinal) && property.name != "m_Enabled"
                        || property.name is "completionBindings" or "sceneIdentity")
                        continue;
                    fields.Add(ObjectPresetValue.Capture(property, root));
                }
                while (property.Next(false));
            return new ObjectComponentPreset { TypeName = component.GetType().AssemblyQualifiedName, Fields = fields.ToArray() };
        }

        /// <summary>Creates a detached workspace document using the existing typed interaction snapshots.</summary>
        /// <param name="feature">Existing component used by the controls.</param>
        /// <returns>A temporary document owned by the caller.</returns>
        internal static ObjectWorkspace Document(ObjectInteraction feature)
        {
            ObjectWorkspace document = ScriptableObject.CreateInstance<ObjectWorkspace>();
            document.hideFlags = HideFlags.HideAndDontSave;
            document.Target.SetPreview(feature.gameObject);
            document.Navigation.SelectedId = ObjectWorkspaceTarget.FileId(feature);
            switch (feature)
            {
                case ObjectHover hover:
                    document.Category = ObjectInteractionCategory.Hover;
                    document.HasBinding = true;
                    document.Draft = ObjectWorkspace.Copy(hover.Configuration);
                    document.Baseline = ObjectWorkspace.Copy(document.Draft);
                    document.Binding = HoverBindingDraft.Capture(hover);
                    document.OriginalBinding = ObjectWorkspace.Copy(document.Binding);
                    document.Source = document.OriginalPreset = hover.Preset;
                    break;
                case ObjectSingleInteraction single:
                    document.Category = ObjectInteractionCategory.SingleInteraction;
                    document.Single.Kind = single.Kind;
                    document.Single.Read(feature.gameObject);
                    break;
                case ObjectExtendedInteraction extended:
                    document.Category = ObjectInteractionCategory.MultipleInteraction;
                    document.Extended.Select(extended);
                    break;
            }
            return document;
        }

        #endregion

        #region Preview

        /// <summary>Builds an inactive editor-only copy so validation and asset editing never change the source.</summary>
        /// <param name="source">Optional source object; null creates an empty preview root.</param>
        /// <returns>An inactive hidden root owned by the caller.</returns>
        internal static GameObject Preview(GameObject source)
        {
            GameObject staging = new GameObject("Object preset preview") { hideFlags = HideFlags.HideAndDontSave };
            staging.SetActive(false);
            if (source == null)
                return staging;
            GameObject preview = UnityEngine.Object.Instantiate(source, staging.transform, false);
            preview.name = source.name;
            preview.SetActive(false);
            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.transform.SetParent(null, false);
            UnityEngine.Object.DestroyImmediate(staging);
            ObjectPreviewIdentity.Register(preview);
            return preview;
        }

        #endregion

        #endregion
    }
}
