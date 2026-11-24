using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Keeps only substitutions shared by every possible appearance of one item.</summary>
    internal sealed class ItemAppearanceConsensus
    {
        #region State

        private readonly ObjectItem item;
        private readonly List<ContactMeshReplacement> meshes = new List<ContactMeshReplacement>();
        private readonly List<ItemMaterialReplacement> materials = new List<ItemMaterialReplacement>();
        private readonly List<ItemVisibilitySettings> visibility = new List<ItemVisibilitySettings>();
        private bool initialized;

        #endregion

        #region Methods

        #region Intersection

        /// <summary>Captures the original hierarchy used to compare explicit and primary mesh selections.</summary>
        /// <param name="item">Ingredient before any provisional substitutions are applied.</param>
        internal ItemAppearanceConsensus(ObjectItem item)
        {
            this.item = item;
        }

        /// <summary>Removes substitutions that another compatible recipe does not share.</summary>
        /// <param name="settings">Insertion appearance for the ingredient's selected magnet.</param>
        internal void Include(ItemAppearanceSettings settings)
        {
            // Copy mutable collider policies; source recipe settings are never changed by an intersection.
            if (!initialized)
            {
                foreach (ContactMeshReplacement mesh in settings.Meshes)
                    meshes.Add(new ContactMeshReplacement
                        { Path = mesh.Path, PrimaryMesh = mesh.PrimaryMesh, Mesh = mesh.Mesh, UpdateCollider = mesh.UpdateCollider });
                materials.AddRange(settings.Materials);
                visibility.AddRange(settings.Visibility);
                initialized = true;
                return;
            }
            // Each visual channel is independent: conflicting materials must not suppress an agreed mesh.
            for (int index = meshes.Count - 1; index >= 0; index--)
                if (!Matches(meshes[index], settings.Meshes))
                    meshes.RemoveAt(index);
            for (int index = materials.Count - 1; index >= 0; index--)
                if (!Matches(materials[index], settings.Materials))
                    materials.RemoveAt(index);
            for (int index = visibility.Count - 1; index >= 0; index--)
                if (!Matches(visibility[index], settings.Visibility))
                    visibility.RemoveAt(index);
        }

        /// <summary>Creates one reversible configuration after all compatible recipes have contributed.</summary>
        /// <returns>The common mesh, material and visibility substitutions, possibly empty.</returns>
        internal ItemAppearanceSettings Build()
        {
            return new ItemAppearanceSettings { Meshes = meshes.ToArray(), Materials = materials.ToArray(), Visibility = visibility.ToArray() };
        }

        #endregion

        #region Matching

        /// <summary>Matches the actual mesh branch and keeps collider changes only when every recipe requests them.</summary>
        /// <param name="mesh">Common replacement being narrowed.</param>
        /// <param name="alternatives">Replacements from the next compatible recipe.</param>
        /// <returns>True when the same mesh reaches the same existing branch.</returns>
        private bool Matches(ContactMeshReplacement mesh, ContactMeshReplacement[] alternatives)
        {
            if (!ItemAppearanceBindings.TryMesh(item, mesh, out Transform target, out _))
                return false;
            foreach (ContactMeshReplacement other in alternatives)
                if (mesh.Mesh == other.Mesh && ItemAppearanceBindings.TryMesh(item, other, out Transform candidate, out _) && target == candidate)
                {
                    mesh.UpdateCollider &= other.UpdateCollider;
                    return true;
                }
            return false;
        }

        /// <summary>Requires identical material slots on the same renderer while ignoring equivalent path spelling.</summary>
        /// <param name="replacement">Common material substitution being narrowed.</param>
        /// <param name="alternatives">Material substitutions from the next recipe.</param>
        /// <returns>True when all material references match in slot order.</returns>
        private bool Matches(ItemMaterialReplacement replacement, ItemMaterialReplacement[] alternatives)
        {
            if (!ItemAppearanceBindings.TryRenderer(item, replacement, out Renderer target, out _))
                return false;
            foreach (ItemMaterialReplacement other in alternatives)
                if (ItemAppearanceBindings.TryRenderer(item, other, out Renderer candidate, out _) && target == candidate
                    && SameMaterials(replacement.Materials, other.Materials))
                    return true;
            return false;
        }

        /// <summary>Retains visibility only when every recipe agrees about the same renderer.</summary>
        /// <param name="entry">Common visibility setting being narrowed.</param>
        /// <param name="alternatives">Visibility settings from the next recipe.</param>
        /// <returns>True when the same renderer has the same visible state.</returns>
        private bool Matches(ItemVisibilitySettings entry, ItemVisibilitySettings[] alternatives)
        {
            if (!ItemAppearanceBindings.TryRenderer(item, entry.Path, out Renderer target, out _))
                return false;
            foreach (ItemVisibilitySettings other in alternatives)
                if (entry.Visible == other.Visible && ItemAppearanceBindings.TryRenderer(item, other.Path, out Renderer candidate, out _)
                    && target == candidate)
                    return true;
            return false;
        }

        /// <summary>Compares shared material references without cloning or instantiating materials.</summary>
        /// <param name="first">Material slots from the common substitution.</param>
        /// <param name="second">Material slots from another recipe.</param>
        /// <returns>True when both arrays contain identical references in the same order.</returns>
        private static bool SameMaterials(Material[] first, Material[] second)
        {
            if (first.Length != second.Length)
                return false;
            for (int index = 0; index < first.Length; index++)
                if (first[index] != second[index])
                    return false;
            return true;
        }

        #endregion

        #endregion
    }
}
