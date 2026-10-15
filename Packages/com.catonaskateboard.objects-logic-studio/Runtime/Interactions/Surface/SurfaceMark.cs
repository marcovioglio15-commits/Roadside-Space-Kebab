using System;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains reusable deposit geometry without a GameObject, renderer or per-mark update callback.</summary>
    internal sealed class SurfaceMark : IDisposable
    {
        #region State
        internal Mesh Mesh;
        internal Collider Receiver;
        internal SurfaceTrailSettings Settings;
        internal SurfacePalette Palette;
        internal SurfaceMarkTiming Timing;
        internal Vector3 LocalPoint;
        internal Vector3 LocalNormal;
        internal float WetTime;
        internal float Expires;
        internal bool Retiring;
        internal readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        #endregion
        #region Methods
        #region Resources
        /// <summary>Creates native geometry only when this pool slot is used for the first time.</summary>
        internal void Prepare()
        {
            if (Mesh != null)
                return;
            Mesh = new Mesh { name = "Surface Deposit" };
            Mesh.MarkDynamic();
        }

        /// <summary>Releases the owned native mesh when the pool exits.</summary>
        public void Dispose()
        {
            if (Mesh != null)
                UnityEngine.Object.Destroy(Mesh);
        }
        #endregion
        #endregion
    }
}
