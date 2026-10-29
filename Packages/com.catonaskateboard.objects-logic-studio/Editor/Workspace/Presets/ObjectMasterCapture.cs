using System;
using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Builds an exportable master asset with separate main and group preset references.</summary>
    internal static class ObjectMasterCapture
    {
        #region Methods

        #region Assets

        /// <summary>Captures the selected hierarchy into a master and reusable preset subassets.</summary>
        /// <param name="root">Applied object configuration.</param>
        /// <param name="path">New master asset path.</param>
        /// <param name="children">Whether child objects are included.</param>
        /// <returns>The complete saved master asset.</returns>
        internal static ObjectMasterPreset Create(GameObject root, string path, bool children)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            ObjectMasterPreset master = ScriptableObject.CreateInstance<ObjectMasterPreset>();
            ObjectWorkspaceTarget route = new ObjectWorkspaceTarget();
            route.Capture(root, 0);
            master.SourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(route.PrefabGuid));
            master.UnityVersion = Application.unityVersion;
            AssetDatabase.CreateAsset(master, path);
            try
            {
                List<ObjectMasterPreset.SharedEntry> shared = new List<ObjectMasterPreset.SharedEntry>();
                Transform[] nodes = children ? root.GetComponentsInChildren<Transform>(true) : new[] { root.transform };
                foreach (Transform node in nodes)
                {
                    List<ObjectComponentPreset> components = new List<ObjectComponentPreset>();
                    if (node.TryGetComponent(out ObjectIdentity identity))
                        components.Add(ObjectPresetCapture.Component(identity, root.transform));
                    if (node.TryGetComponent(out ObjectItem item))
                        components.Add(ObjectPresetCapture.Component(item, root.transform));
                    if (components.Count == 0)
                        continue;
                    ObjectSharedPreset preset = ScriptableObject.CreateInstance<ObjectSharedPreset>();
                    preset.name = node.name + " · Shared settings";
                    preset.Components = components.ToArray();
                    AssetDatabase.AddObjectToAsset(preset, master);
                    shared.Add(new ObjectMasterPreset.SharedEntry { Path = AnimationUtility.CalculateTransformPath(node, root.transform), Preset = preset });
                }
                List<ObjectMasterPreset.InteractionEntry> interactions = new List<ObjectMasterPreset.InteractionEntry>();
                foreach (Transform node in nodes)
                    foreach (ObjectInteraction feature in node.GetComponents<ObjectInteraction>())
                    {
                        ObjectInteractionPreset preset = Interaction(feature, root.transform, master);
                        interactions.Add(new ObjectMasterPreset.InteractionEntry
                        {
                            Name = feature.InteractionName, Path = AnimationUtility.CalculateTransformPath(node, root.transform),
                            SourceId = ObjectWorkspaceTarget.FileId(feature), Index = Array.IndexOf(node.GetComponents(feature.GetType()), feature), Preset = preset
                        });
                    }
                master.Shared = shared.ToArray();
                master.Interactions = interactions.ToArray();
                EditorUtility.SetDirty(master);
                AssetDatabase.SaveAssets();
                return master;
            }
            catch
            {
                // A failed capture must not leave a master referring to only part of the object.
                AssetDatabase.DeleteAsset(path);
                throw;
            }
        }

        /// <summary>Creates one main interaction preset and its granular group preset references.</summary>
        /// <param name="feature">Applied source interaction.</param>
        /// <param name="root">Configuration root used by local links.</param>
        /// <param name="master">Asset owning newly captured subassets.</param>
        /// <returns>The complete main interaction preset.</returns>
        private static ObjectInteractionPreset Interaction(ObjectInteraction feature, Transform root, ObjectMasterPreset master)
        {
            ObjectInteractionPreset preset = ScriptableObject.CreateInstance<ObjectInteractionPreset>();
            preset.name = feature.InteractionName;
            preset.Configuration = ObjectPresetCapture.Component(feature, root);
            preset.SourcePreset = feature switch
            {
                ObjectHover hover => hover.Preset,
                ObjectSingleInteraction single => single.SettingsPreset,
                ObjectExtendedInteraction extended => extended.SettingsPreset,
                _ => null
            };
            AssetDatabase.AddObjectToAsset(preset, master);
            ObjectWorkspace document = ObjectPresetCapture.Document(feature);
            try
            {
                using SerializedObject data = new SerializedObject(document);
                List<ObjectGroupInfo> groups = ObjectInteractionPanel.Groups(document, data);
                ObjectGroupContext context = ObjectPresetContext.Create(document, preset);
                List<ObjectGroupPreset> references = new List<ObjectGroupPreset>();
                foreach (ObjectGroupInfo group in groups)
                {
                    ObjectGroupPreset saved = ScriptableObject.CreateInstance<ObjectGroupPreset>();
                    saved.name = feature.InteractionName + " · " + group.Title;
                    saved.Group = ObjectWorkspace.Copy(group);
                    saved.Values = ObjectGroupPresetStore.Capture(document, group, root);
                    saved.Context = context;
                    AssetDatabase.AddObjectToAsset(saved, master);
                    references.Add(saved);
                }
                preset.Groups = references.ToArray();
                EditorUtility.SetDirty(preset);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(document);
            }
            return preset;
        }

        #endregion

        #endregion
    }
}
