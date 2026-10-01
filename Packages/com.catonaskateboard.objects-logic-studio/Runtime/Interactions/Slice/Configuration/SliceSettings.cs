using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Matches the Ingredient labels authored in FMOD.</summary>
    public enum SliceIngredient { Bread, Crunchy, Full }

    /// <summary>Places one independent prefab when its slice step succeeds.</summary>
    [Serializable]
    public sealed class SliceSpawn
    {
        #region Fields

        [Tooltip("Active prefab root generated once when this step is performed.")]
        public GameObject Prefab;
        [Tooltip("Spawn position relative to the sliced object's current transform.")]
        public Vector3 Position;
        [Tooltip("Spawn rotation in degrees relative to the sliced object's current rotation.")]
        public Vector3 Rotation;

        #endregion
    }

    /// <summary>Groups the appearance changes and prefab outputs committed by one input press.</summary>
    [Serializable]
    public sealed class SliceStep
    {
        #region Fields

        [Tooltip("Name identifying this step in the ordered sequence.")]
        public string Name = "Slice";
        [Tooltip("Deactivate the sliced item after this final step. Prefab outputs and running VFX keep their own lifetime.")]
        public bool Consume;
        [Tooltip("Mesh substitutions on existing branches, optionally updating their MeshColliders.")]
        public ContactMeshReplacement[] Meshes = Array.Empty<ContactMeshReplacement>();
        [Tooltip("Complete material-slot replacements on existing renderers.")]
        public ItemMaterialReplacement[] Materials = Array.Empty<ItemMaterialReplacement>();
        [Tooltip("Independent prefab outputs created once during this step. Empty generates no objects.")]
        public SliceSpawn[] Spawns = Array.Empty<SliceSpawn>();

        #endregion
    }

    /// <summary>Configures a finite sequence that advances by one step for each eligible input press.</summary>
    [Serializable]
    public sealed class SliceSettings
    {
        #region Fields

        [Header("Targeting")]
        [Tooltip("Reach, aim and obstruction settings for each slice press.")]
        public TransferTargetSettings Target = new TransferTargetSettings();
        [Tooltip("Higher priority wins when multiple Slice interactions can handle the same press. Equal priorities use distance.")]
        public int Priority;
        [Tooltip("Optional direct-contact requirement evaluated before accepting each cut.")]
        public FlagContactRequirement Contact = new FlagContactRequirement();
        [Header("Sequence")]
        [Tooltip("Minimum seconds between accepted presses. Zero allows one step on each performed input event.")]
        public float Interval = 0.15f;
        [Tooltip("Ordered slice steps. Started is emitted on the first step and Completed after the final step. Finished sequences cannot run again on this instance.")]
        public SliceStep[] Steps = Array.Empty<SliceStep>();

        [Header("Slice Audio")]
        [Tooltip("Play the slice cue for each successfully committed cut.")]
        public bool Sound = true;
        [Tooltip("Ingredient parameter label supplied to the slice event.")]
        public SliceIngredient Ingredient = SliceIngredient.Full;

        #endregion

        #region Methods

        #region Validation

        /// <summary>Checks reusable data without modifying values, source prefabs or materials.</summary>
        /// <param name="warning">Receives the first incomplete or invalid step.</param>
        /// <returns>True when targeting and every step contain usable settings.</returns>
        public bool TryValidate(out string warning)
        {
            // Component paths are checked separately on the destination item.
            warning = "Slice targeting is missing.";
            if (Target == null || !Target.TryValidate(out warning))
                return false;
            if (Contact == null || !Contact.TryValidate(out warning))
                return false;
            if (!InteractionValues.Finite(Interval) || Interval < 0f || Steps == null || Steps.Length == 0)
            {
                warning = "Slice needs a nonnegative finite interval and at least one step.";
                return false;
            }
            for (int index = 0; index < Steps.Length; index++)
            {
                SliceStep step = Steps[index];
                if (step == null || step.Meshes == null || step.Materials == null || step.Spawns == null
                    || !step.Consume && step.Meshes.Length + step.Materials.Length + step.Spawns.Length == 0)
                {
                    warning = "Each Slice step needs a visual change, a prefab output or final consumption.";
                    return false;
                }
                if (step.Consume && index != Steps.Length - 1)
                {
                    warning = "Consumption must be the last Slice step; a consumed item cannot perform later cuts.";
                    return false;
                }
                if (!step.Consume && !ItemAppearanceSettings.TryValidate(step.Meshes, step.Materials, out warning))
                    return false;
                foreach (SliceSpawn spawn in step.Spawns)
                    if (spawn == null || !SpawnManagementSettings.Prefab(spawn.Prefab) || !spawn.Prefab.activeSelf
                        || !InteractionValues.Finite(spawn.Position) || !InteractionValues.Finite(spawn.Rotation))
                    {
                        warning = "Choose active prefab roots and finite local poses for Slice outputs.";
                        return false;
                    }
            }
            warning = string.Empty;
            return true;
        }

        #endregion

        #endregion
    }
}
