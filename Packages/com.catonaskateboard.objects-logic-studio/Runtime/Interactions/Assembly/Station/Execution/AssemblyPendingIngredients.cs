using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Retains visual ingredient placements while recipes on the same table remain undecided.</summary>
    internal sealed class AssemblyPendingIngredients
    {
        #region State

        private readonly List<AssemblyIngredientDock> docks = new List<AssemblyIngredientDock>();
        private readonly HashSet<ObjectItem> withdrawn = new HashSet<ObjectItem>();
        private readonly List<ObjectItem> removed = new List<ObjectItem>();
        private readonly AssemblyIngredientSelection selection = new AssemblyIngredientSelection();

        #endregion

        #region Methods

        #region Contacts

        /// <summary>Rearms externally moved ingredients only after they leave the actual contact surface.</summary>
        /// <param name="contacts">Physical contacts before adding retained magnet previews.</param>
        internal void Refresh(HashSet<ObjectItem> contacts)
        {
            // Movement may release a preview outside the trigger during this same query.
            RefreshDocks();
            removed.Clear();
            foreach (ObjectItem item in withdrawn)
                if (item == null || !contacts.Contains(item))
                    removed.Add(item);
            foreach (ObjectItem item in removed)
                withdrawn.Remove(item);
        }

        /// <summary>Checks whether an external interaction has withdrawn this ingredient from automatic snapping.</summary>
        /// <param name="item">Physical contact candidate.</param>
        /// <returns>True until the item leaves this contact surface.</returns>
        internal bool IsWithdrawn(ObjectItem item) => withdrawn.Contains(item);

        /// <summary>Shares live preview ingredients with every competing recipe on the same table.</summary>
        /// <param name="ingredients">Reusable collection receiving distinct candidates.</param>
        internal void Collect(List<ObjectGrab> ingredients)
        {
            // Validate first so a grabbed, consumed or ejected object cannot survive as stale shared contact.
            RefreshDocks();
            foreach (AssemblyIngredientDock dock in docks)
                if (!ingredients.Contains(dock.Ingredient))
                    ingredients.Add(dock.Ingredient);
        }

        /// <summary>Removes expired placements while retaining reusable collection capacity.</summary>
        private void RefreshDocks()
        {
            // Reverse traversal permits removal without allocating a second list of previews.
            for (int index = docks.Count - 1; index >= 0; index--)
            {
                AssemblyIngredientDock dock = docks[index];
                ObjectItem released = dock.Refresh(dock.Station);
                if (released != null)
                    withdrawn.Add(released);
                if (dock.Ingredient == null)
                    docks.RemoveAt(index);
            }
        }

        #endregion

        #region Placement

        /// <summary>Displays an undecided recipe without claiming the ingredients for its product prefab.</summary>
        /// <param name="station">Station providing the provisional magnet layout.</param>
        /// <param name="ingredients">Validated insertion sequence still compatible with another recipe.</param>
        internal void Show(ObjectAssemblyStation station, IReadOnlyList<ObjectGrab> ingredients)
        {
            // Existing shared previews stay in place instead of jumping between alternative station layouts.
            selection.Begin(station.Template.Settings);
            for (int index = 0; index < ingredients.Count; index++)
            {
                ObjectGrab ingredient = ingredients[index];
                if (!selection.Append(station.Template.Settings, ingredient, index + 1, out int magnet, out _))
                    return;
                if (ingredient.Dock != null || ingredient.IsHeld)
                    continue;
                AssemblyIngredientDock dock = new AssemblyIngredientDock();
                dock.Snap(station, ingredient, magnet);
                if (dock.Ingredient != null)
                    docks.Add(dock);
            }
        }

        /// <summary>Restores temporary poses and physics overrides when this station becomes unavailable.</summary>
        internal void Release()
        {
            // Actual assembly transfers release their individual previews before disabling their interactions.
            foreach (AssemblyIngredientDock dock in docks)
                dock.Release();
            docks.Clear();
            withdrawn.Clear();
        }

        #endregion

        #endregion
    }
}
