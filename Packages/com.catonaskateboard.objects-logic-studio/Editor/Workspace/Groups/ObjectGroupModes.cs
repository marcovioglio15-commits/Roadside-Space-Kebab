using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Finds groups belonging to alternate modes on a detached document for search and preset capture.</summary>
    internal static class ObjectGroupModes
    {
        #region Methods

        #region Discovery

        /// <summary>Adds mode-specific groups without switching the visible interaction or its proposal.</summary>
        /// <param name="state">Current interaction draft.</param>
        /// <param name="groups">Groups already discovered in its current mode.</param>
        internal static void Collect(ObjectWorkspace state, List<ObjectGroupInfo> groups)
        {
            if (ObjectWorkspaceDrafts.Selected(state) is not (ObjectEject or ObjectAmbient or ObjectSpawnManager or ObjectGravityGenerator or ObjectDialogue or ObjectDispenser))
                return;
            ObjectWorkspace preview = ScriptableObject.CreateInstance<ObjectWorkspace>();
            try
            {
                ObjectWorkspaceDraft.Capture(state).Restore(preview);
                switch (ObjectWorkspaceDrafts.Selected(state))
                {
                    case ObjectEject:
                        preview.Single.Draft.Eject.Behavior = state.Single.Draft.Eject.Behavior == EjectBehavior.Impulse ? EjectBehavior.BlackHole : EjectBehavior.Impulse;
                        break;
                    case ObjectAmbient:
                        preview.Extended.Draft.Ambient.Mode = state.Extended.Draft.Ambient.Mode == AmbientMode.Ambient ? AmbientMode.Radio : AmbientMode.Ambient;
                        break;
                    case ObjectSpawnManager:
                        preview.Extended.Draft.SpawnManagement.Mode = state.Extended.Draft.SpawnManagement.Mode == SpawnManagementMode.DayFlow ? SpawnManagementMode.CompletionRules : SpawnManagementMode.DayFlow;
                        break;
                    case ObjectGravityGenerator:
                        preview.Extended.Draft.GravityGenerator.AffectPlayer = true;
                        preview.Extended.Draft.GravityGenerator.AffectObjects = true;
                        break;
                    case ObjectDialogue:
                        preview.Extended.Draft.Dialogue.Trigger = DialogueTrigger.InputAction;
                        break;
                    case ObjectDispenser:
                        preview.Single.Draft.Dispenser.Unlimited = false;
                        break;
                }
                using SerializedObject data = new SerializedObject(preview);
                List<ObjectGroupInfo> additional = new List<ObjectGroupInfo>();
                using (ObjectGroupScope scope = new ObjectGroupScope(additional))
                    ObjectInteractionPanel.Draw(preview, data);
                foreach (ObjectGroupInfo group in additional)
                    if (!groups.Exists(existing => existing.Parent == group.Parent && existing.Section == group.Section && existing.Title == group.Title))
                        groups.Add(group);
            }
            finally
            {
                Object.DestroyImmediate(preview);
            }
        }

        #endregion

        #endregion
    }
}
