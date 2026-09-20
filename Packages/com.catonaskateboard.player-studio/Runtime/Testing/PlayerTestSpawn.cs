using UnityEngine;

namespace CatOnASkateboard.PlayerStudio
{
    /// <summary>Marks the editable starting pose used when Player Studio prepares its test scene.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTestSpawn : MonoBehaviour
    {
        #region Methods

#if UNITY_EDITOR
        /// <summary>Shows the spawn direction when selecting the marker in the test scene.</summary>
        private void OnDrawGizmosSelected()
        {
            // The marker has no runtime update or generated objects.
            Gizmos.color = new Color(0.2f, 0.85f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawRay(transform.position, transform.forward);
        }
#endif

        #endregion
    }
}
