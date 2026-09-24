using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Connects an authored outline shell to its original static or skinned renderer.</summary>
    [Serializable]
    public sealed class OutlineBinding
    {
        #region Fields

        [Header("Authored Geometry")]
        [Tooltip("Original renderer whose mesh, visibility and transform supply the outline.")]
        public Renderer Source;
        [Tooltip("Dedicated child renderer created in the prefab before Play.")]
        public Renderer Shell;
        [Tooltip("Original static mesh filter, or empty for skinned geometry.")]
        public MeshFilter SourceMesh;
        [Tooltip("Static outline mesh filter, or empty for skinned geometry.")]
        public MeshFilter ShellMesh;

        #endregion

        #region State

        private Mesh previousMesh;
        private Material previousMaterial;

        #endregion

        #region Methods

        #region Presentation

        /// <summary>Mirrors explicit mesh changes and renderer visibility without cloning mesh assets.</summary>
        /// <param name="visible">Whether the owning interaction may present its outline.</param>
        /// <param name="material">Authored material selecting the outline's depth behavior.</param>
        internal void Sync(bool visible, Material material)
        {
            // Missing dependencies simply hide their shell until authoring repairs them.
            if (Shell == null)
                return;
            Shell.enabled = visible && Source != null && Source.enabled && !Source.forceRenderingOff;
            if (!Shell.enabled)
                return;
            Mesh mesh = Source is SkinnedMeshRenderer skin ? skin.sharedMesh : SourceMesh != null ? SourceMesh.sharedMesh : null;
            if (mesh != previousMesh || material != previousMaterial)
            {
                // Allocate slots only at an explicit mesh or depth-mode change, never during ordinary frames.
                Material[] materials = new Material[mesh != null ? mesh.subMeshCount : 0];
                for (int index = 0; index < materials.Length; index++)
                    materials[index] = material;
                Shell.sharedMaterials = materials;
                previousMesh = mesh;
                previousMaterial = material;
            }
            if (SourceMesh != null && ShellMesh != null && ShellMesh.sharedMesh != SourceMesh.sharedMesh)
                ShellMesh.sharedMesh = SourceMesh.sharedMesh;
            else if (Source is SkinnedMeshRenderer source && Shell is SkinnedMeshRenderer shell)
            {
                if (shell.sharedMesh != source.sharedMesh)
                {
                    shell.sharedMesh = source.sharedMesh;
                    shell.localBounds = source.localBounds;
                }
                // Copy animated blend weights; bones and root bone are already bound in the prefab.
                if (source.sharedMesh != null)
                    for (int index = 0; index < source.sharedMesh.blendShapeCount; index++)
                        shell.SetBlendShapeWeight(index, source.GetBlendShapeWeight(index));
            }
        }

        #endregion

        #endregion
    }
}
