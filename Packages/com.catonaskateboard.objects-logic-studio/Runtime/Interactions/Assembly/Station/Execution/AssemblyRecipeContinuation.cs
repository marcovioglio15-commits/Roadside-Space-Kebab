using System.Collections.Generic;
using UnityEngine;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Revises a pending prolonged recipe when a later ingredient distinguishes an available extension.</summary>
    internal sealed class AssemblyRecipeContinuation
    {
        #region State

        private readonly List<ObjectGrab> originals = new List<ObjectGrab>();
        private readonly List<ObjectGrab> ingredients = new List<ObjectGrab>();

        #endregion

        #region Methods
        #region Selection

        /// <summary>Checks a continuation without detaching ingredients, creating products or interrupting a charge.</summary>
        /// <param name="station">Station retaining the current recipe.</param>
        /// <param name="ingredient">New independent ingredient offered by input or contact.</param>
        /// <param name="requireHeld">Whether the caller already checked the player's carry slot.</param>
        /// <returns>The sibling station that can continue this exact ordered composition, or null.</returns>
        internal ObjectAssemblyStation Find(ObjectAssemblyStation station, ObjectGrab ingredient, bool requireHeld)
        {
            ObjectAssemblyProduct previous = station.CurrentProduct;
            if (previous == null || !previous.CanRevise || previous.Table != station || ingredient == null
                || ingredient.transform.IsChildOf(previous.transform) || previous.transform.IsChildOf(ingredient.transform))
                return null;
            previous.CollectIngredients(originals);
            ingredients.Clear();
            ingredients.AddRange(originals);
            ingredients.Add(ingredient);
            return AssemblyInteractionRegistry.FindContinuation(station, ingredients, requireHeld);
        }

        #endregion
        #region Commit

        /// <summary>Transfers the original ingredient roots into a prepared replacement, restoring the previous recipe on failure.</summary>
        /// <param name="station">Station retaining the previous product.</param>
        /// <param name="ingredient">New ingredient that distinguishes the next recipe.</param>
        /// <param name="requireHeld">Whether this is an explicit player insertion.</param>
        /// <returns>True when the alternative product owns the full composition.</returns>
        internal bool Execute(ObjectAssemblyStation station, ObjectGrab ingredient, bool requireHeld)
        {
            ObjectAssemblyStation next = Find(station, ingredient, requireHeld);
            if (next == null)
                return false;
            ObjectAssemblyProduct previous = station.CurrentProduct;
            ObjectAssemblyProduct replacement = next.PrepareProduct();
            // Bind replacement presentation before touching the current product or its suspended ingredients.
            if (!replacement.CanAcceptSequence(ingredients, out _, previous))
            {
                Object.Destroy(replacement.gameObject);
                return false;
            }
            previous.ReleaseIngredientsForRevision();
            if (!replacement.AcceptSequence(ingredients))
            {
                Object.Destroy(replacement.gameObject);
                if (!previous.RestoreIngredientsAfterRevision(originals))
                {
                    previous.ReleaseTable();
                    Object.Destroy(previous.gameObject);
                    Debug.LogWarning("Assembly could not restore the previous recipe after its ingredient bindings changed. The original ingredients remain in the scene.", station);
                }
                return false;
            }
            // Only the abandoned empty shell is removed; ingredient instances and their own state survive.
            previous.ReleaseTable();
            Object.Destroy(previous.gameObject);
            next.ActivateProduct(replacement, false);
            return true;
        }

        #endregion
        #endregion
    }
}
