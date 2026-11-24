using System.Collections.Generic;
using CatOnASkateboard.StudioIdentity;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Updates provisional ingredient visuals only when their composition, identity or available recipes change.</summary>
    internal sealed class AssemblyPreviewAppearance
    {
        #region State

        private readonly List<ObjectGrab> previousIngredients = new List<ObjectGrab>();
        private readonly List<ObjectAssemblyStation> previousChoices = new List<ObjectAssemblyStation>();
        private readonly List<ObjectAssemblyStation> choices = new List<ObjectAssemblyStation>();
        private readonly List<ItemAppearanceConsensus> common = new List<ItemAppearanceConsensus>();
        private readonly AssemblyIngredientSelection selection = new AssemblyIngredientSelection();
        private bool dirty = true;
        private bool subscribed;

        #endregion

        #region Methods

        #region Lifecycle

        /// <summary>Releases identity subscriptions and cached ingredient references when a preview ends.</summary>
        internal void Clear()
        {
            if (subscribed)
                ObjectIdentity.Changed -= IdentityChanged;
            subscribed = false;
            dirty = true;
            previousIngredients.Clear();
            previousChoices.Clear();
            choices.Clear();
            common.Clear();
        }

        /// <summary>Invalidates only compositions containing an ingredient whose flags have changed.</summary>
        /// <param name="identity">Identity updated by another interaction.</param>
        private void IdentityChanged(ObjectIdentity identity)
        {
            foreach (ObjectGrab ingredient in previousIngredients)
                if (ingredient != null && ingredient.Identity == identity)
                {
                    dirty = true;
                    return;
                }
        }

        #endregion

        #region Evaluation

        /// <summary>Applies only insertion substitutions shared by every recipe accepting the complete pending composition.</summary>
        /// <param name="station">Station owning at least one of the shared previews.</param>
        /// <param name="ingredients">Full proposed insertion sequence, including sibling previews.</param>
        internal void Refresh(ObjectAssemblyStation station, IReadOnlyList<ObjectGrab> ingredients)
        {
            // Availability checks are cheap; bindings and appearance snapshots run only when this state changes.
            AssemblyInteractionRegistry.CollectChoices(station, choices);
            if (!dirty && Same(previousIngredients, ingredients) && Same(previousChoices, choices))
                return;
            previousIngredients.Clear();
            previousIngredients.AddRange(ingredients);
            previousChoices.Clear();
            previousChoices.AddRange(choices);
            if (!subscribed)
            {
                ObjectIdentity.Changed += IdentityChanged;
                subscribed = true;
            }
            // Resolve candidates against original geometry, including primary meshes hidden by a previous preview.
            foreach (ObjectGrab ingredient in ingredients)
                ingredient.Dock?.SuspendAppearance();
            try
            {
                Rebuild(ingredients);
                dirty = false;
            }
            finally
            {
                foreach (ObjectGrab ingredient in ingredients)
                    ingredient.Dock?.ResumeAppearance();
            }
        }

        /// <summary>Intersects the selected magnet appearance across all viable recipe alternatives.</summary>
        /// <param name="ingredients">Pending ingredients evaluated against their original visuals.</param>
        private void Rebuild(IReadOnlyList<ObjectGrab> ingredients)
        {
            common.Clear();
            foreach (ObjectGrab ingredient in ingredients)
                common.Add(new ItemAppearanceConsensus(ingredient.Item));
            // Actual insertion retains order checks; alternatives may still be awaiting an earlier numbered base.
            foreach (ObjectAssemblyStation choice in choices)
            {
                if (!choice.Template.CanAcceptSequence(ingredients, out _, enforceOrder: false))
                    continue;
                AssemblyProductSettings settings = choice.Template.Settings;
                selection.Begin(settings);
                for (int index = 0; index < ingredients.Count; index++)
                    if (selection.Append(settings, ingredients[index], index + 1, out int magnet, out _, false))
                        common[index].Include(settings.Magnets[magnet].Appearance);
            }
            // An empty intersection restores that channel until a later ingredient distinguishes the recipe.
            for (int index = 0; index < ingredients.Count; index++)
                ingredients[index].Dock?.PrepareAppearance(common[index].Build());
        }

        /// <summary>Compares cached recipe and ingredient lists without allocating a new state key.</summary>
        /// <param name="first">Previously evaluated references.</param>
        /// <param name="second">Current references in deterministic order.</param>
        /// <typeparam name="T">Unity reference type stored by these lists.</typeparam>
        /// <returns>True when both lists contain the same references in the same order.</returns>
        private static bool Same<T>(IReadOnlyList<T> first, IReadOnlyList<T> second) where T : Object
        {
            if (first.Count != second.Count)
                return false;
            for (int index = 0; index < first.Count; index++)
                if (first[index] != second[index])
                    return false;
            return true;
        }

        #endregion

        #endregion
    }
}
