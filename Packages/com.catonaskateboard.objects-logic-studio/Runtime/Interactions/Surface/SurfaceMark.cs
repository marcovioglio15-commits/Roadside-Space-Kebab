using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Identifies pooled cosmetic geometry so item appearance and assembly never claim it.</summary>
    [AddComponentMenu("")]
    internal sealed class SurfaceMark : MonoBehaviour
    {
        #region Properties

        internal Mesh Mesh { get; private set; }
        internal MeshRenderer Renderer { get; private set; }
        internal Collider Receiver { get; set; }
        internal float Expires { get; set; }
        internal MaterialPropertyBlock Properties { get; private set; }

        #endregion
        #region Methods
        #region Resources

        /// <summary>Creates reusable rendering resources only during pool initialization.</summary>
        /// <param name="material">Shared deposit shader material.</param>
        internal void Initialize(Material material)
        {
            Properties = new MaterialPropertyBlock();
            Mesh = new Mesh { name = "Surface Deposit" };
            Mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = Mesh;
            Renderer = gameObject.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = true;
            Renderer.enabled = false;
        }

        /// <summary>Releases the reusable native mesh when the entire pool is destroyed.</summary>
        private void OnDestroy()
        {
            if (Mesh != null)
                Destroy(Mesh);
        }

        #endregion
        #endregion
    }
}
