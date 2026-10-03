using System.Collections.Generic;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Builds a possible recipe from available ingredients without spawning or consuming anything.</summary>
    internal sealed class AssemblyRecipeProposal
    {
        #region State

        private readonly AssemblyIngredientSelection selection = new AssemblyIngredientSelection();
        private readonly List<ObjectGrab> ingredients = new List<ObjectGrab>();

        #endregion

        #region Properties

        /// <summary>Ordered physical objects accepted by the proposed recipe.</summary>
        internal IReadOnlyList<ObjectGrab> Ingredients => ingredients;

        #endregion

        #region Methods

        #region Selection

        /// <summary>Retains a shared prefix until it identifies a recipe, respecting required magnet order.</summary>
        /// <param name="station">Empty station evaluating its product against the alternatives.</param>
        /// <param name="candidates">Eligible objects ordered by contact age or explicit placement.</param>
        /// <param name="explicitInput">Include the newly held object before evaluating an input command.</param>
        internal void Build(ObjectAssemblyStation station, IReadOnlyList<ObjectGrab> candidates, bool explicitInput = false)
        {
            // Retry earlier candidates after each accepted slot; a later-arriving first ingredient may unlock them.
            ingredients.Clear();
            ObjectAssemblyProduct template = station.Template;
            selection.Begin(template.Settings);
            for (int pass = 0; pass < candidates.Count; pass++)
            {
                bool appended = false;
                foreach (ObjectGrab candidate in candidates)
                    if (!ingredients.Contains(candidate) && template.CanConsider(candidate)
                        && selection.Append(template.Settings, candidate, ingredients.Count + 1, out _, out _))
                    {
                        ingredients.Add(candidate);
                        // Ordinary contact recipes still insert one object per tick and preserve their event counts.
                        if (!explicitInput && station.CanStart(ingredients))
                            return;
                        appended = true;
                        break;
                    }
                if (!appended)
                    break;
            }
        }

        #endregion

        #endregion
    }
}
