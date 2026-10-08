using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio.Editor
{
    /// <summary>Prepares imported visual meshes once so builds can deform private vertex buffers.</summary>
    internal static class ElasticAuthoring
    {
        #region Methods
        #region Preparation
        /// <summary>Enables read access only on imported models used by the selected visual hierarchy.</summary>
        /// <param name="root">Object whose existing visual meshes need CPU access.</param>
        internal static void Prepare(GameObject root)
        {
            if (root == null)
                return;
            HashSet<string> paths = new HashSet<string>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                Collect(filter.sharedMesh, paths);
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Collect(skin.sharedMesh, paths);
            foreach (string path in paths)
                if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
                {
                    Undo.RecordObject(importer, "Prepare Elastic Mesh");
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
        }
        /// <summary>Collects imported model paths without changing readable standalone mesh assets.</summary>
        /// <param name="mesh">Visual source mesh.</param>
        /// <param name="paths">Unique source model paths.</param>
        private static void Collect(Mesh mesh, HashSet<string> paths)
        {
            if (mesh != null && !mesh.isReadable)
                paths.Add(AssetDatabase.GetAssetPath(mesh));
        }
        #endregion
        #endregion
    }
}
