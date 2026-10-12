using System;

namespace CatOnASkateboard.ObjectsLogicStudio
{
    /// <summary>Input-driven actions that may take a shared button press before Dialogue.</summary>
    [Flags]
    public enum DialogueActionPriority
    {
        None = 0, Grab = 1, Drop = 2, Throw = 4, Container = 8, Dispenser = 16,
        TriggerAnimation = 32, Eject = 64, ModifyByContact = 128, Slice = 256,
        GravityGenerator = 512, SpraySauce = 1024, AssemblyProduct = 2048, All = 4095
    }

    /// <summary>Maps input participants to priority choices without changing their ordinary dispatch order.</summary>
    internal static class DialogueActionRules
    {
        #region Methods
        #region Selection

        /// <summary>Checks the selected types before performing targeting or changing carry ownership.</summary>
        /// <param name="feature">Bound input participant.</param>
        /// <param name="allowed">Types selected on the dialogue, or All for ordinary dispatch.</param>
        /// <returns>True when this feature may compete for the pending press.</returns>
        internal static bool Allows(ObjectInteraction feature, DialogueActionPriority allowed)
        {
            // The assembly pickup proxy also represents Grab on products waiting for finalization.
            DialogueActionPriority kind = feature switch
            {
                ObjectGrab => DialogueActionPriority.Grab,
                ObjectDrop => DialogueActionPriority.Drop,
                ObjectThrow => DialogueActionPriority.Throw,
                ObjectContainer => DialogueActionPriority.Container,
                ObjectDispenser => DialogueActionPriority.Dispenser,
                ObjectTriggerAnimation => DialogueActionPriority.TriggerAnimation,
                ObjectEject => DialogueActionPriority.Eject,
                ObjectContactModifier => DialogueActionPriority.ModifyByContact,
                ObjectSlice => DialogueActionPriority.Slice,
                ObjectGravityGenerator => DialogueActionPriority.GravityGenerator,
                ObjectSpraySauce => DialogueActionPriority.SpraySauce,
                ObjectAssemblyProduct product => DialogueActionPriority.AssemblyProduct
                    | (product.Pickup?.Grab != null ? DialogueActionPriority.Grab : DialogueActionPriority.None),
                _ => DialogueActionPriority.None
            };
            return (allowed & kind) != 0;
        }

        #endregion
        #endregion
    }
}
