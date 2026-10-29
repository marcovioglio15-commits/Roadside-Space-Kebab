using CatOnASkateboard.StudioInput.Editor;
using UnityEditor;
using UnityEngine.InputSystem;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps conditional action bindings in one small group for every extended interaction.</summary>
    internal static class ObjectInteractionBindings
    {
        #region Methods

        #region Input

        /// <summary>Shows only action roles used by the current activation mode.</summary>
        /// <param name="data">Serialized workspace or group draft.</param>
        /// <param name="state">Workspace supplying the selected interaction.</param>
        internal static void Draw(SerializedObject data, ObjectWorkspace state)
        {
            bool start = state.Extended.Kind switch
            {
                ExtendedInteractionKind.Dialogue => state.Extended.Draft.Dialogue.Trigger == DialogueTrigger.InputAction,
                ExtendedInteractionKind.SpraySauce or ExtendedInteractionKind.Slice => true,
                ExtendedInteractionKind.GravityGenerator => state.Extended.Draft.GravityGenerator.Restore != GravityRestoreMode.Timer,
                ExtendedInteractionKind.PlayAmbient => state.Extended.Draft.Ambient.Mode == AmbientMode.Radio,
                ExtendedInteractionKind.AssemblyStation => state.Extended.Draft.AssemblyStation.Trigger == AssemblyStationTrigger.InputAction,
                ExtendedInteractionKind.ModifyByContact => state.Extended.Draft.Contact.RequiresInput,
                _ => false
            };
            if (start)
                StudioInputActionMenu.Draw(data, "Extended.Draft.StartAction", "ObjectsLogicStudio.Start", Button);
            if (state.Extended.Kind == ExtendedInteractionKind.Dialogue)
                StudioInputActionMenu.Draw(data, "Extended.Draft.AdvanceAction", "ObjectsLogicStudio.Advance", Button);
        }

        /// <summary>Offers button actions without enabling their action maps.</summary>
        /// <param name="action">Action offered by the picker.</param>
        /// <returns>True for a button action.</returns>
        private static bool Button(InputAction action)
        {
            return action.type == InputActionType.Button;
        }

        #endregion

        #endregion
    }
}
