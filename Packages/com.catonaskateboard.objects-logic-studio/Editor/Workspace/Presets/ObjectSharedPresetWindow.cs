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

        #endregion

        #region State

        private string warning = string.Empty;

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
        }

        #endregion

        #region Drawing

        /// <summary>Shows existing flags and shared switches without exposing serialized preset storage.</summary>
        private void OnGUI()
        {
            if (preset == null || preview == null)
                return;
            EditorGUILayout.LabelField("Shared settings", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
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
                    data.ApplyModifiedProperties();
                }
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.LabelField(warning, EditorStyles.wordWrappedMiniLabel);
            using EditorGUILayout.HorizontalScope row = new EditorGUILayout.HorizontalScope();
            if (GUILayout.Button(new GUIContent("Save preset", "Save these shared settings.")))
            {
                if (EditorJsonUtility.ToJson(preset) != baseline)
                    warning = "This preset changed elsewhere. Discard to reload it.";
                else
                {
                    List<ObjectComponentPreset> components = new List<ObjectComponentPreset>();
                    foreach (ObjectComponentPreset component in preset.Components)
                        components.Add(ObjectPresetCapture.Component(preview.GetComponent(Type.GetType(component.TypeName, true)), preview.transform));
                    Undo.RecordObject(preset, "Save shared settings");
                    preset.Components = components.ToArray();
                    EditorUtility.SetDirty(preset);
                    AssetDatabase.SaveAssetIfDirty(preset);
                    baseline = EditorJsonUtility.ToJson(preset);
                    warning = "Shared preset saved.";
                }
            }
            if (GUILayout.Button(new GUIContent("Discard", "Reload the saved shared settings.")))
                Load();
        }

        #endregion

        #endregion
    }
}
