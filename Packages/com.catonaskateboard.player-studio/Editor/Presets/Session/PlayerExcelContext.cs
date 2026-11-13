using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Adapts retained player module sessions to contextual preset actions.</summary>
    internal static class PlayerExcelContext
    {
        #region Methods
        #region Context

        /// <summary>Finds the asset currently opened by a module, independently of proposed slot changes.</summary>
        /// <param name="state">Player proposals.</param>
        /// <param name="module">Master is -1; modules use the existing tab order.</param>
        /// <returns>The exact preset retaining this proposal.</returns>
        internal static ScriptableObject Source(PlayerStudioState state, int module)
        {
            return module switch
            {
                -1 => state.Selection.MasterSession.Source,
                0 => state.Body.Source,
                1 => state.Locomotion.Source,
                _ => Module(state, module).Source
            };
        }

        /// <summary>Connects one exact player proposal without bypassing its shared Apply/Discard footer.</summary>
        /// <param name="state">Current player proposals.</param>
        /// <param name="owner">Window retaining serialized session values and Undo.</param>
        /// <param name="module">Master or module index.</param>
        /// <returns>A context that includes current unsaved values.</returns>
        internal static StudioPresetContext Create(PlayerStudioState state, PlayerStudioWindow owner, int module)
        {
            ScriptableObject source = Source(state, module);
            return new StudioPresetContext(source, owner, () => Capture(state, owner, module), proposal =>
            {
                using SerializedObject incoming = new SerializedObject(proposal);
                switch (module)
                {
                    case -1:
                        PlayerMasterPreset master = (PlayerMasterPreset)proposal;
                        state.Selection.MasterSession.SetDraft(master.BodyPreset, master.InputPreset, master.LocomotionPreset, master.CameraPreset, master.ToolsPreset);
                        break;
                    case 0:
                        state.Body.SetDraft(incoming.FindProperty("radius").floatValue, incoming.FindProperty("height").floatValue);
                        break;
                    case 1:
                        state.Locomotion.ReadDraft(incoming);
                        break;
                    default:
                        PlayerModuleEditSession session = Module(state, module);
                        SerializedObject working = session.GetEditor(owner);
                        PlayerPresetDraftCopy.Apply(incoming, (ScriptableObject)working.targetObject);
                        working.Update();
                        session.Capture();
                        break;
                }
                owner.HandleDraftChanged();
            }, () => owner != null && source != null && Source(state, module) == source);
        }

        /// <summary>Copies pending values into the corresponding typed preset.</summary>
        /// <param name="state">Current player proposals.</param>
        /// <param name="owner">Window owning cached module editors.</param>
        /// <param name="module">Master or module index.</param>
        /// <returns>An isolated typed snapshot.</returns>
        private static Object Capture(PlayerStudioState state, PlayerStudioWindow owner, int module)
        {
            ScriptableObject copy = (ScriptableObject)StudioPresetContext.Clone(Source(state, module));
            using SerializedObject values = new SerializedObject(copy);
            switch (module)
            {
                case -1:
                    state.Selection.MasterSession.WriteDraft(values);
                    break;
                case 0:
                    state.Body.WriteDraft(values);
                    break;
                case 1:
                    state.Locomotion.WriteDraft(values);
                    break;
                default:
                    PlayerPresetDraftCopy.Apply(Module(state, module).GetEditor(owner), copy);
                    return copy;
            }
            values.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        /// <summary>Shares the existing cached module sessions for Input, Camera and Tools.</summary>
        /// <param name="state">Current player proposals.</param>
        /// <param name="module">Existing module index.</param>
        /// <returns>The retained module session.</returns>
        private static PlayerModuleEditSession Module(PlayerStudioState state, int module)
        {
            return module switch { 2 => state.Input, 3 => state.Camera, _ => state.Tools };
        }

        #endregion
        #endregion
    }
}
