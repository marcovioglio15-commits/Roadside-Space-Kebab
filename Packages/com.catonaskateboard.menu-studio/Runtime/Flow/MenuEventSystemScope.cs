using UnityEngine;
using UnityEngine.EventSystems;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Prevents an additive menu scene from competing with an existing gameplay EventSystem.</summary>
    [RequireComponent(typeof(EventSystem))]
    public sealed class MenuEventSystemScope : MonoBehaviour
    {
        #region Methods
        #region Lifecycle

        /// <summary>Retains a single active event system when the pause scene is loaded.</summary>
        private void Awake()
        {
            // Both systems are preauthored; additive loading never creates another runtime UI hierarchy.
            EventSystem own = GetComponent<EventSystem>();
            foreach (EventSystem system in FindObjectsByType<EventSystem>())
                if (system != own && system.isActiveAndEnabled && system.gameObject.scene != gameObject.scene)
                {
                    foreach (BaseInputModule module in GetComponents<BaseInputModule>())
                        module.enabled = false;
                    own.enabled = false;
                    return;
                }
        }

        #endregion
        #endregion
    }
}
