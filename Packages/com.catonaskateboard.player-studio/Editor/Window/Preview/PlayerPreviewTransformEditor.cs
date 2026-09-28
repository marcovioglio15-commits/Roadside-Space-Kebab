using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.PlayerStudio.Editor
{
    /// <summary>Owns the collapsible preview transform panel and routes manipulation to the chosen player part.</summary>
    [Serializable]
    internal sealed class PlayerPreviewTransformEditor
    {
        #region Serialized Fields

        [Header("Preview Editing")]
        [Tooltip("Show numeric transforms above the preview; closing the panel retains every draft.")]
        [SerializeField]
        private bool expanded;

        [Tooltip("Player transform controls and their retained native handle preferences.")]
        [SerializeField]
        private PlayerTransformDraftView playerView = new PlayerTransformDraftView();

        #endregion

        #region Properties

        /// <summary>Visibility affects presentation only, never whether the session has changes.</summary>
        public bool Expanded => expanded;

        #endregion

        #region Methods

        #region Panel

        /// <summary>Toggles the complete panel from the preview toolbar without discarding values.</summary>
        public void Toggle()
        {
            // The retained root proposal is independent of panel visibility.
            expanded = !expanded;
        }



        /// <summary>Draws the proposed player pose without modifying its hierarchy.</summary>
        /// <param name="root">Player transform proposal.</param>
        /// <param name="owner">Window owning draft Undo.</param>
        /// <returns>True when the root proposal changed.</returns>
        public bool Draw(PlayerTransformEditSession root, UnityEngine.Object owner)
        {
            // A closed panel leaves the retained proposal untouched.
            return expanded && root.Source != null && playerView.Draw(root, owner);
        }

        #endregion

        #region Handles and Picking

        /// <summary>Edits the root through native preview handles.</summary>
        /// <param name="root">Retained root pose proposal.</param>
        /// <param name="owner">Window owning draft Undo.</param>
        /// <returns>True when a handle changed the pose.</returns>
        public bool DrawHandles(PlayerTransformEditSession root, UnityEngine.Object owner)
        {
            // Camera navigation keeps priority over manipulation.
            return expanded && !Tools.viewToolActive && !EditorApplication.isPlayingOrWillChangePlaymode
                && playerView.DrawHandles(root, owner);
        }

        /// <summary>Selects the clicked player part only when no handle or navigation gesture owns the mouse.</summary>
        /// <param name="host">Player currently opened in the tool.</param>
        /// <param name="root">Player pose used by the preview.</param>
        /// <returns>True when a click opened the matching transform controls.</returns>
        public bool Pick(PlayerHost host, PlayerTransformEditSession root)
        {
            // The passive default control yields to all visible native handles.
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (host == null || Tools.viewToolActive || Event.current.alt || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            if (Event.current.type == EventType.Layout)
                HandleUtility.AddDefaultControl(control);
            if (Event.current.type != EventType.MouseDown || Event.current.button != 0
                || GUIUtility.hotControl != 0 || HandleUtility.nearestControl != control)
                return false;
            GameObject picked = HandleUtility.PickGameObject(Event.current.mousePosition, false);
            bool pickedPlayer = picked != null && picked.GetComponentInParent<PlayerHost>() == host;
            if (!pickedPlayer)
            {
                // The player often has no renderer: its body outline still provides a selectable region.
                if (picked != null || !root.TryValidateNumbers(out _) || !host.TryGetBodySettings(out PlayerBodySettings body, out _))
                    return false;
                Ray worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                Matrix4x4 inverse = root.WorldMatrix.inverse;
                if (!new Bounds(body.Center, new Vector3(body.Radius * 2f, body.Height, body.Radius * 2f))
                    .IntersectRay(new Ray(inverse.MultiplyPoint3x4(worldRay.origin), inverse.MultiplyVector(worldRay.direction))))
                    return false;
            }
            expanded = true;
            Event.current.Use();
            return true;
        }

        #endregion

        #endregion
    }
}
