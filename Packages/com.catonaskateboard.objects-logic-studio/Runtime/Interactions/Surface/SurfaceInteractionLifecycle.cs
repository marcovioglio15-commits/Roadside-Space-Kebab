using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Restarts retained visual interactions once per Play session without scene scans during gameplay.</summary>
    internal static class SurfaceInteractionLifecycle
    {
        #region Methods
        #region Initialization
        /// <summary>Rebinds only active features when domain and scene reload are disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Rebuild()
        {
            foreach (ObjectExtendedInteraction feature in Object.FindObjectsByType<ObjectExtendedInteraction>())
                if (feature.isActiveAndEnabled)
                    switch (feature)
                    {
                        case ObjectElasticDeformation elastic:
                            elastic.BeginSession();
                            break;
                        case ObjectDirtTrail dirt:
                            dirt.BeginSession();
                            break;
                        case ObjectSpraySauce spray:
                            spray.BeginSession();
                            break;
                    }
        }
        #endregion
        #endregion
    }
}
