using CatOnASkateboard.StudioIdentity;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Connects the selected prefab branch to its authored identity Inspector.</summary>
    internal static class ObjectIdentityControls
    {
        #region Methods

        #region Drawing

        /// <summary>Opens the flag selector or prepares an identity on a plain contact surface.</summary>
        /// <param name="target">Selected branch inside the open prefab workspace.</param>
        /// <param name="pending">Whether an interaction draft must be resolved before structural edits.</param>
        internal static void Draw(GameObject target, bool pending)
        {
            // Membership belongs to the object, independently of any interaction preset.
            ObjectIdentity identity = target.GetComponent<ObjectIdentity>();
            using (new EditorGUI.DisabledScope(pending && identity == null))
                if (GUILayout.Button(new GUIContent(identity != null ? "Edit Object Flags" : "Add Object Identity",
                    "Open this object's flags in the Inspector. Use its searchable selector to combine, create or import flags.")))
                {
                    if (identity == null)
                    {
                        identity = Undo.AddComponent<ObjectIdentity>(target);
                        ObjectAuthoringSave.Save(target);
                    }
                    Selection.activeObject = identity;
                    EditorGUIUtility.PingObject(identity);
                }
        }

        #endregion

        #endregion
    }
}
