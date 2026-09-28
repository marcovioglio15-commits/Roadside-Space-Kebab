using System;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Performs explicit structural edits on open prefab objects and saves them with Undo.</summary>
    internal static class ExtendedInteractionAuthoring
    {
        #region Methods

        #region Structure

        /// <summary>Adds shared item state so a prefab can participate in contact and retain consumption receipts.</summary>
        /// <param name="target">Selected editable prefab branch.</param>
        /// <returns>The existing or newly added item component.</returns>
        internal static ObjectItem Prepare(GameObject target)
        {
            // A passive counterpart needs item state even when it owns no interaction component.
            Validate(target);
            ObjectItem item = target.GetComponent<ObjectItem>();
            if (item == null)
                item = Undo.AddComponent<ObjectItem>(target);
            ObjectAuthoringSave.Save(target);
            return item;
        }

        /// <summary>Adds a separate contact or dialogue card without replacing existing interactions.</summary>
        /// <param name="target">Open prefab branch receiving the component.</param>
        /// <param name="kind">Requested interaction category feature.</param>
        /// <returns>The saved component with its required item state and optional authored HUD.</returns>
        internal static ObjectExtendedInteraction Add(GameObject target, ExtendedInteractionKind kind)
        {
            // Group dependencies and presentation into the same reversible structural operation.
            Validate(target);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add object interaction");
            if (target.GetComponent<ObjectItem>() == null)
                Undo.AddComponent<ObjectItem>(target);
            ObjectExtendedInteraction feature = kind switch
            {
                ExtendedInteractionKind.Slice => Undo.AddComponent<ObjectSlice>(target),
                ExtendedInteractionKind.SpawnManagement => Undo.AddComponent<ObjectSpawnManager>(target),
                ExtendedInteractionKind.AssemblyStation => Undo.AddComponent<ObjectAssemblyStation>(target),
                ExtendedInteractionKind.AssemblyProduct => Undo.AddComponent<ObjectAssemblyProduct>(target),
                ExtendedInteractionKind.ModifyByContact => Undo.AddComponent<ObjectContactModifier>(target),
                ExtendedInteractionKind.Dialogue => Undo.AddComponent<ObjectDialogue>(target),
                ExtendedInteractionKind.Outline => Undo.AddComponent<ObjectOutline>(target),
                ExtendedInteractionKind.Unlock => Undo.AddComponent<ObjectInteractionUnlock>(target),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            using (SerializedObject data = new SerializedObject(feature))
            {
                data.FindProperty("interactionName").stringValue = kind == ExtendedInteractionKind.Unlock
                    ? "Availability Rule" : ObjectNames.NicifyVariableName(kind.ToString());
                data.ApplyModifiedProperties();
            }
            if (feature is ObjectOutline outline)
                OutlineAuthoring.Rebuild(outline);
            if (feature is ObjectAssemblyStation station)
                AssemblyAuthoring.Prepare(station);
            ObjectAuthoringSave.Save(target);
            Undo.CollapseUndoOperations(group);
            return feature;
        }

        /// <summary>Removes only the requested feature, preserving shared state and manually editable HUD content.</summary>
        /// <param name="feature">Component represented by the selected card.</param>
        internal static void Remove(ObjectExtendedInteraction feature)
        {
            // A shared HUD may still belong to another dialogue and remains available for later reuse.
            Validate(feature.gameObject);
            GameObject target = feature.gameObject;
            Undo.DestroyObjectImmediate(feature);
            ObjectAuthoringSave.Save(target);
        }

        /// <summary>Rejects structural edits outside a writable native prefab workspace.</summary>
        /// <param name="target">Object requested by an authoring action.</param>
        private static void Validate(GameObject target)
        {
            // Runtime instances and unopened source assets never become edit targets.
            if (!ObjectAuthoringSave.TryValidate(target, out string warning) || EditorUtility.IsPersistent(target)
                || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(warning.Length > 0 ? warning : "Open the prefab workspace in Edit mode first.");
        }

        #endregion

        #endregion
    }
}
