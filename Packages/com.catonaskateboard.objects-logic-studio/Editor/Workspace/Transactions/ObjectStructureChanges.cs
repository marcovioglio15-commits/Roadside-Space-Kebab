using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Keeps Add and Remove reversible and checks references before saving structural edits.</summary>
    internal static class ObjectStructureChanges
    {
        #region Methods

        #region Staging

        /// <summary>Stages an interaction addition without writing the prefab.</summary>
        /// <param name="state">Workspace receiving the change.</param>
        /// <param name="target">Existing prefab branch.</param>
        /// <param name="type">Registered component type.</param>
        internal static void Add(ObjectWorkspace state, GameObject target, Type type)
        {
            ObjectWorkspaceTarget route = new ObjectWorkspaceTarget();
            route.Capture(target, 0);
            state.Structure.Add(new ObjectStructureChange { Target = route, TypeName = type.FullName, Add = true });
            state.Persist();
        }

        /// <summary>Stages removal of the exact selected component.</summary>
        /// <param name="state">Workspace receiving the change.</param>
        /// <param name="feature">Existing component to remove.</param>
        internal static void Remove(ObjectWorkspace state, ObjectInteraction feature)
        {
            ObjectWorkspaceTarget route = new ObjectWorkspaceTarget();
            route.Capture(feature.gameObject, 0);
            long id = ObjectWorkspaceTarget.FileId(feature);
            if (!state.Structure.Exists(change => !change.Add && change.ComponentId == id && change.Target.PrefabGuid == route.PrefabGuid))
                state.Structure.Add(new ObjectStructureChange { Target = route, TypeName = feature.GetType().FullName, ComponentId = id });
            state.Persist();
        }

        /// <summary>Counts staged operations for the visible prefab.</summary>
        /// <param name="state">Workspace containing staged edits.</param>
        /// <returns>The selected prefab's operation count.</returns>
        internal static int Count(ObjectWorkspace state)
        {
            return state.Structure.FindAll(change => change.Target.PrefabGuid == state.Target.PrefabGuid).Count;
        }

        #endregion

        #region Validation

        /// <summary>Checks missing objects, required Grab components and references to components being removed.</summary>
        /// <param name="state">Workspace containing the planned operations.</param>
        /// <param name="warning">Receives the first problem.</param>
        /// <returns>True when every structural operation is safe to apply.</returns>
        internal static bool Validate(ObjectWorkspace state, out string warning)
        {
            warning = string.Empty;
            foreach (ObjectStructureChange change in state.Structure)
            {
                if (change.Target.PrefabGuid != state.Target.PrefabGuid)
                    continue;
                GameObject target = change.Target.Resolve();
                Type type = ResolveType(change.TypeName);
                if (target == null || type == null || !ObjectAuthoringSave.TryValidate(target, out warning))
                {
                    warning = "The object or interaction is no longer available.";
                    return false;
                }
                if (change.Add)
                {
                    if (type.IsDefined(typeof(DisallowMultipleComponent), true) && target.GetComponent(type) != null)
                    {
                        warning = "This object already has " + ObjectInteractionCatalog.Find(type).Name + ".";
                        return false;
                    }
                    if ((type == typeof(ObjectDrop) || type == typeof(ObjectThrow)) && target.GetComponent<ObjectGrab>() == null
                        && !state.Structure.Exists(other => other.Add && other.Target.ObjectPath == change.Target.ObjectPath
                            && other.Target.PrefabGuid == change.Target.PrefabGuid && other.TypeName == typeof(ObjectGrab).FullName))
                    {
                        warning = "Add Grab before Drop or Throw.";
                        return false;
                    }
                    continue;
                }
                ObjectInteraction removed = Find(change);
                if (removed == null)
                {
                    warning = "The interaction to remove is no longer available.";
                    return false;
                }
                foreach (ObjectInteraction feature in target.transform.root.GetComponentsInChildren<ObjectInteraction>(true))
                {
                    if (feature == removed || state.Structure.Exists(other => !other.Add && other.ComponentId == ObjectWorkspaceTarget.FileId(feature)))
                        continue;
                    using SerializedObject data = new SerializedObject(feature);
                    using SerializedProperty property = data.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == removed)
                        {
                            warning = feature.InteractionName + " still uses this interaction. Change that link first.";
                            return false;
                        }
                    if (removed is ObjectGrab && feature.gameObject == target && feature is ObjectRelease)
                    {
                        warning = "Remove Drop and Throw before Grab.";
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Resolves only registered interaction types.</summary>
        /// <param name="name">Saved full type name.</param>
        /// <returns>The registered component type or null.</returns>
        internal static Type ResolveType(string name)
        {
            foreach (ObjectInteractionCatalog.Entry entry in ObjectInteractionCatalog.Entries)
                if (entry.Type.FullName == name)
                    return entry.Type;
            return null;
        }

        /// <summary>Finds the exact existing component represented by a removal.</summary>
        /// <param name="change">Saved object and component identity.</param>
        /// <returns>The component or null.</returns>
        private static ObjectInteraction Find(ObjectStructureChange change)
        {
            GameObject target = change.Target.Resolve();
            if (target != null)
                foreach (ObjectInteraction feature in target.GetComponents<ObjectInteraction>())
                    if (ObjectWorkspaceTarget.FileId(feature) == change.ComponentId)
                        return feature;
            return null;
        }

        #endregion

        #region Applying

        /// <summary>Applies validated operations inside the caller's Undo group.</summary>
        /// <param name="state">Workspace containing the selected prefab's operations.</param>
        /// <returns>The last added interaction, or null when only removals were applied.</returns>
        internal static ObjectInteraction Apply(ObjectWorkspace state)
        {
            ObjectInteraction created = null;
            List<ObjectStructureChange> changes = state.Structure.FindAll(change => change.Target.PrefabGuid == state.Target.PrefabGuid);
            // Add Grab first so newly added release components see their required owner.
            changes.Sort((left, right) => Priority(left).CompareTo(Priority(right)));
            foreach (ObjectStructureChange change in changes)
            {
                GameObject target = change.Target.Resolve();
                if (!change.Add)
                {
                    Undo.DestroyObjectImmediate(Find(change));
                    continue;
                }
                ObjectInteraction feature = (ObjectInteraction)Undo.AddComponent(target, ResolveType(change.TypeName));
                created = feature;
                feature.enabled = false;
                using SerializedObject data = new SerializedObject(feature);
                data.FindProperty("interactionName").stringValue = ObjectInteractionCatalog.Name(feature);
                data.ApplyModifiedProperties();
                Prepare(feature);
            }
            if (changes.Count > 0)
                ObjectAuthoringSave.Save(state.Target.Resolve());
            return created;
        }

        /// <summary>Adds Grab before release actions and removes it after dependent actions.</summary>
        /// <param name="change">Staged component operation.</param>
        /// <returns>The dependency order used by the transaction.</returns>
        private static int Priority(ObjectStructureChange change)
        {
            return change.TypeName == typeof(ObjectGrab).FullName ? change.Add ? 0 : 3 : change.Add ? 1 : 2;
        }

        /// <summary>Prepares existing visual and staging dependencies in Edit mode.</summary>
        /// <param name="feature">New or imported component.</param>
        internal static void Prepare(ObjectInteraction feature)
        {
            switch (feature)
            {
                case ObjectOutline outline:
                    OutlineAuthoring.Rebuild(outline);
                    break;
                case ObjectElasticDeformation or ObjectSpraySauce:
                    ElasticAuthoring.Prepare(feature.gameObject);
                    break;
                case ObjectAssemblyStation station:
                    AssemblyAuthoring.Prepare(station);
                    break;
                case ObjectSpawnManager manager:
                    if (manager.Settings.Mode == SpawnManagementMode.DayFlow || manager.Settings.Animation.Enabled)
                        InteractionStagingAuthoring.Prepare(manager, "Spawn Staging");
                    if (manager.Settings.Mode == SpawnManagementMode.DayFlow)
                        SpawnFlowAuthoring.Prepare(manager);
                    break;
            }
        }

        #endregion

        #endregion
    }
}
