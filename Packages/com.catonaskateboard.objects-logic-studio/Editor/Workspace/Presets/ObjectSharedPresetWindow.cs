using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioColors.Editor;
using CatOnASkateboard.StudioIdentity.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Edits reusable flags and shared item switches on an isolated object.</summary>
    internal sealed class ObjectSharedPresetWindow : EditorWindow
    {
        #region Fields

        [Tooltip("Shared settings being edited.")]
        [SerializeField]
        private ObjectSharedPreset preset;
        [Tooltip("Temporary object used by this editor.")]
        [SerializeField]
        private GameObject preview;
        [Tooltip("Saved preset state when editing began.")]
        [SerializeField]
        private string baseline;
        [Tooltip("Preview values last loaded from the source preset.")]
        [SerializeField]
        private ObjectComponentPreset[] original;

        #endregion

        #region State

        private string warning = string.Empty;
        private Vector2 scroll;

        #endregion

        #region Methods

        #region Lifetime

        /// <summary>Opens shared settings independently of any selected gameplay prefab.</summary>
        /// <param name="preset">Reusable identity and item settings.</param>
        internal static void Open(ObjectSharedPreset preset)
        {
            ObjectSharedPresetWindow window = CreateInstance<ObjectSharedPresetWindow>();
            window.preset = preset;
            window.titleContent = new GUIContent(preset.name);
            window.minSize = new Vector2(360f, 220f);
            window.Load();
            window.Show();
        }

        /// <summary>Tracks edits and delayed flag menu selections for Unity's close confirmation.</summary>
        private void OnEnable()
        {
            StudioFieldMenu.Changed += FieldChanged;
            ObjectFlagSelector.SelectionChanged += FieldChanged;
            Undo.undoRedoPerformed += RefreshPending;
            if (preview != null)
                RefreshPending();
        }

        /// <summary>Disconnects the window without changing its working copy during a script reload.</summary>
        private void OnDisable()
        {
            StudioFieldMenu.Changed -= FieldChanged;
            ObjectFlagSelector.SelectionChanged -= FieldChanged;
            Undo.undoRedoPerformed -= RefreshPending;
        }

        /// <summary>Releases the editor-only object when the window closes.</summary>
        private void OnDestroy()
        {
            if (preview != null)
                DestroyImmediate(preview);
        }

        /// <summary>Rebuilds the isolated values from the saved preset.</summary>
        private void Load()
        {
            if (preview != null)
                DestroyImmediate(preview);
            preview = ObjectPresetCapture.Preview(null);
            foreach (ObjectComponentPreset component in preset.Components)
            {
                Type type = Type.GetType(component.TypeName, true);
                Component target = preview.GetComponent(type) ?? preview.AddComponent(type);
                using SerializedObject data = new SerializedObject(target);
                foreach (ObjectPresetValue field in component.Fields)
                    field.Apply(data.FindProperty(field.Name), preview.transform);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            baseline = EditorJsonUtility.ToJson(preset);
            original = Capture();
            RefreshPending();
        }

        #endregion

        #region Drawing

        /// <summary>Shows existing flags and shared switches without exposing serialized preset storage.</summary>
        private void OnGUI()
        {
            if (preset == null || preview == null)
                return;
            EditorGUILayout.LabelField("Shared settings", EditorStyles.boldLabel);
            using EditorGUI.DisabledScope locked = new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode);
            using (EditorGUILayout.ScrollViewScope view = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = view.scrollPosition;
                foreach (ObjectComponentPreset component in preset.Components)
                {
                    Type type = Type.GetType(component.TypeName, true);
                    using SerializedObject data = new SerializedObject(preview.GetComponent(type));
                    foreach (ObjectPresetValue field in component.Fields)
                    {
                        SerializedProperty property = data.FindProperty(field.Name);
                        if (property.name == "flags")
                            ObjectFlagSelector.Draw(property);
                        else
                            StudioFieldGUI.PropertyField(property, new GUIContent(property.name == "m_Enabled" ? "Enabled" : property.displayName, ObjectFieldText.Tooltip(property)), true);
                    }
                    if (data.ApplyModifiedProperties())
                        RefreshPending();
                }
            }
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
            {
                if (GUILayout.Button(new GUIContent("Apply", "Save these shared settings.")))
                    ApplyChanges();
                if (GUILayout.Button(new GUIContent("Discard", "Reload the saved shared settings.")))
                    DiscardChanges();
            }
        }

        #endregion

        #region Transactions

        /// <summary>Captures the displayed components using the same schema as the preset.</summary>
        /// <returns>Portable shared component settings.</returns>
        private ObjectComponentPreset[] Capture()
        {
            List<ObjectComponentPreset> components = new List<ObjectComponentPreset>();
            foreach (ObjectComponentPreset component in preset.Components)
                components.Add(ObjectPresetCapture.Component(preview.GetComponent(Type.GetType(component.TypeName, true)), preview.transform));
            return components.ToArray();
        }

        /// <summary>Updates the unsaved state after a delayed field menu edit.</summary>
        /// <param name="target">Object whose value changed.</param>
        private void FieldChanged(UnityEngine.Object target)
        {
            if (target is Component component && component.gameObject == preview)
                RefreshPending();
        }

        /// <summary>Compares the isolated preview with the last applied or loaded values.</summary>
        private void RefreshPending()
        {
            if (preview == null || preset == null)
                return;
            ObjectComponentPreset[] values = Capture();
            hasUnsavedChanges = original == null || values.Length != original.Length;
            for (int index = 0; !hasUnsavedChanges && index < values.Length; index++)
                hasUnsavedChanges = JsonUtility.ToJson(values[index]) != JsonUtility.ToJson(original[index]);
            saveChangesMessage = "Apply or discard the unsaved shared preset changes.";
            Repaint();
        }

        /// <summary>Saves the shared preview values and keeps them pending if validation fails.</summary>
        /// <returns>True when the preset was written.</returns>
        private bool ApplyChanges()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play Mode before applying the shared preset.");
                if (EditorJsonUtility.ToJson(preset) != baseline)
                    throw new InvalidOperationException("This preset changed elsewhere. Discard to reload it.");
                ObjectComponentPreset[] components = Capture();
                StudioAssetWrites.Save(preset, "Apply shared settings", () => preset.Components = components);
                baseline = EditorJsonUtility.ToJson(preset);
                original = components;
                warning = "Shared preset saved.";
                base.SaveChanges();
                Repaint();
                return true;
            }
            catch (Exception exception)
            {
                warning = exception.Message;
                Repaint();
                return false;
            }
        }

        /// <summary>Applies this preset from Unity's close prompt.</summary>
        public override void SaveChanges()
        {
            if (!ApplyChanges())
                throw new InvalidOperationException(warning);
        }

        /// <summary>Reloads the applied preset without writing the preview.</summary>
        public override void DiscardChanges()
        {
            Load();
            warning = string.Empty;
            base.DiscardChanges();
        }

        #endregion

        #endregion
    }
}
