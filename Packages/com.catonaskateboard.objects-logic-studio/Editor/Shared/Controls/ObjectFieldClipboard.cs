using System;
using CatOnASkateboard.StudioColors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Copies identity-backed configurations through resolved snapshots instead of source-only file IDs.</summary>
    [InitializeOnLoad]
    internal static class ObjectFieldClipboard
    {
        #region State

        private static ObjectFieldTarget clipboard;

        #endregion

        #region Methods
        #region Menu

        /// <summary>Registers the mapped clipboard only for fields containing prefab-local identities.</summary>
        static ObjectFieldClipboard()
        {
            StudioFieldMenu.ClipboardMenu += Populate;
        }

        /// <summary>Replaces raw Copy/Paste for complete rules, recipes and identity-backed selectors.</summary>
        /// <param name="menu">Shared context menu receiving the clipboard commands.</param>
        /// <param name="properties">Fields represented by this field, section or interaction header.</param>
        /// <returns>True when this selection requires identity-aware clipboard handling.</returns>
        private static bool Populate(GenericMenu menu, SerializedProperty[] properties)
        {
            if (properties.Length == 0 || properties[0].serializedObject.targetObject is not ObjectWorkspace state
                || !Array.Exists(properties, property => property != null
                    && (property.propertyPath.StartsWith("Extended.Draft.Unlock", StringComparison.Ordinal)
                        || property.propertyPath.StartsWith("Extended.Draft.Orders", StringComparison.Ordinal)
                        || property.propertyPath.StartsWith("Extended.Draft.AssemblyProduct", StringComparison.Ordinal))
                    && (property.propertyType == SerializedPropertyType.Generic || property.name.EndsWith("Id", StringComparison.Ordinal)
                        || property.propertyPath.Contains(".SourceIds.Array.data[") || property.propertyPath.Contains(".TargetIds.Array.data["))))
                return false;
            ObjectFieldTarget target = ObjectFieldTarget.Create(properties);
            if (target == null)
            {
                menu.AddDisabledItem(new GUIContent("Copy"));
                menu.AddDisabledItem(new GUIContent("Paste"));
                return true;
            }
            Func<bool> guard = StudioFieldMenu.Guard(state, properties[0].propertyPath);
            menu.AddItem(new GUIContent("Copy"), false, () => clipboard = target);
            if (GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode && Compatible(clipboard, target))
            {
                ObjectFieldTarget copied = clipboard;
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    if (guard())
                        Paste(state, copied, target);
                });
            }
            else
                menu.AddDisabledItem(new GUIContent("Paste"));
            return true;
        }

        /// <summary>Checks complete field routes before offering a mapped paste.</summary>
        /// <param name="copied">Retained resolved source snapshot.</param>
        /// <param name="target">Current destination field set.</param>
        /// <returns>True when both selections describe the same interaction fields.</returns>
        private static bool Compatible(ObjectFieldTarget copied, ObjectFieldTarget target)
        {
            if (copied == null || copied.Source == null || copied.Type != target.Type || copied.Fields.Length != target.Fields.Length)
                return false;
            for (int index = 0; index < copied.Fields.Length; index++)
                if (copied.Fields[index].Path != target.Fields[index].Path)
                    return false;
            return true;
        }

        #endregion
        #region Transfer

        /// <summary>Applies mapped values to a detached snapshot before committing the complete draft once.</summary>
        /// <param name="state">Destination workspace retaining pending edits.</param>
        /// <param name="copied">Resolved source values and persistent reference context.</param>
        /// <param name="target">Destination field routes.</param>
        internal static void Paste(ObjectWorkspace state, ObjectFieldTarget copied, ObjectFieldTarget target)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !Compatible(copied, target))
                return;
            ObjectExtendedInteraction source = state.Extended.Resolve(state.Target.Resolve());
            ObjectFieldSnapshot snapshot = ScriptableObject.CreateInstance<ObjectFieldSnapshot>();
            try
            {
                // Local mappings are resolved atomically; a missing destination leaves the proposal untouched.
                snapshot.Read(state, source);
                using (SerializedObject data = new SerializedObject(snapshot))
                {
                    for (int index = 0; index < copied.Fields.Length; index++)
                    {
                        string path = "Extended." + ObjectFieldWorkspace.DraftPath(source, target.Fields[index].Path, false);
                        copied.Fields[index].Value.Apply(data.FindProperty(path),
                            reference => ObjectFieldBatch.MapReference(reference, copied.Source, source));
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                switch (source)
                {
                    case ObjectAvailableOrders:
                        snapshot.Extended.OrdersCompletionId = snapshot.Extended.Orders.CompletionSource != null
                            ? ObjectWorkspaceTarget.FileId(snapshot.Extended.Orders.CompletionSource) : 0;
                        snapshot.Extended.Orders.CompletionSource = null;
                        break;
                    case ObjectInteractionUnlock:
                        snapshot.Extended.Unlock = UnlockInteractionDraft.Capture(snapshot.Extended.Unlock.Settings);
                        break;
                    case ObjectAssemblyProduct:
                        snapshot.Extended.AssemblyProduct = AssemblyProductDraft.Capture(snapshot.Extended.AssemblyProduct.Settings);
                        break;
                }
                Undo.RecordObject(state, "Paste interaction settings");
                state.Extended.Draft = snapshot.Extended;
                state.Persist();
                StudioFieldMenu.Notify(state);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Paste was not applied: " + exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        /// <summary>Retains a source component that survives closing its prefab stage.</summary>
        /// <param name="source">Current source in a prefab stage or persistent asset.</param>
        /// <returns>The exact saved counterpart, or the original source when no asset exists.</returns>
        internal static ObjectInteraction Persistent(ObjectInteraction source)
        {
            if (source == null || EditorUtility.IsPersistent(source))
                return source;
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(source.gameObject);
            GameObject root = stage != null ? AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath) : null;
            if (root != null)
                foreach (ObjectInteraction candidate in root.GetComponentsInChildren<ObjectInteraction>(true))
                    if (candidate.GetType() == source.GetType() && ObjectWorkspaceTarget.FileId(candidate) == ObjectWorkspaceTarget.FileId(source))
                        return candidate;
            return source;
        }

        #endregion
        #endregion
    }
}
