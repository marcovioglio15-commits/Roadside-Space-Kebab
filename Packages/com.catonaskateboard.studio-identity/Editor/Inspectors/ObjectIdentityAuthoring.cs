using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.StudioIdentity.Editor
{
    /// <summary>Authors persistent object membership with optional Undo at explicit setup boundaries.</summary>
    public static class ObjectIdentityAuthoring
    {
        #region Methods

        #region Assignment

        /// <summary>Adds one flag while preserving existing membership on a scene or loaded prefab object.</summary>
        /// <param name="target">Object receiving the identity.</param>
        /// <param name="flag">Existing catalog definition to include.</param>
        /// <param name="undo">Record Undo for live authoring; disable for isolated prefab contents.</param>
        public static void EnsureFlag(GameObject target, ObjectFlag flag, bool undo = true)
        {
            // Only editor setup may create the component; gameplay uses authored dependencies.
            if (target == null || flag == null)
                return;
            ObjectIdentity identity = target.GetComponent<ObjectIdentity>();
            if (identity == null)
                identity = undo ? Undo.AddComponent<ObjectIdentity>(target) : target.AddComponent<ObjectIdentity>();
            if (identity.Has(flag))
                return;
            using SerializedObject data = new SerializedObject(identity);
            SerializedProperty flags = data.FindProperty("flags");
            flags.arraySize++;
            flags.GetArrayElementAtIndex(flags.arraySize - 1).objectReferenceValue = flag;
            if (undo)
                data.ApplyModifiedProperties();
            else
                data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(identity);
        }

        #endregion

        #region Discovery

        /// <summary>Resolves a unique active scene identity when editing observer connections.</summary>
        /// <param name="flag">Required player flag.</param>
        /// <returns>The single matching identity, or null for missing or ambiguous membership.</returns>
        public static ObjectIdentity FindUnique(ObjectFlag flag)
        {
            // Explicit setup refreshes may inspect the scene; runtime uses its own active registry.
            ObjectIdentity found = null;
            if (flag == null)
                return null;
            foreach (ObjectIdentity identity in Object.FindObjectsByType<ObjectIdentity>())
                if (identity.isActiveAndEnabled && !EditorSceneManager.IsPreviewScene(identity.gameObject.scene) && identity.Has(flag))
                {
                    if (found != null)
                        return null;
                    found = identity;
                }
            return found;
        }

        /// <summary>Captures stable authored membership for editor conflict detection.</summary>
        /// <param name="target">Object whose defaults are being compared.</param>
        /// <returns>Stable asset GUIDs, including unresolved entries, or an empty snapshot.</returns>
        public static string Capture(GameObject target)
        {
            // GUIDs survive renaming, domain reload and prefab-stage reopening.
            if (target == null || !target.TryGetComponent(out ObjectIdentity identity))
                return string.Empty;
            StringBuilder snapshot = new StringBuilder();
            foreach (ObjectFlag flag in identity.AuthoredFlags)
                snapshot.Append(flag != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(flag)) : "missing").Append(';');
            return snapshot.ToString();
        }

        #endregion

        #endregion
    }
}
