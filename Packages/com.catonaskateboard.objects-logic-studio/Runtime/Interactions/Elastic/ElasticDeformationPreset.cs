using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Stores reusable elastic deformation configuration independently of object input bindings.</summary>
    [CreateAssetMenu(menuName = "Objects Logic Studio/Elastic Deformation Preset")]
    public sealed class ElasticDeformationPreset : ExtendedInteractionPreset
    {
        #region Fields
        [Header("Elastic Deformation")]
        [Tooltip("Reusable interaction settings copied locally when imported.")]
        public ElasticSettings Settings = new ElasticSettings();
        #endregion
        #region Properties
        /// <summary>Interaction accepting this settings snapshot.</summary>
        public override ExtendedInteractionKind Kind => ExtendedInteractionKind.ElasticDeformation;
        #endregion
    }
}
