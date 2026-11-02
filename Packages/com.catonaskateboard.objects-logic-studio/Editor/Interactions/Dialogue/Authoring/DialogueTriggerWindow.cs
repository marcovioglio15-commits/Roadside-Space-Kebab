using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Owns independent trigger edits with optional destination order context.</summary>
    public sealed class DialogueTriggerWindow : StudioAssetWindow
    {
        #region Serialized Fields

        [Header("Context")]
        [Tooltip("Optional destination order settings used only to populate the trigger menus.")]
        [SerializeReference]
        private OrderSettings orders;

        #endregion

        #region Properties

        protected override Type AssetType => typeof(DialogueTriggerPreset);

        #endregion

        #region Methods

        #region Window

        /// <summary>Opens a trigger without applying the containing tree's pending changes.</summary>
        /// <param name="trigger">Shared trigger preset.</param>
        /// <param name="context">Optional destination order configuration.</param>
        public static void Open(DialogueTriggerPreset trigger, OrderSettings context = null)
        {
            DialogueTriggerWindow window = GetWindow<DialogueTriggerWindow>("Dialogue Trigger");
            if (!window.hasUnsavedChanges)
                window.orders = context != null ? ObjectWorkspace.Copy(context) : null;
            window.Select(trigger);
        }

        /// <summary>Supplies order choices to the detached preset inspector.</summary>
        /// <param name="editor">Cached editor for the detached trigger.</param>
        protected override void DrawInspector(UnityEditor.Editor editor)
        {
            ((DialogueTriggerEditor)editor).Draw(orders);
        }

        /// <summary>Checks the selected event branch before saving.</summary>
        /// <param name="proposal">Detached trigger.</param>
        /// <param name="issue">Receives missing condition data.</param>
        /// <returns>True when the trigger is complete.</returns>
        protected override bool Validate(ScriptableObject proposal, out string issue)
        {
            return ((DialogueTriggerPreset)proposal).TryValidate(orders, orders != null, out issue);
        }

        #endregion

        #endregion
    }

    /// <summary>Shows only the event settings used by this trigger.</summary>
    [CustomEditor(typeof(DialogueTriggerPreset))]
    internal sealed class DialogueTriggerEditor : UnityEditor.Editor
    {
        #region Methods

        #region Drawing

        /// <summary>Routes saved presets into independent editing.</summary>
        public override void OnInspectorGUI()
        {
            if (EditorUtility.IsPersistent(target))
            {
                if (StudioButton.Draw(new GUIContent("Open Trigger", "Edit this shared trigger in an independent Apply/Discard session.")))
                    DialogueTriggerWindow.Open((DialogueTriggerPreset)target);
                return;
            }
            Draw(null);
        }

        /// <summary>Edits only fields meaningful for the selected event.</summary>
        /// <param name="orders">Optional order menu context.</param>
        internal void Draw(OrderSettings orders)
        {
            serializedObject.Update();
            StudioFieldGUI.PropertyField(serializedObject.FindProperty("Type"));
            DialogueTrigger type = (DialogueTrigger)serializedObject.FindProperty("Type").enumValueIndex;
            if (type == DialogueTrigger.InputAction)
                StudioFieldGUI.PropertyField(serializedObject.FindProperty("Action"));
            if (type == DialogueTrigger.Consumption)
            {
                DialogueConsumptionControls.Draw(serializedObject.FindProperty("Consumption"), "Filter Consumption", orders?.Catalog, orders);
                DialogueConsumptionControls.Draw(serializedObject.FindProperty("AdditionalConsumption"), "Additional Condition", orders?.Catalog, orders);
            }
            if (type is DialogueTrigger.Proximity or DialogueTrigger.InputAction)
            {
                StudioFieldGUI.PropertyField(serializedObject.FindProperty("OverrideRange"));
                if (serializedObject.FindProperty("OverrideRange").boolValue)
                {
                    StudioFieldGUI.PropertyField(serializedObject.FindProperty("Distance"));
                    StudioFieldGUI.PropertyField(serializedObject.FindProperty("ExitDistance"));
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #endregion
    }
}
