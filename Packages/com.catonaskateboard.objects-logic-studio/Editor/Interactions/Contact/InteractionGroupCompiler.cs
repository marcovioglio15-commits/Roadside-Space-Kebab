using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Prepares the existing group assets for runtime contact imports without editor dependencies.</summary>
    internal static class InteractionGroupCompiler
    {
        #region Methods

        #region Compilation

        /// <summary>Compiles selected groups in order and reports incompatible fields without changing source assets.</summary>
        /// <param name="change">Contact operation receiving prepared group patches.</param>
        /// <returns>True when every selected group can be applied at runtime.</returns>
        internal static bool Compile(InteractionChangeSettings change)
        {
            change.GroupWarning = string.Empty;
            change.CompiledKind = change.Kind;
            if (!change.UsesGroups)
                return true;
            List<InteractionGroupPatch> patches = new List<InteractionGroupPatch>();
            try
            {
                // Compatibility comes from the same group type metadata used by the normal group windows.
                foreach (ScriptableObject source in change.GroupPresets)
                {
                    if (source is not ObjectGroupPreset preset || !Matches(preset, change.Kind))
                        throw new InvalidOperationException("Choose settings-group presets for " + change.Kind + ".");
                    patches.Add(InteractionGroupWriter.Compile(preset, change.Kind));
                }
                if (patches.Count == 0)
                    throw new InvalidOperationException("Select at least one settings-group preset.");
                change.Groups = patches.ToArray();
                change.RequiresAction = NeedsAction(change);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                change.GroupWarning = exception.Message;
                change.Groups = Array.Empty<InteractionGroupPatch>();
                return false;
            }
        }

        /// <summary>Filters the group picker by its existing interaction type metadata.</summary>
        /// <param name="preset">Existing group asset.</param>
        /// <param name="kind">Operation's interaction family.</param>
        /// <returns>True when this is a compatible settings group.</returns>
        internal static bool Matches(ObjectGroupPreset preset, InteractionRuntimeKind kind)
        {
            // Group identity remains unchanged; no parallel family-specific preset type is introduced.
            return preset?.Group != null && preset.Group.TypeName == InteractionRuntimeCatalog.ComponentType(kind)?.FullName;
        }

        /// <summary>Reads conditional input requirements from an inactive temporary component.</summary>
        /// <param name="change">Operation whose groups have already been compiled.</param>
        /// <returns>True when its resulting configuration uses a start Button.</returns>
        private static bool NeedsAction(InteractionChangeSettings change)
        {
            GameObject root = new GameObject("Group input context") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            try
            {
                // Inactive staging prevents gameplay registration, UI or physics updates.
                ObjectInteraction feature = InteractionRuntimeCatalog.Create(change.Kind, root);
                foreach (InteractionGroupPatch group in change.Groups)
                    if (!group.Apply(feature, out _))
                        return true;
                return feature switch
                {
                    ObjectDialogue dialogue => dialogue.Settings.NeedsStartAction,
                    ObjectSingleInteraction single => single.UsesInput,
                    ObjectContactModifier contact => contact.Settings.RequiresInput,
                    ObjectGravityGenerator gravity => gravity.Settings.UsesRestoreInput,
                    ObjectAmbient ambient => ambient.Settings.Mode == AmbientMode.Radio,
                    ObjectAssemblyStation station => station.Settings.Trigger == AssemblyStationTrigger.InputAction,
                    ObjectRequestedInteraction or ObjectSlice => true,
                    _ => false
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        #endregion

        #region References

        /// <summary>Converts one portable editor slot to a compiled asset or local-type descriptor.</summary>
        /// <param name="link">Existing group reference metadata.</param>
        /// <returns>A runtime-safe reference with no editor type lookup.</returns>
        internal static InteractionGroupReference Reference(ObjectPresetLink link)
        {
            if (link.External)
                throw new InvalidOperationException("This group contains a scene reference. Choose a group using assets or local object references.");
            if (link.Asset != null)
                return new InteractionGroupReference { Asset = link.Asset, Kind = InteractionGroupObjectKind.Asset };
            Type type = Type.GetType(link.TypeName, false);
            InteractionGroupReference reference = new InteractionGroupReference { Path = link.Path, Index = link.Index, Count = link.Count };
            // Editor compilation resolves class names once; the player uses only compiled enum branches.
            foreach (InteractionRuntimeKind kind in Enum.GetValues(typeof(InteractionRuntimeKind)))
                if (InteractionRuntimeCatalog.ComponentType(kind) == type)
                {
                    reference.Kind = InteractionGroupObjectKind.Interaction;
                    reference.Interaction = kind;
                    return reference;
                }
            reference.Kind = type == typeof(GameObject) ? InteractionGroupObjectKind.GameObject
                : type == typeof(Transform) ? InteractionGroupObjectKind.Transform
                : type == typeof(RectTransform) ? InteractionGroupObjectKind.RectTransform
                : type == typeof(BoxCollider) ? InteractionGroupObjectKind.BoxCollider
                : type == typeof(SphereCollider) ? InteractionGroupObjectKind.SphereCollider
                : type == typeof(CapsuleCollider) ? InteractionGroupObjectKind.CapsuleCollider
                : type == typeof(MeshCollider) ? InteractionGroupObjectKind.MeshCollider
                : type == typeof(MeshRenderer) ? InteractionGroupObjectKind.MeshRenderer
                : type == typeof(SkinnedMeshRenderer) ? InteractionGroupObjectKind.SkinnedMeshRenderer
                : type == typeof(SpriteRenderer) ? InteractionGroupObjectKind.SpriteRenderer
                : type == typeof(Rigidbody) ? InteractionGroupObjectKind.Rigidbody
                : type == typeof(AudioSource) ? InteractionGroupObjectKind.AudioSource
                : type == typeof(Camera) ? InteractionGroupObjectKind.Camera
                : type == typeof(Light) ? InteractionGroupObjectKind.Light
                : type == typeof(Animator) ? InteractionGroupObjectKind.Animator
                : type == typeof(ParticleSystem) ? InteractionGroupObjectKind.ParticleSystem
                : type == typeof(Canvas) ? InteractionGroupObjectKind.Canvas
                : type == typeof(CanvasGroup) ? InteractionGroupObjectKind.CanvasGroup
                : throw new InvalidOperationException("Unsupported local group reference: " + link.Label);
            return reference;
        }

        #endregion

        #endregion
    }
}
