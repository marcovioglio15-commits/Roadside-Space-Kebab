using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CatOnASkateboard.MenuStudio
{
    /// <summary>Suspends active gameplay maps while menus or scene fades own input.</summary>
    internal static class MenuGameplayGate
    {
        #region State

        private static readonly HashSet<Object> owners = new HashSet<Object>();
        private static readonly List<(PlayerInput Player, InputActionMap Map)> maps = new List<(PlayerInput, InputActionMap)>();

        #endregion

        #region Methods
        #region Ownership

        /// <summary>Resets leases and scene subscriptions before each Play session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            // Retained scene components can acquire new ownership after a domain-reload-free restart.
            owners.Clear();
            maps.Clear();
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        /// <summary>Suspends the currently enabled gameplay map once for the first overlay.</summary>
        /// <param name="owner">Visible menu or active fade owning the gate.</param>
        internal static void Acquire(Object owner)
        {
            // Shared UI and Menu maps remain available for navigation and pause cancellation.
            if (owners.Add(owner) && owners.Count == 1)
                Capture();
        }

        /// <summary>Restores only maps disabled by this gate when the final owner closes.</summary>
        /// <param name="owner">Overlay releasing input ownership.</param>
        internal static void Release(Object owner)
        {
            // Destroyed players from an unloaded scene must not have their old action copies re-enabled.
            if (!owners.Remove(owner) || owners.Count > 0)
                return;
            foreach ((PlayerInput player, InputActionMap map) in maps)
                if (player != null && player.isActiveAndEnabled)
                    map.Enable();
            maps.Clear();
        }

        /// <summary>Captures players entering while an overlay still blocks gameplay.</summary>
        /// <param name="scene">Newly loaded scene.</param>
        /// <param name="mode">Single or additive loading mode.</param>
        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            // Discovery occurs at a scene boundary, never in the frame loop.
            if (owners.Count > 0)
                Capture();
        }

        /// <summary>Disables each currently active gameplay map and retains its original owner.</summary>
        private static void Capture()
        {
            // Already disabled maps are absent from the restore list, preserving project-owned restrictions.
            foreach (PlayerInput player in Object.FindObjectsByType<PlayerInput>())
                if (player.currentActionMap is InputActionMap map && map.enabled && map.name != "UI" && map.name != "Menu")
                {
                    maps.Add((player, map));
                    map.Disable();
                }
        }

        #endregion
        #endregion
    }
}
