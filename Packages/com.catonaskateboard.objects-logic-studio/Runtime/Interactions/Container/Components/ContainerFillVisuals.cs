using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Updates visual-only container contents when a fill step or its count changes.</summary>
    internal sealed class ContainerFillVisuals
    {
        #region State

        /// <summary>Caches a visible unit and the source that determines its geometry.</summary>
        private sealed class Unit
        {
            internal Transform Root;
            internal GameObject Source;
            internal InventoryPortionLook Look;
        }

        private readonly Transform owner;
        private readonly List<InventoryPortionLook> scooped = new List<InventoryPortionLook>();
        private readonly List<Unit> units = new List<Unit>();
        private readonly Dictionary<GameObject, InventoryPortionLook> models = new Dictionary<GameObject, InventoryPortionLook>();

        #endregion

        #region Methods

        #region Content

        /// <summary>Retains the box transform used by the visual units.</summary>
        /// <param name="owner">Container whose local coordinates define its fill layout.</param>
        internal ContainerFillVisuals(Transform owner)
        {
            this.owner = owner;
        }

        /// <summary>Captures the look of a scooped item before its original disappears.</summary>
        /// <param name="source">Actual output object or its prefab for a virtual portion.</param>
        internal void Record(GameObject source)
        {
            scooped.Add(source != null ? new InventoryPortionLook(source) : null);
        }

        /// <summary>Applies the reached layout using one reusable unit per occupied magnet.</summary>
        /// <param name="step">Active step or null when no items should show.</param>
        /// <param name="count">Committed content count.</param>
        /// <param name="initial">Number of scooped units present before any live receipt.</param>
        /// <param name="stored">Existing stored originals, or null for scoop mode.</param>
        internal void Update(InventoryItemFill step, int count, int initial, IReadOnlyList<StoredObject> stored)
        {
            int visible = step != null ? Mathf.Min(count, step.Magnets.Length) : 0;
            for (int index = 0; index < visible; index++)
            {
                if (index == units.Count)
                    units.Add(new Unit());
                Unit unit = units[index];
                // An explicit ingredient model also supplies a look for initial fill.
                GameObject source = step.VisualPrefab;
                InventoryPortionLook look = null;
                if (source != null)
                {
                    if (!models.TryGetValue(source, out look))
                    {
                        look = new InventoryPortionLook(source);
                        models.Add(source, look);
                    }
                }
                else if (stored != null && index < stored.Count && stored[index].Grab != null)
                {
                    source = stored[index].Grab.gameObject;
                    look = unit.Source == source ? unit.Look : new InventoryPortionLook(source);
                }
                else if (index >= initial && index - initial < scooped.Count)
                    look = scooped[index - initial];

                // Build only when this slot first appears or receives a different mesh source.
                if (unit.Look != look)
                {
                    Release(unit);
                    unit.Root = look?.Build(owner);
                    unit.Look = look;
                    unit.Source = source;
                }
                if (unit.Root != null)
                {
                    InventoryFillMagnet magnet = step.Magnets[index];
                    unit.Root.SetLocalPositionAndRotation(magnet.Position, Quaternion.Euler(magnet.Rotation));
                    unit.Root.localScale = magnet.Scale;
                    SetVisible(unit, true);
                }
            }
            for (int index = visible; index < units.Count; index++)
                SetVisible(units[index], false);
        }

        #endregion

        #region Lifecycle

        /// <summary>Hides copies while their container interaction is disabled.</summary>
        internal void Hide()
        {
            foreach (Unit unit in units)
                SetVisible(unit, false);
        }

        /// <summary>Releases generated meshes and captured looks when the interaction is removed or reset.</summary>
        internal void Clear()
        {
            foreach (Unit unit in units)
                Release(unit);
            units.Clear();
            scooped.Clear();
            models.Clear();
        }

        /// <summary>Changes visibility only when the unit crosses a fill or activation boundary.</summary>
        /// <param name="unit">Reusable mesh instance.</param>
        /// <param name="visible">Whether this position is occupied.</param>
        private static void SetVisible(Unit unit, bool visible)
        {
            if (unit.Root != null && unit.Root.gameObject.activeSelf != visible)
                unit.Root.gameObject.SetActive(visible);
        }

        /// <summary>Hides and removes the render objects before replacing their geometry.</summary>
        /// <param name="unit">Visual instance being replaced or cleared.</param>
        private static void Release(Unit unit)
        {
            if (unit.Root == null)
                return;
            SetVisible(unit, false);
            Object.Destroy(unit.Root.gameObject);
            unit.Root = null;
        }

        #endregion

        #endregion
    }
}
